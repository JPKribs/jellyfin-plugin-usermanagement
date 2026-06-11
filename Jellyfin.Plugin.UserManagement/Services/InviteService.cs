using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Creates and redeems self-service signup invites. Redemption is the only anonymous-reachable
/// write path in the plugin, so all validation (token, PIN, expiry, usage) happens here, server-side,
/// and created accounts are forced to be non-administrators.
/// </summary>
public sealed class InviteService : IDisposable
{
    private readonly IUserManager _userManager;
    private readonly GroupService _groupService;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly InviteStatusStore _statusStore;
    private readonly ActivityLogger _activity;
    private readonly ILogger<InviteService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteService"/> class.
    /// </summary>
    public InviteService(
        IUserManager userManager,
        GroupService groupService,
        ICryptoProvider cryptoProvider,
        InviteStatusStore statusStore,
        ActivityLogger activity,
        ILogger<InviteService> logger)
    {
        _userManager = userManager;
        _groupService = groupService;
        _cryptoProvider = cryptoProvider;
        _statusStore = statusStore;
        _activity = activity;
        _logger = logger;
    }

    private static string DisplayName(Invite invite)
        => invite.Label.Length > 0 ? invite.Label : "Untitled invite";

    private static InviteStatus StatusFor(InviteStatusData data, Guid id)
        => data.Invites.TryGetValue(id, out var status) ? status : (data.Invites[id] = new InviteStatus());

    /// <summary>
    /// Reports whether a PIN is acceptable: either absent, or exactly six digits to mirror the Quick
    /// Connect code format invitees already know.
    /// </summary>
    /// <param name="pin">The candidate PIN.</param>
    /// <returns><c>true</c> when the PIN is empty or a six digit code.</returns>
    public static bool IsValidPin(string? pin)
    {
        var trimmed = pin?.Trim() ?? string.Empty;
        return trimmed.Length == 0 || (trimmed.Length == 6 && trimmed.All(char.IsAsciiDigit));
    }

    /// <summary>
    /// Resolves the group an invite places new accounts in, either the current default group or the
    /// invite's explicit one.
    /// </summary>
    /// <param name="invite">The invite.</param>
    /// <returns>The target group, or <c>null</c> when none resolves.</returns>
    public GroupDefinition? ResolveTargetGroup(Invite invite)
    {
        ArgumentNullException.ThrowIfNull(invite);
        return invite.UseDefaultGroup
            ? _groupService.GetDefaultGroup()
            : (invite.GroupId is { } gid ? _groupService.FindGroup(gid) : null);
    }

    /// <summary>
    /// Disables every enabled invite whose target group disallows all password changes. Such a group is
    /// admin managed, so its outstanding invites are shut off the moment the group is switched over.
    /// </summary>
    /// <param name="config">The configuration to normalize in place.</param>
    /// <returns>The number of invites disabled.</returns>
    public static int DisableInvitesForBlockedGroups(Configuration.PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var blockedIds = config.Groups.Where(g => g.BlocksInvites()).Select(g => g.Id).ToHashSet();
        var defaultBlocked = config.DefaultGroupId is { } did && blockedIds.Contains(did);

        var disabled = 0;
        foreach (var invite in config.Invites)
        {
            if (!invite.Enabled)
            {
                continue;
            }

            var blocked = invite.UseDefaultGroup
                ? defaultBlocked
                : invite.GroupId is { } gid && blockedIds.Contains(gid);
            if (blocked)
            {
                invite.Enabled = false;
                disabled++;
            }
        }

        return disabled;
    }

    /// <summary>
    /// Reports whether a resource URL is an absolute http or https URL, so a resource button can never
    /// carry another scheme onto the public page.
    /// </summary>
    /// <param name="url">The candidate URL.</param>
    /// <returns><c>true</c> when the URL is absolute http or https.</returns>
    public static bool IsValidResourceUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Creates and stores a new invite, hashing the PIN (the plaintext is never persisted).
    /// </summary>
    public Invite Create(CreateInviteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsValidPin(request.Pin))
        {
            throw new ArgumentException("An invite PIN must be exactly six digits.", nameof(request));
        }

        var resources = new List<InviteResource>();
        foreach (var resource in request.Resources)
        {
            var title = resource.Title?.Trim() ?? string.Empty;
            if (title.Length == 0 || !IsValidResourceUrl(resource.Url))
            {
                throw new ArgumentException("Each resource needs a title and an absolute http or https URL.", nameof(request));
            }

            resources.Add(new InviteResource { Title = title, Url = resource.Url.Trim() });
        }

        var target = request.UseDefaultGroup
            ? _groupService.GetDefaultGroup()
            : (request.GroupId is { } gid ? _groupService.FindGroup(gid) : null);
        if (target.BlocksInvites())
        {
            throw new ArgumentException("The group disallows all password changes, so it cannot be used for invites.", nameof(request));
        }

        var trimmedPin = request.Pin?.Trim() ?? string.Empty;
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Token = GenerateToken(),
            Label = (request.Label ?? string.Empty).Trim(),
            Message = (request.Message ?? string.Empty).Trim(),
            Resources = resources,
            PinHash = trimmedPin.Length == 0 ? string.Empty : _cryptoProvider.CreatePasswordHash(trimmedPin).ToString(),
            UseDefaultGroup = request.UseDefaultGroup,
            GroupId = request.UseDefaultGroup ? null : request.GroupId,
            ExpiresAt = request.ExpiresAt,
            MaxUses = request.MaxUses < 0 ? 0 : request.MaxUses,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        Plugin.Instance?.MutateConfiguration(cfg =>
        {
            cfg.Invites.Add(invite);
            return true;
        });

        _activity.Log("Invite '" + DisplayName(invite) + "' was created", "UserManagement.InviteCreated");
        return invite;
    }

    /// <summary>
    /// Generates a 192-bit URL-safe random token.
    /// </summary>
    public static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    /// <summary>Finds an invite by its token, comparing in constant time per candidate.</summary>
    public Invite? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var candidate = System.Text.Encoding.UTF8.GetBytes(token);
        return Plugin.Instance?.ReadConfiguration(c => c.Invites
            .FirstOrDefault(i => TokenMatches(i.Token, candidate)));
    }

    private static bool TokenMatches(string? stored, byte[] candidate)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        var storedBytes = System.Text.Encoding.UTF8.GetBytes(stored);
        return storedBytes.Length == candidate.Length
            && CryptographicOperations.FixedTimeEquals(storedBytes, candidate);
    }

    /// <summary>
    /// Whether the invite is currently redeemable: enabled, not expired, and with uses remaining. Expiry is
    /// checked directly so an invite stops working on its expiry date even before the scheduled task runs.
    /// </summary>
    /// <param name="invite">The invite.</param>
    /// <param name="status">The invite's runtime status, or <c>null</c> when it has never been used.</param>
    /// <returns>Whether the invite can be redeemed right now.</returns>
    public static bool IsRedeemable(Invite invite, InviteStatus? status)
    {
        ArgumentNullException.ThrowIfNull(invite);
        if (!invite.Enabled || IsExpired(invite))
        {
            return false;
        }

        return invite.MaxUses <= 0 || (status?.UsedCount ?? 0) < invite.MaxUses;
    }

    /// <summary>
    /// Whether the invite is redeemable right now, reading the runtime status from the store and
    /// checking that the target group still accepts invites.
    /// </summary>
    /// <param name="invite">The invite.</param>
    /// <returns>Whether the invite can be redeemed right now.</returns>
    public bool IsRedeemableNow(Invite invite)
    {
        ArgumentNullException.ThrowIfNull(invite);
        return IsRedeemable(invite, _statusStore.Load().Invites.GetValueOrDefault(invite.Id))
            && !ResolveTargetGroup(invite).BlocksInvites();
    }

    /// <summary>
    /// Whether the invite has reached its expiry date. Expiry is day based, so an invite is expired on or
    /// after its expiry date, matching the scheduled task that disables it.
    /// </summary>
    /// <param name="invite">The invite to check.</param>
    /// <returns>True when the invite has an expiry date that has been reached.</returns>
    public static bool IsExpired(Invite invite)
    {
        ArgumentNullException.ThrowIfNull(invite);
        return invite.ExpiresAt is { } due && due.Date <= DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Returns every invite in its dashboard shape, with runtime usage merged in from the status store
    /// and the PIN hash redacted to a boolean.
    /// </summary>
    /// <returns>The invite summaries.</returns>
    public List<InviteSummary> GetSummaries()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return new List<InviteSummary>();
        }

        var status = _statusStore.Load();
        return plugin.ReadConfiguration(c => c.Invites
            .Select(i => InviteSummary.FromInvite(i, status.Invites.GetValueOrDefault(i.Id)))
            .ToList());
    }

    /// <summary>
    /// Deletes an invite and its runtime status entry.
    /// </summary>
    /// <param name="id">The invite id.</param>
    /// <returns><c>true</c> if the invite existed and was removed.</returns>
    public bool Delete(Guid id)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return false;
        }

        var removed = false;
        plugin.MutateConfiguration(cfg =>
        {
            removed = cfg.Invites.RemoveAll(i => i.Id.Equals(id)) > 0;
            return removed;
        });

        if (removed)
        {
            var data = _statusStore.Load();
            if (data.Invites.Remove(id))
            {
                _statusStore.Save(data);
            }
        }

        return removed;
    }

    /// <summary>
    /// Disables every still-enabled invite whose expiry date has been reached. Expiry is day-based: an
    /// invite is disabled the first time this runs on or after its expiry date, so the run time of the
    /// scheduled task is when the invite actually stops working.
    /// </summary>
    /// <returns>The number of invites disabled.</returns>
    public int ExpireInvites()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return 0;
        }

        var disabled = 0;
        var expiredNames = new List<string>();
        plugin.MutateConfiguration(cfg =>
        {
            foreach (var invite in cfg.Invites)
            {
                if (invite.Enabled && IsExpired(invite))
                {
                    invite.Enabled = false;
                    expiredNames.Add(DisplayName(invite));
                    disabled++;
                }
            }

            return disabled > 0;
        });

        foreach (var name in expiredNames)
        {
            _activity.Log("Invite '" + name + "' expired and was disabled", "UserManagement.InviteExpired");
        }

        if (disabled > 0)
        {
            _logger.LogInformation("Disabled {Count} expired invite(s)", disabled);
        }

        return disabled;
    }

    /// <summary>
    /// Enables or disables an invite. Enabling also clears the wrong-PIN counter, so an invite that
    /// locked itself after too many PIN attempts gets a clean slate. Enabling is refused while the
    /// invite is past its expiry date (move the expiry instead) or while its target group disallows
    /// all password changes, so a lock applied by switching the group over cannot be undone until the
    /// group leaves that mode.
    /// </summary>
    /// <returns>The outcome.</returns>
    public InviteToggleResult SetEnabled(Guid id, bool enabled)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return InviteToggleResult.NotFound;
        }

        var result = InviteToggleResult.NotFound;
        plugin.MutateConfiguration(cfg =>
        {
            var invite = cfg.Invites.FirstOrDefault(i => i.Id.Equals(id));
            if (invite is null)
            {
                return false;
            }

            if (enabled && IsExpired(invite))
            {
                result = InviteToggleResult.Expired;
                return false;
            }

            if (enabled && ResolveTargetGroup(invite).BlocksInvites())
            {
                result = InviteToggleResult.GroupBlocksInvites;
                return false;
            }

            result = InviteToggleResult.Updated;
            invite.Enabled = enabled;
            return true;
        });

        if (result == InviteToggleResult.Updated && enabled)
        {
            ClearPinFailures(id);
        }

        return result;
    }

    /// <summary>
    /// Clears an invite's wrong PIN counter in the status store, giving a re-enabled or revived invite
    /// a clean slate.
    /// </summary>
    private void ClearPinFailures(Guid id)
    {
        var data = _statusStore.Load();
        if (data.Invites.TryGetValue(id, out var status) && status.FailedPinAttempts != 0)
        {
            status.FailedPinAttempts = 0;
            _statusStore.Save(data);
        }
    }

    /// <summary>
    /// Changes an invite's expiry date. Moving it to a future date (or clearing it) also re-enables the
    /// invite and clears its PIN lockout, so an invite that the expiry task disabled can be revived.
    /// The revival is skipped when the invite's target group disallows all password changes, so the date
    /// still updates but the invite stays disabled until the group leaves that mode.
    /// </summary>
    /// <returns><c>true</c> if the invite existed and was updated.</returns>
    public bool SetExpiry(Guid id, DateTime? expiresAt)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return false;
        }

        var found = false;
        var revived = false;
        plugin.MutateConfiguration(cfg =>
        {
            var invite = cfg.Invites.FirstOrDefault(i => i.Id.Equals(id));
            if (invite is null)
            {
                return false;
            }

            found = true;
            invite.ExpiresAt = expiresAt;
            if ((expiresAt is not { } due || due.Date > DateTime.UtcNow.Date)
                && !ResolveTargetGroup(invite).BlocksInvites())
            {
                invite.Enabled = true;
                revived = true;
            }

            return true;
        });

        if (revived)
        {
            ClearPinFailures(id);
        }

        return found;
    }

    /// <summary>
    /// Validates an invite and, if everything checks out, creates a new (non-admin) account.
    /// </summary>
    public async Task<InviteRedeemResult> RedeemAsync(
        string token,
        string? pin,
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plugin = Plugin.Instance;
            var invite = FindByToken(token);
            if (plugin is null || invite is null)
            {
                return InviteRedeemResult.Fail("This invite link is not valid.");
            }

            if (!invite.Enabled)
            {
                return InviteRedeemResult.Fail("This invite is no longer available.");
            }

            if (IsExpired(invite))
            {
                return InviteRedeemResult.Fail("This invite has expired.");
            }

            var statusData = _statusStore.Load();
            var status = StatusFor(statusData, invite.Id);

            if (invite.MaxUses > 0 && status.UsedCount >= invite.MaxUses)
            {
                return InviteRedeemResult.Fail("This invite has already been fully used.");
            }

            var (rateLimitCount, rateLimitWindowMinutes) = plugin.ReadConfiguration(c =>
                (c.InviteRateLimitCount, c.InviteRateLimitWindowMinutes));
            if (rateLimitCount > 0 && rateLimitWindowMinutes > 0)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-rateLimitWindowMinutes);
                if (status.RecentRedemptions.Count(t => t >= cutoff) >= rateLimitCount)
                {
                    return InviteRedeemResult.Fail("This invite was used recently. Please try again later.");
                }
            }

            if (!string.IsNullOrEmpty(invite.PinHash) && !VerifyPin(invite, pin))
            {
                status.FailedPinAttempts++;
                _statusStore.Save(statusData);
                _activity.Log(
                    "An incorrect PIN was entered for invite '" + DisplayName(invite) + "'",
                    "UserManagement.InvitePinFailed",
                    overview: "Failed attempts: " + status.FailedPinAttempts,
                    severity: LogLevel.Warning);

                if (status.FailedPinAttempts >= Math.Max(1, plugin.ReadConfiguration(c => c.MaxPinAttempts)))
                {
                    // The lockout itself flips the admin facing switch, which is a config write, but it
                    // happens at most once per lockout rather than on every attempt.
                    plugin.MutateConfiguration(cfg =>
                    {
                        var stored = cfg.Invites.FirstOrDefault(i => i.Id.Equals(invite.Id));
                        if (stored is null || !stored.Enabled)
                        {
                            return false;
                        }

                        stored.Enabled = false;
                        return true;
                    });

                    _logger.LogWarning("Invite {InviteId} locked after {Count} wrong PIN attempts", invite.Id, status.FailedPinAttempts);
                    _activity.Log(
                        "Invite '" + DisplayName(invite) + "' was locked after too many incorrect PIN attempts",
                        "UserManagement.InviteLocked",
                        severity: LogLevel.Warning);
                    return InviteRedeemResult.Fail("Too many incorrect PIN attempts. This invite has been locked.");
                }

                return InviteRedeemResult.Fail("Incorrect PIN.");
            }

            username = username?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                return InviteRedeemResult.Fail("Please choose a username.");
            }

            if (string.IsNullOrEmpty(password))
            {
                return InviteRedeemResult.Fail("Please choose a password.");
            }

            var targetGroup = ResolveTargetGroup(invite);

            // Backstop for configuration drift, for example a default group switched to a blocking one
            // after this invite was issued but before the save path disabled it.
            if (targetGroup.BlocksInvites())
            {
                return InviteRedeemResult.Fail("This invite is no longer available.");
            }

            var passwordErrors = PasswordValidator.Validate(password, targetGroup?.Password);
            if (passwordErrors.Count > 0)
            {
                return InviteRedeemResult.Fail(string.Join(" ", passwordErrors));
            }

            if (_userManager.GetUserByName(username) is not null)
            {
                return InviteRedeemResult.Fail("An unknown error occurred. Please check your inputs and try again.");
            }

            Guid userId;
            try
            {
                var created = await _userManager.CreateUserAsync(username).ConfigureAwait(false);
                userId = created.Id;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invite redemption rejected invalid username");
                return InviteRedeemResult.Fail("That username isn't allowed. Try a different one.");
            }

            try
            {
                // The scope lets the password rule provider recognize this as the account creation
                // password rather than a member's self service change, which groups may disallow.
                using (InviteRedemptionScope.Begin())
                {
                    await WithUserRetryAsync(userId, u => _userManager.ChangePassword(u.Id, password)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set password for invited user; rolling back");
                try
                {
                    await _userManager.DeleteUserAsync(userId).ConfigureAwait(false);
                }
                catch (Exception delEx)
                {
                    _logger.LogWarning(delEx, "Rollback delete failed for {UserId}", userId);
                }

                return InviteRedeemResult.Fail("Could not create the account. Please try again.");
            }

            status.UsedCount++;
            status.FailedPinAttempts = 0;
            if (rateLimitWindowMinutes > 0)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-rateLimitWindowMinutes);
                status.RecentRedemptions.RemoveAll(t => t < cutoff);
                status.RecentRedemptions.Add(DateTime.UtcNow);
            }

            // Drop status for invites that no longer exist so the store does not grow without bound.
            var validIds = plugin.ReadConfiguration(c => c.Invites.Select(i => i.Id).ToHashSet());
            foreach (var key in statusData.Invites.Keys.Where(k => !validIds.Contains(k)).ToList())
            {
                statusData.Invites.Remove(key);
            }

            _statusStore.Save(statusData);

            if (targetGroup is not null)
            {
                plugin.MutateConfiguration(cfg =>
                {
                    foreach (var g in cfg.Groups)
                    {
                        g.MemberIds.Remove(userId);
                    }

                    var stored = cfg.Groups.FirstOrDefault(g => g.Id.Equals(targetGroup.Id));
                    (stored ?? targetGroup).MemberIds.Add(userId);
                    return true;
                });

                try
                {
                    await WithUserRetryAsync(userId, u => _groupService.ApplyGroupAsync(u, targetGroup)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply invite group to {UserId}", userId);
                }

                if (targetGroup.Password is { Enabled: true })
                {
                    try
                    {
                        await WithUserRetryAsync(userId, u => _groupService.EnrollAsync(u)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to enroll invited user {UserId} in password rules", userId);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Invite {InviteId} resolved to no group; new user {UserId} left unassigned", invite.Id, userId);
            }

            try
            {
                await WithUserRetryAsync(userId, async u =>
                {
                    var policy = _userManager.GetUserDto(u, string.Empty).Policy;
                    if (policy.IsAdministrator)
                    {
                        policy.IsAdministrator = false;
                        await _userManager.UpdatePolicyAsync(u.Id, policy).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enforce non-admin for {UserId}", userId);
            }

            _logger.LogInformation("Invite {InviteId} redeemed; created user {UserId}", invite.Id, userId);
            _activity.Log(
                "User '" + username + "' was created with invite '" + DisplayName(invite) + "'",
                "UserManagement.InviteRedeemed",
                userId);
            if (invite.MaxUses > 0 && status.UsedCount >= invite.MaxUses)
            {
                _activity.Log(
                    "Invite '" + DisplayName(invite) + "' was fully consumed",
                    "UserManagement.InviteConsumed");
            }

            var success = InviteRedeemResult.Ok("Your account has been created!");

            // Re-validate at serve time so a resource edited through the generic configuration endpoint
            // can never put a non http(s) link in front of the new user.
            success.Resources = invite.Resources
                .Where(r => !string.IsNullOrWhiteSpace(r.Title) && IsValidResourceUrl(r.Url))
                .ToList();
            return success;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WithUserRetryAsync(Guid userId, Func<User, Task> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            var user = _userManager.GetUserById(userId);
            if (user is null)
            {
                return;
            }

            try
            {
                await action(user).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < 5 && IsConcurrency(ex))
            {
                _logger.LogDebug("Concurrency updating user {UserId} (attempt {Attempt}); retrying", userId, attempt);
                await Task.Delay(75 * attempt).ConfigureAwait(false);
            }
        }
    }

    private static bool IsConcurrency(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e.GetType().Name.Contains("Concurrency", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool VerifyPin(Invite invite, string? pin)
    {
        if (string.IsNullOrEmpty(invite.PinHash))
        {
            return true;
        }

        try
        {
            return _cryptoProvider.Verify(PasswordHash.Parse(invite.PinHash), (pin ?? string.Empty).Trim());
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}

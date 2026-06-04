using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Services;
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
    private readonly ILogger<InviteService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteService"/> class.
    /// </summary>
    public InviteService(
        IUserManager userManager,
        GroupService groupService,
        ICryptoProvider cryptoProvider,
        ILogger<InviteService> logger)
    {
        _userManager = userManager;
        _groupService = groupService;
        _cryptoProvider = cryptoProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates and stores a new invite, hashing the PIN (the plaintext is never persisted).
    /// </summary>
    public Invite Create(string? label, string? pin, bool useDefaultGroup, Guid? groupId, DateTime? expiresAt, int maxUses)
    {
        var trimmedPin = pin?.Trim() ?? string.Empty;
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Token = GenerateToken(),
            Label = (label ?? string.Empty).Trim(),
            PinHash = trimmedPin.Length == 0 ? string.Empty : _cryptoProvider.CreatePasswordHash(trimmedPin).ToString(),
            UseDefaultGroup = useDefaultGroup,
            GroupId = useDefaultGroup ? null : groupId,
            ExpiresAt = expiresAt,
            MaxUses = maxUses < 0 ? 0 : maxUses,
            UsedCount = 0,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        Plugin.Instance?.MutateConfiguration(cfg =>
        {
            cfg.Invites.Add(invite);
            return true;
        });

        return invite;
    }

    /// <summary>
    /// Generates a 192-bit URL-safe random token.
    /// </summary>
    public static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    /// <summary>Finds an invite by its token.</summary>
    public Invite? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return Plugin.Instance?.ReadConfiguration(c => c.Invites
            .FirstOrDefault(i => string.Equals(i.Token, token, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Whether the invite is currently redeemable (enabled, uses remaining). Expiry is not checked here:
    /// it is day-based and enforced by the scheduled expiry task, which disables the invite when it runs.
    /// </summary>
    public static bool IsRedeemable(Invite invite)
    {
        ArgumentNullException.ThrowIfNull(invite);
        if (!invite.Enabled)
        {
            return false;
        }

        return invite.MaxUses <= 0 || invite.UsedCount < invite.MaxUses;
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

        var today = DateTime.UtcNow.Date;
        var disabled = 0;
        plugin.MutateConfiguration(cfg =>
        {
            foreach (var invite in cfg.Invites)
            {
                if (invite.Enabled && invite.ExpiresAt is { } due && due.Date <= today)
                {
                    invite.Enabled = false;
                    disabled++;
                }
            }

            return disabled > 0;
        });

        if (disabled > 0)
        {
            _logger.LogInformation("Disabled {Count} expired invite(s)", disabled);
        }

        return disabled;
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

            if (invite.MaxUses > 0 && invite.UsedCount >= invite.MaxUses)
            {
                return InviteRedeemResult.Fail("This invite has already been fully used.");
            }

            var rateLimited = plugin.ReadConfiguration(c =>
            {
                if (c.InviteRateLimitCount <= 0 || c.InviteRateLimitWindowMinutes <= 0)
                {
                    return false;
                }

                var cutoff = DateTime.UtcNow.AddMinutes(-c.InviteRateLimitWindowMinutes);
                return invite.RecentRedemptions.Count(t => t >= cutoff) >= c.InviteRateLimitCount;
            });
            if (rateLimited)
            {
                return InviteRedeemResult.Fail("This invite was used recently. Please try again later.");
            }

            if (!string.IsNullOrEmpty(invite.PinHash) && !VerifyPin(invite, pin))
            {
                var locked = false;
                plugin.MutateConfiguration(cfg =>
                {
                    invite.FailedPinAttempts++;
                    if (invite.FailedPinAttempts >= Math.Max(1, cfg.MaxPinAttempts))
                    {
                        invite.Enabled = false;
                        locked = true;
                    }

                    return true;
                });

                if (locked)
                {
                    _logger.LogWarning("Invite {InviteId} locked after {Count} wrong PIN attempts", invite.Id, invite.FailedPinAttempts);
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

            var targetGroup = invite.UseDefaultGroup
                ? _groupService.GetDefaultGroup()
                : (invite.GroupId is { } gid ? _groupService.FindGroup(gid) : null);

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
                await WithUserRetryAsync(userId, u => _userManager.ChangePassword(u.Id, password)).ConfigureAwait(false);
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

            plugin.MutateConfiguration(cfg =>
            {
                invite.UsedCount++;
                invite.FailedPinAttempts = 0;
                if (cfg.InviteRateLimitWindowMinutes > 0)
                {
                    var cutoff = DateTime.UtcNow.AddMinutes(-cfg.InviteRateLimitWindowMinutes);
                    invite.RecentRedemptions.RemoveAll(t => t < cutoff);
                    invite.RecentRedemptions.Add(DateTime.UtcNow);
                }

                return true;
            });

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
            return InviteRedeemResult.Ok("Your account has been created. You can now sign in.");
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

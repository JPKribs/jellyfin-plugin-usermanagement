using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Groups;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Passwords;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Invites;

/// <summary>
/// Creates and redeems self-service signup invites. Redemption is the only anonymous-reachable
/// write path in the plugin, so all validation (token, PIN, expiry, usage) happens here, server-side,
/// and created accounts are forced to be non-administrators.
/// </summary>
public sealed class InviteService : IDisposable
{
    private readonly IUserManager _userManager;
    private readonly GroupService _groupService;
    private readonly ICryptoProvider _crypto;
    private readonly ILogger<InviteService> _logger;

    // Serializes redemptions so usage counts and PIN-attempt counters can't race.
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteService"/> class.
    /// </summary>
    public InviteService(
        IUserManager userManager,
        GroupService groupService,
        ICryptoProvider crypto,
        ILogger<InviteService> logger)
    {
        _userManager = userManager;
        _groupService = groupService;
        _crypto = crypto;
        _logger = logger;
    }

    /// <summary>
    /// Creates and stores a new invite, hashing the PIN (the plaintext is never persisted).
    /// </summary>
    public Invite Create(string? label, string? pin, Guid? groupId, DateTime? expiresAt, int maxUses)
    {
        var trimmedPin = pin?.Trim() ?? string.Empty;
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Token = GenerateToken(),
            Label = (label ?? string.Empty).Trim(),
            PinHash = trimmedPin.Length == 0 ? string.Empty : _crypto.CreatePasswordHash(trimmedPin).ToString(),
            GroupId = groupId,
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

    /// <summary>Finds an invite by its token (constant work; tokens are unguessable).</summary>
    public Invite? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return Plugin.Instance?.ReadConfiguration(c => c.Invites
            .FirstOrDefault(i => string.Equals(i.Token, token, StringComparison.Ordinal)));
    }

    /// <summary>Whether the invite is currently redeemable (enabled, not expired, uses remaining).</summary>
    public static bool IsRedeemable(Invite invite)
    {
        ArgumentNullException.ThrowIfNull(invite);
        if (!invite.Enabled)
        {
            return false;
        }

        if (invite.ExpiresAt is { } expires && expires <= DateTime.UtcNow)
        {
            return false;
        }

        return invite.MaxUses <= 0 || invite.UsedCount < invite.MaxUses;
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

            if (invite.ExpiresAt is { } expires && expires <= DateTime.UtcNow)
            {
                return InviteRedeemResult.Fail("This invite has expired.");
            }

            if (invite.MaxUses > 0 && invite.UsedCount >= invite.MaxUses)
            {
                return InviteRedeemResult.Fail("This invite has already been fully used.");
            }

            // PIN check with lockout.
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

            // Account details.
            username = username?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                return InviteRedeemResult.Fail("Please choose a username.");
            }

            if (string.IsNullOrEmpty(password))
            {
                return InviteRedeemResult.Fail("Please choose a password.");
            }

            var passwordErrors = PasswordValidator.Validate(password, plugin.Configuration);
            if (passwordErrors.Count > 0)
            {
                return InviteRedeemResult.Fail(string.Join(" ", passwordErrors));
            }

            if (_userManager.GetUserByName(username) is not null)
            {
                return InviteRedeemResult.Fail("That username is already taken.");
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

            // Creating the user fires UserCreated, which our own group event consumer handles on
            // another thread — it can update the new user's policy concurrently. So set the password
            // against a freshly-loaded user and retry on optimistic-concurrency conflicts. If it still
            // fails, roll the account back so we never leave a passwordless orphan.
            try
            {
                await WithUserRetryAsync(userId, u => _userManager.ChangePassword(u, password)).ConfigureAwait(false);
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

            // The account exists with a password — consume the invite now so it can't be over-used,
            // even if a best-effort step below hiccups.
            plugin.MutateConfiguration(_ =>
            {
                invite.UsedCount++;
                invite.FailedPinAttempts = 0;
                return true;
            });

            // Group assignment (invite's group wins; one group at a time). Best-effort.
            if (invite.GroupId is { } groupId)
            {
                var group = _groupService.FindGroup(groupId);
                if (group is not null)
                {
                    plugin.MutateConfiguration(cfg =>
                    {
                        foreach (var g in cfg.Groups)
                        {
                            g.MemberIds.Remove(userId);
                        }

                        group.MemberIds.Add(userId);
                        return true;
                    });

                    try
                    {
                        await WithUserRetryAsync(userId, u => _groupService.ApplyGroupAsync(u, group)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply invite group to {UserId}", userId);
                    }
                }
            }

            // Hard rule: an invited account is never an administrator, whatever a group says.
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

    // Runs an action against a freshly-loaded user, retrying on optimistic-concurrency conflicts
    // (the type is matched by name to avoid a hard EF Core reference).
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

    // Verifies a PIN against the stored salted hash. Fails closed on any malformed hash.
    private bool VerifyPin(Invite invite, string? pin)
    {
        if (string.IsNullOrEmpty(invite.PinHash))
        {
            return true;
        }

        try
        {
            return _crypto.Verify(PasswordHash.Parse(invite.PinHash), (pin ?? string.Empty).Trim());
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

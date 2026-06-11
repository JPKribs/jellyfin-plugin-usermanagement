using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Model.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Authentication provider that enforces the configured password requirements on password changes
/// for users assigned to it. Login verification is delegated to <see cref="ICryptoProvider"/> exactly
/// like the built-in provider, so assigning a user does not change how their existing password works.
/// Administrators are never validated (and are never assigned to this provider).
/// </summary>
public class PasswordRuleAuthenticationProvider : IAuthenticationProvider, IRequiresResolvedUser
{
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ActivityLogger _activity;
    private readonly ILogger<PasswordRuleAuthenticationProvider> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordRuleAuthenticationProvider"/> class.
    /// </summary>
    public PasswordRuleAuthenticationProvider(
        ICryptoProvider cryptoProvider,
        ActivityLogger activity,
        ILogger<PasswordRuleAuthenticationProvider> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _cryptoProvider = cryptoProvider;
        _activity = activity;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string Name => "User Management Password Rules";

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public Task<ProviderAuthenticationResult> Authenticate(string username, string password)
        => throw new NotImplementedException("Authentication requires a resolved user.");

    /// <inheritdoc />
    public Task<ProviderAuthenticationResult> Authenticate(string username, string password, User? resolvedUser)
    {
        ArgumentNullException.ThrowIfNull(resolvedUser);

        var success = string.IsNullOrEmpty(resolvedUser.Password)
            ? string.IsNullOrEmpty(password)
            : _cryptoProvider.Verify(PasswordHash.Parse(resolvedUser.Password), password);

        if (!success)
        {
            throw new AuthenticationException("Invalid username or password.");
        }

        return Task.FromResult(new ProviderAuthenticationResult { Username = resolvedUser.Username });
    }

    /// <inheritdoc />
    public bool HasPassword(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return !string.IsNullOrEmpty(user.Password);
    }

    /// <inheritdoc />
    public Task ChangePassword(User user, string newPassword)
    {
        ArgumentNullException.ThrowIfNull(user);

        var isAdmin = user.HasPermission(PermissionKind.IsAdministrator);

        if (!isAdmin)
        {
            // Enrollment is triggered by membership in any password enforcing group, so the lookup must
            // match: take the first enabled policy among the user's groups, not the first group blindly.
            var policy = Plugin.Instance?.ReadConfiguration(c =>
                c.Groups.Where(g => g.MemberIds.Contains(user.Id))
                    .Select(g => g.Password)
                    .FirstOrDefault(p => p is { Enabled: true }));

            // InitialOnly lets a member with no password set their first one. Invite redemption is
            // always allowed to set the account creation password: it runs anonymously (the new user
            // can already be enrolled by the creation event before redemption sets the password), so
            // without the explicit scope it would look like a member's self service attempt.
            if (policy is { Enabled: true } && policy.ChangeMode != PasswordChangeMode.Allowed && !CallerIsAdministrator())
            {
                var settingFirst = string.IsNullOrEmpty(user.Password);
                var allowed = settingFirst
                    && (policy.ChangeMode == PasswordChangeMode.InitialOnly || InviteRedemptionScope.IsActive);
                if (!allowed)
                {
                    _logger.LogInformation("Rejected password change for {UserId}: the group disallows self service changes", user.Id);
                    _activity.Log(
                        "A password change for '" + user.Username + "' was blocked",
                        "UserManagement.PasswordChangeBlocked",
                        user.Id,
                        "The user's group disallows self service password changes.",
                        LogLevel.Warning);
                    throw new ArgumentException("Password changes are disabled for your account. Ask your server administrator to change it for you.");
                }
            }

            var errors = PasswordValidator.Validate(newPassword, policy);
            if (errors.Count > 0)
            {
                var reasons = string.Join(" ", errors);
                _logger.LogWarning("Rejected password change for {UserId}: {Reasons}", user.Id, reasons);
                _activity.Log(
                    "A password change for '" + user.Username + "' was rejected by group password rules",
                    "UserManagement.PasswordChangeRejected",
                    user.Id,
                    reasons,
                    LogLevel.Warning);

                // ArgumentException maps to HTTP 400 in Jellyfin's exception middleware, so the password
                // form fails fast with an error. AuthenticationException maps to 401, which the web client
                // treats as a lost session and leaves the dialog spinning with no feedback at all.
                throw new ArgumentException(reasons);
            }
        }

        user.Password = string.IsNullOrEmpty(newPassword)
            ? null
            : _cryptoProvider.CreatePasswordHash(newPassword).ToString();

        return Task.CompletedTask;
    }

    // The same core path delivers self service changes, admin resets, and internal calls, so the caller
    // is read from the current request. No request at all means internal server work, which is allowed.
    private bool CallerIsAdministrator()
    {
        var principal = _httpContextAccessor?.HttpContext?.User;
        return principal is null || principal.IsInRole("Administrator");
    }
}

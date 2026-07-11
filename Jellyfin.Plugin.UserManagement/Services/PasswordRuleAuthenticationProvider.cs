using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Model.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Authentication provider that enforces the configured password requirements on password changes
/// for users assigned to it. Login verification is delegated to <see cref="ICryptoProvider"/> exactly
/// like the built-in provider, so assigning a user does not change how their existing password works.
/// Administrators are never validated (and are never assigned to this provider), and changes made by
/// an administrator on a member's behalf bypass enforcement entirely so a reset always works.
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
        // Core probes every provider when the username matches no account. AuthenticationException is
        // the one type its provider loop catches, so anything else would turn a typoed username into
        // an HTTP 500 for the whole login request.
        if (resolvedUser is null)
        {
            throw new AuthenticationException("Invalid username or password.");
        }

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

        // Group rules constrain member self service only: a member changing their own password with
        // their own session. Everything else must pass untouched, because each is a rescue or creation
        // flow with its own gatekeeper. The dashboard's Reset Password is an admin change to the empty
        // string and is the only rescue for a member locked out under rules they cannot satisfy. Core's
        // forgot password flow runs anonymously and sets the password to a pin only an administrator
        // can read off the server. Invite redemption also runs anonymously and validates against its
        // target group before creating the account.
        if (!isAdmin && CallerIsSelfService())
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
            if (policy is { Enabled: true } && policy.ChangeMode != PasswordChangeMode.Allowed)
            {
                var settingFirst = string.IsNullOrEmpty(user.Password);
                var allowed = settingFirst
                    && (policy.ChangeMode == PasswordChangeMode.InitialOnly || InviteRedemptionScope.IsActive);
                if (!allowed)
                {
                    _logger.LogInformation("Rejected password change for {UserId}: the group disallows self service changes", user.Id);
                    _activity.Log(
                        "Password change blocked: " + user.Username,
                        "UserManagement.PasswordChangeBlocked",
                        overview: "The group disallows self-service password changes.",
                        severity: LogLevel.Warning,
                        userId: user.Id);
                    throw new ArgumentException("Password changes are disabled for your account. Ask your server administrator to change it for you.");
                }
            }

            var errors = PasswordValidator.Validate(newPassword, policy);
            if (errors.Count > 0)
            {
                var reasons = string.Join(" ", errors);
                _logger.LogWarning("Rejected password change for {UserId}: {Reasons}", user.Id, reasons);
                _activity.Log(
                    "Password change rejected by group password rules: " + user.Username,
                    "UserManagement.PasswordChangeRejected",
                    overview: reasons,
                    severity: LogLevel.Warning,
                    userId: user.Id);

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

    // The same core path delivers self service changes, admin resets, pin redemptions, invite
    // signups, and internal calls, so the caller is read from the current request. Only an
    // authenticated non administrator is a member acting on their own account: no request at all
    // means internal server work, and an unauthenticated request is an anonymous rescue or signup
    // flow that is gated elsewhere.
    private bool CallerIsSelfService()
    {
        var principal = _httpContextAccessor?.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true && !principal.IsInRole("Administrator");
    }
}

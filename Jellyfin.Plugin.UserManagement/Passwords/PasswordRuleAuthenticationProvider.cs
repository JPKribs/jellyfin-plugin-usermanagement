using System;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Model.Cryptography;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Passwords;

/// <summary>
/// Authentication provider that enforces the configured password requirements on password changes
/// for users assigned to it. Login verification is delegated to <see cref="ICryptoProvider"/> exactly
/// like the built-in provider, so assigning a user does not change how their existing password works.
/// Administrators are never validated (and are never assigned to this provider).
/// </summary>
public class PasswordRuleAuthenticationProvider : IAuthenticationProvider, IRequiresResolvedUser
{
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ILogger<PasswordRuleAuthenticationProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordRuleAuthenticationProvider"/> class.
    /// </summary>
    public PasswordRuleAuthenticationProvider(
        ICryptoProvider cryptoProvider,
        ILogger<PasswordRuleAuthenticationProvider> logger)
    {
        _cryptoProvider = cryptoProvider;
        _logger = logger;
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

        var config = Plugin.Instance?.Configuration;
        var isAdmin = user.HasPermission(PermissionKind.IsAdministrator);

        // Validate for non-admins. The validator decides how to treat an empty password
        // (allowed, or rejected when "disallow empty" is on). Admin accounts are accepted as-is.
        if (config is not null && !isAdmin)
        {
            var errors = PasswordValidator.Validate(newPassword, config);
            if (errors.Count > 0)
            {
                _logger.LogInformation("Rejected password change for {UserId}: requirements not met", user.Id);
                throw new AuthenticationException(string.Join(" ", errors));
            }
        }

        user.Password = string.IsNullOrEmpty(newPassword)
            ? null
            : _cryptoProvider.CreatePasswordHash(newPassword).ToString();

        return Task.CompletedTask;
    }
}

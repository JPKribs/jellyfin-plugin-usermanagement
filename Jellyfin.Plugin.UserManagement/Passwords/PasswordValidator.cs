using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.UserManagement.Configuration;

namespace Jellyfin.Plugin.UserManagement.Passwords;

/// <summary>
/// Checks a candidate password against the configured requirements. Each rule is checked
/// independently so failures produce specific, actionable messages.
/// </summary>
public static class PasswordValidator
{
    /// <summary>
    /// Validates a password against the configured requirements.
    /// </summary>
    /// <param name="password">The candidate password.</param>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns>An empty list when valid; otherwise one message per failed rule.</returns>
    public static IReadOnlyList<string> Validate(string? password, PluginConfiguration config)
    {
        var errors = new List<string>();
        password ??= string.Empty;

        // An empty password is either disallowed outright, or allowed with no further checks
        // (there's nothing to check on "no password").
        if (password.Length == 0)
        {
            if (config.PasswordDisallowEmpty)
            {
                errors.Add("A password is required.");
            }

            return errors;
        }

        if (password.Length < config.PasswordMinLength)
        {
            errors.Add($"Password must be at least {config.PasswordMinLength} characters long.");
        }

        if (config.PasswordRequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one capital letter.");
        }

        if (config.PasswordRequireNumber && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one number.");
        }

        if (config.PasswordRequireSymbol && !password.Any(IsSymbol))
        {
            errors.Add("Password must contain at least one symbol.");
        }

        return errors;
    }

    // A "symbol" is any visible non-alphanumeric, non-whitespace character.
    private static bool IsSymbol(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c);
}

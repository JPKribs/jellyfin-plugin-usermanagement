using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.UserManagement.Models;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Checks a candidate password against a group's password policy. Each rule is checked
/// independently so failures produce specific, actionable messages.
/// </summary>
public static class PasswordValidator
{
    /// <summary>
    /// Validates a password against a group's password policy.
    /// </summary>
    /// <param name="password">The candidate password.</param>
    /// <param name="policy">The group's password policy, or null when no policy applies (no enforcement).</param>
    /// <returns>An empty list when valid; otherwise one message per failed rule.</returns>
    public static IReadOnlyList<string> Validate(string? password, PasswordPolicy? policy)
    {
        var errors = new List<string>();

        if (policy is null || !policy.Enabled)
        {
            return errors;
        }

        password ??= string.Empty;

        if (password.Length == 0)
        {
            if (policy.DisallowEmpty)
            {
                errors.Add("A password is required.");
            }

            return errors;
        }

        if (password.Length < policy.MinLength)
        {
            errors.Add($"Password must be at least {policy.MinLength} characters long.");
        }

        if (policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one capital letter.");
        }

        if (policy.RequireNumber && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one number.");
        }

        if (policy.RequireSymbol && !password.Any(IsSymbol))
        {
            errors.Add("Password must contain at least one symbol.");
        }

        return errors;
    }

    private static bool IsSymbol(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c);
}

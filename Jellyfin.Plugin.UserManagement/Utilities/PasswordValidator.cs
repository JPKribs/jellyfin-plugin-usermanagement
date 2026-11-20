using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.UserManagement.Models;
using JPKribs.Jellyfin.Base;

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
    /// <param name="language">The language of the messages, such as de, or <c>null</c> for US English.</param>
    /// <returns>An empty list when valid; otherwise one message per failed rule.</returns>
    public static IReadOnlyList<string> Validate(string? password, PasswordPolicy? policy, string? language = null)
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
                errors.Add(BaseTranslations.Get(TranslationKeys.ErrorPasswordRequired, language));
            }

            return errors;
        }

        if (password.Length < policy.MinLength)
        {
            errors.Add(BaseTranslations.Format(TranslationKeys.ErrorPasswordTooShort, language, policy.MinLength));
        }

        if (policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add(BaseTranslations.Get(TranslationKeys.ErrorPasswordNeedsCapital, language));
        }

        if (policy.RequireNumber && !password.Any(char.IsDigit))
        {
            errors.Add(BaseTranslations.Get(TranslationKeys.ErrorPasswordNeedsNumber, language));
        }

        if (policy.RequireSymbol && !password.Any(IsSymbol))
        {
            errors.Add(BaseTranslations.Get(TranslationKeys.ErrorPasswordNeedsSymbol, language));
        }

        return errors;
    }

    private static bool IsSymbol(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c);
}

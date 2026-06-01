namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The password complexity rules a group enforces on its enrolled members (and on invite signups
/// that target the group). A group with all rules at their defaults enforces nothing.
/// </summary>
public class PasswordPolicy
{
    /// <summary>
    /// Gets or sets a value indicating whether these rules are enforced on the group's members.
    /// When true, members are enrolled in password-rule enforcement; when false, nothing is enforced
    /// and members are not enrolled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether an empty / no password is disallowed.</summary>
    public bool DisallowEmpty { get; set; }

    /// <summary>Gets or sets the minimum password length.</summary>
    public int MinLength { get; set; } = 8;

    /// <summary>Gets or sets a value indicating whether a capital letter is required.</summary>
    public bool RequireUppercase { get; set; }

    /// <summary>Gets or sets a value indicating whether a number is required.</summary>
    public bool RequireNumber { get; set; }

    /// <summary>Gets or sets a value indicating whether a symbol is required.</summary>
    public bool RequireSymbol { get; set; }
}

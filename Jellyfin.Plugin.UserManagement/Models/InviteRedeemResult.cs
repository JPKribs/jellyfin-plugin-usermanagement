using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The outcome of an invite redemption attempt.
/// </summary>
public class InviteRedeemResult
{
    /// <summary>Gets or sets a value indicating whether an account was created.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets a user-facing message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the resource links shown to the new user after a successful signup.</summary>
    public List<InviteResource> Resources { get; set; } = new();

    /// <summary>Creates a successful result.</summary>
    public static InviteRedeemResult Ok(string message) => new() { Success = true, Message = message };

    /// <summary>Creates a failed result.</summary>
    public static InviteRedeemResult Fail(string message) => new() { Success = false, Message = message };
}

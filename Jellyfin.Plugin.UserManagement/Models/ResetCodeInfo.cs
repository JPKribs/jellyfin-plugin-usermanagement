using System;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// An active password reset code surfaced from one of Jellyfin's <c>passwordreset*.json</c> files,
/// so an administrator can read the code from the dashboard instead of the server's filesystem.
/// </summary>
public class ResetCodeInfo
{
    /// <summary>Gets or sets the user the reset was requested for.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the reset code the user must enter.</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>Gets or sets when the code stops working.</summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the code has already expired.</summary>
    public bool Expired { get; set; }
}

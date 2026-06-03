using System;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Records the authentication provider a user had before being enrolled in password-rule
/// enforcement, so it can be restored when the user is un-enrolled.
/// </summary>
public class ProviderEnrollment
{
    /// <summary>Gets or sets the enrolled user's ID.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the authentication provider ID the user had before enrollment.</summary>
    public string OriginalProviderId { get; set; } = string.Empty;
}

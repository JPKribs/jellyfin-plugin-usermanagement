namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// A link presented to a newly created user on the invite success page, for example a request site,
/// a how-to guide, or a community chat.
/// </summary>
public class InviteResource
{
    /// <summary>Gets or sets the button text shown to the new user.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the absolute http or https URL the button opens.</summary>
    public string Url { get; set; } = string.Empty;
}

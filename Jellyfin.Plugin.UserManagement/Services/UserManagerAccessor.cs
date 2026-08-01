using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Holds the server's <see cref="IUserManager"/> for the one caller outside DI: the configuration
/// save hook on the <c>Plugin</c> class, which needs the current user list to drop administrators and
/// deleted accounts from group membership. Assigned when the service graph is built.
/// </summary>
public static class UserManagerAccessor
{
    /// <summary>Gets or sets the server's user manager, or null before the service graph is built.</summary>
    public static IUserManager? Instance { get; set; }
}

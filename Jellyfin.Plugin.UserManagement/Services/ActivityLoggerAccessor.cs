using JPKribs.Jellyfin.Base;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Holds the DI-registered <see cref="ActivityLogger"/> for the one caller outside DI: the configuration
/// save hook on the <c>Plugin</c> class. Assigned when the singleton is first constructed.
/// </summary>
public static class ActivityLoggerAccessor
{
    /// <summary>Gets or sets the shared activity logger, or null before the service graph is built.</summary>
    public static ActivityLogger? Instance { get; set; }
}

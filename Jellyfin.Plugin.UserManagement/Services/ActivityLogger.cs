using System;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Model.Activity;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Writes the plugin's audit events to Jellyfin's activity log, where administrators already look.
/// Entries are fire and forget and failures are swallowed, since an audit entry must never break the
/// flow it documents. The static instance exists for the one chokepoint without DI access, the
/// configuration save hook on the Plugin class.
/// </summary>
public sealed class ActivityLogger
{
    private readonly IActivityManager _activityManager;
    private readonly ILogger<ActivityLogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityLogger"/> class.
    /// </summary>
    /// <param name="activityManager">Jellyfin's activity manager.</param>
    /// <param name="logger">The logger.</param>
    public ActivityLogger(IActivityManager activityManager, ILogger<ActivityLogger> logger)
    {
        _activityManager = activityManager;
        _logger = logger;
        Instance = this;
    }

    /// <summary>Gets the most recently constructed instance, for callers outside DI.</summary>
    public static ActivityLogger? Instance { get; private set; }

    /// <summary>
    /// Writes an entry to the activity log without awaiting or throwing.
    /// </summary>
    /// <param name="name">The entry headline shown in the activity feed.</param>
    /// <param name="type">The entry type, namespaced under <c>UserManagement.</c>.</param>
    /// <param name="userId">The related user, or default for none.</param>
    /// <param name="overview">Optional detail text.</param>
    /// <param name="severity">The severity, informational by default.</param>
    public void Log(string name, string type, Guid userId = default, string? overview = null, LogLevel severity = LogLevel.Information)
        => _ = WriteAsync(name, type, userId, overview, severity);

    private async Task WriteAsync(string name, string type, Guid userId, string? overview, LogLevel severity)
    {
        try
        {
            await _activityManager.CreateAsync(new ActivityLog(name, type, userId)
            {
                ShortOverview = overview ?? string.Empty,
                LogSeverity = severity
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not write activity log entry of type {Type}", type);
        }
    }
}

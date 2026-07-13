using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that logs out stale devices for members of groups with session cleanup enabled,
/// so the run time determines when cleanup windows take effect.
/// </summary>
public class SessionCleanupTask : IScheduledTask
{
    private readonly SessionCleanupService _sessionCleanup;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCleanupTask"/> class.
    /// </summary>
    public SessionCleanupTask(SessionCleanupService sessionCleanup)
    {
        _sessionCleanup = sessionCleanup;
    }

    /// <inheritdoc />
    public string Name => "Clean Expired Sessions";

    /// <inheritdoc />
    public string Key => "UserManagementSessionCleanup";

    /// <inheritdoc />
    public string Description => "Logs out devices that have not checked back into the server within their group's session cleanup windows.";

    /// <inheritdoc />
    public string Category => "User Management";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _sessionCleanup.CleanupAsync(progress, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.Zero.Ticks
        };
    }
}

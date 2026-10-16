using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Services;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that logs out stale devices for members of groups with session cleanup enabled,
/// so the run time determines when cleanup windows take effect.
/// </summary>
public class SessionCleanupTask : PluginScheduledTask
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
    public override string Name => "Clean Expired Sessions";

    /// <inheritdoc />
    public override string Key => "UserManagementSessionCleanup";

    /// <inheritdoc />
    public override string Description => "Logs out devices that have not checked back into the server within their group's session cleanup windows.";

    /// <inheritdoc />
    public override string Category => "User Management";

    /// <inheritdoc />
    public override async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _sessionCleanup.CleanupAsync(progress, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <inheritdoc />
    public override IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return Daily(TimeSpan.Zero);
    }
}

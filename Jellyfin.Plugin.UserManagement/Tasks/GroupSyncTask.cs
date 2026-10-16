using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.UserManagement.Services;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that reconciles every group-assigned member back to their group's policy,
/// repairing drift caused by direct dashboard edits.
/// </summary>
public class GroupSyncTask : PluginScheduledTask
{
    private readonly GroupService _groupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupSyncTask"/> class.
    /// </summary>
    public GroupSyncTask(GroupService groupService)
    {
        _groupService = groupService;
    }

    /// <inheritdoc />
    public override string Name => "Apply group permissions";

    /// <inheritdoc />
    public override string Key => "UserManagementGroupSync";

    /// <inheritdoc />
    public override string Description => "Re-applies each group's permission template to its members.";

    /// <inheritdoc />
    public override string Category => "User Management";

    /// <inheritdoc />
    public override Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _groupService.SyncAllAsync(progress, cancellationToken);

    /// <inheritdoc />
    public override IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return OnStartup();
    }
}

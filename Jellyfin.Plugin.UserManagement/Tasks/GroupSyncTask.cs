using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.UserManagement.Services;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that reconciles every group-assigned member back to their group's policy,
/// repairing drift caused by direct dashboard edits.
/// </summary>
public class GroupSyncTask : IScheduledTask
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
    public string Name => "Apply group permissions";

    /// <inheritdoc />
    public string Key => "UserManagementGroupSync";

    /// <inheritdoc />
    public string Description => "Re-applies each group's permission template to its members.";

    /// <inheritdoc />
    public string Category => "User Management";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _groupService.SyncAllAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.StartupTrigger
        };
    }
}

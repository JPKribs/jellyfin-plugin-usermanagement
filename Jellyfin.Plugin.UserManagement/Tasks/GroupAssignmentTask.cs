using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.UserManagement.Services;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that adds users who are not yet in any group to the default group, so new
/// accounts inherit the default group's permissions on the next apply.
/// </summary>
public class GroupAssignmentTask : PluginScheduledTask
{
    private readonly GroupService _groupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupAssignmentTask"/> class.
    /// </summary>
    public GroupAssignmentTask(GroupService groupService)
    {
        _groupService = groupService;
    }

    /// <inheritdoc />
    public override string Name => "Add users to groups";

    /// <inheritdoc />
    public override string Key => "UserManagementAssignUsers";

    /// <inheritdoc />
    public override string Description => "Adds users not yet in any group to the default group.";

    /// <inheritdoc />
    public override string Category => "User Management";

    /// <inheritdoc />
    public override Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _groupService.AssignUnassignedToDefault();
        progress.Report(100);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return OnStartup();
    }
}

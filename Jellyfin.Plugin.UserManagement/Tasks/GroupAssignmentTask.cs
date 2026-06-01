using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.UserManagement.Services;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that adds users who are not yet in any group to the default group, so new
/// accounts inherit the default group's permissions on the next apply.
/// </summary>
public class GroupAssignmentTask : IScheduledTask
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
    public string Name => "Add users to groups";

    /// <inheritdoc />
    public string Key => "UserManagementAssignUsers";

    /// <inheritdoc />
    public string Description => "Adds users not yet in any group to the default group.";

    /// <inheritdoc />
    public string Category => "User Management";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _groupService.AssignUnassignedToDefault();
        progress.Report(100);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(12).Ticks
        };
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.UserManagement.Services;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that disables or deletes members of groups whose expiry date has passed,
/// according to each group's configured expiry action. Administrators are never affected.
/// </summary>
public class GroupExpiryTask : IScheduledTask
{
    private readonly GroupService _groupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupExpiryTask"/> class.
    /// </summary>
    public GroupExpiryTask(GroupService groupService)
    {
        _groupService = groupService;
    }

    /// <inheritdoc />
    public string Name => "Apply group expiry";

    /// <inheritdoc />
    public string Key => "UserManagementGroupExpiry";

    /// <inheritdoc />
    public string Description => "Disables or deletes members of groups that have reached their expiry date.";

    /// <inheritdoc />
    public string Category => "User Management";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _groupService.ExpireGroupsAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(24).Ticks
        };
    }
}

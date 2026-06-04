using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Jellyfin.Plugin.UserManagement.Services;

namespace Jellyfin.Plugin.UserManagement.Tasks;

/// <summary>
/// Scheduled task that applies day-based expiry: it disables or deletes members of groups whose expiry
/// date has passed, and disables invites whose expiry date has passed. Expiry takes effect when this
/// task runs, so the run time is the effective expiration time. Administrators are never affected.
/// </summary>
public class GroupExpiryTask : IScheduledTask
{
    private readonly GroupService _groupService;
    private readonly InviteService _inviteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupExpiryTask"/> class.
    /// </summary>
    public GroupExpiryTask(GroupService groupService, InviteService inviteService)
    {
        _groupService = groupService;
        _inviteService = inviteService;
    }

    /// <inheritdoc />
    public string Name => "Process expired and inactive users";

    /// <inheritdoc />
    public string Key => "UserManagementGroupExpiry";

    /// <inheritdoc />
    public string Description => "Disables or deletes members of expired groups, disables inactive members, and disables expired invites.";

    /// <inheritdoc />
    public string Category => "User Management";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _groupService.ExpireGroupsAsync(progress, cancellationToken).ConfigureAwait(false);
        await _groupService.DisableInactiveMembersAsync(cancellationToken).ConfigureAwait(false);
        _inviteService.ExpireInvites();
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

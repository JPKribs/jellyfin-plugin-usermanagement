using System;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.UserManagement.Utilities;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Reacts to user deletion by dropping the account from every group member list and from the
/// enrollment records right away. The normalize pass on the next save or sync would catch the same
/// stale entries, so this only shortens the window in which the dashboard counts a deleted account
/// as a member.
/// </summary>
public class UserDeletedEventConsumer : IEventConsumer<UserDeletedEventArgs>
{
    private readonly ILogger<UserDeletedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeletedEventConsumer"/> class.
    /// </summary>
    public UserDeletedEventConsumer(ILogger<UserDeletedEventConsumer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task OnEvent(UserDeletedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return Task.CompletedTask;
        }

        var userId = eventArgs.Argument.Id;

        try
        {
            // The deleted account is removed directly rather than through a normalize pass. Normalize
            // treats an empty user list as unreadable and leaves membership alone, so a server whose
            // last non admin user was just deleted would otherwise keep the stale entry.
            var changed = false;
            plugin.MutateConfiguration(cfg =>
            {
                var removed = GroupMembership.RemoveMembers(cfg.Groups, id => id.Equals(userId)).Count;
                var enrollments = cfg.ProviderEnrollments.RemoveAll(e => e.UserId.Equals(userId));
                changed = removed > 0 || enrollments > 0;
                return changed;
            });

            if (changed)
            {
                _logger.LogInformation("Removed deleted user {UserId} from group membership", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove deleted user {UserId} from group membership", userId);
        }

        return Task.CompletedTask;
    }
}

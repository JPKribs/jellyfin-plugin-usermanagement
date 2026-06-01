using System;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Passwords;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Groups;

/// <summary>
/// Reacts to user creation: places the new (non-admin) user in the default group, and optionally
/// enrolls them in password-rule enforcement. Clean seam: no polling, no invasive hook.
/// </summary>
public class GroupEventConsumer : IEventConsumer<UserCreatedEventArgs>
{
    private readonly GroupService _groupService;
    private readonly IUserManager _userManager;
    private readonly ILogger<GroupEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupEventConsumer"/> class.
    /// </summary>
    public GroupEventConsumer(GroupService groupService, IUserManager userManager, ILogger<GroupEventConsumer> logger)
    {
        _groupService = groupService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserCreatedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var user = eventArgs.Argument;

        if (user.HasPermission(PermissionKind.IsAdministrator))
        {
            return;
        }

        var defaultGroup = _groupService.GetDefaultGroup();
        if (defaultGroup is not null)
        {
            try
            {
                plugin.MutateConfiguration(cfg =>
                {
                    if (defaultGroup.MemberIds.Contains(user.Id))
                    {
                        return false;
                    }

                    foreach (var group in cfg.Groups)
                    {
                        group.MemberIds.Remove(user.Id);
                    }

                    defaultGroup.MemberIds.Add(user.Id);
                    return true;
                });

                await _groupService.ApplyGroupAsync(user, defaultGroup).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply default group to new user {UserId}", user.Id);
            }
        }

        if (defaultGroup is { Password.Enabled: true })
        {
            try
            {
                var providerId = typeof(PasswordRuleAuthenticationProvider).FullName!;
                if (!string.Equals(user.AuthenticationProviderId, providerId, StringComparison.Ordinal))
                {
                    user.AuthenticationProviderId = providerId;
                    await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enroll new user {UserId} in password rules", user.Id);
            }
        }
    }
}

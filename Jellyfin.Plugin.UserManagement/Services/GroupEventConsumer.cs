using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Reacts to user creation: places the new (non-admin) user in the default group, and optionally
/// enrolls them in password-rule enforcement. Clean seam: no polling, no invasive hook. Accounts created
/// by an invite redemption are left to the redemption, which places them in the invite's own group.
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

        // An invite redemption creates the account inside its scope and then places it in the invite's
        // group itself. Applying the default group here first would leave that group's settings on every
        // field the invite's group inherits, and its password rule enrollment behind. The account is
        // instead held disabled: core creates it with no password, and this event is the earliest point
        // the plugin can reach it, so it stays unusable until the redemption has set the password.
        if (InviteRedemptionScope.IsActive)
        {
            await HoldDisabledAsync(user).ConfigureAwait(false);
            return;
        }

        var defaultGroup = _groupService.GetDefaultGroup();
        if (defaultGroup is not null)
        {
            try
            {
                plugin.MutateConfiguration(cfg =>
                {
                    // Re-resolve inside the lock: a dashboard save between the read above and here
                    // replaces the configuration object, and adding to the group read earlier would
                    // write to a copy that is never persisted.
                    var stored = cfg.Groups.FirstOrDefault(g => g.Id.Equals(defaultGroup.Id));
                    if (stored is null || stored.MemberIds.Contains(user.Id))
                    {
                        return false;
                    }

                    foreach (var group in cfg.Groups)
                    {
                        group.MemberIds.Remove(user.Id);
                    }

                    stored.MemberIds.Add(user.Id);
                    plugin.Normalize(cfg, keepMember: user.Id);
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
                await _groupService.EnrollAsync(user).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enroll new user {UserId} in password rules", user.Id);
            }
        }
    }

    private async Task HoldDisabledAsync(Jellyfin.Database.Implementations.Entities.User user)
    {
        try
        {
            var policy = _userManager.GetUserDto(user, string.Empty).Policy;
            if (!policy.IsDisabled)
            {
                policy.IsDisabled = true;
                await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to hold invited user {UserId} disabled until their password is set", user.Id);
        }
    }
}

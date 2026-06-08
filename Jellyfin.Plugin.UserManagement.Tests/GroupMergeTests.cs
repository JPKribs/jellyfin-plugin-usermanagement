using System;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Model.Users;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the Override / Inherit behavior of <see cref="GroupPermissionsExtensions.ApplyTo"/>.
/// </summary>
public class GroupMergeTests
{
    [Fact]
    public void Merge_UnmanagedPermission_LeavesPolicyUntouched()
    {
        var policy = new UserPolicy { EnableRemoteAccess = false };
        var perms = new GroupPermissions { ManageEnableRemoteAccess = false, EnableRemoteAccess = true };

        perms.ApplyTo(policy, Guid.NewGuid());

        Assert.False(policy.EnableRemoteAccess);
    }

    [Fact]
    public void Merge_ManagedPermission_OverwritesPolicy()
    {
        var policy = new UserPolicy { EnableRemoteAccess = true };
        var perms = new GroupPermissions { ManageEnableRemoteAccess = true, EnableRemoteAccess = false };

        perms.ApplyTo(policy, Guid.NewGuid());

        Assert.False(policy.EnableRemoteAccess);
    }

    [Fact]
    public void Merge_ManagedLibraryAccess_AppliesFoldersAndFlag()
    {
        var folder = Guid.NewGuid();
        var policy = new UserPolicy { EnableAllFolders = true };
        var perms = new GroupPermissions
        {
            ManageLibraryAccess = true,
            EnableAllFolders = false,
            EnabledFolders = { folder }
        };

        perms.ApplyTo(policy, Guid.NewGuid());

        Assert.False(policy.EnableAllFolders);
        Assert.Equal(new[] { folder }, policy.EnabledFolders);
    }

    [Fact]
    public void Merge_ManagedAccessSchedule_ProducesScheduleForUser()
    {
        var userId = Guid.NewGuid();
        var policy = new UserPolicy();
        var perms = new GroupPermissions
        {
            ManageAccessSchedules = true,
            AccessSchedules = { new AccessScheduleEntry { DayOfWeek = "Everyday", StartHour = 8, EndHour = 22 } }
        };

        perms.ApplyTo(policy, userId);

        var schedule = Assert.Single(policy.AccessSchedules);
        Assert.Equal(8, schedule.StartHour);
        Assert.Equal(22, schedule.EndHour);
        Assert.Equal(userId, schedule.UserId);
    }

    [Fact]
    public void Merge_BlockUnratedItems_KeepsOnlyParseableNames()
    {
        var policy = new UserPolicy();
        var perms = new GroupPermissions
        {
            ManageBlockUnratedItems = true,
            BlockUnratedItems = { "Movie", "NotARealUnratedItem" }
        };

        perms.ApplyTo(policy, Guid.NewGuid());

        Assert.Single(policy.BlockUnratedItems);
    }
}

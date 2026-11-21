using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Queries;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Entities.Security;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using Jellyfin.Plugin.UserManagement.Utilities;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// The wording of the plugin's summed and run summary activity entries.
/// </summary>
[Collection("Plugin")]
public class ActivityWordingTests
{
    private static (IActivityManager Manager, List<ActivityLog> Entries) Capture()
    {
        var entries = new List<ActivityLog>();
        var manager = Substitute.For<IActivityManager>();
        manager.CreateAsync(Arg.Do<ActivityLog>(entries.Add)).Returns(Task.CompletedTask);
        return (manager, entries);
    }

    private static async Task<List<ActivityLog>> WaitForAsync(List<ActivityLog> entries, int count)
    {
        // The logger writes off the calling thread, so give it a moment to land.
        for (var i = 0; i < 100 && entries.Count < count; i++)
        {
            await Task.Delay(20);
        }

        return entries;
    }

    [Fact]
    public async Task PasswordRulesEnrolled_SumsUsersIntoOneEntry()
    {
        var (manager, entries) = Capture();
        using var activity = TestSupport.NewActivity(manager);
        activity.Add(UserManagementActivity.PasswordRulesEnrolled, "bob");
        activity.Add(UserManagementActivity.PasswordRulesEnrolled, "alice");
        activity.Summary.Flush();

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("Users enrolled in password rules", entry.Name);
        Assert.Equal("Enrolled 2 users in group password rules: alice, bob", entry.ShortOverview);
        Assert.Equal("UserManagement.PasswordRulesEnrolled", entry.Type);
    }

    [Fact]
    public async Task PasswordRulesUnenrolled_OneUser_ReadsSingular()
    {
        var (manager, entries) = Capture();
        using var activity = TestSupport.NewActivity(manager);
        activity.Add(UserManagementActivity.PasswordRulesUnenrolled, "alice");
        activity.Summary.Flush();

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("User removed from password rules", entry.Name);
        Assert.Equal("Removed alice from group password rules", entry.ShortOverview);
    }

    [Fact]
    public async Task InvitePinFailed_CountsByInvite()
    {
        var (manager, entries) = Capture();
        using var activity = TestSupport.NewActivity(manager);
        activity.Add(UserManagementActivity.InvitePinFailed, "Family");
        activity.Add(UserManagementActivity.InvitePinFailed, "Friends");
        activity.Add(UserManagementActivity.InvitePinFailed, "Family");
        activity.Summary.Flush();

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("PIN attempts failed", entry.Name);
        Assert.Equal("3 incorrect PINs were entered for Family (2), Friends", entry.ShortOverview);
        Assert.Equal("Warning", entry.LogSeverity.ToString());
    }

    [Fact]
    public async Task InvitePinFailed_OneInvite_StatesTheCountOnce()
    {
        var (manager, entries) = Capture();
        using var activity = TestSupport.NewActivity(manager);
        activity.Add(UserManagementActivity.InvitePinFailed, "Family");
        activity.Add(UserManagementActivity.InvitePinFailed, "Family");
        activity.Summary.Flush();

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("2 incorrect PINs were entered for Family", entry.ShortOverview);
    }

    [Fact]
    public async Task InvitePinFailed_OneAttempt_ReadsSingular()
    {
        var (manager, entries) = Capture();
        using var activity = TestSupport.NewActivity(manager);
        activity.Add(UserManagementActivity.InvitePinFailed, "Family");
        activity.Summary.Flush();

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("PIN attempt failed", entry.Name);
        Assert.Equal("An incorrect PIN was entered for Family", entry.ShortOverview);
    }

    [Fact]
    public void UserRunTally_NamesGroupsAndUsers()
    {
        var tally = new UserRunTally();
        tally.Add("Kids", "carol");
        tally.Add("Guests", "alice");
        tally.Add("Kids", "bob");

        Assert.Equal("Users disabled", tally.Name(UserRunWording.ExpiryDisabled, null));
        Assert.Equal(
            "Disabled 3 users when their group expired in Kids (2), Guests: alice, bob, carol",
            tally.Overview(UserRunWording.ExpiryDisabled, null));
    }

    [Fact]
    public void UserRunTally_OneUser_NamesTheUserAndGroup()
    {
        var tally = new UserRunTally();
        tally.Add("Guests", "alice");

        Assert.Equal("User deleted", tally.Name(UserRunWording.ExpiryDeleted, null));
        Assert.Equal("Deleted alice when their group expired in Guests", tally.Overview(UserRunWording.ExpiryDeleted, null));
    }

    [Fact]
    public async Task DisableInactiveMembers_WritesOneEntryForTheRun()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var (manager, entries) = Capture();
        var svc = TestSupport.NewGroupService(um, activityManager: manager);
        var alice = TestSupport.NewUser("alice");
        var bob = TestSupport.NewUser("bob");
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), Name = "Kids", DisableInactiveUsers = true, InactiveDays = 30, MemberIds = { alice.Id, bob.Id } });
            return true;
        });
        foreach (var user in new[] { alice, bob })
        {
            um.GetUserById(user.Id).Returns(user);
            um.GetUserDto(user, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy(), LastActivityDate = DateTime.UtcNow.AddDays(-60) });
        }

        await svc.DisableInactiveMembersAsync(CancellationToken.None);

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("Users disabled", entry.Name);
        Assert.Equal("Disabled 2 users for inactivity in Kids after 30 days: alice, bob", entry.ShortOverview);
        Assert.Equal("UserManagement.InactiveDisabled", entry.Type);
        Assert.Equal(Guid.Empty, entry.UserId);
    }

    [Fact]
    public async Task ExpireGroups_OneUser_AttachesTheUser()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var (manager, entries) = Capture();
        var svc = TestSupport.NewGroupService(um, activityManager: manager);
        var alice = TestSupport.NewUser("alice");
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Guests",
                ExpiresOn = DateTime.UtcNow.AddDays(-1),
                ExpiryAction = GroupExpiryAction.Disable,
                MemberIds = { alice.Id }
            });
            return true;
        });
        um.GetUserById(alice.Id).Returns(alice);
        um.GetUserDto(alice, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy() });

        await svc.ExpireGroupsAsync(null, CancellationToken.None);

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("User disabled", entry.Name);
        Assert.Equal("Disabled alice when their group expired in Guests", entry.ShortOverview);
        Assert.Equal("UserManagement.GroupExpiryDisabled", entry.Type);
        Assert.Equal(alice.Id, entry.UserId);
    }

    [Fact]
    public async Task Cleanup_OneSession_WritesOneEntryForTheUser()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var dm = Substitute.For<IDeviceManager>();
        var sm = Substitute.For<ISessionManager>();
        sm.Logout(Arg.Any<Device>()).Returns(Task.CompletedTask);
        var (manager, entries) = Capture();
        var alice = TestSupport.NewUser("alice");
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition
            {
                Id = Guid.NewGuid(),
                CleanupSessions = true,
                SessionCleanupRules = { new SessionCleanupRule { Mode = SessionCleanupClientMode.All, Days = 7 } },
                MemberIds = { alice.Id }
            });
            return true;
        });
        um.GetUserById(alice.Id).Returns(alice);
        var stale = new Device(alice.Id, "Jellyfin Web", "1.0", "device", Guid.NewGuid().ToString("N")) { DateLastActivity = DateTime.UtcNow.AddDays(-20) };
        dm.GetDevices(Arg.Any<DeviceQuery>()).Returns(new QueryResult<Device>(new[] { stale }));
        var svc = new SessionCleanupService(um, dm, sm, TestSupport.NewActivityLogger(manager), Substitute.For<ILogger<SessionCleanupService>>());

        await svc.CleanupAsync(null, CancellationToken.None);

        var entry = Assert.Single(await WaitForAsync(entries, 1));
        Assert.Equal("Session logged out", entry.Name);
        Assert.Equal("Logged out alice's stale session on Jellyfin Web", entry.ShortOverview);
        Assert.Equal("UserManagement.SessionCleanup", entry.Type);
        Assert.Equal(alice.Id, entry.UserId);
    }

    [Fact]
    public void SessionCleanupOverview_OneSession_NamesTheUser()
    {
        var overview = SessionCleanupService.CleanupOverview(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["Johnny"] = new Dictionary<string, int> { ["Jellyfin Web"] = 1 }
        });
        Assert.Equal("Logged out Johnny's stale session on Jellyfin Web", overview);
    }

    [Fact]
    public void SessionCleanupOverview_ManyUsers_CountsByUserAndApp()
    {
        var overview = SessionCleanupService.CleanupOverview(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["Sarah"] = new Dictionary<string, int> { ["Swiftfin"] = 1 },
            ["Johnny"] = new Dictionary<string, int> { ["Infuse"] = 2 }
        });
        Assert.Equal("Logged out 3 stale sessions for Johnny on Infuse (2) and Sarah on Swiftfin", overview);
    }

    [Fact]
    public void SessionCleanupOverview_OneUserOneApp_StatesTheCountOnce()
    {
        var overview = SessionCleanupService.CleanupOverview(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["Johnny"] = new Dictionary<string, int> { ["Infuse"] = 3 }
        });
        Assert.Equal("Logged out 3 stale sessions for Johnny on Infuse", overview);
    }

    [Fact]
    public void ActivityLists_JoinAnd_JoinsTheLastWithAnd()
    {
        Assert.Equal(string.Empty, ActivityLists.JoinAnd(Array.Empty<string>(), null));
        Assert.Equal("a", ActivityLists.JoinAnd(new[] { "a" }, null));
        Assert.Equal("a, b and c", ActivityLists.JoinAnd(new[] { "a", "b", "c" }, null));
    }

    [Fact]
    public void ActivityLists_Counted_LeavesOutCountsOfOne()
    {
        var counts = new Dictionary<string, int> { ["Swiftfin"] = 1, ["Infuse"] = 2 };
        Assert.Equal("Infuse (2), Swiftfin", ActivityLists.Counted(counts));
        Assert.Equal("Infuse", ActivityLists.Counted(new Dictionary<string, int> { ["Infuse"] = 2 }));
        Assert.Equal("Infuse (2)", ActivityLists.Counted(new Dictionary<string, int> { ["Infuse"] = 2 }, dropSingle: false));
    }

    [Fact]
    public void InviteExpiredOverview_NamesTheInvites()
    {
        Assert.Equal("2 invites were disabled at their expiration date: Family, Friends", InviteService.ExpiredOverview(new[] { "Family", "Friends" }));
        Assert.Equal("The invite Family was disabled at its expiration date", InviteService.ExpiredOverview(new[] { "Family" }));
    }

    [Fact]
    public void PasswordRejectedOverview_JoinsTheBrokenRules()
    {
        var overview = PasswordRuleAuthenticationProvider.RejectedOverview("alice", new[]
        {
            "Password must contain at least one number.",
            "Password must contain at least one symbol."
        });
        Assert.Equal("Rejected alice's new password because it breaks the group's rules: Password must contain at least one number, Password must contain at least one symbol", overview);
    }
}

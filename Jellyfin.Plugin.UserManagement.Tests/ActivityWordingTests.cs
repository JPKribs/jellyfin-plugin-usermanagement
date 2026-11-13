using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;
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
        Assert.Equal("2 Users Enrolled in Password Rules", entry.Name);
        Assert.Equal("2 users were enrolled in group password rules: alice, bob", entry.ShortOverview);
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
        Assert.Equal("1 User Removed from Password Rules", entry.Name);
        Assert.Equal("1 user was removed from group password rules: alice", entry.ShortOverview);
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
        Assert.Equal("3 PIN Attempts Failed", entry.Name);
        Assert.Equal("3 incorrect PINs were entered for Family (2), Friends (1)", entry.ShortOverview);
        Assert.Equal("Warning", entry.LogSeverity.ToString());
    }

    [Fact]
    public void UserRunTally_NamesGroupsAndUsers()
    {
        var tally = new UserRunTally();
        tally.Add("Kids", "carol");
        tally.Add("Guests", "alice");
        tally.Add("Kids", "bob");

        Assert.Equal("3 Users Disabled", tally.Name("Disabled"));
        Assert.Equal(
            "3 users were disabled when their group expired in Kids (2), Guests (1): alice, bob, carol",
            tally.Overview("disabled when their group expired"));
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
        Assert.Equal("2 Users Disabled", entry.Name);
        Assert.Equal("2 users were disabled for inactivity in Kids after 30 days (2): alice, bob", entry.ShortOverview);
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
        Assert.Equal("1 User Disabled", entry.Name);
        Assert.Equal("1 user was disabled when their group expired in Guests (1): alice", entry.ShortOverview);
        Assert.Equal("UserManagement.GroupExpiryDisabled", entry.Type);
        Assert.Equal(alice.Id, entry.UserId);
    }

    [Fact]
    public void SessionCleanupOverview_CountsByApp()
    {
        var overview = SessionCleanupService.CleanupOverview(3, new Dictionary<string, int> { ["Swiftfin"] = 1, ["Infuse"] = 2 });
        Assert.Equal("3 stale sessions were logged out from Infuse (2), Swiftfin (1)", overview);
    }

    [Fact]
    public void InviteExpiredOverview_NamesTheInvites()
    {
        Assert.Equal("2 invites were disabled at their expiry date: Family, Friends", InviteService.ExpiredOverview(new[] { "Family", "Friends" }));
        Assert.Equal("1 invite was disabled at their expiry date: Family", InviteService.ExpiredOverview(new[] { "Family" }));
    }

    [Fact]
    public void PasswordRejectedOverview_JoinsTheBrokenRules()
    {
        var overview = PasswordRuleAuthenticationProvider.RejectedOverview(new[]
        {
            "Password must contain at least one number.",
            "Password must contain at least one symbol."
        });
        Assert.Equal("Password must contain at least one number; Password must contain at least one symbol", overview);
    }
}

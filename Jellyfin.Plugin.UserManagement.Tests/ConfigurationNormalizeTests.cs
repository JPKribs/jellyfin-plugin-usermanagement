using System;
using System.Linq;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller.Library;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the configuration normalize pass that runs on every save: administrators and deleted
/// accounts are dropped from group membership, orphaned enrollment records are pruned, and a default
/// group that no longer exists is cleared.
/// </summary>
[Collection("Plugin")]
public class ConfigurationNormalizeTests : IDisposable
{
    private readonly IUserManager _userManager = TestSupport.NewUserManager();

    public ConfigurationNormalizeTests()
    {
        UserManagerAccessor.Instance = _userManager;
    }

    public void Dispose()
    {
        UserManagerAccessor.Instance = null;
        GC.SuppressFinalize(this);
    }

    private static User NewAdmin(string name = "admin")
    {
        var admin = TestSupport.NewUser(name);
        admin.SetPermission(PermissionKind.IsAdministrator, true);
        return admin;
    }

    private void WithUsers(params User[] users) => _userManager.GetUsers().Returns(users.ToList());

    [Fact]
    public void Normalize_PromotedAdmin_IsDroppedFromMembership()
    {
        var plugin = TestSupport.NewPlugin();
        var admin = NewAdmin();
        var member = TestSupport.NewUser("member");
        WithUsers(admin, member);

        var group = new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { admin.Id, member.Id } };
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });

        var changed = plugin.ReadConfiguration(cfg => plugin.Normalize(cfg));

        Assert.True(changed);
        Assert.Equal(new[] { member.Id }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
    }

    [Fact]
    public void Normalize_PromotedAdmin_KeepsTheirEnrollmentRecord()
    {
        var plugin = TestSupport.NewPlugin();
        var admin = NewAdmin();
        WithUsers(admin);

        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { admin.Id } });
            cfg.ProviderEnrollments.Add(new ProviderEnrollment { UserId = admin.Id, OriginalProviderId = "original" });
            return true;
        });

        plugin.ReadConfiguration(cfg => plugin.Normalize(cfg));

        // The enrollment reconcile needs the record to put them back on their original provider.
        Assert.Single(plugin.ReadConfiguration(c => c.ProviderEnrollments));
    }

    [Fact]
    public void Normalize_DeletedUser_IsDroppedFromMembershipAndEnrollments()
    {
        var plugin = TestSupport.NewPlugin();
        var living = TestSupport.NewUser("living");
        var goneId = Guid.NewGuid();
        WithUsers(living);

        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { living.Id, goneId } });
            cfg.ProviderEnrollments.Add(new ProviderEnrollment { UserId = goneId });
            return true;
        });

        var changed = plugin.ReadConfiguration(cfg => plugin.Normalize(cfg));

        Assert.True(changed);
        Assert.Equal(new[] { living.Id }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
        Assert.Empty(plugin.ReadConfiguration(c => c.ProviderEnrollments));
    }

    [Fact]
    public void Normalize_KeepMember_SurvivesTheDeletedUserPrune()
    {
        // The paths that add a member in the same write pass the id through, so an account the server
        // has not published yet is not mistaken for a deleted one and dropped straight back out.
        var plugin = TestSupport.NewPlugin();
        var existing = TestSupport.NewUser("existing");
        var fresh = Guid.NewGuid();
        WithUsers(existing);

        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { existing.Id, fresh } });
            return true;
        });

        plugin.ReadConfiguration(cfg => plugin.Normalize(cfg, keepMember: fresh));

        Assert.Equal(new[] { existing.Id, fresh }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
    }

    [Fact]
    public void Normalize_UnreadableUserList_LeavesMembershipAlone()
    {
        var plugin = TestSupport.NewPlugin();
        var member = TestSupport.NewUser();
        WithUsers();

        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { member.Id } });
            return true;
        });

        var changed = plugin.ReadConfiguration(cfg => plugin.Normalize(cfg));

        Assert.False(changed);
        Assert.Equal(new[] { member.Id }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
    }

    [Fact]
    public void Normalize_DefaultGroupThatNoLongerExists_IsCleared()
    {
        var plugin = TestSupport.NewPlugin();
        WithUsers(TestSupport.NewUser());
        plugin.MutateConfiguration(cfg => { cfg.DefaultGroupId = Guid.NewGuid(); return true; });

        var changed = plugin.ReadConfiguration(cfg => plugin.Normalize(cfg));

        Assert.True(changed);
        Assert.Null(plugin.ReadConfiguration(c => c.DefaultGroupId));
    }

    [Fact]
    public void Normalize_SettledConfiguration_ReportsNoChange()
    {
        var plugin = TestSupport.NewPlugin();
        var member = TestSupport.NewUser();
        WithUsers(member);

        var groupId = Guid.NewGuid();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = groupId, MemberIds = { member.Id } });
            cfg.DefaultGroupId = groupId;
            return true;
        });

        Assert.False(plugin.ReadConfiguration(cfg => plugin.Normalize(cfg)));
    }

    [Fact]
    public void UpdateConfiguration_RunsTheNormalizePass()
    {
        var plugin = TestSupport.NewPlugin();
        var admin = NewAdmin();
        var member = TestSupport.NewUser("member");
        WithUsers(admin, member);

        var incoming = new Configuration.PluginConfiguration();
        incoming.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { admin.Id, member.Id } });
        plugin.UpdateConfiguration(incoming);

        Assert.Equal(new[] { member.Id }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
    }
}

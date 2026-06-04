using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Stateful tests for <see cref="Services.GroupService"/> lifecycle behavior.
/// </summary>
[Collection("Plugin")]
public class GroupServiceStatefulTests
{
    [Fact]
    public async Task DisableInactiveMembers_InactiveMember_IsDisabled()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), DisableInactiveUsers = true, InactiveDays = 30, MemberIds = { user.Id } });
            return true;
        });
        um.GetUserById(user.Id).Returns(user);
        um.GetUserDto(user, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy(), LastActivityDate = DateTime.UtcNow.AddDays(-60) });

        await svc.DisableInactiveMembersAsync(CancellationToken.None);

        await um.Received().UpdatePolicyAsync(user.Id, Arg.Is<UserPolicy>(p => p.IsDisabled));
    }

    [Fact]
    public async Task DisableInactiveMembers_RecentlyActive_IsNotDisabled()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), DisableInactiveUsers = true, InactiveDays = 30, MemberIds = { user.Id } });
            return true;
        });
        um.GetUserById(user.Id).Returns(user);
        um.GetUserDto(user, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy(), LastActivityDate = DateTime.UtcNow.AddDays(-1) });

        await svc.DisableInactiveMembersAsync(CancellationToken.None);

        await um.DidNotReceive().UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>());
    }

    [Fact]
    public async Task DisableInactiveMembers_NeverSignedIn_IsLeftAlone()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), DisableInactiveUsers = true, InactiveDays = 30, MemberIds = { user.Id } });
            return true;
        });
        um.GetUserById(user.Id).Returns(user);
        um.GetUserDto(user, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy() });

        await svc.DisableInactiveMembersAsync(CancellationToken.None);

        await um.DidNotReceive().UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>());
    }

    [Fact]
    public async Task ExpireGroups_DeleteAction_DeletesMember()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition
            {
                Id = Guid.NewGuid(),
                ExpiresOn = DateTime.UtcNow.AddDays(-1),
                ExpiryAction = GroupExpiryAction.Delete,
                MemberIds = { user.Id }
            });
            return true;
        });
        um.GetUserById(user.Id).Returns(user);

        await svc.ExpireGroupsAsync(null, CancellationToken.None);

        await um.Received().DeleteUserAsync(user.Id);
    }

    [Fact]
    public async Task ApplyGroup_ForceEnabledButExpired_KeepsMemberDisabled()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            ExpiresOn = DateTime.UtcNow.AddDays(-1),
            Permissions = new GroupPermissions { ManageIsDisabled = true, IsDisabled = false }
        };
        um.GetUserDto(user, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy { IsDisabled = false } });

        var applied = await svc.ApplyGroupAsync(user, group);

        Assert.True(applied);
        await um.Received().UpdatePolicyAsync(user.Id, Arg.Is<UserPolicy>(p => p.IsDisabled));
    }

    [Fact]
    public async Task ApplyGroup_ForceEnabledNotExpired_EnablesMember()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Permissions = new GroupPermissions { ManageIsDisabled = true, IsDisabled = false }
        };
        um.GetUserDto(user, Arg.Any<string>()).Returns(new UserDto { Policy = new UserPolicy { IsDisabled = true } });

        await svc.ApplyGroupAsync(user, group);

        await um.Received().UpdatePolicyAsync(user.Id, Arg.Is<UserPolicy>(p => !p.IsDisabled));
    }
}

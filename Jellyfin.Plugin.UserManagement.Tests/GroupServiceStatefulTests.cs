using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
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
    private const string DefaultProviderId = "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";

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

    [Fact]
    public async Task Enroll_DefaultProviderUser_IsSwitchedThroughPolicyUpdate()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        var providerId = typeof(Services.PasswordRuleAuthenticationProvider).FullName;

        await svc.EnrollAsync(user);

        await um.Received().UpdatePolicyAsync(user.Id, Arg.Is<UserPolicy>(p => p.AuthenticationProviderId == providerId));
        Assert.Equal(1, plugin.ReadConfiguration(c => c.ProviderEnrollments.Count));
    }

    [Fact]
    public async Task Enroll_ExternallyAuthenticatedUser_IsLeftOnTheirProvider()
    {
        // Switching an LDAP or SSO user onto the rule provider would validate logins against a local
        // hash they do not have, turning the account into a blank password login.
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        var ldapProvider = "Jellyfin.Plugin.LDAP_Auth.LdapAuthenticationProviderPlugin";
        user.AuthenticationProviderId = ldapProvider;

        await svc.EnrollAsync(user);

        Assert.Equal(ldapProvider, user.AuthenticationProviderId);
        Assert.Equal(0, plugin.ReadConfiguration(c => c.ProviderEnrollments.Count));
        await um.DidNotReceive().UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>());
    }

    [Fact]
    public async Task Reconcile_EnrolledMemberPromotedToAdmin_IsRevertedToTheirOriginalProvider()
    {
        // Left enrolled, they stay on this plugin's authentication provider, and removing the plugin
        // leaves Jellyfin with no provider for that id, which locks the account out entirely.
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var admin = TestSupport.NewUser("admin");
        admin.SetPermission(PermissionKind.IsAdministrator, true);
        admin.AuthenticationProviderId = typeof(Services.PasswordRuleAuthenticationProvider).FullName!;
        um.GetUsers().Returns(new List<User> { admin });

        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition
            {
                Id = Guid.NewGuid(),
                Password = new PasswordPolicy { Enabled = true },
                MemberIds = { admin.Id }
            });
            cfg.ProviderEnrollments.Add(new ProviderEnrollment
            {
                UserId = admin.Id,
                OriginalProviderId = DefaultProviderId
            });
            return true;
        });

        await svc.ReconcileEnrollmentAsync();

        await um.Received().UpdatePolicyAsync(admin.Id, Arg.Is<UserPolicy>(p => p.AuthenticationProviderId == DefaultProviderId));
        Assert.Equal(0, plugin.ReadConfiguration(c => c.ProviderEnrollments.Count));
    }

    [Fact]
    public async Task Reconcile_AdminWhoWasNeverEnrolled_IsLeftAlone()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var admin = TestSupport.NewUser("admin");
        admin.SetPermission(PermissionKind.IsAdministrator, true);
        um.GetUsers().Returns(new List<User> { admin });

        await svc.ReconcileEnrollmentAsync();

        Assert.Equal(DefaultProviderId, admin.AuthenticationProviderId);
        await um.DidNotReceive().UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>());
    }
}

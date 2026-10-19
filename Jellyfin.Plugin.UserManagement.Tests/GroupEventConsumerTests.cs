using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using MediaBrowser.Model.Users;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for <see cref="Services.GroupEventConsumer"/>: a user created outside an invite lands in the
/// default group with its settings and password rules, while an invite created one is left to the
/// redemption and only held disabled.
/// </summary>
[Collection("Plugin")]
public class GroupEventConsumerTests
{
    private static GroupDefinition AddDefaultGroup(Plugin plugin, bool passwordRules)
    {
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Permissions = new GroupPermissions { ManageEnableRemoteAccess = true, EnableRemoteAccess = false },
            Password = new PasswordPolicy { Enabled = passwordRules }
        };
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(group);
            cfg.DefaultGroupId = group.Id;
            return true;
        });
        return group;
    }

    private static (Plugin Plugin, FakeUserServer Server, Jellyfin.Database.Implementations.Entities.User User) Setup(bool admin = false)
    {
        var plugin = TestSupport.NewPlugin();
        var server = new FakeUserServer();
        var user = TestSupport.NewUser("created");
        if (admin)
        {
            user.SetPermission(PermissionKind.IsAdministrator, true);
        }

        server.Users[user.Id] = user;
        server.Policies[user.Id] = new UserPolicy { AuthenticationProviderId = FakeUserServer.DefaultProviderId, EnableRemoteAccess = true, IsAdministrator = admin };
        return (plugin, server, user);
    }

    [Fact]
    public async Task OnEvent_NewUser_JoinsTheDefaultGroupAndIsEnrolled()
    {
        var (plugin, server, user) = Setup();
        var group = AddDefaultGroup(plugin, passwordRules: true);

        await server.Consumer.OnEvent(new UserCreatedEventArgs(user));

        Assert.Equal(new[] { user.Id }, plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == group.Id).MemberIds.ToList()));
        Assert.False(server.Policies[user.Id].EnableRemoteAccess);
        Assert.Equal(typeof(Services.PasswordRuleAuthenticationProvider).FullName, user.AuthenticationProviderId);
    }

    [Fact]
    public async Task OnEvent_DefaultGroupWithoutPasswordRules_DoesNotEnroll()
    {
        var (plugin, server, user) = Setup();
        AddDefaultGroup(plugin, passwordRules: false);

        await server.Consumer.OnEvent(new UserCreatedEventArgs(user));

        Assert.Equal(FakeUserServer.DefaultProviderId, user.AuthenticationProviderId);
        Assert.Empty(plugin.ReadConfiguration(c => c.ProviderEnrollments.ToList()));
    }

    [Fact]
    public async Task OnEvent_Administrator_IsLeftAlone()
    {
        var (plugin, server, user) = Setup(admin: true);
        var group = AddDefaultGroup(plugin, passwordRules: true);

        await server.Consumer.OnEvent(new UserCreatedEventArgs(user));

        Assert.Empty(plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == group.Id).MemberIds.ToList()));
        await server.Um.DidNotReceive().UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>());
    }

    [Fact]
    public async Task OnEvent_NoDefaultGroup_ChangesNothing()
    {
        var (plugin, server, user) = Setup();

        await server.Consumer.OnEvent(new UserCreatedEventArgs(user));

        Assert.Empty(plugin.ReadConfiguration(c => c.Groups.ToList()));
        await server.Um.DidNotReceive().UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>());
    }

    [Fact]
    public async Task OnEvent_DuringInviteRedemption_SkipsTheDefaultGroupAndHoldsTheAccountDisabled()
    {
        var (plugin, server, user) = Setup();
        var group = AddDefaultGroup(plugin, passwordRules: true);

        using (InviteRedemptionScope.Begin())
        {
            await server.Consumer.OnEvent(new UserCreatedEventArgs(user));
        }

        Assert.Empty(plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == group.Id).MemberIds.ToList()));
        Assert.True(server.Policies[user.Id].IsDisabled);
        Assert.True(server.Policies[user.Id].EnableRemoteAccess);
        Assert.Equal(FakeUserServer.DefaultProviderId, user.AuthenticationProviderId);
        Assert.False(InviteRedemptionScope.IsActive);
    }
}

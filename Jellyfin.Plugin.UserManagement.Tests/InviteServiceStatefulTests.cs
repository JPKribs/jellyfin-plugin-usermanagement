using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller.Library;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Stateful tests for <see cref="InviteService"/> against an in-memory configuration.
/// </summary>
[Collection("Plugin")]
public class InviteServiceStatefulTests
{
    private static (Plugin Plugin, IUserManager Um, InviteService Service) Setup()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var groups = TestSupport.NewGroupService(um);
        var service = TestSupport.NewInviteService(um, groups);
        return (plugin, um, service);
    }

    private static Invite AddInvite(Plugin plugin, Action<Invite> configure)
    {
        var invite = new Invite { Id = Guid.NewGuid(), Token = InviteService.GenerateToken(), Enabled = true };
        configure(invite);
        plugin.MutateConfiguration(cfg => { cfg.Invites.Add(invite); return true; });
        return invite;
    }

    [Fact]
    public void Create_NoPin_StoresEnabledInviteWithEmptyHash()
    {
        var (plugin, _, service) = Setup();
        var groupId = Guid.NewGuid();

        var invite = service.Create("Family", null, false, groupId, null, 3);

        Assert.True(invite.Enabled);
        Assert.Equal(string.Empty, invite.PinHash);
        Assert.Equal(groupId, invite.GroupId);
        Assert.Equal(3, invite.MaxUses);
        Assert.Single(plugin.ReadConfiguration(c => c.Invites));
    }

    [Fact]
    public void SetEnabled_ReEnable_ClearsPinLockout()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => { i.Enabled = false; i.FailedPinAttempts = 5; });

        Assert.True(service.SetEnabled(invite.Id, true));

        var stored = plugin.ReadConfiguration(c => c.Invites.Single());
        Assert.True(stored.Enabled);
        Assert.Equal(0, stored.FailedPinAttempts);
    }

    [Fact]
    public void SetEnabled_UnknownId_ReturnsFalse()
    {
        var (_, _, service) = Setup();
        Assert.False(service.SetEnabled(Guid.NewGuid(), true));
    }

    [Fact]
    public void SetExpiry_FutureDate_RevivesInvite()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => { i.Enabled = false; i.FailedPinAttempts = 4; });

        Assert.True(service.SetExpiry(invite.Id, DateTime.UtcNow.AddDays(7)));

        var stored = plugin.ReadConfiguration(c => c.Invites.Single());
        Assert.True(stored.Enabled);
        Assert.Equal(0, stored.FailedPinAttempts);
    }

    [Fact]
    public void SetExpiry_PastDate_DoesNotRevive()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => i.Enabled = false);

        Assert.True(service.SetExpiry(invite.Id, DateTime.UtcNow.AddDays(-1)));

        Assert.False(plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
    }

    [Fact]
    public void ExpireInvites_DisablesPastDatedEnabledInvites()
    {
        var (plugin, _, service) = Setup();
        AddInvite(plugin, i => i.ExpiresAt = DateTime.UtcNow.AddDays(-1));
        AddInvite(plugin, i => i.ExpiresAt = DateTime.UtcNow.AddDays(5));
        AddInvite(plugin, i => i.ExpiresAt = null);

        var disabled = service.ExpireInvites();

        Assert.Equal(1, disabled);
        Assert.Equal(1, plugin.ReadConfiguration(c => c.Invites.Count(i => !i.Enabled)));
    }

    [Fact]
    public async Task RedeemAsync_DisabledInvite_Fails()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => i.Enabled = false);

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RedeemAsync_FullyUsed_Fails()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => { i.MaxUses = 2; i.UsedCount = 2; });

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RedeemAsync_RateLimited_Fails()
    {
        var (plugin, _, service) = Setup();
        plugin.MutateConfiguration(cfg => { cfg.InviteRateLimitCount = 1; cfg.InviteRateLimitWindowMinutes = 5; return true; });
        var invite = AddInvite(plugin, i => i.RecentRedemptions.Add(DateTime.UtcNow));

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RedeemAsync_WrongPin_IncrementsFailures()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => i.PinHash = "not-a-valid-hash");

        var result = await service.RedeemAsync(invite.Token, "0000", "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, plugin.ReadConfiguration(c => c.Invites.Single().FailedPinAttempts));
    }

    [Fact]
    public async Task RedeemAsync_TakenUsername_ReturnsGenericError()
    {
        var (plugin, um, service) = Setup();
        var invite = AddInvite(plugin, _ => { });
        um.GetUserByName("taken").Returns(TestSupport.NewUser("taken"));

        var result = await service.RedeemAsync(invite.Token, null, "taken", "password", CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain("taken", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RedeemAsync_HappyPath_CreatesUserAndCountsUse()
    {
        var (plugin, um, service) = Setup();
        var groupId = Guid.NewGuid();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(new GroupDefinition { Id = groupId, Name = "G" }); return true; });
        var invite = AddInvite(plugin, i => { i.UseDefaultGroup = false; i.GroupId = groupId; });

        var created = TestSupport.NewUser("newuser");
        um.GetUserByName("newuser").Returns((User?)null);
        um.CreateUserAsync("newuser").Returns(Task.FromResult(created));
        um.GetUserById(created.Id).Returns(created);

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.True(result.Success);
        await um.Received().CreateUserAsync("newuser");
        Assert.Equal(1, plugin.ReadConfiguration(c => c.Invites.Single().UsedCount));
    }
}

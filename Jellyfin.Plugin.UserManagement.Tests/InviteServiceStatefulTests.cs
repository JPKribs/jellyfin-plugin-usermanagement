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
        var (plugin, um, service, _) = SetupWithStore();
        return (plugin, um, service);
    }

    private static (Plugin Plugin, IUserManager Um, InviteService Service, InviteStatusStore Store) SetupWithStore()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var groups = TestSupport.NewGroupService(um);
        var store = TestSupport.NewInviteStatusStore();
        var service = TestSupport.NewInviteService(um, groups, store);
        return (plugin, um, service, store);
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

        var invite = service.Create(new CreateInviteRequest
        {
            Label = "Family",
            UseDefaultGroup = false,
            GroupId = groupId,
            MaxUses = 3
        });

        Assert.True(invite.Enabled);
        Assert.Equal(string.Empty, invite.PinHash);
        Assert.Equal(groupId, invite.GroupId);
        Assert.Equal(3, invite.MaxUses);
        Assert.Single(plugin.ReadConfiguration(c => c.Invites));
    }

    [Fact]
    public void Create_WithMessageAndResources_StoresThem()
    {
        var (plugin, _, service) = Setup();

        var invite = service.Create(new CreateInviteRequest
        {
            Label = "Friends",
            Message = "Welcome aboard!",
            Resources = { new InviteResource { Title = "Requests", Url = "https://requests.example.com" } },
            UseDefaultGroup = false,
            GroupId = Guid.NewGuid()
        });

        Assert.Equal("Welcome aboard!", invite.Message);
        var resource = Assert.Single(invite.Resources);
        Assert.Equal("Requests", resource.Title);
    }

    [Theory]
    [InlineData("", "https://ok.example.com")]
    [InlineData("Guide", "javascript:alert(1)")]
    [InlineData("Guide", "ftp://files.example.com")]
    [InlineData("Guide", "/relative/path")]
    public void Create_InvalidResource_Throws(string title, string url)
    {
        var (_, _, service) = Setup();
        Assert.Throws<ArgumentException>(() => service.Create(new CreateInviteRequest
        {
            Resources = { new InviteResource { Title = title, Url = url } },
            UseDefaultGroup = false,
            GroupId = Guid.NewGuid()
        }));
    }

    private static GroupDefinition DisallowedGroup()
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Locked",
            Password = new PasswordPolicy { Enabled = true, ChangeMode = PasswordChangeMode.Disallowed }
        };

    [Fact]
    public void Create_GroupDisallowsPasswordChanges_Throws()
    {
        var (plugin, _, service) = Setup();
        var group = DisallowedGroup();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });

        Assert.Throws<ArgumentException>(() => service.Create(new CreateInviteRequest
        {
            Label = "Locked",
            UseDefaultGroup = false,
            GroupId = group.Id
        }));
        Assert.Empty(plugin.ReadConfiguration(c => c.Invites));
    }

    [Fact]
    public async Task RedeemAsync_TargetGroupBecameDisallowed_Fails()
    {
        var (plugin, _, service) = Setup();
        var group = DisallowedGroup();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });
        var invite = AddInvite(plugin, i => { i.UseDefaultGroup = false; i.GroupId = group.Id; });

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no longer available", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisableInvitesForBlockedGroups_DisablesExplicitAndDefaultGroupInvites()
    {
        var (plugin, _, _) = Setup();
        var blocked = DisallowedGroup();
        var open = new GroupDefinition { Id = Guid.NewGuid(), Name = "Open" };
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(blocked);
            cfg.Groups.Add(open);
            cfg.DefaultGroupId = blocked.Id;
            return true;
        });
        var explicitBlocked = AddInvite(plugin, i => { i.UseDefaultGroup = false; i.GroupId = blocked.Id; });
        var viaDefault = AddInvite(plugin, i => i.UseDefaultGroup = true);
        var stillFine = AddInvite(plugin, i => { i.UseDefaultGroup = false; i.GroupId = open.Id; });

        var disabled = plugin.ReadConfiguration(InviteService.DisableInvitesForBlockedGroups);

        Assert.Equal(2, disabled);
        var invites = plugin.ReadConfiguration(c => c.Invites.ToDictionary(i => i.Id, i => i.Enabled));
        Assert.False(invites[explicitBlocked.Id]);
        Assert.False(invites[viaDefault.Id]);
        Assert.True(invites[stillFine.Id]);
    }

    [Fact]
    public void DisableInvitesForBlockedGroups_AlreadyDisabledInvitesAreNotCounted()
    {
        var (plugin, _, _) = Setup();
        var blocked = DisallowedGroup();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(blocked); return true; });
        AddInvite(plugin, i => { i.Enabled = false; i.UseDefaultGroup = false; i.GroupId = blocked.Id; });

        Assert.Equal(0, plugin.ReadConfiguration(InviteService.DisableInvitesForBlockedGroups));
    }

    [Fact]
    public void SetEnabled_ReEnable_ClearsPinLockout()
    {
        var (plugin, _, service, store) = SetupWithStore();
        var invite = AddInvite(plugin, i => i.Enabled = false);
        var data = new InviteStatusData();
        data.Invites[invite.Id] = new InviteStatus { FailedPinAttempts = 5 };
        store.Save(data);

        Assert.Equal(InviteToggleResult.Updated, service.SetEnabled(invite.Id, true));

        Assert.True(plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
        Assert.Equal(0, store.Load().Invites[invite.Id].FailedPinAttempts);
    }

    [Fact]
    public void SetEnabled_UnknownId_ReportsNotFound()
    {
        var (_, _, service) = Setup();
        Assert.Equal(InviteToggleResult.NotFound, service.SetEnabled(Guid.NewGuid(), true));
    }

    [Fact]
    public void SetEnabled_ExpiredInvite_RefusesToEnable()
    {
        var (plugin, _, service) = Setup();
        var invite = AddInvite(plugin, i => { i.Enabled = false; i.ExpiresAt = DateTime.UtcNow.AddDays(-1); });

        Assert.Equal(InviteToggleResult.Expired, service.SetEnabled(invite.Id, true));
        Assert.False(plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
    }

    [Fact]
    public void SetEnabled_BlockedGroupInvite_RefusesToEnable()
    {
        var (plugin, _, service) = Setup();
        var group = DisallowedGroup();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });
        var invite = AddInvite(plugin, i => { i.Enabled = false; i.UseDefaultGroup = false; i.GroupId = group.Id; });

        Assert.Equal(InviteToggleResult.GroupBlocksInvites, service.SetEnabled(invite.Id, true));
        Assert.False(plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
    }

    [Fact]
    public void SetEnabled_Disable_AlwaysAllowed()
    {
        var (plugin, _, service) = Setup();
        var group = DisallowedGroup();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });
        var invite = AddInvite(plugin, i => { i.UseDefaultGroup = false; i.GroupId = group.Id; });

        Assert.Equal(InviteToggleResult.Updated, service.SetEnabled(invite.Id, false));
        Assert.False(plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
    }

    [Fact]
    public void SetExpiry_BlockedGroupInvite_UpdatesDateWithoutReviving()
    {
        var (plugin, _, service) = Setup();
        var group = DisallowedGroup();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });
        var invite = AddInvite(plugin, i => { i.Enabled = false; i.UseDefaultGroup = false; i.GroupId = group.Id; });

        Assert.True(service.SetExpiry(invite.Id, DateTime.UtcNow.AddDays(7)));

        var stored = plugin.ReadConfiguration(c => c.Invites.Single());
        Assert.False(stored.Enabled);
        Assert.NotNull(stored.ExpiresAt);
    }

    [Fact]
    public void SetExpiry_FutureDate_RevivesInvite()
    {
        var (plugin, _, service, store) = SetupWithStore();
        var invite = AddInvite(plugin, i => i.Enabled = false);
        var data = new InviteStatusData();
        data.Invites[invite.Id] = new InviteStatus { FailedPinAttempts = 4 };
        store.Save(data);

        Assert.True(service.SetExpiry(invite.Id, DateTime.UtcNow.AddDays(7)));

        Assert.True(plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
        Assert.Equal(0, store.Load().Invites[invite.Id].FailedPinAttempts);
    }

    [Fact]
    public async Task RedeemAsync_HappyPath_WritesActivityLogEntry()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var groups = TestSupport.NewGroupService(um);
        var activityManager = Substitute.For<MediaBrowser.Model.Activity.IActivityManager>();
        var service = new InviteService(
            um,
            groups,
            Substitute.For<MediaBrowser.Model.Cryptography.ICryptoProvider>(),
            TestSupport.NewInviteStatusStore(),
            TestSupport.NewActivityLogger(activityManager),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<InviteService>>());

        var groupId = Guid.NewGuid();
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(new GroupDefinition { Id = groupId, Name = "G" }); return true; });
        var invite = AddInvite(plugin, i => { i.UseDefaultGroup = false; i.GroupId = groupId; i.Label = "Family"; });

        var created = TestSupport.NewUser("newuser");
        um.GetUserByName("newuser").Returns((User?)null);
        um.CreateUserAsync("newuser").Returns(Task.FromResult(created));
        um.GetUserById(created.Id).Returns(created);

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.True(result.Success);
        await activityManager.Received().CreateAsync(Arg.Is<Jellyfin.Database.Implementations.Entities.ActivityLog>(
            e => e.Type == "UserManagement.InviteRedeemed" && e.Name.Contains("Family")));
    }

    [Fact]
    public void GetSummaries_MergesStoreCountsIntoTheDashboardShape()
    {
        var (plugin, _, service, store) = SetupWithStore();
        var invite = AddInvite(plugin, i => i.MaxUses = 3);
        var data = new InviteStatusData();
        data.Invites[invite.Id] = new InviteStatus { UsedCount = 3 };
        store.Save(data);

        var summary = service.GetSummaries().Single(s => s.Id == invite.Id);

        Assert.Equal(3, summary.UsedCount);
        Assert.False(service.IsRedeemableNow(invite));
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
        var (plugin, _, service, store) = SetupWithStore();
        var invite = AddInvite(plugin, i => i.MaxUses = 2);
        var data = new InviteStatusData();
        data.Invites[invite.Id] = new InviteStatus { UsedCount = 2 };
        store.Save(data);

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RedeemAsync_RateLimited_Fails()
    {
        var (plugin, _, service, store) = SetupWithStore();
        plugin.MutateConfiguration(cfg => { cfg.InviteRateLimitCount = 1; cfg.InviteRateLimitWindowMinutes = 5; return true; });
        var invite = AddInvite(plugin, _ => { });
        var data = new InviteStatusData();
        data.Invites[invite.Id] = new InviteStatus { RecentRedemptions = { DateTime.UtcNow } };
        store.Save(data);

        var result = await service.RedeemAsync(invite.Token, null, "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RedeemAsync_WrongPin_IncrementsFailures()
    {
        var (plugin, _, service, store) = SetupWithStore();
        var invite = AddInvite(plugin, i => i.PinHash = "not-a-valid-hash");

        var result = await service.RedeemAsync(invite.Token, "0000", "newuser", "password", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, store.Load().Invites[invite.Id].FailedPinAttempts);
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
        var (plugin, um, service, store) = SetupWithStore();
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
        Assert.Equal(1, store.Load().Invites[invite.Id].UsedCount);
    }
}

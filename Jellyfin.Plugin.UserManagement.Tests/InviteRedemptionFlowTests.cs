using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Model.Cryptography;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// End to end invite redemption against <see cref="FakeUserServer"/>, where creating the account raises
/// the user created event exactly as core does. Covers the group the new account ends up with, its
/// password rule enrollment, the disabled hold while it has no password, and the rollback paths.
/// </summary>
[Collection("Plugin")]
public sealed class InviteRedemptionFlowTests : IDisposable
{
    private readonly Guid _folderA = Guid.NewGuid();
    private readonly Plugin _plugin = TestSupport.NewPlugin();
    private readonly FakeUserServer _server = new();
    private readonly InviteStatusStore _store = TestSupport.NewInviteStatusStore();
    private readonly InviteService _service;

    public InviteRedemptionFlowTests()
    {
        UserManagerAccessor.Instance = _server.Um;
        _service = TestSupport.NewInviteService(_server.Um, _server.Groups, _store);
    }

    public void Dispose() => UserManagerAccessor.Instance = null;

    /// <summary>
    /// A default group that restricts libraries and remote access and enforces password rules: the
    /// settings an invite into another group must never leave behind.
    /// </summary>
    private GroupDefinition AddRestrictiveDefaultGroup()
    {
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Default",
            Permissions = new GroupPermissions
            {
                ManageLibraryAccess = true,
                EnableAllFolders = false,
                EnabledFolders = { _folderA },
                ManageEnableRemoteAccess = true,
                EnableRemoteAccess = false
            },
            Password = new PasswordPolicy { Enabled = true, MinLength = 4 }
        };
        _plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(group);
            cfg.DefaultGroupId = group.Id;
            return true;
        });
        return group;
    }

    private GroupDefinition AddGroup(Action<GroupDefinition>? configure = null)
    {
        var group = new GroupDefinition { Id = Guid.NewGuid(), Name = "Target" };
        configure?.Invoke(group);
        _plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });
        return group;
    }

    private Invite AddInvite(Action<Invite> configure)
    {
        var invite = new Invite { Id = Guid.NewGuid(), Token = InviteService.GenerateToken(), Enabled = true };
        configure(invite);
        _plugin.MutateConfiguration(cfg => { cfg.Invites.Add(invite); return true; });
        return invite;
    }

    private Task<InviteRedeemResult> Redeem(Invite invite, string username = "newuser", string? pin = null)
        => _service.RedeemAsync(invite.Token, pin, username, "Passw0rd!", CancellationToken.None);

    [Fact]
    public async Task Redeem_InviteGroupDiffersFromDefault_GetsOnlyTheInviteGroup()
    {
        var defaultGroup = AddRestrictiveDefaultGroup();
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        var user = _server.Single("newuser");
        var policy = _server.Policies[user.Id];

        // The target group inherits library and remote access, so the account keeps the server's own
        // defaults rather than the default group's restrictions.
        Assert.True(policy.EnableAllFolders);
        Assert.Empty(policy.EnabledFolders ?? Array.Empty<Guid>());
        Assert.True(policy.EnableRemoteAccess);
        Assert.False(policy.IsDisabled);
        Assert.False(policy.IsAdministrator);

        // Membership and enrollment follow the invite's group only.
        Assert.Equal(new[] { user.Id }, _plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == target.Id).MemberIds.ToList()));
        Assert.Empty(_plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == defaultGroup.Id).MemberIds.ToList()));
        Assert.Equal(FakeUserServer.DefaultProviderId, user.AuthenticationProviderId);
        Assert.Empty(_plugin.ReadConfiguration(c => c.ProviderEnrollments.ToList()));
    }

    [Fact]
    public async Task Redeem_TargetEnforcesPasswordRules_IsEnrolled()
    {
        AddRestrictiveDefaultGroup();
        var target = AddGroup(g => g.Password = new PasswordPolicy { Enabled = true, MinLength = 4 });
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        var user = _server.Single("newuser");
        Assert.Equal(typeof(PasswordRuleAuthenticationProvider).FullName, user.AuthenticationProviderId);
        var enrollment = Assert.Single(_plugin.ReadConfiguration(c => c.ProviderEnrollments.ToList()));
        Assert.Equal(user.Id, enrollment.UserId);
        Assert.Equal(FakeUserServer.DefaultProviderId, enrollment.OriginalProviderId);
    }

    [Fact]
    public async Task Redeem_DefaultGroupInvite_GetsTheDefaultGroup()
    {
        var defaultGroup = AddRestrictiveDefaultGroup();
        var invite = AddInvite(i => i.UseDefaultGroup = true);

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        var user = _server.Single("newuser");
        var policy = _server.Policies[user.Id];
        Assert.False(policy.EnableAllFolders);
        Assert.Equal(new[] { _folderA }, policy.EnabledFolders);
        Assert.False(policy.EnableRemoteAccess);
        Assert.False(policy.IsDisabled);
        Assert.Contains(user.Id, _plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == defaultGroup.Id).MemberIds.ToList()));
        Assert.Equal(typeof(PasswordRuleAuthenticationProvider).FullName, user.AuthenticationProviderId);
    }

    [Fact]
    public async Task Redeem_PasswordIsSetWhileTheAccountIsDisabled()
    {
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);

        // Created, held disabled by the creation event, password set, then released.
        var events = _server.Events;
        Assert.Equal("create", events[0]);
        Assert.Equal("policy:disabled", events[1]);
        Assert.Contains("password:while-disabled", events);
        Assert.DoesNotContain("password:while-enabled", events);
        Assert.Equal("policy:enabled", events[^1]);
        Assert.False(_server.Policies[_server.Single("newuser").Id].IsDisabled);
    }

    [Fact]
    public async Task Redeem_GroupManagesDisabledFlag_KeepsTheGroupValue()
    {
        var target = AddGroup(g =>
        {
            g.Permissions.ManageIsDisabled = true;
            g.Permissions.IsDisabled = true;
        });
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        Assert.True(_server.Policies[_server.Single("newuser").Id].IsDisabled);
    }

    [Fact]
    public async Task Redeem_PasswordSetFails_RollsBackTheAccountAndDoesNotCountAUse()
    {
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });
        _server.FailPasswordChange = true;

        var result = await Redeem(invite);

        Assert.False(result.Success);
        Assert.Contains("Could not create the account", result.Message, StringComparison.Ordinal);
        Assert.Contains("delete", _server.Events);
        Assert.Empty(_server.Users);
        Assert.Empty(_plugin.ReadConfiguration(c => c.Groups.SelectMany(g => g.MemberIds).ToList()));
        Assert.Equal(0, _store.Find(invite.Id)?.UsedCount ?? 0);
    }

    [Fact]
    public async Task Redeem_ExpiredTargetGroup_IsRefusedWithAClearMessage()
    {
        var target = AddGroup(g => g.ExpiresOn = DateTime.UtcNow.Date.AddDays(-1));
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        var result = await Redeem(invite);

        Assert.False(result.Success);
        Assert.Contains("group has expired", result.Message, StringComparison.Ordinal);
        Assert.Empty(_server.Users);
        Assert.False(_service.IsRedeemableNow(invite));
    }

    [Fact]
    public async Task Redeem_ExpiredDefaultGroup_IsRefusedForDefaultGroupInvites()
    {
        var defaultGroup = AddRestrictiveDefaultGroup();
        _plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Single(g => g.Id == defaultGroup.Id).ExpiresOn = DateTime.UtcNow.Date;
            return true;
        });
        var invite = AddInvite(i => i.UseDefaultGroup = true);

        var result = await Redeem(invite);

        Assert.False(result.Success);
        Assert.Contains("group has expired", result.Message, StringComparison.Ordinal);
        Assert.Empty(_server.Users);
    }

    [Fact]
    public void IsRedeemableNow_GroupExpiringLater_IsStillRedeemable()
    {
        var target = AddGroup(g => g.ExpiresOn = DateTime.UtcNow.Date.AddDays(3));
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        Assert.True(_service.IsRedeemableNow(invite));
    }

    [Fact]
    public async Task Redeem_InviteGroupDeleted_FallsBackToTheDefaultGroup()
    {
        var defaultGroup = AddRestrictiveDefaultGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = Guid.NewGuid(); });

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        var user = _server.Single("newuser");
        Assert.Contains(user.Id, _plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == defaultGroup.Id).MemberIds.ToList()));
        Assert.False(_server.Policies[user.Id].EnableAllFolders);
    }

    [Fact]
    public async Task Redeem_WrongPinAtThreshold_LocksTheInvite()
    {
        _plugin.MutateConfiguration(cfg => { cfg.MaxPinAttempts = 3; return true; });
        var target = AddGroup();
        var service = new InviteService(_server.Um, _server.Groups, new FixedPinCrypto("123456"), _store, TestSupport.NewActivityLogger(), Substitute.For<ILogger<InviteService>>());
        var invite = AddInvite(i =>
        {
            i.UseDefaultGroup = false;
            i.GroupId = target.Id;
            i.PinHash = "$PBKDF2$iterations=1000$00$00";
        });

        var first = await service.RedeemAsync(invite.Token, "000000", "a", "Passw0rd!", CancellationToken.None);
        var second = await service.RedeemAsync(invite.Token, "000000", "a", "Passw0rd!", CancellationToken.None);

        Assert.Equal("Incorrect PIN.", first.Message);
        Assert.Equal("Incorrect PIN.", second.Message);
        Assert.True(_plugin.ReadConfiguration(c => c.Invites.Single().Enabled));

        var third = await service.RedeemAsync(invite.Token, "000000", "a", "Passw0rd!", CancellationToken.None);

        Assert.Contains("locked", third.Message, StringComparison.Ordinal);
        Assert.False(_plugin.ReadConfiguration(c => c.Invites.Single().Enabled));
        Assert.Equal(3, _store.Find(invite.Id)!.FailedPinAttempts);

        // Locked means locked: even the right PIN is refused now.
        var correct = await service.RedeemAsync(invite.Token, "123456", "a", "Passw0rd!", CancellationToken.None);
        Assert.False(correct.Success);
        Assert.Empty(_server.Users);
    }

    [Fact]
    public async Task Redeem_ConcurrentLockoutReset_IsNotOverwritten()
    {
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });
        var other = AddInvite(i => i.Enabled = false);
        _store.Update(d =>
        {
            d.Invites[other.Id] = new InviteStatus { FailedPinAttempts = 4 };
            return true;
        });

        // An administrator re-enables the other invite (clearing its PIN lockout) while this redemption
        // is creating the account.
        _server.OnCreated = _ => _service.SetEnabled(other.Id, true);

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, _store.Find(other.Id)!.FailedPinAttempts);
        Assert.Equal(1, _store.Find(invite.Id)!.UsedCount);
    }

    [Fact]
    public async Task Redeem_InviteDeletedDuringRedemption_IsNotBroughtBackIntoTheStore()
    {
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });
        _server.OnCreated = _ => _service.Delete(invite.Id);

        var result = await Redeem(invite);

        Assert.True(result.Success, result.Message);
        Assert.Null(_store.Find(invite.Id));
    }

    [Fact]
    public async Task Redeem_ConfiguredSignupLimit_PausesTheInvite()
    {
        _plugin.MutateConfiguration(cfg => { cfg.InviteRateLimitCount = 2; cfg.InviteRateLimitWindowMinutes = 60; return true; });
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        Assert.True((await Redeem(invite, "one")).Success);
        Assert.True((await Redeem(invite, "two")).Success);
        var third = await Redeem(invite, "three");

        Assert.False(third.Success);
        Assert.Contains("used recently", third.Message, StringComparison.Ordinal);
        Assert.Equal(2, _server.Users.Count);
    }

    [Fact]
    public async Task Redeem_GroupApplyFails_RollsBackInsteadOfReleasingOnServerDefaults()
    {
        var target = AddGroup(g =>
        {
            g.Permissions.ManageLibraryAccess = true;
            g.Permissions.EnableAllFolders = false;
        });
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        // Only the write that applies the group fails; the hold and the release would both succeed.
        _server.FailPolicyUpdate = p => !p.EnableAllFolders;

        var result = await Redeem(invite);

        Assert.False(result.Success);
        Assert.Contains("Could not create the account", result.Message, StringComparison.Ordinal);
        Assert.Contains("delete", _server.Events);
        Assert.DoesNotContain("policy:enabled", _server.Events);
        Assert.Empty(_server.Users);
        Assert.Empty(_plugin.ReadConfiguration(c => c.Groups.SelectMany(g => g.MemberIds).ToList()));
        Assert.Equal(0, _store.Find(invite.Id)?.UsedCount ?? 0);
    }

    [Fact]
    public async Task Redeem_ConfigurationSaveFails_RollsBackAndTheUsernameCanBeRetried()
    {
        var plugin = TestSupport.NewPlugin(out var xml);
        var target = new GroupDefinition { Id = Guid.NewGuid(), Name = "Target" };
        var invite = new Invite { Id = Guid.NewGuid(), Token = InviteService.GenerateToken(), Enabled = true, UseDefaultGroup = false, GroupId = target.Id };
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(target); cfg.Invites.Add(invite); return true; });

        // Every configuration save from account creation on throws, as a full or read only disk would.
        var failSaves = false;
        xml.When(x => x.SerializeToFile(Arg.Any<object>(), Arg.Any<string>()))
            .Do(_ =>
            {
                if (failSaves)
                {
                    throw new System.IO.IOException("disk full");
                }
            });
        _server.OnCreated = _ => failSaves = true;

        var failed = await Redeem(invite, "retry");

        Assert.False(failed.Success);
        Assert.Contains("Could not create the account", failed.Message, StringComparison.Ordinal);
        Assert.Empty(_server.Users);
        Assert.Empty(plugin.ReadConfiguration(c => c.Groups.SelectMany(g => g.MemberIds).ToList()));
        Assert.Equal(0, _store.Find(invite.Id)?.UsedCount ?? 0);

        failSaves = false;
        _server.OnCreated = null;
        var retried = await Redeem(invite, "retry");

        Assert.True(retried.Success, retried.Message);
        Assert.False(_server.Policies[_server.Single("retry").Id].IsDisabled);
        Assert.Equal(1, _store.Find(invite.Id)!.UsedCount);
    }

    [Fact]
    public async Task AdminCreatesUserDuringARedemption_StillGetsTheDefaultGroupAndIsNotHeld()
    {
        var defaultGroup = AddRestrictiveDefaultGroup();
        var target = AddGroup();
        var invite = AddInvite(i => { i.UseDefaultGroup = false; i.GroupId = target.Id; });

        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.BeforePassword = async () =>
        {
            paused.TrySetResult();
            await resume.Task.ConfigureAwait(false);
        };

        var redemption = Redeem(invite, "invitee");
        await paused.Task;

        // An administrator creates an account from another request while the redemption is mid flight.
        _server.BeforePassword = null;
        var adminCreated = await _server.Um.CreateUserAsync("dashboarduser");

        resume.SetResult();
        var result = await redemption;

        Assert.True(result.Success, result.Message);
        var adminPolicy = _server.Policies[adminCreated.Id];
        Assert.False(adminPolicy.IsDisabled);
        Assert.False(adminPolicy.EnableAllFolders);
        Assert.Contains(adminCreated.Id, _plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == defaultGroup.Id).MemberIds.ToList()));

        var invitee = _server.Single("invitee");
        Assert.Contains(invitee.Id, _plugin.ReadConfiguration(c => c.Groups.Single(g => g.Id == target.Id).MemberIds.ToList()));
        Assert.True(_server.Policies[invitee.Id].EnableAllFolders);
    }

    /// <summary>Accepts exactly one PIN; ICryptoProvider's span parameters cannot be substituted.</summary>
    private sealed class FixedPinCrypto : ICryptoProvider
    {
        private readonly string _pin;

        public FixedPinCrypto(string pin) => _pin = pin;

        public string DefaultHashMethod => "PBKDF2";

        public PasswordHash CreatePasswordHash(ReadOnlySpan<char> password) => throw new NotSupportedException();

        public bool Verify(PasswordHash hash, ReadOnlySpan<char> password) => password.SequenceEqual(_pin);

        public byte[] GenerateSalt() => new byte[16];

        public byte[] GenerateSalt(int length) => new byte[length];
    }
}

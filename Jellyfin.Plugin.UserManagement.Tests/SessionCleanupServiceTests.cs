using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Data.Queries;
using Jellyfin.Database.Implementations.Entities.Security;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for <see cref="SessionCleanupService"/> rule matching and cleanup behavior.
/// </summary>
[Collection("Plugin")]
public class SessionCleanupServiceTests
{
    private static SessionCleanupService NewService(IUserManager um, IDeviceManager dm, ISessionManager sm)
        => new(um, dm, sm, TestSupport.NewActivityLogger(), Substitute.For<ILogger<SessionCleanupService>>());

    private static Device NewDevice(Guid userId, string appName, int daysSinceActivity)
        => new(userId, appName, "1.0", "device", Guid.NewGuid().ToString("N"))
        {
            DateLastActivity = DateTime.UtcNow.AddDays(-daysSinceActivity)
        };

    private static SessionCleanupRule Rule(SessionCleanupClientMode mode, int days, params string[] clients)
        => new() { Mode = mode, Days = days, Clients = new List<string>(clients) };

    [Theory]
    [InlineData(SessionCleanupClientMode.All, "Swiftfin", true)]
    [InlineData(SessionCleanupClientMode.Only, "Swiftfin", true)]
    [InlineData(SessionCleanupClientMode.Only, "Infuse", false)]
    [InlineData(SessionCleanupClientMode.Only, "swiftfin", true)]
    [InlineData(SessionCleanupClientMode.AllExcept, "Swiftfin", false)]
    [InlineData(SessionCleanupClientMode.AllExcept, "Infuse", true)]
    [InlineData(SessionCleanupClientMode.AllExcept, "swiftfin", false)]
    public void RuleMatches_ByMode(SessionCleanupClientMode mode, string appName, bool expected)
    {
        Assert.Equal(expected, SessionCleanupService.RuleMatches(Rule(mode, 30, "Swiftfin"), appName));
    }

    [Fact]
    public void MinMatchingDays_OverlappingRules_ShortestWindowWins()
    {
        var rules = new[]
        {
            Rule(SessionCleanupClientMode.All, 90),
            Rule(SessionCleanupClientMode.Only, 7, "Jellyfin Web")
        };

        Assert.Equal(7, SessionCleanupService.MinMatchingDays(rules, "Jellyfin Web"));
        Assert.Equal(90, SessionCleanupService.MinMatchingDays(rules, "Infuse"));
    }

    [Fact]
    public void MinMatchingDays_NoRuleCoversClient_ReturnsNull()
    {
        var rules = new[] { Rule(SessionCleanupClientMode.Only, 7, "Jellyfin Web") };

        Assert.Null(SessionCleanupService.MinMatchingDays(rules, "Infuse"));
    }

    private static (IUserManager Um, IDeviceManager Dm, ISessionManager Sm, Guid UserId) NewCleanupFixture(
        SessionCleanupRule rule, params Device[] devices)
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var dm = Substitute.For<IDeviceManager>();
        var sm = Substitute.For<ISessionManager>();
        sm.Logout(Arg.Any<Device>()).Returns(Task.CompletedTask);
        var user = TestSupport.NewUser();
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition
            {
                Id = Guid.NewGuid(),
                CleanupSessions = true,
                SessionCleanupRules = { rule },
                MemberIds = { user.Id }
            });
            return true;
        });
        um.GetUserById(user.Id).Returns(user);
        dm.GetDevices(Arg.Any<DeviceQuery>()).Returns(new QueryResult<Device>(devices));
        return (um, dm, sm, user.Id);
    }

    [Fact]
    public async Task Cleanup_StaleMatchingDevice_IsLoggedOut()
    {
        var stale = NewDevice(Guid.NewGuid(), "Jellyfin Web", 20);
        var (um, dm, sm, _) = NewCleanupFixture(Rule(SessionCleanupClientMode.All, 7), stale);

        var removed = await NewService(um, dm, sm).CleanupAsync(null, CancellationToken.None);

        Assert.Equal(1, removed);
        await sm.Received(1).Logout(stale);
    }

    [Fact]
    public async Task Cleanup_RecentDevice_IsKept()
    {
        var fresh = NewDevice(Guid.NewGuid(), "Jellyfin Web", 2);
        var (um, dm, sm, _) = NewCleanupFixture(Rule(SessionCleanupClientMode.All, 7), fresh);

        var removed = await NewService(um, dm, sm).CleanupAsync(null, CancellationToken.None);

        Assert.Equal(0, removed);
        await sm.DidNotReceive().Logout(Arg.Any<Device>());
    }

    [Fact]
    public async Task Cleanup_ClientNotCovered_IsKept()
    {
        var stale = NewDevice(Guid.NewGuid(), "Infuse", 20);
        var (um, dm, sm, _) = NewCleanupFixture(Rule(SessionCleanupClientMode.Only, 7, "Jellyfin Web"), stale);

        var removed = await NewService(um, dm, sm).CleanupAsync(null, CancellationToken.None);

        Assert.Equal(0, removed);
        await sm.DidNotReceive().Logout(Arg.Any<Device>());
    }

    [Fact]
    public async Task Cleanup_ExcludedClient_IsKept()
    {
        var stale = NewDevice(Guid.NewGuid(), "Jellyfin Web", 20);
        var (um, dm, sm, _) = NewCleanupFixture(Rule(SessionCleanupClientMode.AllExcept, 7, "Jellyfin Web"), stale);

        var removed = await NewService(um, dm, sm).CleanupAsync(null, CancellationToken.None);

        Assert.Equal(0, removed);
        await sm.DidNotReceive().Logout(Arg.Any<Device>());
    }

    [Fact]
    public async Task Cleanup_AdminMember_IsSkipped()
    {
        var stale = NewDevice(Guid.NewGuid(), "Jellyfin Web", 20);
        var (um, dm, sm, userId) = NewCleanupFixture(Rule(SessionCleanupClientMode.All, 7), stale);
        var admin = TestSupport.NewUser("admin", userId);
        admin.SetPermission(PermissionKind.IsAdministrator, true);
        um.GetUserById(userId).Returns(admin);

        var removed = await NewService(um, dm, sm).CleanupAsync(null, CancellationToken.None);

        Assert.Equal(0, removed);
        await sm.DidNotReceive().Logout(Arg.Any<Device>());
    }

    [Fact]
    public async Task Cleanup_CleanupDisabled_DoesNothing()
    {
        var stale = NewDevice(Guid.NewGuid(), "Jellyfin Web", 20);
        var (um, dm, sm, userId) = NewCleanupFixture(Rule(SessionCleanupClientMode.All, 7), stale);
        Plugin.Instance!.MutateConfiguration(cfg =>
        {
            cfg.Groups[0].CleanupSessions = false;
            return true;
        });

        var removed = await NewService(um, dm, sm).CleanupAsync(null, CancellationToken.None);

        Assert.Equal(0, removed);
        await sm.DidNotReceive().Logout(Arg.Any<Device>());
    }
}

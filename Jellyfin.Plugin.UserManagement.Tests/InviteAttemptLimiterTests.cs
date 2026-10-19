using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserManagement.Api;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the per client limiter in front of the anonymous invite endpoints, and for the controller
/// answering 429 once a client is over it.
/// </summary>
[Collection("Plugin")]
public class InviteAttemptLimiterTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Redeem_OverTheLimit_IsRefusedUntilTheWindowPasses()
    {
        var clock = new ManualClock();
        var limiter = new InviteAttemptLimiter(clock);

        for (var i = 0; i < InviteAttemptLimiter.MaxRedeemAttempts; i++)
        {
            Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, "203.0.113.5"));
        }

        Assert.False(limiter.TryAcquire(InviteEndpoint.Redeem, "203.0.113.5"));
        Assert.True(limiter.RetryAfterSeconds(InviteEndpoint.Redeem, "203.0.113.5") > 0);

        clock.Now += InviteAttemptLimiter.Window;
        Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, "203.0.113.5"));
    }

    [Fact]
    public void Limits_ArePerClientAndPerEndpoint()
    {
        var limiter = new InviteAttemptLimiter(new ManualClock());
        for (var i = 0; i < InviteAttemptLimiter.MaxRedeemAttempts; i++)
        {
            limiter.TryAcquire(InviteEndpoint.Redeem, "203.0.113.5");
        }

        Assert.False(limiter.TryAcquire(InviteEndpoint.Redeem, "203.0.113.5"));
        Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, "203.0.113.6"));
        Assert.True(limiter.TryAcquire(InviteEndpoint.Info, "203.0.113.5"));
    }

    [Fact]
    public void RefusedAttempts_AreNotCounted()
    {
        var clock = new ManualClock();
        var limiter = new InviteAttemptLimiter(clock);
        for (var i = 0; i < InviteAttemptLimiter.MaxRedeemAttempts; i++)
        {
            limiter.TryAcquire(InviteEndpoint.Redeem, "c");
            clock.Now += TimeSpan.FromSeconds(1);
        }

        for (var i = 0; i < 50; i++)
        {
            Assert.False(limiter.TryAcquire(InviteEndpoint.Redeem, "c"));
        }

        // The first counted attempt ages out of the window and frees exactly one slot.
        clock.Now = clock.Now - TimeSpan.FromSeconds(InviteAttemptLimiter.MaxRedeemAttempts) + InviteAttemptLimiter.Window + TimeSpan.FromMilliseconds(1);
        Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, "c"));
        Assert.False(limiter.TryAcquire(InviteEndpoint.Redeem, "c"));
    }

    [Fact]
    public void Table_IsCappedAndForgetsTheLeastRecentlySeenClient()
    {
        var clock = new ManualClock();
        var limiter = new InviteAttemptLimiter(clock, capacity: 3);

        // "a" uses its whole budget, then three other clients arrive.
        for (var i = 0; i < InviteAttemptLimiter.MaxRedeemAttempts; i++)
        {
            limiter.TryAcquire(InviteEndpoint.Redeem, "a");
        }

        Assert.False(limiter.TryAcquire(InviteEndpoint.Redeem, "a"));
        foreach (var c in new[] { "b", "c", "d" })
        {
            clock.Now += TimeSpan.FromSeconds(1);
            Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, c));
        }

        Assert.Equal(3, limiter.TrackedClients);
    }

    [Fact]
    public void ExpiredClients_AreDroppedAsRequestsArrive()
    {
        var clock = new ManualClock();
        var limiter = new InviteAttemptLimiter(clock);
        for (var i = 0; i < 500; i++)
        {
            limiter.TryAcquire(InviteEndpoint.Info, "10.0.0." + i);
        }

        clock.Now += InviteAttemptLimiter.Window + TimeSpan.FromSeconds(1);
        limiter.TryAcquire(InviteEndpoint.Info, "fresh");

        Assert.Equal(1, limiter.TrackedClients);
    }

    [Theory]
    [InlineData("2001:db8:1:2:aaaa::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:ffff:ffff:ffff:ffff", "2001:db8:1:2::/64")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("203.0.113.9", "203.0.113.9")]
    public void ClientKey_GroupsIPv6ByPrefixAndUnmapsIPv4(string address, string expected)
        => Assert.Equal(expected, Utilities.ClientAddress.Key(IPAddress.Parse(address)));

    [Fact]
    public void RotatingAddressesInOneIPv6Prefix_ShareOneBudget()
    {
        var limiter = new InviteAttemptLimiter(new ManualClock());
        for (var i = 1; i <= InviteAttemptLimiter.MaxRedeemAttempts; i++)
        {
            Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, Utilities.ClientAddress.Key(IPAddress.Parse("2001:db8::" + i.ToString("x", System.Globalization.CultureInfo.InvariantCulture)))));
        }

        Assert.False(limiter.TryAcquire(InviteEndpoint.Redeem, Utilities.ClientAddress.Key(IPAddress.Parse("2001:db8::ffff:1"))));
        Assert.True(limiter.TryAcquire(InviteEndpoint.Redeem, Utilities.ClientAddress.Key(IPAddress.Parse("2001:db8:0:1::1"))));
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.5", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::5", true)]
    [InlineData("::ffff:192.168.1.10", true)]
    [InlineData("203.0.113.9", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("2001:db8::1", false)]
    public void IsPrivate_RecognizesProxyStyleAddresses(string address, bool expected)
        => Assert.Equal(expected, Utilities.ClientAddress.IsPrivate(IPAddress.Parse(address)));

    [Fact]
    public void Controller_UntrustedProxy_IsWarnedAboutOnce()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var service = TestSupport.NewInviteService(um, TestSupport.NewGroupService(um));
        var limiter = new InviteAttemptLimiter(new ManualClock());
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<InviteController>>();

        for (var i = 0; i < 3; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.2");
            context.Request.Headers["X-Forwarded-For"] = "203.0.113." + i;
            var controller = new InviteController(service, limiter, Substitute.For<IServerApplicationPaths>(), logger)
            {
                ControllerContext = new ControllerContext { HttpContext = context }
            };
            controller.Info("nope");
        }

        Assert.Single(logger.ReceivedCalls(), c => c.GetMethodInfo().Name == "Log"
            && (Microsoft.Extensions.Logging.LogLevel)c.GetArguments()[0]! == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public void Controller_DirectClientWithoutForwardedHeader_IsNotWarnedAbout()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var service = TestSupport.NewInviteService(um, TestSupport.NewGroupService(um));
        var limiter = new InviteAttemptLimiter(new ManualClock());
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<InviteController>>();
        var controller = NewControllerWithLogger(service, limiter, "192.168.1.20", logger);

        controller.Info("nope");

        Assert.DoesNotContain(logger.ReceivedCalls(), c => c.GetMethodInfo().Name == "Log");
    }

    private static InviteController NewControllerWithLogger(InviteService service, InviteAttemptLimiter limiter, string ip, Microsoft.Extensions.Logging.ILogger<InviteController> logger)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return new InviteController(service, limiter, Substitute.For<IServerApplicationPaths>(), logger)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static InviteController NewController(InviteService service, InviteAttemptLimiter limiter, string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return new InviteController(service, limiter, Substitute.For<IServerApplicationPaths>(), Substitute.For<Microsoft.Extensions.Logging.ILogger<InviteController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Fact]
    public async Task Controller_FailedRedeemAttemptsCount_AndTheNextIsAnswered429()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var service = TestSupport.NewInviteService(um, TestSupport.NewGroupService(um));
        var limiter = new InviteAttemptLimiter(new ManualClock());
        var controller = NewController(service, limiter, "198.51.100.7");

        // Every attempt fails (unknown token), and every one still counts.
        for (var i = 0; i < InviteAttemptLimiter.MaxRedeemAttempts; i++)
        {
            var ok = Assert.IsType<OkObjectResult>(await controller.Redeem("nope", new RedeemInviteRequest(), CancellationToken.None));
            Assert.Equal(200, ok.StatusCode ?? 200);
        }

        var refused = Assert.IsType<ObjectResult>(await controller.Redeem("nope", new RedeemInviteRequest(), CancellationToken.None));
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.StatusCode);
        Assert.True(controller.Response.Headers.ContainsKey("Retry-After"));

        // Another address is unaffected.
        var other = NewController(service, limiter, "198.51.100.8");
        Assert.IsType<OkObjectResult>(await other.Redeem("nope", new RedeemInviteRequest(), CancellationToken.None));
    }

    [Fact]
    public void Controller_InfoOverTheLimit_IsAnswered429()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var service = TestSupport.NewInviteService(um, TestSupport.NewGroupService(um));
        var limiter = new InviteAttemptLimiter(new ManualClock());
        var controller = NewController(service, limiter, "198.51.100.9");

        var results = Enumerable.Range(0, InviteAttemptLimiter.MaxInfoRequests + 1).Select(_ => controller.Info("nope")).ToList();

        Assert.All(results.Take(InviteAttemptLimiter.MaxInfoRequests), r => Assert.IsType<OkObjectResult>(r));
        var refused = Assert.IsType<ObjectResult>(results[^1]);
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.StatusCode);
    }
}

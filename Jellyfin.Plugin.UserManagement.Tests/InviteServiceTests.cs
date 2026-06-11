using System;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the static helpers on <see cref="InviteService"/>.
/// </summary>
public class InviteServiceTests
{
    [Fact]
    public void GenerateToken_Is48LowercaseHexChars()
    {
        var token = InviteService.GenerateToken();
        Assert.Equal(48, token.Length);
        Assert.Matches("^[0-9a-f]+$", token);
    }

    [Fact]
    public void GenerateToken_ProducesUniqueValues()
    {
        Assert.NotEqual(InviteService.GenerateToken(), InviteService.GenerateToken());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("123456", true)]
    [InlineData(" 123456 ", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12345a", false)]
    [InlineData("12 456", false)]
    [InlineData("①②③④⑤⑥", false)]
    public void IsValidPin_RequiresSixDigitsOrNoPin(string? pin, bool expected)
        => Assert.Equal(expected, InviteService.IsValidPin(pin));

    [Theory]
    [InlineData("https://example.com/path", true)]
    [InlineData("http://example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("/relative", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidResourceUrl_RequiresAbsoluteHttp(string? url, bool expected)
        => Assert.Equal(expected, InviteService.IsValidResourceUrl(url));

    [Fact]
    public void IsRedeemable_FreshInvite_True()
    {
        var invite = new Invite { Enabled = true, MaxUses = 0 };
        Assert.True(InviteService.IsRedeemable(invite, null));
    }

    [Fact]
    public void IsRedeemable_Disabled_False()
    {
        var invite = new Invite { Enabled = false };
        Assert.False(InviteService.IsRedeemable(invite, null));
    }

    [Fact]
    public void IsRedeemable_ExpiredInvite_False()
    {
        // An invite past its expiry date is rejected immediately, not only after the cleanup task runs.
        var invite = new Invite { Enabled = true, ExpiresAt = DateTime.UtcNow.AddDays(-7) };
        Assert.False(InviteService.IsRedeemable(invite, null));
    }

    [Theory]
    [InlineData(-7, true)]   // expired a week ago
    [InlineData(0, true)]    // day based: not valid on the expiry date itself, matching the task
    [InlineData(7, false)]   // a week out, still valid
    public void IsExpired_IsDayBased(int dayOffset, bool expected)
    {
        var invite = new Invite { ExpiresAt = DateTime.UtcNow.AddDays(dayOffset) };
        Assert.Equal(expected, InviteService.IsExpired(invite));
    }

    [Fact]
    public void IsExpired_NoExpiryDate_False()
    {
        Assert.False(InviteService.IsExpired(new Invite { ExpiresAt = null }));
    }

    [Fact]
    public void IsRedeemable_UsesExhausted_False()
    {
        var invite = new Invite { Enabled = true, MaxUses = 2 };
        Assert.False(InviteService.IsRedeemable(invite, new InviteStatus { UsedCount = 2 }));
    }

    [Fact]
    public void IsRedeemable_UsesRemaining_True()
    {
        var invite = new Invite { Enabled = true, MaxUses = 2 };
        Assert.True(InviteService.IsRedeemable(invite, new InviteStatus { UsedCount = 1 }));
    }
}

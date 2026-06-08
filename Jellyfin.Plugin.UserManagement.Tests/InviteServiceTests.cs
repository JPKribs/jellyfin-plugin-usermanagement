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

    [Fact]
    public void IsRedeemable_FreshInvite_True()
    {
        var invite = new Invite { Enabled = true, MaxUses = 0, UsedCount = 0 };
        Assert.True(InviteService.IsRedeemable(invite));
    }

    [Fact]
    public void IsRedeemable_Disabled_False()
    {
        var invite = new Invite { Enabled = false };
        Assert.False(InviteService.IsRedeemable(invite));
    }

    [Fact]
    public void IsRedeemable_ExpiredInvite_False()
    {
        // An invite past its expiry date is rejected immediately, not only after the cleanup task runs.
        var invite = new Invite { Enabled = true, ExpiresAt = DateTime.UtcNow.AddDays(-7) };
        Assert.False(InviteService.IsRedeemable(invite));
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
        var invite = new Invite { Enabled = true, MaxUses = 2, UsedCount = 2 };
        Assert.False(InviteService.IsRedeemable(invite));
    }

    [Fact]
    public void IsRedeemable_UsesRemaining_True()
    {
        var invite = new Invite { Enabled = true, MaxUses = 2, UsedCount = 1 };
        Assert.True(InviteService.IsRedeemable(invite));
    }
}

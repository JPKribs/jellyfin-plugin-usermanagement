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
    public void IsRedeemable_IgnoresExpiryDate_DisabledByTaskInstead()
    {
        var invite = new Invite { Enabled = true, ExpiresAt = DateTime.UtcNow.AddDays(-7) };
        Assert.True(InviteService.IsRedeemable(invite));
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

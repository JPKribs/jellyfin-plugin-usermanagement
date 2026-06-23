using System;
using Jellyfin.Plugin.UserManagement.Models;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the redacted invite shape the dashboard receives. The PIN hash must never leave the
/// server, so the summary type must not even have a property for it.
/// </summary>
public class InviteSummaryTests
{
    [Fact]
    public void FromInvite_WithPin_SetsHasPinAndCarriesNoHash()
    {
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Token = "abc123",
            Label = "Family",
            PinHash = "$PBKDF2$iterations=210000$deadbeef$cafef00d",
            MaxUses = 5
        };

        var summary = InviteSummary.FromInvite(invite, new InviteStatus { UsedCount = 2 });

        Assert.True(summary.HasPin);
        Assert.Equal(invite.Token, summary.Token);
        Assert.Equal(invite.Label, summary.Label);
        Assert.Equal(invite.MaxUses, summary.MaxUses);
        Assert.Equal(2, summary.UsedCount);
        Assert.Null(typeof(InviteSummary).GetProperty("PinHash"));
    }

    [Fact]
    public void FromInvite_WithoutPin_HasPinIsFalse()
    {
        var summary = InviteSummary.FromInvite(new Invite { Id = Guid.NewGuid(), Token = "t" });

        Assert.False(summary.HasPin);
    }

    [Fact]
    public void FromInvite_CarriesMessageAndResources()
    {
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Token = "t",
            Message = "Welcome!",
            Resources =
            {
                new InviteResource { Title = "Requests", Url = "https://requests.example.com" }
            }
        };

        var summary = InviteSummary.FromInvite(invite);

        Assert.Equal("Welcome!", summary.Message);
        var resource = Assert.Single(summary.Resources);
        Assert.Equal("Requests", resource.Title);
        Assert.Equal("https://requests.example.com", resource.Url);
    }
}

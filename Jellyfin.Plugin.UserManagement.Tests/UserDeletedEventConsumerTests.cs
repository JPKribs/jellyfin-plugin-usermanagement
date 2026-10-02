using System;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for <see cref="UserDeletedEventConsumer"/>: a deleted account leaves every group and loses its
/// enrollment record as soon as the server raises the event, and other members are untouched.
/// </summary>
[Collection("Plugin")]
public class UserDeletedEventConsumerTests
{
    private static UserDeletedEventConsumer NewConsumer()
        => new(Substitute.For<ILogger<UserDeletedEventConsumer>>());

    [Fact]
    public async Task OnEvent_DeletedMember_IsRemovedFromGroupAndEnrollments()
    {
        var plugin = TestSupport.NewPlugin();
        var deleted = TestSupport.NewUser("gone");
        var kept = TestSupport.NewUser("kept");
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { deleted.Id, kept.Id } });
            cfg.ProviderEnrollments.Add(new ProviderEnrollment { UserId = deleted.Id, OriginalProviderId = "x" });
            cfg.ProviderEnrollments.Add(new ProviderEnrollment { UserId = kept.Id, OriginalProviderId = "x" });
            return true;
        });

        await NewConsumer().OnEvent(new UserDeletedEventArgs(deleted));

        Assert.Equal(new[] { kept.Id }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
        Assert.Equal(new[] { kept.Id }, plugin.ReadConfiguration(c => c.ProviderEnrollments.ConvertAll(e => e.UserId)));
    }

    [Fact]
    public async Task OnEvent_UnknownUser_LeavesConfigurationUntouched()
    {
        var plugin = TestSupport.NewPlugin();
        var member = TestSupport.NewUser("member");
        plugin.MutateConfiguration(cfg =>
        {
            cfg.Groups.Add(new GroupDefinition { Id = Guid.NewGuid(), MemberIds = { member.Id } });
            return true;
        });

        await NewConsumer().OnEvent(new UserDeletedEventArgs(TestSupport.NewUser("stranger")));

        Assert.Equal(new[] { member.Id }, plugin.ReadConfiguration(c => c.Groups[0].MemberIds));
    }
}

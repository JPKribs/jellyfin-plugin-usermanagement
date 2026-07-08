using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Controller;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests that <see cref="Services.GroupService.ApplyGroupAsync"/> pushes a group's managed
/// <see cref="GroupConfiguration"/> onto the member through <c>UpdateConfigurationAsync</c>, honoring
/// the Override / Inherit model and the <see cref="GroupConfigurationExtensions.ManagesAnything"/> gate.
/// </summary>
[Collection("Plugin")]
public class GroupServiceConfigTests
{
    private static UserDto DtoWith(UserConfiguration config)
        => new() { Policy = new UserPolicy(), Configuration = config };

    [Fact]
    public async Task ApplyGroup_ManagedConfig_UpdatesConfiguration()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        um.GetUserDto(user, Arg.Any<string>())
            .Returns(DtoWith(new UserConfiguration { SubtitleMode = SubtitlePlaybackMode.Default }));

        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Configuration = new GroupConfiguration { ManageSubtitleMode = true, SubtitleMode = "Smart" }
        };

        await svc.ApplyGroupAsync(user, group);

        await um.Received().UpdateConfigurationAsync(
            user.Id,
            Arg.Is<UserConfiguration>(c => c.SubtitleMode == SubtitlePlaybackMode.Smart));
    }

    [Fact]
    public async Task ApplyGroup_ManagesNothing_DoesNotUpdateConfiguration()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        um.GetUserDto(user, Arg.Any<string>()).Returns(DtoWith(new UserConfiguration()));

        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Permissions = new GroupPermissions { ManageEnableRemoteAccess = true, EnableRemoteAccess = false }
        };

        await svc.ApplyGroupAsync(user, group);

        await um.Received().UpdatePolicyAsync(user.Id, Arg.Any<UserPolicy>());
        await um.DidNotReceive().UpdateConfigurationAsync(Arg.Any<Guid>(), Arg.Any<UserConfiguration>());
    }

    [Fact]
    public async Task ApplyGroup_UnmanagedField_IsLeftUntouched()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        um.GetUserDto(user, Arg.Any<string>()).Returns(DtoWith(new UserConfiguration
        {
            SubtitleMode = SubtitlePlaybackMode.None,
            AudioLanguagePreference = "eng"
        }));

        // Manages only the subtitle mode; the audio language preference must survive as the user had it.
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Configuration = new GroupConfiguration { ManageSubtitleMode = true, SubtitleMode = "Smart" }
        };

        await svc.ApplyGroupAsync(user, group);

        await um.Received().UpdateConfigurationAsync(
            user.Id,
            Arg.Is<UserConfiguration>(c =>
                c.SubtitleMode == SubtitlePlaybackMode.Smart && c.AudioLanguagePreference == "eng"));
    }

    [Fact]
    public async Task ApplyGroup_ManagedLibraryLists_AppliesToConfiguration()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var svc = TestSupport.NewGroupService(um);
        var user = TestSupport.NewUser();
        um.GetUserDto(user, Arg.Any<string>()).Returns(DtoWith(new UserConfiguration()));

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Configuration = new GroupConfiguration { ManageOrderedViews = true, OrderedViews = { a, b } }
        };

        await svc.ApplyGroupAsync(user, group);

        await um.Received().UpdateConfigurationAsync(
            user.Id,
            Arg.Is<UserConfiguration>(c => c.OrderedViews.Length == 2 && c.OrderedViews[0] == a && c.OrderedViews[1] == b));
    }

    [Fact]
    public async Task ApplyGroup_ManagedHomeSections_WritesOrderedSectionsToDisplayPreferences()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var user = TestSupport.NewUser();
        um.GetUserDto(user, Arg.Any<string>()).Returns(DtoWith(new UserConfiguration()));

        var displayPrefs = Substitute.For<IDisplayPreferencesManager>();
        var entity = new DisplayPreferences(user.Id, Guid.Empty, "emby");
        displayPrefs.GetDisplayPreferences(user.Id, Arg.Any<Guid>(), "emby").Returns(entity);
        var svc = TestSupport.NewGroupService(um, displayPrefs);

        var group = new GroupDefinition
        {
            Id = Guid.NewGuid(),
            Configuration = new GroupConfiguration
            {
                ManageHomeSections = true,
                HomeSections = { "resume", "nextup", "none" }
            }
        };

        await svc.ApplyGroupAsync(user, group);

        // The web section keys map (case-insensitively) onto HomeSectionType, ordered by slot.
        Assert.Equal(3, entity.HomeSections.Count);
        var ordered = entity.HomeSections.OrderBy(h => h.Order).ToList();
        Assert.Equal(HomeSectionType.Resume, ordered[0].Type);
        Assert.Equal(HomeSectionType.NextUp, ordered[1].Type);
        Assert.Equal(HomeSectionType.None, ordered[2].Type);
        displayPrefs.Received().UpdateDisplayPreferences(entity);
    }

    [Fact]
    public async Task ApplyGroup_UnmanagedHomeSections_DoesNotTouchDisplayPreferences()
    {
        TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var user = TestSupport.NewUser();
        um.GetUserDto(user, Arg.Any<string>()).Returns(DtoWith(new UserConfiguration()));

        var displayPrefs = Substitute.For<IDisplayPreferencesManager>();
        var svc = TestSupport.NewGroupService(um, displayPrefs);

        var group = new GroupDefinition { Id = Guid.NewGuid(), Configuration = new GroupConfiguration() };

        await svc.ApplyGroupAsync(user, group);

        displayPrefs.DidNotReceive().UpdateDisplayPreferences(Arg.Any<DisplayPreferences>());
    }
}

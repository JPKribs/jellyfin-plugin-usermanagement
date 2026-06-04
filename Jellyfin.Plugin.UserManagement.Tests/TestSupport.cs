using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Shared setup for tests that exercise the stateful services against an in-memory plugin
/// configuration and a substituted <see cref="IUserManager"/>.
/// </summary>
internal static class TestSupport
{
    /// <summary>Creates a fresh plugin instance backed by a default in-memory configuration.</summary>
    public static Plugin NewPlugin()
    {
        var paths = Substitute.For<IApplicationPaths>();
        paths.PluginConfigurationsPath.Returns(Path.Combine(Path.GetTempPath(), "um-tests-" + Guid.NewGuid()));
        var xml = Substitute.For<IXmlSerializer>();
        var plugin = new Plugin(paths, xml, Substitute.For<ILogger<Plugin>>());
        plugin.UpdateConfiguration(new PluginConfiguration());
        return plugin;
    }

    /// <summary>Creates a substituted user manager with all async members returning completed tasks.</summary>
    public static IUserManager NewUserManager()
    {
        var um = Substitute.For<IUserManager>();
        um.UpdatePolicyAsync(Arg.Any<Guid>(), Arg.Any<UserPolicy>()).Returns(Task.CompletedTask);
        um.UpdateUserAsync(Arg.Any<User>()).Returns(Task.CompletedTask);
        um.DeleteUserAsync(Arg.Any<Guid>()).Returns(Task.CompletedTask);
        um.ChangePassword(Arg.Any<Guid>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        um.GetUserDto(Arg.Any<User>(), Arg.Any<string>()).Returns(_ => new UserDto { Policy = new UserPolicy() });
        return um;
    }

    public static User NewUser(string name = "user", Guid? id = null)
    {
        var user = new User(
            name,
            "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider",
            "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider")
        {
            Id = id ?? Guid.NewGuid()
        };
        return user;
    }

    public static GroupService NewGroupService(IUserManager um)
        => new(um, Substitute.For<ILogger<GroupService>>());

    public static InviteService NewInviteService(IUserManager um, GroupService groups)
        => new(um, groups, Substitute.For<ICryptoProvider>(), Substitute.For<ILogger<InviteService>>());
}

/// <summary>Serializes tests that mutate the static <see cref="Plugin.Instance"/> singleton.</summary>
[CollectionDefinition("Plugin", DisableParallelization = true)]
public class PluginCollection
{
}

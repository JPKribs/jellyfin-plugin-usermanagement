using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserManagement.Api;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Request validation on the admin invite endpoint.
/// </summary>
[Collection("Plugin")]
public class InviteControllerValidationTests
{
    private static (Plugin Plugin, UserManagementController Controller) Setup()
    {
        var plugin = TestSupport.NewPlugin();
        var um = TestSupport.NewUserManager();
        var groups = TestSupport.NewGroupService(um);
        var invites = TestSupport.NewInviteService(um, groups);
        var paths = Substitute.For<IApplicationPaths>();
        paths.ProgramDataPath.Returns(System.IO.Path.GetTempPath());
        var resets = new ResetCodeService(paths, Substitute.For<ILogger<ResetCodeService>>());
        var controller = new UserManagementController(groups, invites, resets, Substitute.For<ILogger<UserManagementController>>());
        return (plugin, controller);
    }

    private static Guid AddGroup(Plugin plugin)
    {
        var group = new GroupDefinition { Id = Guid.NewGuid(), Name = "Family" };
        plugin.MutateConfiguration(cfg => { cfg.Groups.Add(group); return true; });
        return group.Id;
    }

    [Fact]
    public void CreateInvite_NullResources_IsAccepted()
    {
        var (plugin, controller) = Setup();
        var groupId = AddGroup(plugin);

        var result = controller.CreateInvite(new CreateInviteRequest { Resources = null!, UseDefaultGroup = false, GroupId = groupId });

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(plugin.ReadConfiguration(c => c.Invites));
    }

    [Fact]
    public void CreateInvite_NullResourceEntry_IsBadRequest()
    {
        var (plugin, controller) = Setup();
        var groupId = AddGroup(plugin);

        var result = controller.CreateInvite(new CreateInviteRequest
        {
            Resources = new List<InviteResource> { null! },
            UseDefaultGroup = false,
            GroupId = groupId
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(plugin.ReadConfiguration(c => c.Invites));
    }
}

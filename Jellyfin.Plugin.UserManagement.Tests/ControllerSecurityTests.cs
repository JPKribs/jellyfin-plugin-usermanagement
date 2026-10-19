using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.UserManagement.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Guards the authorization shape of the API: the admin controller requires elevation for every action,
/// and only the public invite controller is reachable anonymously.
/// </summary>
public class ControllerSecurityTests
{
    [Fact]
    public void AdminController_RequiresElevation()
    {
        var attribute = typeof(UserManagementController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("RequiresElevation", attribute!.Policy);
    }

    [Fact]
    public void AdminController_HasNoAnonymousActions()
    {
        var anonymous = typeof(UserManagementController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(anonymous);
        Assert.Null(typeof(UserManagementController).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void InviteController_ActionsAreAnonymousOnPurpose()
    {
        var actions = typeof(InviteController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(actions);
        Assert.All(actions, m => Assert.NotNull(m.GetCustomAttribute<AllowAnonymousAttribute>()));
    }
}

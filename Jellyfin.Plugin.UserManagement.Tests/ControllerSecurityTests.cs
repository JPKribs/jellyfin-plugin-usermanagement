using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.UserManagement.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Guards the authorization shape of the API: the admin controller requires elevation for every action but
/// its strings, and the public invite controller is reachable anonymously.
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

    /// <summary>
    /// The only anonymous action on the admin controller is Translations, which the signup page needs.
    /// True: every other admin route still needs an elevated user.
    /// False: an admin route would be open to anyone, or the signup page could not load its strings.
    /// </summary>
    [Fact]
    public void AdminController_OnlyTranslationsIsAnonymous()
    {
        var anonymous = typeof(UserManagementController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(new[] { nameof(UserManagementController.Translations) }, anonymous);
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

using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.UserManagement.Groups;
using Jellyfin.Plugin.UserManagement.Invites;
using Jellyfin.Plugin.UserManagement.Passwords;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.UserManagement;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
/// <remarks>
/// The group sync scheduled task is discovered by Jellyfin's assembly scanning and constructed
/// through this container, so <see cref="GroupService"/> must be registered. The event consumer and
/// the authentication provider are resolved from DI (as <c>IEventConsumer&lt;T&gt;</c> and
/// <c>IAuthenticationProvider</c> respectively), so both are registered explicitly.
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<GroupService>();
        serviceCollection.AddSingleton<InviteService>();
        serviceCollection.AddSingleton<IEventConsumer<UserCreatedEventArgs>, GroupEventConsumer>();

        // Must be registered so the UserManager can resolve users assigned to it; otherwise enrolled
        // users fall back to InvalidAuthProvider and can neither log in nor change their password.
        serviceCollection.AddSingleton<IAuthenticationProvider, PasswordRuleAuthenticationProvider>();
    }
}

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
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<GroupService>();
        serviceCollection.AddSingleton<InviteService>();
        serviceCollection.AddSingleton<IEventConsumer<UserCreatedEventArgs>, GroupEventConsumer>();

        serviceCollection.AddSingleton<IAuthenticationProvider, PasswordRuleAuthenticationProvider>();
    }
}

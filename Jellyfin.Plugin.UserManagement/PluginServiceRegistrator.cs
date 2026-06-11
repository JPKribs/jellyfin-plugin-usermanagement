using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.UserManagement.Services;
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
        serviceCollection.AddSingleton<ActivityLogger>();
        serviceCollection.AddSingleton<GroupService>();
        serviceCollection.AddSingleton<InviteService>();
        serviceCollection.AddSingleton<InviteStatusStore>();
        serviceCollection.AddSingleton<ResetCodeService>();
        serviceCollection.AddSingleton<IEventConsumer<UserCreatedEventArgs>, GroupEventConsumer>();

        serviceCollection.AddSingleton<IAuthenticationProvider, PasswordRuleAuthenticationProvider>();
    }
}

using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.UserManagement.Services;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Activity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ActivityLogger>(sp =>
        {
            var log = new ActivityLogger(
                sp.GetRequiredService<IActivityManager>(),
                sp.GetRequiredService<ILogger<ActivityLogger>>());
            ActivityLoggerAccessor.Instance = log;
            return log;
        });
        serviceCollection.AddSingleton<GroupService>();
        serviceCollection.AddSingleton<SessionCleanupService>();
        serviceCollection.AddSingleton<InviteService>();
        serviceCollection.AddSingleton<InviteStatusStore>();
        serviceCollection.AddSingleton<ResetCodeService>();
        serviceCollection.AddSingleton<IEventConsumer<UserCreatedEventArgs>, GroupEventConsumer>();

        serviceCollection.AddSingleton<IAuthenticationProvider, PasswordRuleAuthenticationProvider>();
    }
}

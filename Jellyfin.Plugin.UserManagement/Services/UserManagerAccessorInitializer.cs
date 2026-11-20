using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Publishes the server's user manager to <see cref="UserManagerAccessor"/> and its configuration to
/// <see cref="ServerLanguage"/> as the server starts, before any dashboard save or scheduled task can reach
/// the code that needs them.
/// </summary>
public sealed class UserManagerAccessorInitializer : IHostedService
{
    private readonly IUserManager _userManager;
    private readonly IServerConfigurationManager _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagerAccessorInitializer"/> class.
    /// </summary>
    /// <param name="userManager">The server's user manager.</param>
    /// <param name="configuration">The server's configuration, read for its display language.</param>
    public UserManagerAccessorInitializer(IUserManager userManager, IServerConfigurationManager configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        UserManagerAccessor.Instance = _userManager;
        ServerLanguage.Configuration = _configuration;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

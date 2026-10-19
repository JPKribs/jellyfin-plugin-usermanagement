using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Publishes the server's user manager to <see cref="UserManagerAccessor"/> as the server starts, before
/// any dashboard save or scheduled task can reach the configuration save hook that needs it.
/// </summary>
public sealed class UserManagerAccessorInitializer : IHostedService
{
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagerAccessorInitializer"/> class.
    /// </summary>
    /// <param name="userManager">The server's user manager.</param>
    public UserManagerAccessorInitializer(IUserManager userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        UserManagerAccessor.Instance = _userManager;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

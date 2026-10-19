using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Jellyfin.Plugin.UserManagement.Configuration;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for what happens at server startup (the user manager published for the save hook) and for the
/// configuration defaults a new or upgraded server starts with.
/// </summary>
[Collection("Plugin")]
public class StartupAndDefaultsTests
{
    [Fact]
    public async Task Registrator_PublishesTheUserManagerAtStartup_WithoutBuildingGroupService()
    {
        UserManagerAccessor.Instance = null;
        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, Substitute.For<IServerApplicationHost>());

        var hosted = services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(UserManagerAccessorInitializer));
        var um = Substitute.For<IUserManager>();
        var provider = new ServiceCollection().AddSingleton(um).BuildServiceProvider();
        var initializer = (IHostedService)ActivatorUtilities.CreateInstance(provider, hosted.ImplementationType!);

        await initializer.StartAsync(CancellationToken.None);

        try
        {
            Assert.Same(um, UserManagerAccessor.Instance);
        }
        finally
        {
            UserManagerAccessor.Instance = null;
        }
    }

    [Fact]
    public void Registrator_RegistersTheAttemptLimiterAsASingleton()
    {
        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, Substitute.For<IServerApplicationHost>());

        var descriptor = services.Single(d => d.ServiceType == typeof(InviteAttemptLimiter));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(new ServiceCollection().AddSingleton<InviteAttemptLimiter>().BuildServiceProvider().GetRequiredService<InviteAttemptLimiter>());
    }

    [Fact]
    public void NewInstall_StartsWithAnInviteSignupLimit()
    {
        var config = new PluginConfiguration();

        Assert.Equal(10, config.InviteRateLimitCount);
        Assert.Equal(60, config.InviteRateLimitWindowMinutes);
    }

    [Fact]
    public void SavedFileWithoutTheSetting_PicksUpTheDefault()
    {
        // A file last written by a version older than the setting has no element for it at all.
        const string Xml = "<?xml version=\"1.0\"?><PluginConfiguration xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"><MaxPinAttempts>5</MaxPinAttempts></PluginConfiguration>";
        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var reader = new StringReader(Xml);

        var loaded = (PluginConfiguration)serializer.Deserialize(reader)!;

        Assert.Equal(10, loaded.InviteRateLimitCount);
        Assert.Equal(60, loaded.InviteRateLimitWindowMinutes);
    }

    [Fact]
    public void SavedFileWithTheSettingOff_KeepsItOff()
    {
        var saved = new PluginConfiguration { InviteRateLimitCount = 0, InviteRateLimitWindowMinutes = 0 };
        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var buffer = new MemoryStream();
        serializer.Serialize(buffer, saved);
        buffer.Position = 0;

        var loaded = (PluginConfiguration)serializer.Deserialize(buffer)!;

        Assert.Equal(0, loaded.InviteRateLimitCount);
        Assert.Equal(0, loaded.InviteRateLimitWindowMinutes);
    }
}

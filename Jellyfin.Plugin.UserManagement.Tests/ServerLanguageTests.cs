using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// The server's display language, which text written for everyone reads in.
/// </summary>
[Collection("Plugin")]
public class ServerLanguageTests
{
    /// <summary>
    /// Before the server starts there is no configuration, and the language is US English.
    /// True: activity text written in a test or early at startup reads in US English.
    /// False: text written before startup would throw or read as keys.
    /// </summary>
    [Fact]
    public void Current_NoConfiguration_IsNull()
    {
        ServerLanguage.Configuration = null;

        Assert.Null(ServerLanguage.Current);
    }

    /// <summary>
    /// Once the server starts, the language is its display language.
    /// True: activity log entries read in the language the administrator picked.
    /// False: every entry would read in US English.
    /// </summary>
    [Fact]
    public void Current_WithConfiguration_IsTheDisplayLanguage()
    {
        var config = Substitute.For<IServerConfigurationManager>();
        config.Configuration.Returns(new ServerConfiguration { UICulture = "en-US" });
        ServerLanguage.Configuration = config;

        try
        {
            Assert.Equal("en-US", ServerLanguage.Current);
        }
        finally
        {
            ServerLanguage.Configuration = null;
        }
    }
}

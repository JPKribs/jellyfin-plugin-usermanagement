using System;
using System.IO;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the reset code reader against files shaped like the ones Jellyfin's built-in reset
/// provider writes.
/// </summary>
public class ResetCodeServiceTests
{
    private static (ResetCodeService Service, string Dir) Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "um-resets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var paths = Substitute.For<IApplicationPaths>();
        paths.ProgramDataPath.Returns(dir);
        return (new ResetCodeService(paths, NullLogger<ResetCodeService>.Instance), dir);
    }

    [Fact]
    public void ReadAll_ParsesCoreShapedFiles()
    {
        var (service, dir) = Setup();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "passwordreset_abc123.json"),
                "{\"Pin\":\"A1B2C3\",\"UserName\":\"alice\",\"ExpirationDate\":\"2099-01-01T00:00:00Z\",\"PinFile\":\"/x\"}");
            File.WriteAllText(
                Path.Combine(dir, "passwordreset_old.json"),
                "{\"Pin\":\"D4E5F6\",\"UserName\":\"bob\",\"ExpirationDate\":\"2001-01-01T00:00:00Z\"}");
            File.WriteAllText(Path.Combine(dir, "unrelated.json"), "{}");

            var resets = service.ReadAll();

            Assert.Equal(2, resets.Count);
            Assert.Equal("alice", resets[0].UserName);
            Assert.Equal("A1B2C3", resets[0].Pin);
            Assert.False(resets[0].Expired);
            Assert.Equal("bob", resets[1].UserName);
            Assert.True(resets[1].Expired);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReadAll_SkipsUnparseableFiles()
    {
        var (service, dir) = Setup();
        try
        {
            File.WriteAllText(Path.Combine(dir, "passwordreset_bad.json"), "not json at all");

            Assert.Empty(service.ReadAll());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReadAll_EmptyDirectory_ReturnsEmpty()
    {
        var (service, dir) = Setup();
        try
        {
            Assert.Empty(service.ReadAll());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

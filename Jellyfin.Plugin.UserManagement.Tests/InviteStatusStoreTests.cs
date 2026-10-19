using System;
using System.IO;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Services;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the in memory cache in front of the invite status file.
/// </summary>
public class InviteStatusStoreTests
{
    private static (InviteStatusStore Store, string Path) NewStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "um-tests-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var paths = Substitute.For<IApplicationPaths>();
        paths.PluginConfigurationsPath.Returns(dir);
        var store = new InviteStatusStore(paths, Substitute.For<ILogger<InviteStatusStore>>());
        return (store, Path.Join(dir, "Jellyfin.Plugin.UserManagement.InviteStatus.json"));
    }

    [Fact]
    public void Reads_AreServedFromMemory_WhileTheFileIsUnchanged()
    {
        var (store, path) = NewStore();
        var id = Guid.NewGuid();
        store.Update(d => { d.Invites[id] = new InviteStatus { UsedCount = 2 }; return true; });
        var stamp = File.GetLastWriteTimeUtc(path);

        // Overwrite the file behind the store's back but keep its write time: a store that went to disk
        // on every read would now see an unreadable file and report nothing.
        File.WriteAllText(path, "not json");
        File.SetLastWriteTimeUtc(path, stamp);

        Assert.Equal(2, store.Find(id)!.UsedCount);
        Assert.Equal(2, store.Load().Invites[id].UsedCount);
    }

    [Fact]
    public void A_FileChangedFromOutside_IsReloaded()
    {
        var (store, path) = NewStore();
        var id = Guid.NewGuid();
        store.Update(d => { d.Invites[id] = new InviteStatus { UsedCount = 2 }; return true; });

        File.WriteAllText(path, "{\"Invites\":{\"" + id + "\":{\"UsedCount\":7,\"FailedPinAttempts\":0,\"RecentRedemptions\":[]}}}");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(7, store.Find(id)!.UsedCount);
    }

    [Fact]
    public void ReturnedValues_AreCopies()
    {
        var (store, _) = NewStore();
        var id = Guid.NewGuid();
        store.Update(d => { d.Invites[id] = new InviteStatus { UsedCount = 1 }; return true; });

        store.Load().Invites[id].UsedCount = 99;
        store.Find(id)!.RecentRedemptions.Add(DateTime.UtcNow);

        Assert.Equal(1, store.Find(id)!.UsedCount);
        Assert.Empty(store.Find(id)!.RecentRedemptions);
    }

    [Fact]
    public void Update_ThatDeclines_LeavesTheStoreUntouched()
    {
        var (store, _) = NewStore();
        var id = Guid.NewGuid();

        store.Update(d => { d.Invites[id] = new InviteStatus { UsedCount = 5 }; return false; });

        Assert.Null(store.Find(id));
    }

    [Fact]
    public void An_UnreadableFile_IsNotCachedAndNeverWrittenOver()
    {
        var (store, path) = NewStore();
        var id = Guid.NewGuid();
        store.Update(d => { d.Invites[id] = new InviteStatus { UsedCount = 4 }; return true; });

        // The file is damaged (a bad restore, a half written copy) with a new write time.
        File.WriteAllText(path, "{ broken");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        // Reads keep the last good status rather than dropping to zero, and a write stays in memory.
        Assert.Equal(4, store.Find(id)!.UsedCount);
        store.Update(d => { d.Invites[id].UsedCount++; return true; });
        Assert.Equal(5, store.Find(id)!.UsedCount);
        Assert.Equal("{ broken", File.ReadAllText(path));

        // Once the file is readable again it is the source of truth.
        File.WriteAllText(path, "{\"Invites\":{\"" + id + "\":{\"UsedCount\":9,\"FailedPinAttempts\":0,\"RecentRedemptions\":[]}}}");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(2));
        Assert.Equal(9, store.Find(id)!.UsedCount);
    }

    [Fact]
    public void An_UnreadableFileAtStartup_IsNotReplacedByEmptyData()
    {
        var (store, path) = NewStore();
        File.WriteAllText(path, "not json");
        var id = Guid.NewGuid();

        Assert.Null(store.Find(id));
        store.Update(d => { d.Invites[id] = new InviteStatus { FailedPinAttempts = 1 }; return true; });

        Assert.Equal("not json", File.ReadAllText(path));
        Assert.Equal(1, store.Find(id)!.FailedPinAttempts);
    }

    [Fact]
    public void Writes_SurviveANewStoreInstance()
    {
        var (store, path) = NewStore();
        var id = Guid.NewGuid();
        store.Update(d => { d.Invites[id] = new InviteStatus { FailedPinAttempts = 3 }; return true; });

        var paths = Substitute.For<IApplicationPaths>();
        paths.PluginConfigurationsPath.Returns(Path.GetDirectoryName(path)!);
        var reopened = new InviteStatusStore(paths, Substitute.For<ILogger<InviteStatusStore>>());

        Assert.Equal(3, reopened.Find(id)!.FailedPinAttempts);
    }
}

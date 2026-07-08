using System;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Model.Configuration;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// Tests for the Override / Inherit behavior of <see cref="GroupConfigurationExtensions.ApplyTo"/>.
/// </summary>
public class GroupConfigurationMergeTests
{
    [Fact]
    public void Merge_UnmanagedSetting_LeavesConfigurationUntouched()
    {
        var config = new UserConfiguration { SubtitleMode = SubtitlePlaybackMode.None };
        var cfg = new GroupConfiguration { ManageSubtitleMode = false, SubtitleMode = "Always" };

        cfg.ApplyTo(config);

        Assert.Equal(SubtitlePlaybackMode.None, config.SubtitleMode);
    }

    [Fact]
    public void Merge_ManagedSubtitleMode_OverwritesConfiguration()
    {
        var config = new UserConfiguration { SubtitleMode = SubtitlePlaybackMode.Default };
        var cfg = new GroupConfiguration { ManageSubtitleMode = true, SubtitleMode = "Smart" };

        cfg.ApplyTo(config);

        Assert.Equal(SubtitlePlaybackMode.Smart, config.SubtitleMode);
    }

    [Fact]
    public void Merge_UnparsableSubtitleMode_LeavesConfigurationUntouched()
    {
        var config = new UserConfiguration { SubtitleMode = SubtitlePlaybackMode.OnlyForced };
        var cfg = new GroupConfiguration { ManageSubtitleMode = true, SubtitleMode = "NotAMode" };

        cfg.ApplyTo(config);

        Assert.Equal(SubtitlePlaybackMode.OnlyForced, config.SubtitleMode);
    }

    [Fact]
    public void Merge_ManagedLanguagePreferences_OverwritesConfiguration()
    {
        var config = new UserConfiguration { AudioLanguagePreference = "eng", SubtitleLanguagePreference = "eng" };
        var cfg = new GroupConfiguration
        {
            ManageAudioLanguagePreference = true,
            AudioLanguagePreference = "fra",
            ManageSubtitleLanguagePreference = true,
            SubtitleLanguagePreference = "deu"
        };

        cfg.ApplyTo(config);

        Assert.Equal("fra", config.AudioLanguagePreference);
        Assert.Equal("deu", config.SubtitleLanguagePreference);
    }

    [Fact]
    public void Merge_ManagedOrderedViews_OverwritesConfiguration()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var config = new UserConfiguration { OrderedViews = new[] { Guid.NewGuid() } };
        var cfg = new GroupConfiguration { ManageOrderedViews = true, OrderedViews = { a, b } };

        cfg.ApplyTo(config);

        Assert.Equal(new[] { a, b }, config.OrderedViews);
    }

    [Fact]
    public void Merge_ManagedExcludes_OverwritesConfiguration()
    {
        var hidden = Guid.NewGuid();
        var config = new UserConfiguration();
        var cfg = new GroupConfiguration
        {
            ManageMyMediaExcludes = true,
            MyMediaExcludes = { hidden },
            ManageLatestItemsExcludes = true,
            LatestItemsExcludes = { hidden }
        };

        cfg.ApplyTo(config);

        Assert.Equal(new[] { hidden }, config.MyMediaExcludes);
        Assert.Equal(new[] { hidden }, config.LatestItemsExcludes);
    }

    [Fact]
    public void Merge_ManagedBooleanToggles_OverwriteConfiguration()
    {
        var config = new UserConfiguration
        {
            PlayDefaultAudioTrack = true,
            EnableNextEpisodeAutoPlay = false,
            HidePlayedInLatest = false
        };
        var cfg = new GroupConfiguration
        {
            ManagePlayDefaultAudioTrack = true,
            PlayDefaultAudioTrack = false,
            ManageEnableNextEpisodeAutoPlay = true,
            EnableNextEpisodeAutoPlay = true,
            ManageHidePlayedInLatest = true,
            HidePlayedInLatest = true
        };

        cfg.ApplyTo(config);

        Assert.False(config.PlayDefaultAudioTrack);
        Assert.True(config.EnableNextEpisodeAutoPlay);
        Assert.True(config.HidePlayedInLatest);
    }

    [Fact]
    public void ManagesAnything_IsFalse_ForFreshConfiguration()
    {
        Assert.False(new GroupConfiguration().ManagesAnything());
    }

    [Fact]
    public void ManagesAnything_IsTrue_WhenAnySettingManaged()
    {
        Assert.True(new GroupConfiguration { ManageCastReceiverId = true }.ManagesAnything());
    }
}

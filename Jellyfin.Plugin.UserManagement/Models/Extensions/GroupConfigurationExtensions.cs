using System;
using System.Linq;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Applies a group's managed display and playback preferences onto a Jellyfin user configuration. Only the
/// settings the group manages overwrite the user's value, so an unmanaged setting stays as the user has it.
/// </summary>
public static class GroupConfigurationExtensions
{
    /// <summary>Writes the managed configuration values from this group onto a user configuration in place.</summary>
    /// <param name="c">The group's configuration.</param>
    /// <param name="config">The user configuration to update.</param>
    public static void ApplyTo(this GroupConfiguration c, UserConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (c.ManageSubtitleMode
            && Enum.TryParse<SubtitlePlaybackMode>(c.SubtitleMode, out var mode))
        {
            config.SubtitleMode = mode;
        }

        if (c.ManageSubtitleLanguagePreference)
        {
            config.SubtitleLanguagePreference = c.SubtitleLanguagePreference ?? string.Empty;
        }

        if (c.ManageRememberSubtitleSelections)
        {
            config.RememberSubtitleSelections = c.RememberSubtitleSelections;
        }

        if (c.ManageAudioLanguagePreference)
        {
            config.AudioLanguagePreference = c.AudioLanguagePreference ?? string.Empty;
        }

        if (c.ManagePlayDefaultAudioTrack)
        {
            config.PlayDefaultAudioTrack = c.PlayDefaultAudioTrack;
        }

        if (c.ManageRememberAudioSelections)
        {
            config.RememberAudioSelections = c.RememberAudioSelections;
        }

        if (c.ManageEnableNextEpisodeAutoPlay)
        {
            config.EnableNextEpisodeAutoPlay = c.EnableNextEpisodeAutoPlay;
        }

        if (c.ManageCastReceiverId)
        {
            config.CastReceiverId = c.CastReceiverId ?? string.Empty;
        }

        if (c.ManageDisplayMissingEpisodes)
        {
            config.DisplayMissingEpisodes = c.DisplayMissingEpisodes;
        }

        if (c.ManageHidePlayedInLatest)
        {
            config.HidePlayedInLatest = c.HidePlayedInLatest;
        }

        if (c.ManageOrderedViews)
        {
            config.OrderedViews = c.OrderedViews.ToArray();
        }

        if (c.ManageMyMediaExcludes)
        {
            config.MyMediaExcludes = c.MyMediaExcludes.ToArray();
        }

        if (c.ManageLatestItemsExcludes)
        {
            config.LatestItemsExcludes = c.LatestItemsExcludes.ToArray();
        }

        if (c.ManageGroupedFolders)
        {
            config.GroupedFolders = c.GroupedFolders.ToArray();
        }
    }

    /// <summary>Whether any setting on this group configuration is managed.</summary>
    /// <param name="c">The group's configuration.</param>
    /// <returns><c>true</c> if at least one setting is managed.</returns>
    public static bool ManagesAnything(this GroupConfiguration c)
        => c.ManageSubtitleMode
            || c.ManageSubtitleLanguagePreference
            || c.ManageRememberSubtitleSelections
            || c.ManageAudioLanguagePreference
            || c.ManagePlayDefaultAudioTrack
            || c.ManageRememberAudioSelections
            || c.ManageEnableNextEpisodeAutoPlay
            || c.ManageCastReceiverId
            || c.ManageDisplayMissingEpisodes
            || c.ManageHidePlayedInLatest
            || c.ManageOrderedViews
            || c.ManageMyMediaExcludes
            || c.ManageLatestItemsExcludes
            || c.ManageGroupedFolders;
}

using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The display and playback preferences a group applies to its members, mirroring the fields Jellyfin
/// exposes on a user's Display / Home / Playback / Subtitles preference pages. Follows the same
/// Override / Inherit model as <see cref="GroupPermissions"/>: only settings the group manages overwrite
/// the member's value; the rest are left exactly as the user has them.
/// </summary>
public class GroupConfiguration
{
    /// <summary>Gets or sets a value indicating whether the group manages the subtitle mode.</summary>
    public bool ManageSubtitleMode { get; set; }

    /// <summary>Gets or sets the subtitle mode (Default, Smart, OnlyForced, Always, or None).</summary>
    public string SubtitleMode { get; set; } = "Default";

    /// <summary>Gets or sets a value indicating whether the group manages the preferred subtitle language.</summary>
    public bool ManageSubtitleLanguagePreference { get; set; }

    /// <summary>Gets or sets the preferred subtitle language (three-letter ISO code, or empty for any).</summary>
    public string SubtitleLanguagePreference { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the group manages remembering subtitle selections.</summary>
    public bool ManageRememberSubtitleSelections { get; set; }

    /// <summary>Gets or sets a value indicating whether the subtitle track is set based on the previous item.</summary>
    public bool RememberSubtitleSelections { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the preferred audio language.</summary>
    public bool ManageAudioLanguagePreference { get; set; }

    /// <summary>Gets or sets the preferred audio language (three-letter ISO code, or empty for any).</summary>
    public string AudioLanguagePreference { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the group manages the default audio track behavior.</summary>
    public bool ManagePlayDefaultAudioTrack { get; set; }

    /// <summary>Gets or sets a value indicating whether the default audio track plays regardless of language.</summary>
    public bool PlayDefaultAudioTrack { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages remembering audio selections.</summary>
    public bool ManageRememberAudioSelections { get; set; }

    /// <summary>Gets or sets a value indicating whether the audio track is set based on the previous item.</summary>
    public bool RememberAudioSelections { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages automatic next-episode playback.</summary>
    public bool ManageEnableNextEpisodeAutoPlay { get; set; }

    /// <summary>Gets or sets a value indicating whether the next episode plays automatically.</summary>
    public bool EnableNextEpisodeAutoPlay { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the Google Cast version.</summary>
    public bool ManageCastReceiverId { get; set; }

    /// <summary>Gets or sets the Google Cast receiver application ID.</summary>
    public string CastReceiverId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the group manages displaying missing episodes.</summary>
    public bool ManageDisplayMissingEpisodes { get; set; }

    /// <summary>Gets or sets a value indicating whether missing episodes are displayed within seasons.</summary>
    public bool DisplayMissingEpisodes { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages hiding watched content from latest media.</summary>
    public bool ManageHidePlayedInLatest { get; set; }

    /// <summary>Gets or sets a value indicating whether watched content is hidden from 'Recently Added Media'.</summary>
    public bool HidePlayedInLatest { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages the home screen library order.</summary>
    public bool ManageOrderedViews { get; set; }

    /// <summary>Gets or sets the ordered library IDs controlling the order libraries appear on the home screen.</summary>
    public List<Guid> OrderedViews { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages which libraries appear in My Media.</summary>
    public bool ManageMyMediaExcludes { get; set; }

    /// <summary>Gets or sets the library IDs hidden from the My Media home screen row.</summary>
    public List<Guid> MyMediaExcludes { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages which libraries appear in latest sections.</summary>
    public bool ManageLatestItemsExcludes { get; set; }

    /// <summary>Gets or sets the library IDs hidden from home screen sections such as 'Recently Added Media'.</summary>
    public List<Guid> LatestItemsExcludes { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages grouped libraries.</summary>
    public bool ManageGroupedFolders { get; set; }

    /// <summary>Gets or sets the library IDs grouped together into a single home screen view.</summary>
    public List<Guid> GroupedFolders { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages the home screen section order.</summary>
    public bool ManageHomeSections { get; set; }

    /// <summary>
    /// Gets or sets the ordered home screen sections for the web client. Each entry is a section key
    /// (smalllibrarytiles, librarybuttons, activerecordings, resume, resumeaudio, resumebook, latestmedia,
    /// nextup, livetv, none) matching a <see cref="Jellyfin.Database.Implementations.Enums.HomeSectionType"/>.
    /// </summary>
    public List<string> HomeSections { get; set; } = new();
}

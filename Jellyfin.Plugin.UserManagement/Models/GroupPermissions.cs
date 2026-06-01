using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The permission shape a group applies to its members, mirroring Jellyfin's
/// <see cref="MediaBrowser.Model.Users.UserPolicy"/>.
/// </summary>
/// <remarks>
/// Each permission is a pair: a <c>Manage{X}</c> flag and its value. When <c>Manage{X}</c> is
/// <c>true</c> the group owns that permission and overwrites it on every member; when <c>false</c>
/// the permission is inherited (left exactly as the user has it). This is the Override / Inherit model.
/// </remarks>
public class GroupPermissions
{
    // ===== Administration =====
    // Note: the administrator flag is deliberately NOT manageable by groups — groups can never
    // grant or remove admin. This removes any path to silent privilege escalation via group sync.

    /// <summary>Gets or sets a value indicating whether the group manages the hidden flag.</summary>
    public bool ManageIsHidden { get; set; }

    /// <summary>Gets or sets the hidden flag value.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the disabled flag.</summary>
    public bool ManageIsDisabled { get; set; }

    /// <summary>Gets or sets the disabled flag value.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages collection management.</summary>
    public bool ManageEnableCollectionManagement { get; set; }

    /// <summary>Gets or sets the collection management value.</summary>
    public bool EnableCollectionManagement { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages subtitle management.</summary>
    public bool ManageEnableSubtitleManagement { get; set; }

    /// <summary>Gets or sets the subtitle management value.</summary>
    public bool EnableSubtitleManagement { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages lyric management.</summary>
    public bool ManageEnableLyricManagement { get; set; }

    /// <summary>Gets or sets the lyric management value.</summary>
    public bool EnableLyricManagement { get; set; }

    // ===== Library access =====

    /// <summary>Gets or sets a value indicating whether the group manages library access.</summary>
    public bool ManageLibraryAccess { get; set; }

    /// <summary>Gets or sets a value indicating whether members may access all libraries.</summary>
    public bool EnableAllFolders { get; set; } = true;

    /// <summary>Gets or sets the explicitly enabled library item IDs (used when not all-folders).</summary>
    public List<Guid> EnabledFolders { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages channel access.</summary>
    public bool ManageChannelAccess { get; set; }

    /// <summary>Gets or sets a value indicating whether members may access all channels.</summary>
    public bool EnableAllChannels { get; set; } = true;

    /// <summary>Gets or sets the explicitly enabled channel IDs (used when not all-channels).</summary>
    public List<Guid> EnabledChannels { get; set; } = new();

    // ===== Playback &amp; transcoding =====

    /// <summary>Gets or sets a value indicating whether the group manages media playback.</summary>
    public bool ManageEnableMediaPlayback { get; set; }

    /// <summary>Gets or sets the media playback value.</summary>
    public bool EnableMediaPlayback { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages audio transcoding.</summary>
    public bool ManageEnableAudioPlaybackTranscoding { get; set; }

    /// <summary>Gets or sets the audio transcoding value.</summary>
    public bool EnableAudioPlaybackTranscoding { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages video transcoding.</summary>
    public bool ManageEnableVideoPlaybackTranscoding { get; set; }

    /// <summary>Gets or sets the video transcoding value.</summary>
    public bool EnableVideoPlaybackTranscoding { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages playback remuxing.</summary>
    public bool ManageEnablePlaybackRemuxing { get; set; }

    /// <summary>Gets or sets the playback remuxing value.</summary>
    public bool EnablePlaybackRemuxing { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages forced remote-source transcoding.</summary>
    public bool ManageForceRemoteSourceTranscoding { get; set; }

    /// <summary>Gets or sets the forced remote-source transcoding value.</summary>
    public bool ForceRemoteSourceTranscoding { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages media conversion (downloads sync).</summary>
    public bool ManageEnableMediaConversion { get; set; }

    /// <summary>Gets or sets the media conversion value.</summary>
    public bool EnableMediaConversion { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages sync transcoding.</summary>
    public bool ManageEnableSyncTranscoding { get; set; }

    /// <summary>Gets or sets the sync transcoding value.</summary>
    public bool EnableSyncTranscoding { get; set; } = true;

    // ===== Remote &amp; sessions =====

    /// <summary>Gets or sets a value indicating whether the group manages remote access.</summary>
    public bool ManageEnableRemoteAccess { get; set; }

    /// <summary>Gets or sets the remote access value.</summary>
    public bool EnableRemoteAccess { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages remote control of other users.</summary>
    public bool ManageEnableRemoteControlOfOtherUsers { get; set; }

    /// <summary>Gets or sets the remote control of other users value.</summary>
    public bool EnableRemoteControlOfOtherUsers { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages shared device control.</summary>
    public bool ManageEnableSharedDeviceControl { get; set; }

    /// <summary>Gets or sets the shared device control value.</summary>
    public bool EnableSharedDeviceControl { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the max active sessions.</summary>
    public bool ManageMaxActiveSessions { get; set; }

    /// <summary>Gets or sets the maximum active sessions (0 = unlimited).</summary>
    public int MaxActiveSessions { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the remote client bitrate limit.</summary>
    public bool ManageRemoteClientBitrateLimit { get; set; }

    /// <summary>Gets or sets the remote client bitrate limit in bits per second (0 = unlimited).</summary>
    public int RemoteClientBitrateLimit { get; set; }

    // ===== Downloads &amp; deletion =====

    /// <summary>Gets or sets a value indicating whether the group manages content downloading.</summary>
    public bool ManageEnableContentDownloading { get; set; }

    /// <summary>Gets or sets the content downloading value.</summary>
    public bool EnableContentDownloading { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages content deletion.</summary>
    public bool ManageEnableContentDeletion { get; set; }

    /// <summary>Gets or sets a value indicating whether members may delete from all libraries.</summary>
    public bool EnableContentDeletion { get; set; }

    /// <summary>Gets or sets the library IDs members may delete from (used when not all-libraries).</summary>
    public List<string> EnableContentDeletionFromFolders { get; set; } = new();

    // ===== Live TV =====

    /// <summary>Gets or sets a value indicating whether the group manages Live TV access.</summary>
    public bool ManageEnableLiveTvAccess { get; set; }

    /// <summary>Gets or sets the Live TV access value.</summary>
    public bool EnableLiveTvAccess { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages Live TV management.</summary>
    public bool ManageEnableLiveTvManagement { get; set; }

    /// <summary>Gets or sets the Live TV management value.</summary>
    public bool EnableLiveTvManagement { get; set; }

    // ===== Other =====

    /// <summary>Gets or sets a value indicating whether the group manages user preference access.</summary>
    public bool ManageEnableUserPreferenceAccess { get; set; }

    /// <summary>Gets or sets the user preference access value.</summary>
    public bool EnableUserPreferenceAccess { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages public sharing.</summary>
    public bool ManageEnablePublicSharing { get; set; }

    /// <summary>Gets or sets the public sharing value.</summary>
    public bool EnablePublicSharing { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages SyncPlay access.</summary>
    public bool ManageSyncPlayAccess { get; set; }

    /// <summary>Gets or sets the SyncPlay access value (CreateAndJoinGroups, JoinGroups, or None).</summary>
    public string SyncPlayAccess { get; set; } = "CreateAndJoinGroups";
}

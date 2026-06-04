using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// The permission shape a group applies to its members, mirroring the fields exposed on Jellyfin's
/// user-edit page (Profile / Access / Parental Control).
/// </summary>
public class GroupPermissions
{

    /// <summary>Gets or sets a value indicating whether the group manages remote access.</summary>
    public bool ManageEnableRemoteAccess { get; set; }

    /// <summary>Gets or sets the remote access value.</summary>
    public bool EnableRemoteAccess { get; set; } = true;

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

    /// <summary>Gets or sets a value indicating whether the group manages Live TV access.</summary>
    public bool ManageEnableLiveTvAccess { get; set; }

    /// <summary>Gets or sets the Live TV access value.</summary>
    public bool EnableLiveTvAccess { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the group manages Live TV recording management.</summary>
    public bool ManageEnableLiveTvManagement { get; set; }

    /// <summary>Gets or sets the Live TV recording management value.</summary>
    public bool EnableLiveTvManagement { get; set; }

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

    /// <summary>Gets or sets a value indicating whether the group manages the remote client bitrate limit.</summary>
    public bool ManageRemoteClientBitrateLimit { get; set; }

    /// <summary>Gets or sets the remote client bitrate limit in bits per second (0 = unlimited).</summary>
    public int RemoteClientBitrateLimit { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages SyncPlay access.</summary>
    public bool ManageSyncPlayAccess { get; set; }

    /// <summary>Gets or sets the SyncPlay access value (CreateAndJoinGroups, JoinGroups, or None).</summary>
    public string SyncPlayAccess { get; set; } = "CreateAndJoinGroups";

    /// <summary>Gets or sets a value indicating whether the group manages content deletion.</summary>
    public bool ManageEnableContentDeletion { get; set; }

    /// <summary>Gets or sets a value indicating whether members may delete from all libraries.</summary>
    public bool EnableContentDeletion { get; set; }

    /// <summary>Gets or sets the library IDs members may delete from (used when not all-libraries).</summary>
    public List<string> EnableContentDeletionFromFolders { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages remote control of other users.</summary>
    public bool ManageEnableRemoteControlOfOtherUsers { get; set; }

    /// <summary>Gets or sets the remote control of other users value.</summary>
    public bool EnableRemoteControlOfOtherUsers { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages shared device control.</summary>
    public bool ManageEnableSharedDeviceControl { get; set; }

    /// <summary>Gets or sets the shared device control value.</summary>
    public bool EnableSharedDeviceControl { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages content downloading.</summary>
    public bool ManageEnableContentDownloading { get; set; }

    /// <summary>Gets or sets the content downloading value.</summary>
    public bool EnableContentDownloading { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the disabled flag.</summary>
    public bool ManageIsDisabled { get; set; }

    /// <summary>Gets or sets the disabled flag value.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the hidden flag.</summary>
    public bool ManageIsHidden { get; set; }

    /// <summary>Gets or sets the hidden flag value.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the login-attempts lockout.</summary>
    public bool ManageLoginAttemptsBeforeLockout { get; set; }

    /// <summary>Gets or sets the failed login attempts before lockout (0 = default, -1 = disabled).</summary>
    public int LoginAttemptsBeforeLockout { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages the max active sessions.</summary>
    public bool ManageMaxActiveSessions { get; set; }

    /// <summary>Gets or sets the maximum simultaneous user sessions (0 = unlimited).</summary>
    public int MaxActiveSessions { get; set; }

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

    /// <summary>Gets or sets a value indicating whether the group manages device access.</summary>
    public bool ManageDeviceAccess { get; set; }

    /// <summary>Gets or sets a value indicating whether members may access from all devices.</summary>
    public bool EnableAllDevices { get; set; } = true;

    /// <summary>Gets or sets the explicitly enabled device IDs (used when not all-devices).</summary>
    public List<string> EnabledDevices { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages the maximum allowed parental rating.</summary>
    public bool ManageMaxParentalRating { get; set; }

    /// <summary>Gets or sets the maximum allowed parental rating score, or null for no maximum.</summary>
    public int? MaxParentalRating { get; set; }

    /// <summary>Gets or sets the maximum allowed parental sub-rating score, or null.</summary>
    public int? MaxParentalSubRating { get; set; }

    /// <summary>Gets or sets a value indicating whether the group manages blocked unrated item types.</summary>
    public bool ManageBlockUnratedItems { get; set; }

    /// <summary>Gets or sets the unrated item types to block (UnratedItem enum names).</summary>
    public List<string> BlockUnratedItems { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages the allowed tags.</summary>
    public bool ManageAllowedTags { get; set; }

    /// <summary>Gets or sets the tags; only media with at least one of these is shown.</summary>
    public List<string> AllowedTags { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages the blocked tags.</summary>
    public bool ManageBlockedTags { get; set; }

    /// <summary>Gets or sets the tags; media with at least one of these is hidden.</summary>
    public List<string> BlockedTags { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the group manages the access schedule.</summary>
    public bool ManageAccessSchedules { get; set; }

    /// <summary>Gets or sets the access schedule windows.</summary>
    public List<AccessScheduleEntry> AccessSchedules { get; set; } = new();
}

using System;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Model.Users;

namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// Applies a group's managed permissions onto a Jellyfin user policy. Only the toggles the group
/// manages overwrite the user's value, so an unmanaged setting stays as the user already has it.
/// </summary>
public static class GroupPermissionsExtensions
{
    /// <summary>Writes the managed permission values from this group onto a user policy in place.</summary>
    /// <param name="p">The group's permissions.</param>
    /// <param name="policy">The user policy to update.</param>
    /// <param name="userId">The user id, used when building access schedules.</param>
    public static void ApplyTo(this GroupPermissions p, UserPolicy policy, Guid userId)
    {
        if (p.ManageEnableRemoteAccess)
        {
            policy.EnableRemoteAccess = p.EnableRemoteAccess;
        }

        if (p.ManageEnableCollectionManagement)
        {
            policy.EnableCollectionManagement = p.EnableCollectionManagement;
        }

        if (p.ManageEnableSubtitleManagement)
        {
            policy.EnableSubtitleManagement = p.EnableSubtitleManagement;
        }

        if (p.ManageEnableLyricManagement)
        {
            policy.EnableLyricManagement = p.EnableLyricManagement;
        }

        if (p.ManageEnableMediaConversion)
        {
            policy.EnableMediaConversion = p.EnableMediaConversion;
        }

        if (p.ManageEnableLiveTvAccess)
        {
            policy.EnableLiveTvAccess = p.EnableLiveTvAccess;
        }

        if (p.ManageEnableLiveTvManagement)
        {
            policy.EnableLiveTvManagement = p.EnableLiveTvManagement;
        }

        if (p.ManageEnableMediaPlayback)
        {
            policy.EnableMediaPlayback = p.EnableMediaPlayback;
        }

        if (p.ManageEnableAudioPlaybackTranscoding)
        {
            policy.EnableAudioPlaybackTranscoding = p.EnableAudioPlaybackTranscoding;
        }

        if (p.ManageEnableVideoPlaybackTranscoding)
        {
            policy.EnableVideoPlaybackTranscoding = p.EnableVideoPlaybackTranscoding;
        }

        if (p.ManageEnablePlaybackRemuxing)
        {
            policy.EnablePlaybackRemuxing = p.EnablePlaybackRemuxing;
        }

        if (p.ManageForceRemoteSourceTranscoding)
        {
            policy.ForceRemoteSourceTranscoding = p.ForceRemoteSourceTranscoding;
        }

        if (p.ManageRemoteClientBitrateLimit)
        {
            policy.RemoteClientBitrateLimit = p.RemoteClientBitrateLimit;
        }

        if (p.ManageSyncPlayAccess
            && Enum.TryParse<SyncPlayUserAccessType>(p.SyncPlayAccess, out var access))
        {
            policy.SyncPlayAccess = access;
        }

        if (p.ManageEnableContentDeletion)
        {
            policy.EnableContentDeletion = p.EnableContentDeletion;
            policy.EnableContentDeletionFromFolders = p.EnableContentDeletionFromFolders.ToArray();
        }

        if (p.ManageEnableRemoteControlOfOtherUsers)
        {
            policy.EnableRemoteControlOfOtherUsers = p.EnableRemoteControlOfOtherUsers;
        }

        if (p.ManageEnableSharedDeviceControl)
        {
            policy.EnableSharedDeviceControl = p.EnableSharedDeviceControl;
        }

        if (p.ManageEnableContentDownloading)
        {
            policy.EnableContentDownloading = p.EnableContentDownloading;
        }

        if (p.ManageIsDisabled)
        {
            policy.IsDisabled = p.IsDisabled;
        }

        if (p.ManageIsHidden)
        {
            policy.IsHidden = p.IsHidden;
        }

        if (p.ManageLoginAttemptsBeforeLockout)
        {
            policy.LoginAttemptsBeforeLockout = p.LoginAttemptsBeforeLockout;
        }

        if (p.ManageMaxActiveSessions)
        {
            policy.MaxActiveSessions = p.MaxActiveSessions;
        }

        if (p.ManageLibraryAccess)
        {
            policy.EnableAllFolders = p.EnableAllFolders;
            policy.EnabledFolders = p.EnabledFolders.ToArray();
        }

        if (p.ManageChannelAccess)
        {
            policy.EnableAllChannels = p.EnableAllChannels;
            policy.EnabledChannels = p.EnabledChannels.ToArray();
        }

        if (p.ManageDeviceAccess)
        {
            policy.EnableAllDevices = p.EnableAllDevices;
            policy.EnabledDevices = p.EnabledDevices.ToArray();
        }

        if (p.ManageMaxParentalRating)
        {
            policy.MaxParentalRating = p.MaxParentalRating;
            policy.MaxParentalSubRating = p.MaxParentalSubRating;
        }

        if (p.ManageBlockUnratedItems)
        {
            policy.BlockUnratedItems = p.BlockUnratedItems
                .Select(name => Enum.TryParse<UnratedItem>(name, out var item) ? (UnratedItem?)item : null)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToArray();
        }

        if (p.ManageAllowedTags)
        {
            policy.AllowedTags = p.AllowedTags.ToArray();
        }

        if (p.ManageBlockedTags)
        {
            policy.BlockedTags = p.BlockedTags.ToArray();
        }

        if (p.ManageAccessSchedules)
        {
            policy.AccessSchedules = p.AccessSchedules
                .Where(s => Enum.IsDefined(typeof(DynamicDayOfWeek), s.DayOfWeek))
                .Select(s => new AccessSchedule(
                    Enum.Parse<DynamicDayOfWeek>(s.DayOfWeek),
                    s.StartHour,
                    s.EndHour,
                    userId))
                .ToArray();
        }
    }
}

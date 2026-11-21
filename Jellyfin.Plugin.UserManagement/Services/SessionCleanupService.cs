using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Queries;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Logs out stale devices for members of groups with session cleanup enabled. A device is stale when
/// it has not checked back into the server within the window of a rule covering its client; when
/// several rules cover the same client, the shortest window wins. Administrators are never affected.
/// </summary>
public class SessionCleanupService
{
    private readonly IUserManager _userManager;
    private readonly IDeviceManager _deviceManager;
    private readonly ISessionManager _sessionManager;
    private readonly ActivityLogger _activity;
    private readonly ILogger<SessionCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCleanupService"/> class.
    /// </summary>
    public SessionCleanupService(
        IUserManager userManager,
        IDeviceManager deviceManager,
        ISessionManager sessionManager,
        ActivityLogger activity,
        ILogger<SessionCleanupService> logger)
    {
        _userManager = userManager;
        _deviceManager = deviceManager;
        _sessionManager = sessionManager;
        _activity = activity;
        _logger = logger;
    }

    /// <summary>
    /// Whether a rule covers the given client name.
    /// </summary>
    internal static bool RuleMatches(SessionCleanupRule rule, string appName)
        => rule.Mode switch
        {
            SessionCleanupClientMode.All => true,
            SessionCleanupClientMode.Only => rule.Clients.Contains(appName, StringComparer.OrdinalIgnoreCase),
            SessionCleanupClientMode.AllExcept => !rule.Clients.Contains(appName, StringComparer.OrdinalIgnoreCase),
            _ => false
        };

    /// <summary>
    /// The shortest cleanup window among the rules covering the given client, or null when no rule covers it.
    /// </summary>
    internal static int? MinMatchingDays(IEnumerable<SessionCleanupRule> rules, string appName)
    {
        int? min = null;
        foreach (var rule in rules)
        {
            if (rule.Days > 0 && RuleMatches(rule, appName) && (min is null || rule.Days < min))
            {
                min = rule.Days;
            }
        }

        return min;
    }

    /// <summary>
    /// Logs out every stale device of every member of groups with session cleanup enabled.
    /// </summary>
    /// <returns>The number of devices logged out.</returns>
    public async Task<int> CleanupAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            progress?.Report(100);
            return 0;
        }

        var work = plugin.ReadConfiguration(c => c.Groups
            .Where(g => g.CleanupSessions && g.SessionCleanupRules.Count > 0)
            .Select(g => (g.Id, Rules: g.SessionCleanupRules.ToList(), Members: g.MemberIds.ToList()))
            .ToList());

        var total = work.Sum(w => w.Members.Count);
        if (total == 0)
        {
            progress?.Report(100);
            return 0;
        }

        var now = DateTime.UtcNow;
        var removed = 0;
        var removedByUser = new Dictionary<Guid, (string Username, Dictionary<string, int> Apps)>();
        var processed = 0;
        foreach (var (groupId, rules, members) in work)
        {
            foreach (var userId in members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var user = _userManager.GetUserById(userId);
                if (user is null || AdminExemption.IsExempt(user))
                {
                    processed++;
                    progress?.Report(processed * 100.0 / total);
                    continue;
                }

                var devices = _deviceManager.GetDevices(new DeviceQuery { UserId = userId }).Items;
                foreach (var device in devices)
                {
                    if (MinMatchingDays(rules, device.AppName) is not { } days
                        || device.DateLastActivity >= now.AddDays(-days))
                    {
                        continue;
                    }

                    try
                    {
                        await _sessionManager.Logout(device).ConfigureAwait(false);
                        removed++;
                        var app = string.IsNullOrWhiteSpace(device.AppName) ? "Unknown app" : device.AppName;
                        if (!removedByUser.TryGetValue(userId, out var counted))
                        {
                            counted = (user.Username, new Dictionary<string, int>(StringComparer.Ordinal));
                            removedByUser[userId] = counted;
                        }

                        counted.Apps[app] = counted.Apps.GetValueOrDefault(app) + 1;
                        _logger.LogDebug(
                            "Logged out stale device {DeviceName} ({AppName}) of user {UserName} from group {GroupId}",
                            device.DeviceName,
                            device.AppName,
                            user.Username,
                            groupId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to log out device {DeviceId} of user {UserName}", device.DeviceId, user.Username);
                    }
                }

                processed++;
                progress?.Report(processed * 100.0 / total);
            }
        }

        if (removed > 0)
        {
            var language = ServerLanguage.Current;
            _activity.Log(
                BaseTranslations.Get(removed == 1 ? TranslationKeys.ActivitySessionsLoggedOutOne : TranslationKeys.ActivitySessionsLoggedOutMany, language),
                "UserManagement.SessionCleanup",
                CleanupOverview(removedByUser.Values.ToDictionary(u => u.Username, u => (IReadOnlyDictionary<string, int>)u.Apps, StringComparer.Ordinal), language),
                userId: removedByUser.Count == 1 ? removedByUser.Keys.First() : default);
        }

        return removed;
    }

    /// <summary>
    /// The overview of a cleanup run's entry, as "Logged out alice's stale session on Jellyfin Web" for one session, or
    /// "Logged out 3 stale sessions for alice on Infuse (2) and bob on Swiftfin" for more.
    /// </summary>
    /// <param name="removedByUser">How many sessions were logged out from each client app, by username.</param>
    /// <param name="language">The language, or null for US English.</param>
    /// <returns>The overview.</returns>
    public static string CleanupOverview(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> removedByUser, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(removedByUser);
        var users = removedByUser
            .Select(u => (Username: u.Key, Apps: u.Value, Total: u.Value.Values.Where(v => v > 0).Sum()))
            .Where(u => u.Total > 0)
            .OrderByDescending(u => u.Total)
            .ThenBy(u => u.Username, StringComparer.Ordinal)
            .ToList();
        var removed = users.Sum(u => u.Total);
        if (removed == 1)
        {
            var only = users[0];
            return ActivityText.Sentence(BaseTranslations.Format(
                TranslationKeys.ActivitySessionsLoggedOutOverviewOne,
                language,
                only.Username,
                ActivityLists.Counted(only.Apps)))!;
        }

        var parts = users
            .Select(u => BaseTranslations.Format(TranslationKeys.ActivitySessionsUserApps, language, u.Username, ActivityLists.Counted(u.Apps, dropSingle: users.Count == 1)))
            .ToList();
        return ActivityText.Sentence(BaseTranslations.Format(
            TranslationKeys.ActivitySessionsLoggedOutOverviewMany,
            language,
            removed,
            ActivityLists.JoinAnd(parts, language)))!;
    }
}

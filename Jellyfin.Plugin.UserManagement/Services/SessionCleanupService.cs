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
                        _logger.LogInformation(
                            "Logged out stale device {DeviceName} ({AppName}) of user {UserId} from group {GroupId}",
                            device.DeviceName,
                            device.AppName,
                            userId,
                            groupId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to log out device {DeviceId} of user {UserId}", device.DeviceId, userId);
                    }
                }

                processed++;
                progress?.Report(processed * 100.0 / total);
            }
        }

        if (removed > 0)
        {
            _activity.Log(
                "Session cleanup logged out " + removed + " stale session" + (removed == 1 ? string.Empty : "s"),
                "UserManagement.SessionCleanup");
        }

        return removed;
    }
}

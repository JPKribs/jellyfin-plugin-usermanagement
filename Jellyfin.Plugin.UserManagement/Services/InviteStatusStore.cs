using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.UserManagement.Models;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Persists per invite runtime counters (uses, PIN failures, rate limit timestamps) to a JSON file in
/// the plugin configuration folder, the same pattern the DDNS and ServerSync plugins use for runtime
/// state. Keeping these out of the config XML means the configuration is rewritten only when an
/// administrator changes a setting, not on every signup or wrong PIN attempt.
/// </summary>
/// <remarks>
/// The anonymous invite endpoints read the status on every request, so it is held in memory and only
/// re-read when the file's write time changes, which covers both this instance's own writes and a file
/// replaced from outside (a restore, or a manual reset). Callers always get copies, so nothing they do
/// to a returned value can reach the cache without going through <see cref="Update"/>.
/// </remarks>
public sealed class InviteStatusStore
{
    private readonly JsonFileStore<InviteStatusData> _store;
    private readonly string _path;
    private readonly ILogger<InviteStatusStore> _logger;
    private readonly object _lock = new();

    private InviteStatusData? _cache;
    private DateTime _cacheStamp;
    private bool _fileUnreadable;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteStatusStore"/> class.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="logger">The logger.</param>
    public InviteStatusStore(IApplicationPaths paths, ILogger<InviteStatusStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _logger = logger;
        _path = Path.Join(paths.PluginConfigurationsPath, "Jellyfin.Plugin.UserManagement.InviteStatus.json");
        _store = new JsonFileStore<InviteStatusData>(_path, logger);
    }

    /// <summary>
    /// Reads the stored status, or a fresh empty status when none exists or it cannot be read. The result
    /// is a copy: changing it does not change the store.
    /// </summary>
    /// <returns>The status data.</returns>
    public InviteStatusData Load()
    {
        lock (_lock)
        {
            return Copy(Current());
        }
    }

    /// <summary>
    /// Reads one invite's status without copying the whole store.
    /// </summary>
    /// <param name="inviteId">The invite id.</param>
    /// <returns>A copy of the invite's status, or <c>null</c> when it has none yet.</returns>
    public InviteStatus? Find(Guid inviteId)
    {
        lock (_lock)
        {
            return Current().Invites.TryGetValue(inviteId, out var status) ? Copy(status) : null;
        }
    }

    /// <summary>
    /// Replaces the whole status and writes it to disk. A failed write is logged rather than thrown, so a
    /// full or read only disk never fails the signup or PIN check that triggered it; the in memory status
    /// still moves on. Prefer <see cref="Update"/>, which cannot lose a concurrent change.
    /// </summary>
    /// <param name="data">The status to persist.</param>
    public void Save(InviteStatusData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_lock)
        {
            Persist(Copy(data));
        }
    }

    /// <summary>
    /// Reads, mutates and writes the status as one atomic step, so a caller that only needs to adjust a
    /// counter cannot lose a concurrent write between its own read and write. The mutation works on a
    /// copy, so one that returns <c>false</c> after changing something leaves the store untouched. A
    /// failed write is logged rather than thrown, as in <see cref="Save"/>.
    /// </summary>
    /// <param name="mutate">Mutation to apply; return <c>true</c> to persist the change.</param>
    public void Update(Func<InviteStatusData, bool> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (_lock)
        {
            var working = Copy(Current());
            if (mutate(working))
            {
                Persist(working);
            }
        }
    }

    // A file that cannot be read is never cached and never written over: reads fall back to the last
    // good in memory status (or an empty one), the next read tries the file again, and writes stay in
    // memory until the file is readable, so a damaged or half restored file is not replaced by zeros.
    private InviteStatusData Current()
    {
        var stamp = FileStamp();
        if (_cache is not null && stamp == _cacheStamp && !_fileUnreadable)
        {
            return _cache;
        }

        if (TryRead(out var data))
        {
            _cache = data;
            _cacheStamp = stamp;
            _fileUnreadable = false;
            return _cache;
        }

        _fileUnreadable = true;
        return _cache ?? new InviteStatusData();
    }

    private bool TryRead(out InviteStatusData data)
    {
        data = new InviteStatusData();
        try
        {
            if (!File.Exists(_path))
            {
                return true;
            }

            var json = File.ReadAllText(_path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                data = JsonSerializer.Deserialize<InviteStatusData>(json) ?? data;
                data.Invites ??= new Dictionary<Guid, InviteStatus>();
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read the invite status store; keeping changes in memory until it can be read.");
            return false;
        }
    }

    private void Persist(InviteStatusData data)
    {
        _cache = data;
        if (_fileUnreadable)
        {
            return;
        }

        try
        {
            _store.Save(data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write the invite status store.");
        }

        // Taken after the write, successful or not, so the next read keeps this in memory value rather
        // than re-reading the file and undoing it. Only a later outside change to the file moves it on.
        _cacheStamp = FileStamp();
    }

    private DateTime FileStamp()
    {
        try
        {
            // A missing file reports a fixed sentinel date, which is a stable stamp of its own.
            return File.GetLastWriteTimeUtc(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return _cacheStamp;
        }
    }

    private static InviteStatusData Copy(InviteStatusData data)
        => new() { Invites = data.Invites.ToDictionary(e => e.Key, e => Copy(e.Value)) };

    private static InviteStatus Copy(InviteStatus status)
        => new()
        {
            UsedCount = status.UsedCount,
            FailedPinAttempts = status.FailedPinAttempts,
            RecentRedemptions = new List<DateTime>(status.RecentRedemptions)
        };
}

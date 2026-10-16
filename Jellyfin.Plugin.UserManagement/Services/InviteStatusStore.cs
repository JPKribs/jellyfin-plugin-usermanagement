using System;
using System.IO;
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
public sealed class InviteStatusStore
{
    private readonly JsonFileStore<InviteStatusData> _store;
    private readonly ILogger<InviteStatusStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteStatusStore"/> class.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="logger">The logger.</param>
    public InviteStatusStore(IApplicationPaths paths, ILogger<InviteStatusStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _logger = logger;
        _store = new JsonFileStore<InviteStatusData>(
            Path.Join(paths.PluginConfigurationsPath, "Jellyfin.Plugin.UserManagement.InviteStatus.json"),
            logger);
    }

    /// <summary>
    /// Reads the stored status, or a fresh empty status when none exists or it cannot be read.
    /// </summary>
    /// <returns>The status data.</returns>
    public InviteStatusData Load() => _store.Load();

    /// <summary>
    /// Writes the status to disk. A failed write is logged rather than thrown, so a full or read only
    /// disk never fails the signup or PIN check that triggered it.
    /// </summary>
    /// <param name="data">The status to persist.</param>
    public void Save(InviteStatusData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            _store.Save(data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write the invite status store.");
        }
    }

    /// <summary>
    /// Reads, mutates and writes the status as one atomic step, so a caller that only needs to adjust a
    /// counter cannot lose a concurrent write between its own read and write. A failed write is logged
    /// rather than thrown, as in <see cref="Save"/>.
    /// </summary>
    /// <param name="mutate">Mutation to apply; return <c>true</c> to persist the change.</param>
    public void Update(Func<InviteStatusData, bool> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        try
        {
            _store.UpdateIf(mutate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write the invite status store.");
        }
    }
}

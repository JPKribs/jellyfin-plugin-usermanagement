using System;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.UserManagement.Models;
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
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly object _lock = new();
    private readonly string _path;
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
        _path = Path.Join(paths.PluginConfigurationsPath, "Jellyfin.Plugin.UserManagement.InviteStatus.json");
    }

    /// <summary>
    /// Reads the stored status, or a fresh empty status when none exists or it cannot be read.
    /// </summary>
    /// <returns>The status data.</returns>
    public InviteStatusData Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path))
                {
                    var data = JsonSerializer.Deserialize<InviteStatusData>(File.ReadAllText(_path), Options);
                    if (data is not null)
                    {
                        return data;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read the invite status store, starting fresh.");
            }

            return new InviteStatusData();
        }
    }

    /// <summary>
    /// Writes the status to disk.
    /// </summary>
    /// <param name="data">The status to persist.</param>
    public void Save(InviteStatusData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_lock)
        {
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(data, Options));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not write the invite status store.");
            }
        }
    }
}

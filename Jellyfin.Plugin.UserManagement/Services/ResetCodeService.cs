using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.UserManagement.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// Surfaces the password reset codes Jellyfin's built-in reset provider writes to disk. The core
/// provider stores each pending reset as <c>passwordreset*.json</c> in the program data folder and
/// offers no API to read them back, so this is a read-only view of those files for the dashboard.
/// The file layout is core internal, so a parse failure is skipped rather than surfaced as an error.
/// </summary>
public sealed class ResetCodeService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly string _directory;
    private readonly ILogger<ResetCodeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetCodeService"/> class.
    /// </summary>
    /// <param name="paths">The application paths, used to locate the reset files.</param>
    /// <param name="logger">The logger.</param>
    public ResetCodeService(IApplicationPaths paths, ILogger<ResetCodeService> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _directory = paths.ProgramDataPath;
        _logger = logger;
    }

    /// <summary>
    /// Reads every pending reset code, newest expiration first.
    /// </summary>
    /// <returns>The reset codes, empty when there are none or the folder is unreadable.</returns>
    public IReadOnlyList<ResetCodeInfo> ReadAll()
    {
        var results = new List<ResetCodeInfo>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "passwordreset*.json"))
            {
                var entry = ReadOne(file);
                if (entry is not null)
                {
                    results.Add(entry);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not enumerate password reset files in {Directory}", _directory);
        }

        return results.OrderByDescending(r => r.ExpirationDate).ToList();
    }

    private ResetCodeInfo? ReadOne(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            var data = JsonSerializer.Deserialize<ResetFile>(stream, Options);
            if (data is null || string.IsNullOrEmpty(data.Pin))
            {
                return null;
            }

            return new ResetCodeInfo
            {
                UserName = data.UserName ?? string.Empty,
                Pin = data.Pin,
                ExpirationDate = data.ExpirationDate,
                Expired = data.ExpirationDate < DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read password reset file {File}", file);
            return null;
        }
    }

    // Mirrors the shape Jellyfin's DefaultPasswordResetProvider serializes (Pin, UserName,
    // ExpirationDate, PinFile). Unknown members are ignored so a core addition does not break parsing.
    private sealed class ResetFile
    {
        public string? Pin { get; set; }

        public string? UserName { get; set; }

        public DateTime ExpirationDate { get; set; }
    }
}

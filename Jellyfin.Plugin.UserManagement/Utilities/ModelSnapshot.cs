using System;
using System.Text.Json;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Deep copies configuration models by a JSON round trip. Every model is a plain settable property bag,
/// so the round trip reproduces it exactly without a hand written copy to keep in step with each field.
/// </summary>
internal static class ModelSnapshot
{
    /// <summary>Returns an independent deep copy of <paramref name="value"/>.</summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="value">The value to copy.</param>
    /// <returns>The copy.</returns>
    public static T Copy<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))
            ?? throw new InvalidOperationException("Could not copy " + typeof(T).Name + ".");
    }
}

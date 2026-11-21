using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JPKribs.Jellyfin.Base;

namespace Jellyfin.Plugin.UserManagement.Utilities;

/// <summary>
/// Lists for activity overviews, which state each count once and leave out a count of 1.
/// </summary>
public static class ActivityLists
{
    /// <summary>
    /// Lists names with their counts, the largest first, as "Infuse (2), Swiftfin". A count of 1 is left out.
    /// </summary>
    /// <param name="counts">The counts by name. A count of zero or less is left out, and equal counts sort by name.</param>
    /// <param name="dropSingle">Whether to leave out the count when there is only one name, since the overview states the total.</param>
    /// <returns>The list, or an empty string when there is nothing to list.</returns>
    public static string Counted(IReadOnlyDictionary<string, int> counts, bool dropSingle = true)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var entries = counts
            .Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .ToList();
        var bare = dropSingle && entries.Count == 1;
        return string.Join(", ", entries.Select(p => bare || p.Value == 1
            ? p.Key
            : string.Create(CultureInfo.InvariantCulture, $"{p.Key} ({p.Value})")));
    }

    /// <summary>
    /// Joins items as "a, b and c" in the given language.
    /// </summary>
    /// <param name="items">The items, in order.</param>
    /// <param name="language">The language, or null for US English.</param>
    /// <returns>The joined items, or an empty string when there are none.</returns>
    public static string JoinAnd(IReadOnlyList<string> items, string? language)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            _ => BaseTranslations.Format(TranslationKeys.ActivityListAnd, language, string.Join(", ", items.Take(items.Count - 1)), items[^1])
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using JPKribs.Jellyfin.Base;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// The plugin's summed activity entries, in the server's display language. Events that arrive in bursts, such as
/// password rule enrollment while a group save applies to every member or wrong invite PINs, are counted here and
/// written as one entry per outcome instead of one per user or attempt. Registered as a singleton, so the host
/// disposes it on shutdown and anything still waiting is written then.
/// </summary>
public sealed class UserManagementActivity : IDisposable
{
    /// <summary>Users moved onto group password rules, counted by username.</summary>
    public static readonly ActivityOutcome PasswordRulesEnrolled = new(
        "UserManagement.PasswordRulesEnrolled",
        TranslationKeys.ActivityRulesEnrolledOne,
        TranslationKeys.ActivityRulesEnrolledMany,
        (t, language) => BaseTranslations.Format(
            t.Total == 1 ? TranslationKeys.ActivityRulesEnrolledOverviewOne : TranslationKeys.ActivityRulesEnrolledOverviewMany,
            language,
            t.Total,
            Names(t.Sources)));

    /// <summary>Users moved back to their original authentication provider, counted by username.</summary>
    public static readonly ActivityOutcome PasswordRulesUnenrolled = new(
        "UserManagement.PasswordRulesUnenrolled",
        TranslationKeys.ActivityRulesRemovedOne,
        TranslationKeys.ActivityRulesRemovedMany,
        (t, language) => BaseTranslations.Format(
            t.Total == 1 ? TranslationKeys.ActivityRulesRemovedOverviewOne : TranslationKeys.ActivityRulesRemovedOverviewMany,
            language,
            t.Total,
            Names(t.Sources)));

    /// <summary>Wrong invite PINs, counted by invite.</summary>
    public static readonly ActivityOutcome InvitePinFailed = new(
        "UserManagement.InvitePinFailed",
        TranslationKeys.ActivityPinFailedOne,
        TranslationKeys.ActivityPinFailedMany,
        (t, language) => BaseTranslations.Format(
            t.Total == 1 ? TranslationKeys.ActivityPinFailedOverviewOne : TranslationKeys.ActivityPinFailedOverviewMany,
            language,
            t.Total,
            ActivityText.CountedList(t.Sources)),
        LogLevel.Warning);

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementActivity"/> class.
    /// </summary>
    /// <param name="logger">The activity log writer.</param>
    public UserManagementActivity(ActivityLogger logger)
        : this(new ActivitySummary(logger, language: () => ServerLanguage.Current))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementActivity"/> class over a given summary.
    /// </summary>
    /// <param name="summary">The summary that sums and writes the entries.</param>
    public UserManagementActivity(ActivitySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary;
    }

    /// <summary>Gets the summary that sums and writes the entries.</summary>
    public ActivitySummary Summary { get; }

    /// <summary>Counts one event toward its outcome's next summary.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="source">What it is counted under, such as a username or an invite's name.</param>
    public void Add(ActivityOutcome outcome, string source) => Summary.Add(outcome, source);

    /// <inheritdoc />
    public void Dispose() => Summary.Dispose();

    /// <summary>The counted names as a plain list, in order, as "alice, bob".</summary>
    /// <param name="sources">The counts by name.</param>
    /// <returns>The list.</returns>
    public static string Names(IReadOnlyDictionary<string, int> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return string.Join(", ", sources.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Collects the users one scheduled run changed, so the run writes one entry naming them all instead of
/// one entry per user, in the server's display language.
/// </summary>
public sealed class UserRunTally
{
    private readonly Dictionary<string, int> _groups = new(StringComparer.Ordinal);
    private readonly List<string> _usernames = new();
    private Guid _lastUserId;

    /// <summary>Gets how many users were counted.</summary>
    public int Count => _usernames.Count;

    /// <summary>Counts one user.</summary>
    /// <param name="group">The group the change came from, as it should read in the overview.</param>
    /// <param name="username">The user's name.</param>
    /// <param name="userId">The user's id, attached to the entry when only one user changed.</param>
    public void Add(string group, string username, Guid userId = default)
    {
        _groups[group] = _groups.GetValueOrDefault(group) + 1;
        _usernames.Add(username);
        _lastUserId = userId;
    }

    /// <summary>The entry's name, as "3 users disabled".</summary>
    /// <param name="wording">The keys the entry reads from.</param>
    /// <param name="language">The language, or null for US English.</param>
    /// <returns>The name.</returns>
    public string Name(UserRunWording wording, string? language)
    {
        ArgumentNullException.ThrowIfNull(wording);
        return BaseTranslations.Format(Count == 1 ? wording.NameOne : wording.NameMany, language, Count);
    }

    /// <summary>The entry's overview, as "2 users were disabled when their group expired in Kids (2): alice, bob".</summary>
    /// <param name="wording">The keys the entry reads from.</param>
    /// <param name="language">The language, or null for US English.</param>
    /// <returns>The overview.</returns>
    public string Overview(UserRunWording wording, string? language)
    {
        ArgumentNullException.ThrowIfNull(wording);
        return ActivityText.Sentence(BaseTranslations.Format(
            Count == 1 ? wording.OverviewOne : wording.OverviewMany,
            language,
            Count,
            ActivityText.CountedList(_groups),
            string.Join(", ", _usernames.OrderBy(u => u, StringComparer.OrdinalIgnoreCase))))!;
    }

    /// <summary>Writes the run's one entry in the server's display language, when anything was counted.</summary>
    /// <param name="activity">The activity log writer.</param>
    /// <param name="wording">The keys the entry reads from.</param>
    /// <param name="type">The activity type.</param>
    /// <param name="attachUser">Whether to attach the user's id when only one user changed.</param>
    public void Log(ActivityLogger activity, UserRunWording wording, string type, bool attachUser = true)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (Count == 0)
        {
            return;
        }

        var language = ServerLanguage.Current;
        activity.Log(
            Name(wording, language),
            type,
            Overview(wording, language),
            LogLevel.Warning,
            attachUser && Count == 1 ? _lastUserId : default);
    }
}

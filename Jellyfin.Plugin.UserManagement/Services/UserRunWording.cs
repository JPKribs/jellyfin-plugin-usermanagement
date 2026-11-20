using JPKribs.Jellyfin.Base;

namespace Jellyfin.Plugin.UserManagement.Services;

/// <summary>
/// The keys a <see cref="UserRunTally"/> entry reads from. Each name has <c>{0}</c> for the count, and each overview
/// has <c>{0}</c> for the count, <c>{1}</c> for the groups, and <c>{2}</c> for the usernames.
/// </summary>
/// <param name="NameOne">The name for one user.</param>
/// <param name="NameMany">The name for any other count.</param>
/// <param name="OverviewOne">The overview for one user.</param>
/// <param name="OverviewMany">The overview for any other count.</param>
public sealed record UserRunWording(string NameOne, string NameMany, string OverviewOne, string OverviewMany)
{
    /// <summary>Gets the wording for members deleted when their group expired.</summary>
    public static UserRunWording ExpiryDeleted { get; } = new(
        TranslationKeys.ActivityUsersDeletedOne,
        TranslationKeys.ActivityUsersDeletedMany,
        TranslationKeys.ActivityExpiryDeletedOverviewOne,
        TranslationKeys.ActivityExpiryDeletedOverviewMany);

    /// <summary>Gets the wording for members disabled when their group expired.</summary>
    public static UserRunWording ExpiryDisabled { get; } = new(
        TranslationKeys.ActivityUsersDisabledOne,
        TranslationKeys.ActivityUsersDisabledMany,
        TranslationKeys.ActivityExpiryDisabledOverviewOne,
        TranslationKeys.ActivityExpiryDisabledOverviewMany);

    /// <summary>Gets the wording for members disabled for inactivity.</summary>
    public static UserRunWording InactiveDisabled { get; } = new(
        TranslationKeys.ActivityUsersDisabledOne,
        TranslationKeys.ActivityUsersDisabledMany,
        TranslationKeys.ActivityInactiveDisabledOverviewOne,
        TranslationKeys.ActivityInactiveDisabledOverviewMany);
}

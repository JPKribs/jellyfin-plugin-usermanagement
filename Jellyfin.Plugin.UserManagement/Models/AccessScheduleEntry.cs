namespace Jellyfin.Plugin.UserManagement.Models;

/// <summary>
/// A single access-schedule window mirroring Jellyfin's <c>AccessSchedule</c>: the day (or day group)
/// and the start/end hour during which access is allowed.
/// </summary>
public class AccessScheduleEntry
{
    /// <summary>Gets or sets the day of week (a <c>DynamicDayOfWeek</c> name, e.g. Everyday/Weekday/Sunday).</summary>
    public string DayOfWeek { get; set; } = "Everyday";

    /// <summary>Gets or sets the start hour (0–24, fractional allowed).</summary>
    public double StartHour { get; set; }

    /// <summary>Gets or sets the end hour (0–24, fractional allowed).</summary>
    public double EndHour { get; set; }
}

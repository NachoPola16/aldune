namespace Aldune.Core;

/// <summary>
/// The "not edited in N days" filter of Manage notes: a note counts as stale once its last edit is at least that many days old.
/// Only a way to look at the list; nothing is notified, archived or changed because a note is stale.
/// </summary>
public static class StaleNotes
{
    /// <summary>The periods the filter offers, in days (a week, a month, three months, a year).</summary>
    public static readonly IReadOnlyList<int> PresetDays = [7, 30, 90, 365];

    /// <summary><paramref name="days"/> of 0 means no filter (nothing is stale). A date in the future (a clock that was ahead) is never stale.</summary>
    public static bool IsStale(DateTimeOffset updatedAt, int days, DateTimeOffset now) =>
        days > 0 && now - updatedAt >= TimeSpan.FromDays(days);
}

namespace Aldune.Core;

/// <summary>
/// Which versions' news to show after an update. Pure: the app passes the version it last showed (<see cref="AppSettings.LastSeenVersion"/>),
/// the one it is running and the versions that have entries in its catalog.
///
/// A fresh install shows nothing (there is nothing new to someone who just arrived: the welcome toast does that job). An install made before
/// this feature existed has no stored version: it is taken to be the last one without it, so its first update lists everything since.
/// </summary>
public static class WhatsNewPlan
{
    /// <summary>The version assumed for an existing install that never stored one (the last release before «What's new» existed).</summary>
    public static readonly Version BaselineForUnknown = new(2, 3, 0);

    /// <summary>The versions to list, newest first: those after the last one seen, up to and including the running one.</summary>
    public static IReadOnlyList<Version> ToShow(string? lastSeen, bool freshInstall, Version current, IEnumerable<Version> available)
    {
        if (freshInstall) return [];
        var from = Version.TryParse(lastSeen, out var seen) ? seen : BaselineForUnknown;
        return available.Where(v => v > from && v <= current).OrderByDescending(v => v).ToList();
    }
}

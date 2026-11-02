namespace Aldune.Core;

/// <summary>
/// When a saved edit is pushed to the other devices. Android already syncs a few seconds after a save (SyncTriggerPolicy); on Windows the
/// edit used to wait for the polling interval (15 minutes by default). Edits are grouped by a quiet period and passes keep the same
/// minimum gap as <see cref="NoteOpenSync"/>, so typing never turns into a pass per pause.
/// </summary>
public static class LocalEditSync
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(5);

    /// <summary>How long to wait from now before the pass: the quiet period, or until the minimum gap since the last attempt, whichever is later.</summary>
    public static TimeSpan DelayUntilRun(DateTimeOffset? lastAttempt, DateTimeOffset now)
    {
        if (lastAttempt is null) return QuietPeriod;
        var untilGap = lastAttempt.Value + NoteOpenSync.MinGap - now;
        return untilGap > QuietPeriod ? untilGap : QuietPeriod;
    }
}

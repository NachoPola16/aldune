namespace Aldune.Core;

/// <summary>
/// While a note stays open, other devices can edit the same note. Without a periodic check nothing pulls their changes until the person
/// saves again or the (slow) polling interval fires, so two devices with the shared note open drift apart and the next pass reports a
/// conflict. A short poll while a note is open closes that window; it reuses the minimum gap of <see cref="NoteOpenSync"/>, so it never
/// stacks on top of a pass triggered by an edit or by opening a note.
/// </summary>
public static class OpenNoteLiveSync
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public static bool ShouldRun(int openNotes, bool syncConfigured, DateTimeOffset? lastAttempt, DateTimeOffset now, bool running) =>
        openNotes > 0 && NoteOpenSync.ShouldRun(syncConfigured, lastAttempt, now, running);
}

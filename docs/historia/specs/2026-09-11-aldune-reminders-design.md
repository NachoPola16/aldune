# Aldune — Reminders with Windows notification (design spec)

Adds a one-off reminder (specific date/time, not recurring) per note,
with a native Windows notification when it falls due and simple snoozing. See
`docs/ROADMAP.md`, section "Reminders with Windows notification" —
marked there as the biggest functional gap against the competition (Notezilla
has it and it is the most praised thing about that app; Aldune today has nothing of the sort).

## Context and motivation

Aldune has no way for a note to "alert" at a specific
moment — today it only exists if the user remembers to look at it. Notezilla, the
roadmap's explicit reference, offers exactly this and it is its most
praised feature. It is a new subsystem (persistence + periodic firing + native
operating-system notification), not a tweak to something that already exists, hence
the full spec instead of a bounded change.

Decisions taken in the design, with their reasons, so as
not to discuss them again from scratch:

- **Per whole note, not per loose line/task inside a note.** It fits
  the Aldune post-it metaphor (and what Notezilla itself does) and does not contradict the decision already taken several times to
  keep the body as a plain `TextBox` with no structured fields (same
  argument that ruled out the separate title, line dragging and
  the colour picker when creating a note). A reminder per line, moreover,
  would be fragile: the body has no stable identity per line (Alt+Up/
  Down reorders, it is edited, deleted), so "line 3" stops being
  line 3 as soon as something changes above it — a reminder that
  silently detaches from the right task is worse than not having the
  feature.
- **One-off, not recurring**, with snooze. Recurring (daily/weekly) is
  a different product category (calendar/alarm) and a considerably larger
  design surface (what happens with an archived series, how a
  series is edited...) that is not needed for the real case of a post-it ("don't forget
  X").
- **Notification via `NotifyIcon.ShowBalloonTip`** (WinForms, already used by
  `TrayIcon`), not a truly interactive toast
  (`CommunityToolkit.WinUI.Notifications`). The interactive toast with embedded action
  buttons requires registering a shortcut with an AUMID in the
  Start menu pointing to a fixed path — if the portable build is moved to another folder
  or runs from another location (the use case the roadmap itself sells
  as a differentiator: "there is a portable version that needs no installing, no
  competitor offers it"), that shortcut ends up pointing at the old place
  and the toast can no longer reopen the right note. `ShowBalloonTip` does not
  touch anything outside the portable folder and on Windows 10/11 the system itself
  already renders it as a modern notification in the Action Center — it only loses
  the embedded action buttons, and the note opens in under a second on click,
  so snoozing from there is not a heavy step.
- **A reminder that fell due with the app closed or the PC asleep still alerts
  when Aldune is reopened**, instead of being silently dropped — consistent
  with "I don't hijack your data or actions".
- **Picker with quick shortcuts** ("In 1 hour", "Tonight", "Tomorrow
  9:00") **+ exact date/time** for the other cases, not just a raw
  calendar — it covers the most common post-it use without navigating a
  calendar every time.

## Scope

**In:**

- New `NoteReminder` table (`Aldune.Core.NotesDatabase`), with
  `NotesRepository.SetReminder`/`ClearReminder`/`GetReminder`/
  `GetDueReminders`/`GetPendingReminders`.
- `NotesRepository`'s `Delete`/`PurgeExpiredTrash` extended to also clean up
  the `NoteReminder` of the note being deleted — same care they already
  take with `NotePlacement`.
- `Aldune.Windowing.ReminderScheduler` (new): periodic polling while Aldune runs
  + a catch-up pass on startup, fires the alert via
  `NotifyIcon`.
- "Reminder" entry in the "⋯" menu of `NoteWindow`: set, view, remove
  a reminder for the open note. The same panel serves for snoozing
  (open the note from the alert and set a new one).
- Visual indicator on the dock tab (`EdgeDockWindow`) for notes with
  a pending reminder.
- Localisation of the new texts in `Aldune.Resources.Strings` (ES/EN).

**Out of scope (not in this round):**

- Recurring reminders.
- Per-line/loose-task reminders.
- Interactive toast with embedded action buttons (`CommunityToolkit.
  WinUI.Notifications`) — explicitly ruled out because of the tension with
  "portable, no footprint"; see above.
- Any firing when Aldune is not running (Task Scheduler,
  `ScheduledToastNotification`...) — accepted on purpose: the dock does not
  exist either if Aldune is not running, so it is not a new limitation compared with the
  rest of the app.
- Sound/vibration different from what the native Windows notification
  already brings by default.

## Components and changes

### `Aldune.Core.NotesDatabase` — new table

```sql
CREATE TABLE IF NOT EXISTS NoteReminder (
    NoteId TEXT PRIMARY KEY,
    DueAt TEXT NOT NULL   -- ISO-8601 UTC, same convention as CreatedAt/UpdatedAt
);
```

`PRIMARY KEY NoteId`, not a composite key nor its own `Id`: at most one
active reminder per note (consistent with "one-off, not recurring" —
setting a new one replaces any previous one instead of accumulating them). Same
pattern as `NotePlacement`, with `CREATE TABLE IF NOT EXISTS` and no
migrations — an existing user database simply gains the
table the first time it starts with this version.

### `Aldune.Core.NotesRepository` — new methods

- `SetReminder(Guid noteId, DateTimeOffset dueAt)`: `INSERT OR REPLACE`,
  same pattern as `SavePlacement`/`MoveNote`.
- `ClearReminder(Guid noteId)`: `DELETE ... WHERE NoteId = $noteId`, same
  pattern as `DeletePlacement`. It is called both on manual cancel and
  from `ReminderScheduler` right after alerting (one-off: once
  fired, it is no longer pending).
- `GetReminder(Guid noteId) -> DateTimeOffset?`: to paint the current state
  in the "⋯" menu of the open note.
- `GetDueReminders(DateTimeOffset now) -> IReadOnlyList<(Guid NoteId,
  DateTimeOffset DueAt)>`: `WHERE DueAt <= $now`, used both by the periodic
  polling and by the startup catch-up (same query, different
  trigger).
- `GetPendingReminders() -> IReadOnlyDictionary<Guid, DateTimeOffset>`:
  all rows of `NoteReminder`, to paint the indicator in the dock without
  a query per note — it is called together with `GetByState(Active)` when the
  dock reloads.
- `Delete(Guid id)` and `PurgeExpiredTrash(TimeSpan retention)`: they get
  `DELETE FROM NoteReminder WHERE NoteId = ...` added, just as they already do with
  `NotePlacement`, so that a deleted note does not leave an orphaned reminder
  that `GetDueReminders` would try to resolve against a note that no longer
  exists.

### `Aldune.Windowing.ReminderScheduler` (new)

Instantiated once from `App.xaml.cs`, alongside the other application-level
services (the same place that already builds `AppCoordinator`/`TrayIcon`).
It receives `NotesRepository`, `AppCoordinator` (to open notes/the manager) and the
`NotifyIcon` that `TrayIcon` already creates (the existing instance is passed to it, a new one
is not created — a second tray icon would be confusing).

- A `DispatcherTimer` with a 30 s interval runs `GetDueReminders(now)` on
  each tick. Same order of magnitude as other timers in the project
  (autosave 500ms, hover polling 50ms, task purge on
  autosave) — 30 s is plenty for a reminder meant in minutes or
  hours, not seconds.
- In addition, an immediate pass on construction (called from
  `App.OnStartup` after `PurgeExpiredTrash`), for the catch-up of
  reminders that fell due with the app closed — same query, without waiting for the first tick.
- With **one** due: `ClearReminder`, then `NotifyIcon.ShowBalloonTip`
  with the note title (`NoteTitleHelper.GetTitle`) as the balloon
  text. The `NoteId` associated with that specific alert is stored; the
  `BalloonTipClicked` event of the `NotifyIcon` uses it to open that note via
  `AppCoordinator.OpenOrActivateNote`.
- With **several** due at once (typical only of the startup catch-up
  after the app has been closed for a while): all are cleared at once, and the balloon says
  "N pending reminders" instead of listing them; the click opens
  `NotesManagerWindow` instead of N note windows at once — it avoids
  window spam when there are several.
- `NotifyIcon` natively keeps only one balloon tip at a time: if a
  second group of due reminders appears while the previous balloon is still
  visible, `ShowBalloonTip` simply replaces it — acceptable, it is the
  same behaviour any queued Windows notification already has.

### `NoteWindow` — set/view/remove reminder

New "Reminder" entry in `ActionsPopup` (the "⋯" menu), next to
"Export to Markdown" — a clock icon before the text, same style as
the rest of the menu entries.

- **No reminder set**: on click, a panel opens (same visual style as
  `ActionsPopup`, see `NoteMenuItemStyle`) with: three shortcut buttons
  and, below, a `Calendar` + time field for the other cases. A
  "Save" button calls `NotesRepository.SetReminder`. The three shortcuts
  are computed against `DateTimeOffset.Now` (local time, not UTC — converted to
  UTC only when saving, like the rest of the app's timestamps):
  - **"In 1 hour"**: `Now + 1h`, unambiguous.
  - **"Tonight"**: today at 20:00 if `Now` is before 20:00; if it is already
    20:00 or later, it becomes tomorrow at 20:00 (avoids
    proposing a time that has already passed today).
  - **"Tomorrow 9:00"**: the next day at 9:00, always — it does not depend on
    the current time.
  `Aldune.Core.ReminderPresets` (new, pure, TDD) computes the three from
  an explicit `DateTimeOffset "now"`, so that "tonight" can be tested
  on both sides of the 20:00 limit without depending on the real clock.
- **With a reminder set**: the menu entry shows the date/time instead of
  just "Reminder" (e.g. "Reminder: tomorrow 9:00"). On click the
  same panel opens, with an additional "Remove" button that calls
  `ClearReminder`.
- **Snoozing** reuses this same flow with no separate UI: clicking
  the alert balloon opens the note (the reminder that just fired has already
  cleared itself), and "⋯ → Reminder" is right there to set a new
  one. A separate "snooze" panel is not built — it would be
  duplicating exactly the same panel with another title.

### `EdgeDockWindow` — indicator on the tab

`SetNotes`/`Refresh` (where `NoteTitleHelper.GetTabPreview` is already computed
for each tab) queries `GetPendingReminders()` once per refresh
(not per tab) and passes whether that note has an entry. Each tab paints a
small clock glyph (to be chosen from Segoe Fluent Icons, with no visual verification
yet — same warning already noted for other icons) next to the title when applicable.

## Error handling

- **`DueAt` in the past when saving** (the user leaves the panel open
  until the chosen time has passed, or the system clock changes):
  `SetReminder` validates nothing special — it is saved anyway, and the next
  `ReminderScheduler` tick (at most 30 s later) treats it as due and
  alerts. Simpler than rejecting it, and the result (it alerts almost
  immediately) is reasonable.
- **The note is deleted/purged with a pending reminder**: covered by
  the cleanup added to `Delete`/`PurgeExpiredTrash` (see above) — without
  this care, `GetDueReminders` would return a `NoteId` that
  `AppCoordinator.OpenOrActivateNote` could not resolve.
- **The system clock goes backwards** (manual change, DST badly applied):
  same case as "`DueAt` in the past", with no special handling — the
  reminder simply waits until `now >= DueAt` becomes true again.
- **`ShowBalloonTip` while Windows has notifications muted**
  (focus mode/full screen): native system behaviour, Aldune does not detect or handle it — consistent with not
  interfering with the user's decisions about their own system.

## Testing

Core, TDD like the rest of the project:

- `NotesRepositoryReminderTests` (new, same pattern as
  `NotesRepositoryTaskCompletionTests`): `SetReminder`/`GetReminder`
  (round trip), `SetReminder` twice replaces instead of accumulating,
  `ClearReminder`, `GetDueReminders` distinguishes due from future and does not
  return what has already been cleared, `Delete`/`PurgeExpiredTrash` leave
  `NoteReminder` without the deleted note's row.
- `ReminderPresetsTests` (new): "In 1 hour" always `+1h`; "Tonight"
  before 20:00 gives today 20:00, at exactly 20:00 and after it gives tomorrow
  20:00 (the exact limit); "Tomorrow 9:00" gives the next day at 9:00
  regardless of the hour of `now`.

`ReminderScheduler` and the `NoteWindow`/`EdgeDockWindow` changes are the
WPF layer (timer, `NotifyIcon`, UI) — no automated test, same pattern
as the rest of that layer in the project (autosave, hover polling,
animations). Verified by hand: set a reminder 1-2 minutes ahead,
confirm that the alert arrives and that the click opens the right note; force the
"app closed when due" case by closing Aldune and reopening after the
set time.

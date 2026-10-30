# Aldune for Android — F7: reminders, protected notes and conflicts

> 2026-10-18. Closes §2.3, §2.4 and §2.9 of the parent spec (`2026-10-17-aldune-android-design.md`). The decisions marked **(user)** were made by the user
> when preparing F7; the rest are reasoned defaults. Parts: §3 (pure JVM, with TDD) and §4 (Android, done here with probes).

## 1. What goes into F7

1. **Reminders**: one per note, one-off, local to the device; alarm, notification with Done / Snooze / Open, bell with the time in the list.
2. **Protected notes**: open, edit, protect and remove protection with a password (up to F6 they were only shown with a padlock and read-only).
3. **Conflicts**: see the other version and keep one or the other; those left without a note, in a separate list.

Out of F7: biometrics, merging conflicts (stays in Windows), reminders that travel to the PC (format 4 does not carry them), locking the whole app,
repeating reminders.

## 2. Product decisions

| Topic | Decision |
|---|---|
| Re-locking a protected note **(user)** | On leaving the note and when the app goes to the background. Each note is unlocked separately; **no password is remembered**. No inactivity lock. |
| When a reminder fires **(user)** | It is removed when it notifies, as in Windows. The notification stays until dismissed. Done closes it; Snooze creates a new one. If the phone was off or the app dead, it notifies as soon as it starts. |
| Notice in the list **(user)** | Bell and time on the row («hoy 20:00», «mañana 9:00» or a date), next to the padlock and the conflict icon. |
| Protected version in a conflict **(user)** | «Versión protegida» is shown with an Unlock button (that version's password may be a different one). The two conflict buttons work **without** unlocking it. |
| Other version of the conflict **(user)** | Full text of the other version with the differing lines highlighted (position-based comparison, like Core's `ConflictDiff`). |
| Edit versus delete **(user)** | If the edit won and the note had been deleted on another machine: strip «Se borró en otro equipo», with «Quedarme con esta» and «Borrarla» (the deletion travels signed). If the deletion won and it was left without a note: list «N conflictos» with «Recuperar la nota» and «Descartar». |
| Exact time | Inexact alarm by default. «Hora exacta» setting that leads to granting `SCHEDULE_EXACT_ALARM` (parent spec §2.9). Not `USE_EXACT_ALARM`. |
| Password | Minimum 4 characters, twice when protecting (Core's `PasswordRules`); when unlocking only the empty one is rejected. |
| Linked notes | A note linked to a file does not exist on Android; there is nothing to decide. |
| Text in notification | A protected note **never** carries its text: title «Nota protegida» and no tasks. |

## 3. Pure JVM part (pure JVM, with TDD)

Split across the existing modules; **no new dependencies**, no Android. Comments in Spanish explaining the why. Interface texts: never; the fillers and the
labels return types, not strings.

### 3.1 `domain`: reminders

- **`ReminderPresets`** (port of `ReminderPresets.cs`): `inOneHour(now)`, `tonight(now)` (today 20:00; if it is already 20:00 or later, tomorrow 20:00), `tomorrowMorning(now)`
  (next day at 9:00). They receive `ZonedDateTime` and work in its zone (the daylight-saving change does not shift "9:00"). Tests on both sides of the
  20:00 boundary and on the clock-change day.
- **`Reminder`** (`noteId`, `dueAt: Instant`) and **`ReminderStore`** (interface: `get`, `set`, `clear`, `all`) with `InMemoryReminderStore` for tests.
- **`ReminderRules`** (no internal clock: `now` comes in as a parameter):
  1. `toSchedule(reminders, notes, now)`: those with an **active** note and `dueAt > now`.
  2. `overdue(reminders, notes, now)`: active note and `dueAt <= now` (those of a start-up after being off), by ascending `dueAt`.
  3. `orphans(reminders, notes)`: reminders whose note does not exist or is no longer active (archived, trash, deleted by sync). They are cleaned up at start-up and after a sync
     (Windows excludes them when notifying, it does not delete them; on Android they are deleted because there is no other way to see them).
  4. `snooze(kind, now)` → `dueAt`: `OneHour` = +1 h; `TomorrowMorning` = `tomorrowMorning(now)`.
- **`ReminderLabel`**: from `dueAt`, `now` and the zone it returns a sealed type (`Today(time)`, `Tomorrow(time)`, `Date(date, time)`, `Overdue`) for the row; the interface
  turns it into text. Test at midnight and across zones.
- **`ReminderNotificationContent`**: from a note it returns a sealed `Open(noteId, title, pendingTasks: List<String>)` (at most **3**, a named constant,
  the unticked `☐`, trimmed to 120 characters) or `Locked(noteId)`. `Locked` **has nowhere to put text** (structural guarantee, like `WidgetStrip.Locked`).
  Property test: with protected notes whose `text`, tags and `protectedContentJson` carry a marker, no `toString()` of the result contains it.
  It uses `NoteTitleHelper` and `TaskLists` from `text`; do not reimplement the title or the tasks.

### 3.2 `core`: protected notes with the key already derived

`ProtectedNotes.protect/reprotect/tryUnprotect` derive the key on every call (600,000 iterations). To edit without waiting on every autosave:

- **`ProtectedNotes.unlock(content, password)`** → `UnlockedProtectedNote?` (`null` if the password is wrong), which keeps the derived key and the salt and exposes `text`.
- **`UnlockedProtectedNote.reprotect(newText)`** → new `ProtectedNoteContent`, **without deriving again**: same salt, new nonce (like Core's `Reprotect`).
  **`close()`** zeroes the key; using it afterwards throws `IllegalStateException`.
- The current methods do not change. The `tests/vectors/` vectors keep passing; add a round-trip test: `protect` → `unlock` → `reprotect` → `tryUnprotect`
  with the original password returns the new text.
- **`PasswordRules`** (port): `check(password, confirmation?)` → `None | Empty | TooShort | Mismatch`, `MIN_LENGTH = 4`.

### 3.3 `text`: conflict differences

Port of `ConflictDiff.cs`:

- `summarize(winner, losing)` → `ConflictDiffSummary?` (`null` if it is the same text; `\r\n` and `\n` count the same) and `clip(line, max)`.
- **`lineDiff(winner, losing)`** → list of `DiffLine(number, text, differs)` **of the other version** (the losing one), by position, with the missing line counted as
  empty. It is what the strip draws. The edge cases: equal texts, one empty version, one longer than the other, `\r\n`.

### 3.4 `syncclient`: protect and resolve

- **`ProtectionService(store: NoteStore, cipher…, clock)`** on top of `NoteStore` (the same one as `NoteService`):
  - `protect(id, password, confirmation)`: validates with `PasswordRules`; encrypts the text with `ProtectedNotes.protect`, leaves the plain text **empty**, `isProtected = true`,
    `protectedContentJson`, `updatedAt = clock.now()`, `syncAuthorDeviceId = null`. Fails if it is already protected or does not exist.
  - `unlock(id, password)` → `UnlockedProtectedNote` or error `WrongPassword`.
  - `save(id, unlocked, newText)`: `reprotect` + the same columns as above; the plain text stays empty. `StaleWrite` as in `NoteService.edit` if there is a newer version.
  - `removeProtection(id, password)`: decrypts, restores the plain text, `isProtected = false`, `protectedContentJson = null`, new date and author `null`.
  - **All** of them keep colour, tags, `dockPosition`, state and `screenOrigin`. One test per field.
  - A protected note does **not** accept `NoteService.edit` (it already returns `Protected`); do not change that.
- **`ConflictStore`** (interface): `all()`, `get(noteId)`, `delete(noteId)`. `InMemoryConflictStore` for tests. (Room implements it in `app`.)
- **`ConflictResolver(notes: NoteStore, conflicts: ConflictStore, reminders…, clock, deviceId)`**:
  - `kind(conflict)` → `EditVsEdit` (the live note exists and the losing one has a note), `EditVsDelete` (the live note exists and the losing one is a deletion: strip «Se borró en otro
    equipo») or `Orphan` (no live note: a deletion won).
  - `keepCurrent(noteId)`: discards the conflict without touching the note.
  - `useOther(noteId)`: like Core's `RestoreConflict`. With a losing note: applies it as a new version (`updatedAt = clock.now()`, author `null`, keeps its
    tags, colour, state and protection as they come) and discards the conflict. With a deletion as the loser: deletes the live note **leaving the local tombstone** (sync signs it) and
    discards the conflict. If the conflict no longer exists, error `NotFound` and nothing is written.
  - `orphans()`: the conflicts of type `Orphan`, most recent first, for the «N conflictos» list.
  - A conflict whose loser is **protected** is resolved **without unlocking**: `useOther` copies the `protectedContentJson` as is and leaves the plain text empty.
  - If the restored or deleted note had a reminder, `useOther` removes it when the note stops being active or existing (it uses `ReminderRules.orphans`).
  - Tests: the three types × (`keepCurrent`, `useOther`); idempotence (resolving twice); `useOther` of a loser with CRLF keeps `lineEnding`; protected; a write that
    does not happen when it fails.

### 3.5 Definition of done for the JVM part

Baseline of **650** tests (core 82, sync 119, text 100, domain 168, syncclient 181): all keep passing, plus the new ones (≈80-110). No change in behaviour in what already
exists.

## 4. Android part (here, with probes on the Pixel and the tablet)

1. **Alarms**: `ReminderScheduler` on top of `AlarmManager` (`setAndAllowWhileIdle` by default; `setExactAndAllowWhileIdle` if there is permission and the setting is on). One alarm per
   reminder, `PendingIntent` with the note id. They are rescheduled on device boot (F6's `BootReceiver`), on time change (`TIME_SET`) or zone change
   (`TIMEZONE_CHANGED`) and on opening the app (`ReminderRules.toSchedule`).
2. **Notice**: channel «Recordatorios» (high importance). `ReminderNotificationContent` → notification with **Done**, **Snooze** (menu: +1 h / tomorrow 9:00) and **Open**
   (opens the note; a protected one opens the list with its row highlighted, like the widget). `POST_NOTIFICATIONS` is requested when setting the first reminder, not at startup.
3. **Note screen**: in the "⋯" menu, «Recordatorio» opens a sheet with the three shortcuts, «Elegir fecha y hora» (Material 3 pickers) and «Quitar». Fixed text:
   «Este recordatorio solo avisa en este dispositivo».
4. **List**: bell + `ReminderLabel` on the row.
5. **Protected notes**: password dialog (Protect: two fields; Unlock: one) with an indicator while the key is derived (off the UI thread). The editor of an unlocked
   protected note saves with `ProtectionService.save` and leaves no text in `note.text`. The session is closed (`close()`) on leaving the note and on `ON_STOP`. The "⋯"
   menu offers «Proteger» or «Quitar la protección» (asks for the password).
6. **Conflicts**: strip under the note title with the other version read-only (`lineDiff`) and the two buttons; the protected version shows «Versión protegida» +
   Unlock. In the list, a notice «N conflictos» only for the `Orphan` ones, with their two buttons per row.
7. **Settings**: «Recordatorios» (exact time with the permission; link to the system settings when it is missing).
8. **Texts** in the five languages (`android/strings.tsv`, `python android/scripts/gen_strings.py`).

## 5. Verification (F7 gate)

- JVM: `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` green; `lintDebug` with no errors; `gen_strings.py --check`.
- **Reminders** (Pixel and tablet): notice with the app closed and the phone idle (`dumpsys deviceidle force-idle`), exact and inexact; reboot the phone with a
  reminder set; change the time zone; Done / Snooze / Open; overdue reminder with the phone off; archive the note with a reminder → it does not notify.
- **Protected notes**: PBKDF2 **measured on the tablet** (note the ms); a protected note created on Android opens in Windows and one from Windows opens and is edited on Android, and the
  edit comes back to Windows after sync; wrong password; leaving the note and going to the background lock it again; widget, search and notification never show its text.
- **Conflicts**: provoke the three types with the sync probe (F5, `ProbePcSimulator`); resolve each one with both buttons and check the result in the PC simulator;
  a protected loser resolved without unlocking.
- The editor is tested with **Gboard and with a third-party keyboard**. On the tablet in PC mode, Back is not used.
- Pixel: ask for "Do Not Disturb" to be on before touching its screen.

## 6. Risks

- **Alarms on Android 14+**: `SCHEDULE_EXACT_ALARM` comes denied; without it the alarm can take minutes. The interface says so and the setting explains it.
- **Aggressive manufacturers** (battery saving): the Pixel and the tablet are not, but others may kill the alarm. It is documented in `docs/ANDROID_PROBES.md`, not something we try to solve.
- **PBKDF2 on the tablet**: 600,000 iterations can exceed 1 s. That is why the unlocked session keeps the key and `save` does not derive again; if unlocking still exceeds ~3 s,
  decide it with the measured numbers (the iteration count is not lowered: it is compatibility with Windows).
- **New date in `useOther`**: same as in Windows, so that the next sync publishes the decision; if the phone's clock runs behind, the decision may lose against another version.
- **Orphan reminder after sync**: a note archived or deleted on another machine leaves its reminder behind; `ReminderRules.orphans` cleans it on every start-up and after every sync.

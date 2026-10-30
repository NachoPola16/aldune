# Aldune v1 — Design Spec

## Context and goal

Aldune is a notes application for Windows inspired by Hold My Notes,
Noty (aimen08/noty) and noty-sepia: notes anchored to the edge of the
screen that live as a thin strip ("pill") and fan out when the mouse
hovers over them. None of the references exists for Windows — they are
all native macOS apps (Swift/SwiftUI).

Intended use: a personal tool for the author to jot things down quickly,
like a real post-it — with no date handling or reminders, no account and
no sync. Secondary goal, not blocking for v1: if the project turns out
well, publish it so that others can use it.

This spec was independently reviewed (second opinion) before moving on to
the implementation plan; the changes from that review are already
incorporated in the sections below.

## v1 scope

### Core (window mechanics)

- A thin strip anchored to a screen edge (top/bottom/left/right), which
  fans out when the mouse hovers over it.
- Click on a tab of the fan → the note opens at full size in place.
- Multi-monitor: the app can be shown on all connected screens or on just
  one, configurable in Settings.
- Edge position: **a single one, global, for all active screens** in v1
  (see "Postponed to v1.1" for the per-screen variant).
- The window **really changes size** instead of using a transparent
  surface with toggled click-through: small (just the pill) at rest, it
  grows to occupy the fan area while expanded, and shrinks again on
  collapse. That way there is never a transparent zone to manage nor a
  "pass clicks through" state to switch on/off — that design was
  discarded because it was contradictory (a window with the click-through
  style enabled does not receive mouse events, so it could never detect
  the hover that would disable it). This approach also prevents
  `AllowsTransparency` from disabling the system's normal text smoothing
  (ClearType), which would clash with "typography chosen for legibility".
- The pill **does not steal focus** when it expands on hover (window style
  `WS_EX_NOACTIVATE`) — it must not interrupt what is being typed in
  another application. The expanded note window (`NoteWindow`), on the
  other hand, does activate and receive focus when opened, because that is
  where the user is expected to type.
- The collapse of the fan does not depend solely on `MouseLeave` (not very
  reliable: it oscillates at the edge of the expanded area, and may not
  fire if another window ends up on top) — a small delay is applied before
  collapsing (~150-250ms) plus a low-frequency mouse position check
  **only while expanded**, as a safety net. This does not contradict "no
  polling": the hover detection itself is still done through native window
  events; the timer only comes into play as a fallback for the collapse.
- If the chosen edge coincides with that of the Windows taskbar, the dock
  anchors to the work area (excluding the bar), not to the physical edge
  of the screen, so as not to overlap or fight with its z-order.
- Per-monitor DPI aware (Per-Monitor V2): each screen can have a
  different scale factor (e.g. laptop at 150% + external monitor at
  100%); the app re-renders crisply on each one instead of letting
  Windows stretch an already-rendered bitmap.
- Each screen is identified by a stable device id (not by the
  `\\.\DISPLAY1` number, which can change when monitors are reconnected).
  If a monitor with anchored notes is disconnected, those notes are
  temporarily **shown** on the primary screen without **rewriting** their
  stored origin screen — when the original monitor is reconnected, they go
  back to their place. They are never lost or left inaccessible, and the
  original layout is never lost through a disconnect/reconnect.

### Note content

- Free text, with inline checkboxes: a line that starts with `[ ]` is
  shown as ☐ and, when clicked, turns into `[x]`/☑. The syntax lives
  inside the note's own text field (there is no separate tasks table), but
  rendering a clickable ☐ inside a line is real UI work: the note needs a
  display mode (list of lines, with the checkbox ones as a clickable
  control) distinct from its edit mode (plain text) — it is not "free"
  just because it is stored as text.
- Note color, chosen by the user.
- Creation and last-edit date, stored as metadata (not exposed as an
  organization system, only as data).
- Silent autosave: there is no "Save" button, the text is persisted by
  itself as you type (with a small debounce so as not to write to disk on
  every keystroke). When the application closes (including a Windows
  sign-out) an immediate save of any change pending in the debounce is
  forced, so as not to lose the last keystrokes.
- Archive: hides the note from the active view without deleting it.
- Trash: deletion is neither instant nor definitive; deleted notes move to
  a recoverable "Trash" state. Archive and trash are the same state
  mechanism (`Active` / `Archived` / `Trashed`), not two different
  systems — only the view filter that is applied changes.

### Customization

- Light/dark theme: follows the operating system theme by default, but
  with its own three-state selector in Settings (System / Light / Dark) to
  force one manually if the user prefers.
- Fixed typography in the default theme, chosen for legibility — no
  font/size selector in v1.
- The app respects the Windows accessibility setting "Show animations in
  Windows" (Settings → Accessibility → Visual effects): if it is off,
  transitions (fan, note open/close) are applied instantly instead of
  animated. There is no dedicated "battery saver" switch — the animations
  are too light for a dedicated mechanism to make sense, and the
  accessibility setting already covers anyone who wants fewer effects for
  whatever reason.

The "Vintage Paper mode" (texture, sound, per-note tilt, aging) is
postponed to v1.1 — see that section.

### Storage

- Local, in SQLite. Envelope encryption: a symmetric key is generated for
  the database, and that key (not each note separately) is protected with
  DPAPI bound to the Windows user/machine and stored in the local
  configuration file. No server, no account, no telemetry.
  - Honest limit that must be documented in the app itself before
    publishing it: per-user DPAPI protects against another Windows account
    or against someone without your session signed in, but **not** against
    another process running under your same user session.
  - Encrypting the content blocks being able to do SQL/`LIKE` search over
    the text of the notes in the future (search is already out of scope
    for v1, so it is not solved now — the dependency is only noted for
    when it is taken up again).
- **Export/Import** (to move the notes to another computer or make a
  backup): generates a file of its own (`.aldunebackup`, a zip with JSON
  inside, with a format version field from v1 on so the format can evolve
  without breaking old backups) that contains the notes **unencrypted**.
  This is a deliberate decision: the DPAPI key is bound to the machine and
  cannot travel with the file, so encrypting the export would require
  asking the user for a password — discarded because of the friction.
  Instead, the same pattern Firefox uses to export bookmarks is followed
  (unencrypted JSON by default, encryption only as an optional third-party
  extension): the export file is the user's responsibility while it exists
  (do not leave it in a shared folder, move it by hand).
  - **Import semantics**: merges with what exists, never replaces or
    deletes notes that were already there. If an id in the file matches one
    already existing at the destination, a new id is generated for the
    imported note instead of overwriting. Archived and trashed notes are
    included, not just the active ones (full backup fidelity).
  - The import validates the format (including the version) before
    touching the real database; if it is not valid, it is rejected with a
    clear message without modifying anything that exists. On import, the
    notes are encrypted with the destination machine's key.

### Windows integration

- System tray icon, with a menu: open all notes, settings, quit.
- Configurable global keyboard shortcut to create a new note from
  anywhere.
- Auto-start with Windows (can be enabled/disabled in Settings).
- **Single instance**: on startup, the app checks (with a named mutex)
  whether an instance is already running, and if so, it does not start a
  second one — this avoids a broken keyboard shortcut, duplicate docks and
  two processes writing at once to the same database. Relevant because the
  combination of auto-start + tray + global shortcut makes it easy to
  launch the app twice by accident.

### Explicitly out of v1 (no plan to add soon)

Search across notes, tags/folders, cloud sync, reminders or notes with a
date/notification, free theme editor (arbitrary colors chosen by the
user), password-encrypted export. They are left out on purpose so as not
to turn a simple post-it into a task manager — if real use shows that
any of them is needed, it is added in a later version without the current
design blocking it (except search, which would require revisiting the
content encryption when it is taken up again, as noted in Storage).

### Postponed to v1.1 (decided after the design review, not discarded)

They reduce the risk of the first implementation milestone because they
are orthogonal to the hard mechanism of the app (window/hover/
multi-monitor) and it costs nothing to leave them out of the first build:

- **"Vintage Paper" mode** (optional, off by default when it arrives, with
  a sub-switch to separate visual from sound):
  - Visual: background with an aged paper/kraft texture and a warm
    (sepia) palette, slight random tilt per note (like a post-it stuck on
    by hand), typewriter/handwritten-style typography, torn-paper-looking
    border, pushpin or washi tape icon in the corner of the note.
  - Aging by age: older notes look somewhat more yellowed/worn than
    recent ones, computed from the creation date already stored (it needs
    no new data).
  - Sound: a soft paper sound when opening/closing a note, and a
    "crumpled paper" sound when sending a note to the trash (accompanied
    by a small crumpling animation). The audio files must come from a
    royalty-free library (e.g. freesound.org with a CC0 license) — no
    copyrighted asset is used.
- **Independent edge position per screen**: v1 only offers one global
  position for all active screens; the variant "each screen its own edge,
  with a checkbox to go back to global mode" is added in v1.1 on top of
  the same configuration base.

## Technology stack

**C# + WPF (.NET), Windows-only.**

Rationale (not out of familiarity with the author's other languages, but
on purely technical grounds):

- The real difficulty of this app is not in the business logic, it is in
  getting Windows to let a window behave in a non-standard way:
  borderless, live-resizable between pill and fan, anchored to an edge,
  always-on-top, per-monitor DPI aware.
- WPF renders its own UI through vector composition instead of delegating
  to native Windows controls, and for that reason it has 18 years of
  documented precedent for exactly this kind of "window hack" (hidden
  panels that slide out, notes anchored to an edge). It keeps full access
  to Win32 via P/Invoke when it is necessary to drop down a level (tray
  icon, global shortcut).
- Pure C++/Win32 is discarded: it gives maximum control but at a
  development cost and a risk of memory errors that this app does not
  need, because it has no real performance requirement (it spends 99% of
  its time idle).
- Rust + native windowing is discarded: technically solid and with no
  garbage collector, but the specific window pattern Aldune needs has far
  less precedent in the Rust ecosystem on Windows than in WPF — a higher
  risk of getting stuck solving platform problems for the first time.
- Electron and Tauri are discarded: Electron has an idle RAM cost
  (100-200MB+) that is unacceptable for an app that lives all day at the
  edge of the screen; Tauri solves that problem but the UI is still a
  webview, with the same "odd" window problem as Electron.
- WinUI 3 is discarded in favor of WPF despite being Microsoft's "modern"
  direction: it restricts window customization much more for the sake of
  consistency/security, precisely in the part that makes this app unique,
  with less community precedent for resolving questions.

## Architecture

A single WPF application (.exe), MVVM pattern, divided into layers:

- **Window/shell layer**: one `EdgeDockWindow` instance per active screen
  according to Settings. It manages the strip, the hover detection and the
  fan animation using native WPF events (`MouseEnter`/`MouseLeave`) on the
  anchored window itself, which really changes size between the pill state
  and the expanded state (see Core) — no global mouse hook
  (`WH_MOUSE_LL`) is needed for the hover detection itself, only a
  fallback timer for the collapse.
- **Persistence layer**: SQLite + envelope encryption via DPAPI,
  trash/archive, serialization to/from the versioned portable backup
  format.
- **Windows integration layer**: tray icon (interop, WPF has no native
  tray control), global shortcut (`RegisterHotKey`), auto-start (Registry
  entry or shortcut in the Startup folder), single-instance mutex.
- **Settings**: local configuration file separate from the notes database
  (active screens, global edge position, keyboard shortcut, auto-start,
  DPAPI-protected database key).

### Components

1. `EdgeDockWindow` — pill + fan per monitor.
2. `NoteWindow` — expanded note (text, checkboxes, color,
   archive/delete/restore).
3. `NotesRepository` — SQLite access, encryption, trash/archive logic.
4. `ImportExportService` — generates/reads the versioned portable backup
   file, with the merge semantics described in Storage.
5. `TrayIconService` — tray icon + menu.
6. `HotkeyService` — registration of the global shortcut.
7. `SettingsService` — reads/writes the settings file.
8. `MonitorService` — detects connected/disconnected screens (by stable
   device id) and DPI changes, positions each `EdgeDockWindow`.

Each component has a single responsibility and is understandable/
testable without knowing the others — `NotesRepository` knows nothing
about windows, `EdgeDockWindow` knows nothing about SQLite.

## Data model

`Note` table:

- `Id`
- `Text` (free content; checkboxes live as `[ ]`/`[x]` text inside this
  field, rendered as clickable controls in display mode)
- `Color`
- `ScreenOrigin` (stable device id of the origin screen; it is not
  rewritten by a temporary monitor disconnection)
- `CreatedAt`
- `UpdatedAt`
- `State` (`Active` | `Archived` | `Trashed`)

There are no tables for tags, folders or reminders — they were left out
of scope.

## Error handling

- **Corrupt/unreadable database at startup**: the damaged file is copied
  before touching anything, and creating a new empty database is offered
  instead of just failing.
- **DPAPI key unavailable**: covers two different causes and they must be
  distinguished in the message. (a) Damaged Windows profile — may be
  recoverable. (b) The Windows account password was reset by an
  administrator — this **permanently destroys** the DPAPI key and with it
  access to all the encrypted notes; there is no possible recovery except
  restoring from a previously exported `.aldunebackup`. The message must
  say this explicitly, because it is the most likely cause in practice and
  the only one with no technical solution.
- **Import of a corrupt `.aldunebackup` or of an unrecognized format
  version**: the format (including the version) is validated before
  touching the real database; if it fails, it is rejected with a clear
  message without modifying anything that exists.
- **Global keyboard shortcut already used by another app**: the
  registration failure is detected and shown in Settings, instead of
  failing silently.
- **Monitor disconnected with its dock open**: the notes of that screen
  are temporarily shown on the primary screen without losing their real
  `ScreenOrigin` (see Core).
- **Second instance launched**: it is detected via the single-instance
  mutex and a second copy is not opened (optionally, the instance already
  running could be focused/shown — a detail to decide in the
  implementation plan, not blocking for the spec).

## Testing

- `NotesRepository`, `ImportExportService` and `SettingsService` are
  testable in isolation (without bringing up windows): unit tests on CRUD,
  trash/archive, encryption/decryption, and the export→import cycle
  (including the merge with conflicting ids).
- The windows/hover/multi-monitor part depends on the Windows compositor
  and is the hardest to automate; it is validated with a list of targeted
  manual scenarios: single monitor, two monitors with different DPI,
  connecting/disconnecting a monitor with the dock open, edge coinciding
  with the taskbar, launching a second instance.

## Suggested implementation order

To minimize risk in the first milestone, given that the author is new to
C#/WPF/Win32:

1. Window mechanics on a single monitor (pill, hover, expansion, open
   note) — the technically most uncertain core, isolated first.
2. Notes CRUD + storage (SQLite, encryption, trash/archive).
3. Multi-monitor + DPI.
4. Tray, global shortcut, auto-start, single instance.
5. Import/export.

The Vintage Paper mode and the per-screen edge position are left out of
this order — they are v1.1, after v1 works end to end.

## Name

**Aldune** (fan + note) — no collisions found in searches of existing
apps/software at the time of writing this spec. "EdgeNotes" (an existing
screen-edge notes app in the Microsoft Store, same concept), "Perch"
(taken by Perch AI) and "Margin" (used by several established notes apps)
were discarded. Any name that includes "Post-it", a 3M registered
trademark, is avoided.

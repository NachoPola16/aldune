# Settings: Windows and Android in step

They are the same app on two platforms, so a setting that exists on both **has the same name in the interface, the same values and the same default**. This table
lists which settings are common, which belong to one platform only, and the differences that were left on purpose. It is reviewed whenever a setting is added.

State as of 2026-10-30. Windows: `src/Aldune.Core/AppSettings.cs`. Android: `AppSettings` in `android/app/.../data/SettingsStore.kt`.

## Settings tabs

Same order and same names for what is common; what is specific to each platform goes in a fixed place (between Colors and Sync).

| Tab | Windows | Android | What lives there (common) |
|---|---|---|---|
| General | General | General | Language, simplified mode, notice of new versions. Windows adds start with Windows and global shortcuts; Android adds the version. |
| Look | Appearance | Look | Look and its colors, square corners, sync signal, note color as a stripe only, same color on all notes. Windows adds text size. |
| Notes | Notes | Notes | Large checkboxes, move done to the end, hide done, trash, sounds. Windows: remember position and size, reminder with tasks; Android: list order and keyboard on open. |
| Colors | Colors | Colors | Color theme, tone of new notes, color rule and “Apply to existing notes”. Windows adds custom themes. |
| (own) | Dock and screens | Alerts | Windows: edge, alignment, monitor, gestures. Android: quick capture and exact reminders. |
| Sync | Sync | Sync | Sync profiles and automatic copy to a folder. Android adds the local .zip copy. |
| Help / About | Help, About | (in General) | Only Windows has pages of its own. |

On Android the tabs are chips in a 3-column grid (with more tabs, more rows), and the sync one is called “Sync” in every language so that it fits in a third of the screen; before it was a row that scrolled: with six tabs the last one was left off screen without anything saying so. Simplified mode hides Notes, Colors and Sync on both platforms (Windows also hides the advanced parts of the dock).

## Pinned notes (2026-10-31)

Not a setting: a per-note property that syncs (`pinnedToTop`, optional field of the encrypted payload; spec `specs/2026-10-31-pinned-notes-design.md`). Same meaning on both platforms: pinned notes come first in every list, each

**Per-note “done tasks” (`completedTasks`)**: a per-note property that syncs (see SYNC.md): follow this device's settings / keep in place / move to the bottom; the last two never delete or hide. Windows: one cycling menu item in the note menu and in the dock tab menu; Android: the editor's three-dots menu and the note options. **Version history**: local on both (20 per note, at least 5 minutes apart plus one on close, encrypted, none for protected notes, restoring saves the current text first); Windows “Version history…” in the note menu, Android in the note options. **Tags (Android only, local)**: an All chip, an all-tags dialog with search and manual order, and “Show # before tags”. **View label above the dock’s notes (Windows only, `DockViewLabel`)**: fixed while a tag, archive or trash is shown (default) / always fixed / only when the view changes; fixed, a click on it opens the view menu. **Wheel changes dock view (Windows only)**: over the gear always; over the tabs only when they all fit; Ctrl+wheel always; the cycle is Active → each tag with notes → Archived → Trash → All.
group keeps the chosen order. Differences kept on purpose: Android offers it in the long-press sheet, the options sheet and the editor's three-dots menu and marks the row with a pin; Windows in the manager row, the dock tab marker and menu, and the note's menu.
It is **not** Windows' "always on top of other windows" (`NoteUnpinned`, local, never synced).

## Common (same interface text)

| What | Windows | Android | Note |
|---|---|---|---|
| Language | `Language` | `language` | The same eight languages. |
| Look and look colors | `Appearance`, `AspectColors` | `appearance`, `aspectColors` | Same numbers and same color slots per look. |
| Simplified mode | `SimplifiedMode` | `simplifiedMode` | |
| Large checkboxes | `LargeCheckboxes` | `largeCheckboxes` | Factory default: off on Windows, on only in new Android installs (touch screen). |
| Move done to the end | `MoveCompletedTasksToEnd` | `reorderOnCheck` | |
| Hide done tasks | `AutoHideCompletedTasks` + value and unit | `hideDoneTasks` + hours | Windows: minutes, hours, days or weeks with a number. Android: 1 hour, 1 day or 1 week. |
| Days in the trash | `TrashRetentionDays` | `trashDays` | Windows: free number. Android: 7, 14, 30, 60 or 90. |
| Sync signal | `ShowSyncSignal` | `showSyncSignal` | Same text; by default the look decides. |
| Square corners | `SquareCorners` | `squareCorners` | Same. |
| **Note color as a stripe only** | `NoteColorAsStripe` | `fillNoteColor` (inverted, “background” is what is stored) | **Same text and same direction of the switch** in the interface; locked with the same notice in the terminal looks (Telecom, Bash, Phosphor). Since 2026-10-30. |
| Same color on all notes | `UniformNoteColor` | `uniformNoteColor` | |
| Theme, tone and color rule of new notes | `ActiveThemeId`, `NewNoteTone`, `ColorAssignment`, `FixedNoteColor` | `noteThemeId`, `darkNotes`, `colorRule`, `fixedNoteColor` | Android adds “Apply to existing notes”, like Windows. |
| List order | `NotesManagerOrder` | `listOrder` | |
| Reminder with tasks | `ReminderIncludesTasks` | `reminderIncludesTasks` | |
| Sync in the background | `SyncAutomatically` | `syncInBackground` | |
| Notice of new versions | `CheckForUpdatesAutomatically` | `checkUpdates` | Once a day, can be turned off, only opens the release page. |
| Preview in dock or widget | `ShowNotePreview` | `widgetPreview` | |
| Own tag order | `TagOrder` | `tagOrder` | Per device, never synced (the sync format stays 4); alphabetical by default, new tags go last. Windows: arrows in the manager's «Manage tags»; Android: arrows in the all-tags sheet. Not in the exported config file. |

## Windows only

Global shortcuts (including the fixed Ctrl+Alt+H, which hides the dock, and Ctrl+Alt+D, which puts the keyboard on it: Up/Down between tabs, Enter opens, Esc gives it back), dock (edge, alignment, monitor, follow the mouse, hide in full screen, keep open, trackpad gestures), text size
(Android follows the system's), remember note position and size, default note layout, several sync profiles with their folder/server/WebDAV,
custom themes (`CustomThemes`), start with Windows.

## Android only

Keyboard when opening a note, shape of the “+” button (app and widgets), quick capture notification and tile, quick list, exact alarms, notice about a missing
backup. These are things that do not exist on a desktop computer.

## Differences kept on purpose

- **Hide tasks and trash**: Windows accepts any value because it is typed; on the phone fixed options are offered so as not to ask for typing numbers. If
  one day it is wanted equal, the stored model already accepts the free value on Android (`trashDays` between 1 and 365; `hideDoneAfterHours` up to a year).
- **Phosphor**: Windows shows no note color at all (monochrome monitor); Android keeps the note's color stripe.

## Sounds (phase 3)

Same switch (`SoundsEnabled` / `soundsEnabled`), same volume (`SoundVolume` 0-100), same event keys (`reminder`, `syncDone`, `syncFailed`,
`noteCreated`, `noteArchived`, `noteOpened`, `noteClosed`, `taskChecked`, `noteDeleted`, `typing`, `textDeleted`) and same option numbers (7-12 = paper and pen; 12 = eraser).
The WAVs come from `scripts/sounds/generar-sonidos.py`: no harsh brightness, no clicks and leveled by loudness; typing and deleting text are about 8 dB lower.

**Suggested sounds.** When the switch is turned on for the first time, if nothing is chosen for the note events, the four that are
heard now and then are assigned (open = page turn, close = paper folding, tick = pen tap, delete note = paper crumpling). Typing and deleting
text sound on every keypress and are turned on by hand. The “Use suggested sounds” button applies them again whenever wanted.

- **Android does not offer “Reminder”**: reminders already sound through their system notification. “Sync done” sounds only if the user asked for the
  sync (by hand or by pulling the list), as on Windows; “sync failed” always sounds. “New note” sounds when it is saved for the first time.
- **Android only has the five own sounds**: Windows' system sounds (1-5) and the own `.wav` (6) do not exist there and stay silent.
- Android does not play with the phone on silent or vibrate, nor with “Do not disturb” if it does not let system sounds through; “Test” says so with a notice instead of staying mute. Windows has no such mode.
- Windows' volume does not affect system sounds or an own `.wav` (they are played by `SoundPlayer`); Android's affects all of them.

## Automatic copy to a folder (phase 4)

Same names, same manifest format (`.aldune-export.json`), same rules in both apps (`AutoExportPlanner` in Core and its port in
`android/domain`, with the same test cases): one `.md` per note in `notas/` (and `archivadas/` with the checkbox), a fixed name from the first time,
protected notes and trash left out, what leaves is set aside in `_eliminadas/` and **nothing is ever deleted**; a file changed by hand or foreign is not overwritten (the copy
goes next to it as `name (Aldune).md`). The files are in the clear: the setting says so.

- **When it writes:** Windows, a few seconds after changes calm down and on close; Android, 15 s after the last change (WorkManager, each
  change restarts the wait) and once a day as a safety net.
- **Folder:** Windows, a folder picker; Android, the system picker with persistent permission. If on Android the permission or the folder is lost, the
  copy turns off and a notice appears in the list and in Settings (there are no retries); on Windows a failure is noted and retried, and Settings shows it.
- The folder belongs to each machine: it does not travel in export/import of settings.

## Telecom look: signals and grid (decisions)

- **There is no option to hide the “signals”** (the `CHn` prefix, the channel wave): they are the look's identity, and whoever does not want them has other
  light and dark looks. The switches for sync signal, square corners and note color as a stripe only already exist.
- **Width of Settings and of the manager on Windows (decision 2026-10-30):** with the window maximized, the Settings content stays at 780 px next to the side menu (the switches do not stick to the right edge and the text lines stay readable) and the manager body at 1100 px centered, with no side menu. It is kept: it is usual in Windows 11 settings and avoids eyes traveling 1900 px. If someone misses it, raise the `MaxWidth` in `SettingsWindow.xaml` or `NotesManagerWindow.xaml`.
- **Android widget**: in Telecom it carries the monospace typography and the accent, but **not** the `CHn` prefix or the wave (Glance does not draw paths; the color strips already identify the look).
- **Dock tab**: the wave shares a row with the preview (40×12, thin stroke) and only the preview gives up that room; the title keeps its full width.
- **Note grid**: a margin without grid on the left, right and bottom, and the text enters 6 px inside the first square. Bash and Phosphor have no grid.
- **Graph-paper notebook** (looks 15 and 16, light and dark, on both platforms): a sheet of squares of the line height with a red margin, the usual typeface (the other notebooks use serif) and the note color as a stripe, locked as in the other notebooks. In list rows it only carries the margin (nothing crosses the text).
- **Dock buttons**: shorter, lighter shadow (depth 1, blur 8); in the bevel looks there is no drop shadow.
- **Text margins (2026-10-30)**: the gap between the grid edge and the text is the same on the left and on the right (22/21 px on Windows; 16 dp left and 12 dp right on Android, because wrapping a line leaves up to a letter of slack on the right).

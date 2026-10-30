# Decisions of 2026-10-27

Taken with the user before the last release of the batch (Windows 1.7.x and Android 0.1.0). Complements `DECISIONS-2026-10-26.md`.

## Simplified mode on Android: done

On Windows it is a switch in the Settings window: it hides the Notes and Sync pages and the advanced parts of the dock and of the
colors, and the hidden values stay as they are. On Android it does the same on the Settings screen: a button at the end
(“Use simplified mode” / “Use full mode”) that applies instantly, touches no other setting and is never hidden.

| Settings block | Simplified |
|---|---|
| Sync (with “Scan QR”) | shown |
| Look (the list of looks) | shown |
| Language | shown |
| Version | shown |
| Quick capture (notification, quick list, widget, tile) | hidden |
| Reminders (exact time, tasks in the notice) | hidden |
| Advanced look colors and “note look” (uniform color, corners, sync signal) | hidden |
| Notes and tasks (note theme, trash, order, checkboxes, hide done) | hidden |

The rule lives in `SettingsSections` (`android/domain`, with a test) and the screen only queries it. **Custom themes** stay off the phone:
they are only edited on Windows.

## Default settings

Criterion: a default has to work for someone who touches nothing, not hide a feature or spend permissions or battery without being
asked. What was reviewed and decided:

**Android**

| Setting | Default | Decision |
|---|---|---|
| Look | Follow the system | same |
| Large task checkboxes | **on in new installs** | changes: on a touch phone it is the comfortable choice. Whoever already had the app keeps what they saw (off) |
| Move done tasks to the end | off | same (as Windows) |
| Hide done tasks | off | same (do not hide text without being asked) |
| Trash | 30 days | same (as Windows) |
| Capture notification, enlarged widget, exact reminder time | off | same: they ask for permissions or use battery |
| Background sync | on | same (it only acts if a profile is connected) |
| Simplified mode | off | same |

**Windows**: all were reviewed and left as they are: global shortcuts and trackpad gestures on, hide the dock in full screen,
remember note position and size, update notices, text preview, tasks in the reminder; off are sounds, large
checkboxes (it is a mouse), moving and hiding done tasks, and simplified mode.

**Windows Settings window**: it keeps its structure (General, Notes and tasks, Dock and screens, Colors, Sync,
Help, About). One single change: “Show pending tasks in the reminder” moves from Dock to “Notes and tasks”, which is where people
look for it. The Sync page is dense (six buttons in a row) but each button is a different action and grouping them gains no
clarity; it stays.

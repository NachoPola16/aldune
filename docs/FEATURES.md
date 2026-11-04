# What every setting and feature does

A guide to Aldune as it behaves today. Interface names are quoted as they appear in English. The app runs on Windows and Android; unless a section says otherwise it describes Windows, and [Android differences](#android-differences) lists what changes on the phone. For the longer stories see [SYNC.md](SYNC.md), [PROTECTED_NOTES.md](PROTECTED_NOTES.md) and [MIGRATION.md](MIGRATION.md); for which settings both platforms share, [SETTINGS_PARITY.md](SETTINGS_PARITY.md).

Contents: [The dock](#the-dock) · [The view wheel](#the-view-wheel) · [Notes](#notes) · [Manage notes](#manage-notes) · [Settings pages](#settings-pages) · [Quick find](#quick-find) · [Keyboard shortcuts](#keyboard-shortcuts) · [Sync](#sync) · [Import and export](#import-and-export) · [Android differences](#android-differences)

## The dock

The dock is a strip of tabs on one edge of the screen. Move the pointer to that edge and the tabs fan out; each tab is a note. The tabs show the title and, optionally, the first line of the text.

| You do | What happens |
|---|---|
| Click a tab | Opens that note in its own window. |
| Drag a tab | Moves the note; it remembers the spot (see “Remember note positions”). |
| Right-click a tab | Menu without opening the note: **Open**, colours (and **Choose another color…**), **Protect with password…**, **Visible name…**, the done-tasks setting, **Archive**, **Tags…**, **Move to trash**; for a linked file also **Open in its editor**, **Show in File Explorer**, **Copy file path**. |
| Wheel over the Manage notes button | Changes the dock view (see below). |
| Esc while dragging | Cancels the drag. |

Buttons on the dock's bar:

| Button | Click | Right-click |
|---|---|---|
| Open all notes | Opens every note (closes them all if they are already open). | **Opening mode** (Grid, Columns, Cascade by the dock), **Open notes on** (which screen), **Close all notes**. |
| Manage notes (the cogwheel) | Opens [Manage notes](#manage-notes). | The view menu: **Show in dock** (Active notes, All notes, Archived notes, Trash, By tag) and the options for when the view name is shown. |
| Views (a tag icon; off by default, see “Show a button for the views in the dock”) | The list of views. | – (the wheel over it changes view) |
| Sync now | Syncs now. | Opens Settings on the Sync page. |
| New note (+) | Creates a note. | **New note**, **New note from clipboard**, **Open file…** (a linked file, see below) and **Import Markdown as a note…**. |

If the tabs do not fit, small arrows scroll them; with the dock open, Up/Down, PageUp/PageDown, Home and End scroll too. Notes **pinned to the top of the list** carry a marker and come first.

The tray icon offers **New note**, **Manage notes…**, **Settings…**, **Hide the dock** / **Show the dock**, **Check for updates…** and **Exit**.

The name of the current view can float above the notes (see “View name above the notes” in Settings); when it is fixed on screen, clicking it opens the view menu.

## The view wheel

The dock shows one **view** at a time: **Active notes**, one view per **tag**, **Archived notes**, **Trash**, or **All notes**. Turn the mouse wheel to go round them. Order of the cycle: Active → each tag (that has notes) → Archived → Trash → All → Active. A view with nothing in it is skipped, and **All notes** only stops if there is something archived or in the trash (otherwise it would equal Active).

Where the wheel works (Settings → Dock and screens → “Change view with the mouse wheel”, on by default):

- Over the Manage notes button and the Views button: always.
- Over the tabs: only when every note fits on screen (otherwise the wheel scrolls the tabs).
- With **Ctrl** held: always, anywhere on the dock.

Trackpad scrolling is read as continuous gestures if “Trackpad gestures” is on and a precision trackpad is detected.

**Taking things off the wheel.** Under “The wheel stops at”, one switch each for **Active notes**, **Archived notes**, **Trash**, **All notes** and **Tags**. Switched off, the wheel passes over that view. With **Tags** on, a list shows one switch per tag, so you can skip single tags. A view taken off the wheel is not gone: it stays in the view menu (right-click the Manage notes button) and you can still pick it by hand. If you take away the view the dock is showing, the dock moves on to the next stop immediately. If every stop is off, the wheel goes back to Active. These choices are local to the device.

## Notes

A note is a window of its own. The first line is the title; the rest is the body. Press **Esc** to close it; what you wrote is saved as you type.

### The note menu (the ⋯ button)

| Item | What it does |
|---|---|
| Convert to task / Convert to list | Turns the line into a checkbox or a bullet. |
| Uncheck all / Remove checked tasks | For reusable lists such as the shopping list. |
| Reminder | Notifies you at a set time: presets **In 1 hour**, **Tonight**, **Tomorrow 9:00**, or pick a date and time on the calendar; **Save**, **Remove reminder**. With “Show pending tasks in the reminder” the notification lists the first tasks still to do. |
| Colour swatches, **Choose another color…** | The colours of your note theme (and a custom colour). |
| Always on top | Keeps this note in front of other windows (local to this device, never synced). |
| Pin to the top of the list | Pinned notes come first in the dock and in Manage notes. It syncs. |
| Version history… | Up to 20 earlier versions kept on this device (at least 5 minutes apart, plus one on close); restoring first saves the current text. Not kept for protected notes. |
| Done tasks | Per note: follow this device's settings / keep in place / move to the bottom. The last two never delete or hide anything. It syncs. |
| Protect with password… / Remove protection | See [Protected notes](#protected-notes). |
| Visible name… | The name a locked note shows. |
| Tags… | Add or remove tags. |
| (linked notes only) Sync this note, Open in its editor, Show in File Explorer, Copy file path, Convert to normal note | See [Linked files](#linked-files). |
| Export to Markdown | Saves the note as a `.md` file. |
| Return next to the dock, Restore size | Put the window back at the default place or size. |
| Archive, Restore, Move to trash | State of the note. |

### Colours and themes

Every note has a colour taken from the active **note theme** (Settings → Note colors), never a loose colour. The theme sets the colours for new notes and the quick colours in each note's menu. A custom colour can be picked from any note. The look of the app (Settings → Appearance) decides how the colour is shown (a full card or just a side stripe), and some looks (Telecom, Bash, Phosphor) fix that themselves.

### Checklists

Write `Ctrl+L` on a line to make it a task (☐); click the box or press `Ctrl+Enter` to check it off. Enter on a task line starts the next task. `Ctrl+Shift+L` makes a bullet; Tab and Shift+Tab indent and outdent a line, for subtasks. Pasting a Markdown list (`- [ ]`) brings its checkboxes. Settings → Notes can move checked tasks to the end, delete completed tasks after a delay, and make boxes larger.

### Pinned, tags, reminders

- **Pinned** (to the top of the list) is a note property that syncs. It is not the same as **Always on top**, which is a local window setting.
- **Tags** are text labels. Create, rename and delete them from the tag button in Manage notes (“Manage tags”: deleting a tag can also send its notes to the trash). Notes can be filtered by tag in Manage notes, and each tag with notes is a view in the dock.
- **Reminders** are per note; see the menu above.

### Linked files

**Open file…** (right-click the New note button) turns a Markdown or text file on disk into a note that is linked to it. Aldune never deletes a linked file and never writes over one whose content changed behind its back. From the note menu you can open it in its own editor, show it in File Explorer, copy its path, **Convert to normal note**, and **Sync this note**. **Save as linked file…** goes the other way for a normal note.

### Protected notes

A password protects a note with AES-GCM. While locked, the dock and Manage notes show only “Protected note” unless you gave it a **visible name** (optional, in the clear, one line up to 60 characters). The password is asked when you open the note and it locks again when you close it. The password cannot be recovered. Protected notes are left out of exports and of the automatic copy. See [PROTECTED_NOTES.md](PROTECTED_NOTES.md).

## Manage notes

Open it from the tray icon, the dock, or the optional shortcut (see below). A window with three filters, **Active**, **Archived**, **Trash**, and a list of rows.

| Area | What it does |
|---|---|
| Search | “Search your notes…” (Ctrl+F focuses it and selects the current search). Forgives accents and capitals. |
| Tag filter | “Filter by tag”. |
| Sort | **Dock order**, **Recent first**, **Oldest first**, **Title (A–Z)**. The choice is remembered. |
| New note | The + button. |
| Manage tags | The tag button: create (**Create**), list and delete tags; **All tags**. |
| **Select all** | Ticks every row in view. |
| Row buttons | Pin and tags on each row. |
| Footer | **+ Tags** (add tags to the selection), **Delete** (Trash view only, asks for confirmation), **Import**, **Export**, **Archive** / **Restore**, **Trash**, **Reset layout** (“Restore size and position of all notes”). |

**Mouse.** Click a row to open the note. The checkbox selects. **Shift+click** selects the range from the last marked row; **Ctrl+Shift+click** adds it to the selection; Ctrl+click toggles. Once anything is selected, a click toggles the row instead of opening it. **Right-click** a row for **Open**, **Export**, **Archive** / **Restore**, **Trash** / **Delete**; on a selected row it acts on the whole selection, on any other row only on that row.

**Keyboard.** The list is a single Tab stop.

| Key | Does |
|---|---|
| Up / Down | Move to the row above / below. |
| Enter | Open the note. |
| Space | Select or unselect the row (Shift+Space selects the range). |
| **P** | Pin or unpin the focused row. |
| **T** | Edit the tags of the focused row. |
| Ctrl+F | Focus search. |
| Esc | Closes the open tag panel, then clears the search, then closes the window. |
| Tab | Moves between the controls of the window and wraps inside it instead of leaving it. |

**Export** writes the selected notes (or everything in the current view if nothing is selected) as Markdown, to a `.zip` or to a folder. Protected notes are left out and the dialog says how many.

## Settings pages

Open Settings from the tray icon or the dock. The left list has nine pages; the search box above it (Ctrl+F) finds an option by name, explanation or page and tolerates typos. Esc clears the search first; Enter opens the first result.

### General

| Setting | What it does |
|---|---|
| Interface mode | Use simplified mode / full mode. The simplified mode keeps the essentials and hides advanced options; nothing is changed. |
| Open Aldune at sign-in | Starts the app with Windows. |
| Create a note with a keyboard shortcut | Global shortcut for a new note (default Ctrl+Shift+N; on by default; **Reset** restores the default). |
| Open the last edited note with a keyboard shortcut | Default Ctrl+Alt+L; on by default. |
| Open the notes list with a keyboard shortcut | Default Ctrl+Alt+M; **off** by default. |
| Find a note from any program with a keyboard shortcut | Default Ctrl+Alt+F; **off** by default. Opens the [quick-find palette](#quick-find) over whatever program is in front. |
| Language | English, Spanish, German, French, Italian, Portuguese (Brazil), Japanese, Simplified Chinese. A restart applies it to every window (**Restart now**). |
| Check for new versions automatically | Once a day asks GitHub for the list of releases and only opens the release page; nothing about you is sent. |

### Appearance

| Setting | What it does |
|---|---|
| Look of the app | Dark, Light, Pastel, Midnight, Aldune (plum), Same as Windows, Windows XP light, Windows 98 dark, Telecom Light, Telecom Dark, Bash, Phosphor, and the travel / field / grid notebooks (light and dark). Each look has colours you can adjust. |
| Text size | Scales the text of the whole app and of the notes. |
| Square corners on notes, dock and windows | Flat corners. |
| Note color as a side strip (not the whole note) | Colours only a stripe. Locked in looks that decide it themselves. |
| Plain background in open notes | Dock and Manage notes keep the full colour; the open note uses the look's own page. Only for looks with a full-colour card. |

### Notes

| Setting | What it does |
|---|---|
| Remember note positions / sizes | A note reopens where and as big as you left it. Off: next to the dock, standard size growing with the text. |
| Restore size and position of all notes | Resets every note, including open ones; text untouched. |
| Larger task checkboxes | Bigger boxes and click area; lines are roomier. |
| Move checked tasks to the end of the list | Pending tasks stay together on top. |
| Delete completed tasks automatically, **After:** | A checked task stays for the delay you choose (minutes, hours, days or weeks; default 1 day) and then its line is removed from the note. |
| Show pending tasks in the reminder | The reminder notification lists the first tasks left. On by default. |
| Show sync status on each note | A small signal under each note's title; the look decides the default. |
| Same color for every note, **Choose color…** | Display only: each note keeps its own colour. |
| Trash | A trashed note is deleted for good after this many days (default 30). |

### Sounds

“Play sounds” (off by default) and a **Volume** slider. Each event has its own sound or none, with a **Test** button: Reminder, Sync finished, Sync failed, New note, Note archived, Note opened, Note closed, Task checked, Note deleted, Typing, Deleting text. You can choose a built-in sound, a system sound or your own `.wav` (up to 5 MB). **Use the suggested sounds** applies paper-and-pen sounds to the occasional events (open, close, check, delete).

### Note colors

| Setting | What it does |
|---|---|
| Color theme | Pick the theme; **New**, **Duplicate**, **Edit**, **Delete** manage your own themes. |
| New notes are | Light, Dark, or Both, alternating. |
| Color of new notes | Rotate skipping the neighbours' colours, Rotate through the theme, The most different from the rest, or Always the same color. |
| Apply to existing notes… | Recolours your notes to the theme, tone and rule you chose. |

### Dock and screens

| Setting | What it does |
|---|---|
| Screens where Aldune shows | On every connected screen, on the screen with the mouse, or one chosen screen. |
| Position along the edge | Start, centre or end of the edge (named Top/Bottom on a side edge, Left/Right on a top or bottom edge). |
| Screen edge | Right, Left, Top, Bottom. |
| View name above the notes | Fixed while a tag, archive or trash is shown (default) / Always fixed / Only when I change the view / Never show the view name. |
| Shadow on the dock cards | Soft shadows between cards (on by default). |
| Show a button for the views in the dock | The extra round button for the view list. |
| Show a preview of the text | A second line with the start of each note, in the dock and Manage notes (on by default). |
| Hide dock in fullscreen | Hides over real fullscreen windows (on by default). |
| Keep dock open | Keeps it expanded. |
| Trackpad gestures | Continuous scrolling with a precision trackpad (on by default; unavailable without one). |
| Change view with the mouse wheel, The wheel stops at | See [The view wheel](#the-view-wheel). |

### Sync

See [Sync](#sync).

### Help and About

**Help** is a short guide to the dock, notes, lists, sync and keyboard use. **About** has the version, **Check for updates…**, **What's new**, **Restart Aldune**, **Exit Aldune**, and **Configuration**: **Export settings…** / **Import settings…** save or load your look and settings to a file (sync settings, keys, passwords, the chosen screen and note positions are never included).

## Quick find

The optional global shortcut (Settings > General, default Ctrl+Alt+F, off until you turn it on) opens a small borderless window in the middle of the screen where the pointer is, over any program. Pressing the shortcut again closes it.

| You do | What happens |
|---|---|
| Type | The list of notes narrows as you type. It forgives case, accents and a typo or two. Notes whose **title** matches come first, then notes whose text or tags match (the line that matched is shown); within each group the most recently edited note is first. With nothing typed it lists the most recent notes. Only active notes are listed. |
| Up / Down (Page Up / Page Down) | Moves the selection. |
| Enter, or a click on a row | Opens the note (or brings its window to the front) and closes the palette. |
| Ctrl+Enter | Copies the note's whole text to the clipboard, says so in a toast and closes the palette. |
| Esc, a click anywhere else, or the shortcut again | Closes it. |

Each row shows the title, one line of the text and the tags. A **protected** note shows only its visible name (or “Protected note”) and a padlock: its text and tags are neither shown nor searched. Enter asks for the password as usual; Ctrl+Enter never copies a protected note and says so in the footer of the palette instead.

## Keyboard shortcuts

**Global (work from any application):**

| Shortcut | Does | Configurable |
|---|---|---|
| Ctrl+Shift+N | New note | Yes (General) |
| Ctrl+Alt+L | Open the last edited note | Yes |
| Ctrl+Alt+M | Open Manage notes (off by default) | Yes |
| Ctrl+Alt+F | Quick find: a search box over any program (off by default) | Yes |
| Ctrl+Alt+H | Hide / show the dock (also in the tray menu) | No |
| Ctrl+Alt+D | Put the keyboard on the dock: Up/Down between tabs, Home/End, Enter opens the note, Tab reaches the buttons, Esc gives the keyboard back | No |

**In a note:**

| Shortcut | Does |
|---|---|
| Esc | Closes the note (closes the find bar first, if open). |
| Ctrl+L | Make the line a task. |
| Ctrl+Shift+L | Make the line a bullet. |
| Ctrl+Enter | Check / uncheck the task on the cursor's line. |
| Tab / Shift+Tab | Indent / outdent the line. |
| Alt+Up / Alt+Down | Move the line up / down. |
| Ctrl+F | Find in the note (Enter next, Shift+Enter previous, Esc closes). |
| Ctrl+mouse wheel, Ctrl+plus, Ctrl+minus, Ctrl+0 | Zoom this note only; Ctrl+0 resets. |
| Enter or Down in the title | Jump to the body; Up from the first body line goes back to the title. |
| Middle-click on a long list | Autoscroll; another click stops it. |

**In Manage notes:** see [Manage notes](#manage-notes). **In Settings:** Ctrl+F searches. Colour choosers use the arrow keys and Space; Tab shows a focus ring.

## Sync

Notes are encrypted before they leave the device; the store only sees ids, dates and encrypted blocks. Three kinds of store: **Shared folder / NAS** (Windows only), **Self-hosted server** (Docker) and **WebDAV / Nextcloud**. Full set-up steps, server deployment, HTTPS and the security model are in [SYNC.md](SYNC.md). The settings page works like this:

| Setting | What it does |
|---|---|
| Sync profile, **New**, **Delete**, **Name:** | Several profiles can exist at once, each with its own store, key and scope. |
| Sync this profile | Turn on in every profile you want synced; they all sync together. |
| Scope | **All notes**, **Selected notes** (with **Choose notes…**) or **Notes with a tag**. The selection stays on this device. |
| Store | **Shared folder / NAS** (**Browse…**), **Self-hosted server** (Server URL, Access token), **WebDAV / Nextcloud** (URL, Username, Password; prefer an app password). A warning appears for `http://` outside your local network. |
| Sync the look with this profile | Look and colours follow you across devices on this profile; language, text size, shortcuts and the dock stay per device. |
| **I have an invitation…** | Join a shared server with an invitation from its owner (you get your own private space). |
| **Invite someone…** | Create a one-use invitation on your server (asks for the admin token each time). |
| **People on this server…** | List the spaces made from invitations and revoke one. |
| Device sync code, **Generate / copy**, **Share profile**, **Show QR**, **Import code** | The code carries the encryption key (treat it like a password) but never the access token. **Show QR** displays it for 2 minutes for the phone to scan. |
| **Revoke old codes** | Replaces the profile's key; devices with an old code need a new invitation. |
| **Sync now** | Also in the dock. |
| Sync periodically, **Every minutes** | Background checks while Aldune runs (1–1440 minutes, default 15; with several profiles, the shortest interval wins). Off by default. |
| **Review conflicts** | Restore the losing version of a note edited in two places, or dismiss the record. |
| Automatic copy of my notes in a folder, **Choose folder…**, **Include archived notes** | Off by default. One Markdown file per note, written shortly after changes and on exit; never deletes (removed notes move to `_eliminadas`), never overwrites a file you edited by hand, leaves protected notes out. These files are **not** encrypted. |

## Import and export

- **Import notes** from Sticky Notes, Google Keep, Evernote or Markdown/text files: Manage notes → **Import**. Full guide in [MIGRATION.md](MIGRATION.md).
- **Export** one note: note menu → **Export to Markdown**. Several: Manage notes → **Export** (to a `.zip` or a folder).
- **Import Markdown as a note…** and **Open file…** (linked file): right-click the dock's New note button.
- **Automatic copy to a folder**: Settings → Sync (above).
- **Export / import settings**: Settings → About → Configuration.
- On Android: Settings → Sync tab → **Export all notes** (a `.zip` with one Markdown file per note, active and archived) and **Import notes…**.

## Android differences

Same app, same note format, same sync. What changes:

- **Settings tabs:** General, Appearance, Notes, Colors, Alerts, Sync, as chips in a grid (an option to make them compact icons or put them at the bottom). Search is the magnifier next to the title. Sounds live in Alerts; the simplified mode hides Notes, Colors and Sync.
- **No dock, no global shortcuts, no Manage notes window.** The main screen is the list with tabs **Notes**, **Archived** and **Trash**, search, and a + button. A long press on a note opens its options. Tags have an **All** chip, an all-tags sheet with search and manual order, and optionally group “All notes” by tag.
- **Home-screen widgets:** note deck (small 2×2, large 4×4) and a one-tap New note widget; the deck can be limited to a tag and can tick tasks.
- **Quick capture:** a silent notification with a button to start a note, a Quick Settings tile, a **Quick list** you can add to by replying from the notification, and sharing text into Aldune.
- **Reminders:** per note (In 1 hour, Tonight 20:00, or pick date and time); they alert on that device only. **Exact time** (Alerts) uses exact alarms.
- **Sync:** Aldune server and WebDAV only (a shared-folder code is rejected). Paste a profile code or tap **Scan QR** next to **Paste**; add several profiles. Background sync roughly every 15 minutes when Android allows. **Share profile** is done on Windows; **Invite someone…** (server invitations) exists on both, see [SYNC.md](SYNC.md).
- **Notes:** the editor bar has checkbox, bullet, indent, move line, undo/redo; each note has an options sheet (colour, tags, pin, history and more). “Hide completed tasks” only hides them while you edit (Windows deletes the line); trash retention and hide-after use fixed choices. **Keyboard when opening a note** (only new notes / every note / never) is Android-only.
- **Backup:** Settings → Sync tab has **Export all notes**, **Import notes…**, and the automatic copy to a folder. Android's own backup does not include your notes or the key.
  The full-backup zip and the safety copies the app keeps in its private `files/backups/` before «Replace everything» (newest three) hold notes as plain text (protected notes stay password-encrypted); the safety copies are not encrypted with the data key.
- **Sticky Notes import** does not exist on Android (see [MIGRATION.md](MIGRATION.md)).

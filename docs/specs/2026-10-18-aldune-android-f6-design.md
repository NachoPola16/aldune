# Aldune for Android — F6: quick capture and widget

> 2026-10-18. Develops the F6 row of the parent spec ([`2026-10-17-aldune-android-design.md`](2026-10-17-aldune-android-design.md) §2.7, §2.8 and §5) and takes
> precedence over it where it makes things more concrete. The decisions marked **(user)** were made by the user when defining the spec; the rest are reasoned defaults.
> Section §3 is pure JVM and is done with TDD, without the Android SDK.

## 1. What goes into F6

Six entry points for writing a note without going through the list, and a widget to see the deck from the home screen:

| Entry point | What it does | Where it lives |
|---|---|---|
| The list's "+" | Already exists (F4). | — |
| Icon shortcut | Long-press on the icon → «Nota nueva». **Static** shortcut. | `res/xml/shortcuts.xml` |
| Share to Aldune | `ACTION_SEND` of `text/plain`: new note with the shared text, in the editor. | `MainActivity` |
| Quick Settings tile | «Nota nueva» in the Quick Settings panel. | `NewNoteTileService` |
| Persistent notification | A pinned «Nota nueva» notification; **off by default** **(user)**. | `CaptureNotification` |
| Widget | Strips for the active notes and a "+". | Glance (`DockWidget`) |

Out of F6: sharing images or files, sharing several texts (`SEND_MULTIPLE`) **(user: text only)**, dynamic or per-note pinned shortcuts, ticking tasks
from the widget, a lock-screen widget, the final app icon (F8).

## 2. Product decisions

### 2.1 Share to Aldune

- **Text only** **(user)**: the filter is `ACTION_SEND` + `text/plain`. A link arrives as text (Chrome sends the URL in `EXTRA_TEXT` and the
  page title in `EXTRA_SUBJECT`).
- **With a subject, the subject is the title** **(user)**: the subject is the first line, the text the second. If the text already starts with the subject, or they are
  equal, it is not repeated. The exact rules are those of `SharedNoteText` (§3.3).
- **It opens the editor** **(user)**, it does not save silently: the new note comes up with the shared text, the cursor at the end and the keyboard hidden, so you can
  add something. Since it already has text, **the note is created on open** (the rule in §2.7 of the parent spec, "it is created when the first character is typed", is
  for the empty note; here the first text is supplied by whoever shares).
- If what is shared ends up empty (only whitespace) nothing is created: a brief notice «No hay texto que guardar» and it closes. If it is enormous (§3.3), the notice
  «El texto es demasiado largo para una nota» and nothing is created.
- Markdown checkboxes (`- [ ]`, `- [x]`) are converted into `☐`/`☒`, as when pasting into the editor (`TaskLists.convertMarkdownTasks`).
- Aldune opens **inside the sharer's task** (normal on Android): when finished, Back returns to Chrome. The note is saved like any
  other (F4's continuous saving).
- **A single `Intent` creates a single note**: rotating the screen, returning from Recents (`FLAG_ACTIVITY_LAUNCHED_FROM_HISTORY`) or recreating the activity
  after the process was killed must not create another. The intent is marked as handled in the activity's saved state.

### 2.2 Shortcut, tile and notification

All of them launch `MainActivity` with the action `app.aldune.action.NEW_NOTE`: the same screen as the "+" (new-note editor, keyboard hidden; if you leave
without typing, nothing is left behind). On a tablet (dual pane) it opens on the right with the list visible.

- **Shortcut**: static, short label «Nota nueva», its own icon (monochrome vector over the system background).
- **Tile**: `TileService` with a fixed state (it is a button, not a switch). When tapped: on API 34+ `startActivityAndCollapse(PendingIntent)` (the
  `Intent` variant throws an exception with `targetSdk` ≥ 34); before that, the `Intent` one. With the phone locked, `unlockAndRun` first. On API 33+,
  Settings → Captura offers «Añadir el mosaico» (`StatusBarManager.requestAddTileService`); before that, it explains how to add it by hand.
- **Persistent notification** **(user: off by default)**:
  - Switch in Settings → Captura: «Notificación para crear notas». When turned on in Android 13+, `POST_NOTIFICATIONS` is requested; if denied, the
    switch goes back to off with a line explaining it and a button to the system settings.
  - Its own channel «Captura rápida», low importance (no sound or vibration, no badge on the icon). An `ongoing`, silent notification with no timestamp,
    visible on the lock screen (it carries nothing from any note). Tapping it = new note; there is also a «Nota nueva» action.
  - It is posted again on phone boot (`BOOT_COMPLETED`), on app update (`MY_PACKAGE_REPLACED`) and when opening the app, if the setting is
    on. Android 14+ lets the user dismiss an `ongoing` notification: if the user dismisses it, that is respected until the next of those three occasions (it is not
    restored immediately: that would be fighting the user).
  - It is not a foreground service: there is no need, the notification has no work behind it.

### 2.3 Widget

- **Content** **(user)**: **all active notes**, in deck order (the same as the list), in a **scrollable** list inside the
  widget. Safety cap of **50** strips (a huge `RemoteViews` is rejected by the launcher); if there are more, the last row is «N más…» and opens the app.
- **One strip**: the colour of the note's face (text with the contrast from `NoteColorContrast`) and the title (`NoteTitleHelper`, one line, truncated).
  **Titles only by default**; the setting «Mostrar más texto en el widget» (off) adds up to two lines of preview, with the notice «Se verá en la
  pantalla de inicio».
- **Protected** **(user)**: the strip appears in its place in the deck, with its colour, a padlock and «Nota protegida». It **never** carries anything derived from its
  text or its protected content, not even with the preview setting on. (Today a protected note that arrives via sync has `text = ""`; the rule does not
  rely on that: §3.2 enforces it by type.)
- **Tapping a strip** **(user)** opens that note in the editor (phone: full screen; tablet: list + note). A **protected** one opens the list with
  its row highlighted (scrolled into view and highlighted for a moment); opening it is F7's business. If the note no longer exists or is no longer active (the widget was
  behind), the list opens with the notice «Esa nota ya no está en el mazo».
- **"+"** in the header: new note (`NEW_NOTE`). The header says «Aldune»; tapping it opens the list.
- **Empty**: «No hay notas» and the "+".
- **Size** **(user: a single resizable one that adapts)**: a single widget, 4×2 cells by default, resizable in both directions
  (minimum 2×1). By width: under 300 dp, one column of strips; from 300 to 479 dp, a grid of 2; from 480 dp, of 3. It is Glance's
  `SizeMode.Responsive` with those three breakpoints. On the tablet the same widget stretched gives the grid.
- **Appearance**: background and header with the app palette (`AppPalette`) in light or dark according to the system; the note colours do not change.
  Preview in the widget picker (`previewLayout` on API 31+, an image before).
- **When it is repainted**: after any `NoteService` write (create, edit, colour, state, order, delete) and after a sync pass that
  changed something, **batched** (at most once per second: typing in the editor must not repaint the widget on every key). Also when the
  language, the preview setting or the system theme changes. It is painted by the **app process**, which decrypts with the Keystore key (parent spec
  §2.8: there is no plaintext snapshot on disk).
- **If it cannot be read** (the data key does not open, database unavailable): the widget says «Abre Aldune» and tapping it opens the app. After a reboot, until the
  first unlock the app does not run (it is not *direct boot aware*): the launcher shows what it last painted, which is expected.

## 3. Pure JVM part (with TDD)

In the `android/domain` module (package `app.aldune.domain`), which already depends on `android/text`. No Android, no Glance, no interface text: the filler
texts come in as a parameter, just like in `NoteListItem`.

### 3.1 `DockOrder`: the deck order, in one place

`NoteService.dockOrdered` is private and the widget needs exactly the same order. It is extracted to `object DockOrder { fun sort(notes: Collection<Note>): List<Note> }`
with the same rule (position, `null` last; then `createdAt`; then `id`) and `NoteService` uses it. The current ordering tests keep passing without
touching them; the new `DockOrder` ones pin down the rule on its own.

### 3.2 `WidgetContent`: which notes go in and how

```kotlin
sealed interface WidgetStrip {
    val id: String
    val color: String
    data class Open(override val id: String, override val color: String, val title: String, val preview: String?) : WidgetStrip
    data class Locked(override val id: String, override val color: String) : WidgetStrip   // sin ningún campo de texto
}

data class WidgetContent(val strips: List<WidgetStrip>, val hiddenCount: Int)

data class WidgetOptions(val placeholder: String, val showPreview: Boolean = false, val limit: Int = 50)

object WidgetContentBuilder { fun build(notes: Collection<Note>, options: WidgetOptions): WidgetContent }
```

Rules (each with its test):

1. Only `NoteState.ACTIVE`: archived and trash do not appear.
2. `DockOrder.sort` order.
3. `isProtected` → `Locked(id, color)`, **always**, with or without `showPreview`, and even if its `text` is not empty (for example, a note unlocked in
   memory in F7). The type has nowhere to put text: the guarantee is structural. Property test: with protected notes whose `text`, tags and
   `protectedContentJson` contain a marker, no `toString()` of the result contains the marker.
4. `Open.title` = `NoteTitleHelper.getTitle(text, placeholder)` truncated to **120** characters with «…» (without splitting a surrogate pair).
5. `Open.preview` = `null` if `!showPreview` or if the preview comes out empty; otherwise, `NoteTitleHelper.getPreview(text)` truncated to **200** with «…».
6. Tags never appear (the widget does not show them, so there is no need to think about them).
7. `limit` counts strips of every kind; `hiddenCount` = the active ones that do not fit. `limit` ≤ 0 is treated as 0 (everything hidden).
8. No active notes: empty list and `hiddenCount = 0`.
9. Colour: the note's as is (deriving the border and contrast is done by the app with `text`).

### 3.3 `SharedNoteText`: a note from shared text

```kotlin
sealed interface SharedNote {
    data class Text(val value: String) : SharedNote
    data object Empty : SharedNote
    data object TooLong : SharedNote
}
object SharedNoteText { fun compose(text: String?, subject: String?): SharedNote }
```

Rules (each with its test):

1. Line breaks: bare `\r\n` and `\r` become `\n` (what comes out goes to `NoteService.create`, which requires LF).
2. Text: blank lines at the start and at the end are removed, and trailing whitespace; **not** the indentation of the first line (a shared
   indented list keeps its indentation). Whitespace under the project's .NET definition (`DotNetText`).
3. Subject: all whitespace (line breaks included) collapses into a single space and is trimmed; a subject cannot have two lines.
4. No subject → the text. No text → the subject. Neither → `Empty`.
5. If the first line of the text, trimmed, **starts with** the subject (case-insensitive, ordinal comparison) → the text unchanged. It covers
   "subject = text" and the apps that already send "Title\nURL".
6. Otherwise → `subject + "\n" + text`.
7. Then, `TaskLists.convertMarkdownTasks` on the result.
8. More than **1,000,000** characters (UTF-16) after composing → `TooLong`. (The server rejects objects over 5 MB; a million characters encrypted
   and in JSON stays comfortably below that even in the worst UTF-8 case.)
9. Edge cases with tests: subject that is the URL itself, text with `#` at the start (it is respected: the title cleans it when displaying), subject with line breaks,
   text of only whitespace with a valid subject (→ the subject), `null` in either of the two. It is never truncated: it either fits whole or is `TooLong`.

### 3.4 Definition of done for the JVM part

`./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` green, with the previous counts intact and the new ones added; `NoteService`
with no change in behaviour; no file outside `android/domain`.

## 4. Android part (here, with probes)

Order of work, each step tested before the next:

1. **Single capture entry in `MainActivity`**: `NEW_NOTE`, `ACTION_SEND` and "open note `id`" (extra `note_id`) are translated into an event of the
   `NotesViewModel` ("new note", "new note with text", "open note", "highlight note"). `onCreate` and `onNewIntent`; handled only once (§2.1).
   `launchMode` `singleTop` so that the widget and the tile do not stack copies of the app.
2. **Shortcut** (`shortcuts.xml`) and **tile** (`NewNoteTileService`, permission `BIND_QUICK_SETTINGS_TILE`).
3. **Settings → Captura**: «Mostrar más texto en el widget», «Notificación para crear notas», «Añadir el mosaico» (API 33+). In `SettingsStore`, with
   defaults that work if they are missing.
4. **Notification**: channel, permission, `BootReceiver` (`RECEIVE_BOOT_COMPLETED`, which F7 will reuse for reminders), republish on open.
5. **Glance widget**: dependency `androidx.glance:glance-appwidget` (current stable version, checked when adding it; Apache licence, suitable for F-Droid),
   `DockWidget` + `DockWidgetReceiver`, `appwidget-provider` with sizes and preview, `WidgetUpdater` (batched to 1 s) called from the writes of
   `NoteService` via `AppContainer` and from `SyncController` when a pass with changes finishes.
6. **New texts** in `android/strings.tsv` with the five languages and `python android/scripts/gen_strings.py` (+ `--check`): «Nota nueva» (already exists),
   «No hay texto que guardar», «El texto es demasiado largo para una nota», «Captura rápida» (channel and section), «Notificación para crear notas» and its
   explanation, «Mostrar más texto en el widget», «Se verá en la pantalla de inicio», «Añadir el mosaico», «Nota protegida» (already exists), «No hay notas»,
   «N más…» (no plural: «Más notas: N»), «Esa nota ya no está en el mazo», «Abre Aldune», the widget description in the picker.
7. **Icons**: own monochrome vectors for the shortcut, the tile and the notification (the notification requires a small monochrome icon; today the app
   uses `sym_def_app_icon`). The app icon stays for F8.

## 5. Verification (F6 gate)

- JVM: §3.4.
- Device: an instrumented widget test with `glance-appwidget-testing` if it works with the chosen version (with a protected note and the preview setting
  on, the painted tree does not contain its text); if not, the guarantee rests on §3.2 and on the probe.
- **Checklist on the Pixel 10a and on the TB350FU tablet**, with the phone **rebooted** before the second pass (notification and widget survive).
  Before touching the Pixel's screen, ask the user to turn on "Do Not Disturb". On the tablet in PC mode, do not use Back.

| # | Test | Expected |
|---|---|---|
| 1 | Icon shortcut | New-note editor, keyboard hidden; leaving without typing leaves no note |
| 2 | Share a link from Chrome | Note "page title / URL" in the editor; Back returns to Chrome |
| 3 | Share a text that already starts with its subject | Subject not repeated |
| 4 | Share and rotate / return from Recents | A single note |
| 5 | Share `- [ ] a` | `☐ a` |
| 6 | Tile, with the phone unlocked and locked | New-note editor (locked: asks to unlock first) |
| 7 | Notification: turn on (asks for permission), deny the permission | On: it appears; denied: the switch goes back to off with an explanation |
| 8 | Notification after reboot | It is back |
| 9 | Widget: add it; active notes in order; archived and trash do not appear | ok |
| 10 | Widget with a protected note, preview on | Strip with padlock, no title or text |
| 11 | Edit a note in the app / a change arrives via sync | The widget updates within ≤ 2 s; typing does not repaint it on every key |
| 12 | Tap a strip / a protected one / an already deleted one | Editor / list with the row highlighted / list with notice |
| 13 | Stretch the widget (phone) and make it large (tablet) | One column → grid of 2 or 3 |
| 14 | More than 50 notes | 50 strips and «Más notas: N» |
| 15 | Type in a captured note with Gboard and with a third-party keyboard (AnySoftKeyboard) | Enter and Backspace as in F4 |
| 16 | Light and dark system theme; the five languages in the widget and in Settings → Captura | Legible, no stray English texts |

- What was learned goes in `docs/ANDROID_PROBES.md`.

## 6. Risks

1. **Glance and launchers**: Lenovo's (PC mode included) may not respect the sizes or the preview. Tested early (step 5 with a minimal widget
   before polishing it).
2. **Repainting with the app closed**: Glance needs to start the process to paint; if opening the database and decrypting takes a while, the widget looks empty for a moment.
   Measured on the tablet.
3. **Sharing into someone else's task** can leave two copies of `MainActivity` (one in Aldune's task, another in Chrome's). They share the
   process's `AppContainer`, so there are not two databases or two syncs; if odd cross-edits show up in the probe, we switch to a trampoline
   activity.

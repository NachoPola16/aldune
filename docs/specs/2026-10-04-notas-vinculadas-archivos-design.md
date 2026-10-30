# Aldune — Notes linked to `.md` and `.txt` files (design spec)

Decided with the user on 2026-10-04 (phase F1 of a plan removed from the tree),
based on what they asked for in `docs/ROADMAP.md` §11. Ships as **1.6.0**.

## What was asked for

- Open a `.md` that already lives in any folder on the machine and have it **stay in the dock pointing at that
  place**: the note *is* the file (it is edited there and the file changes), not a copy.
- **Choose per note whether it takes part in sync.** Real example: the notes for a course on `Z:`
  (a network volume), which the laptop sees in the same place.
- The user also edits those files with other programs, though not necessarily at the same time.

## What is not included, and why

- **Rendering Markdown with formatting** (big headings, bold): the editor is a plain `TextBox` and tasks,
  bullets, indentation, moving lines and undo depend on it. Redoing it means touching the heart of the app with
  a lot of risk; `.md` edits fine as text.
- **Photos**: already ruled out in `ROADMAP.md` §3. They break the plain-text model that encryption, search, export, sync (server and envelope size)
  and the mobile client rely on.
- **Linking the same note to a file on each machine** ("named roots" that travel through sync):
  with `Z:` it adds nothing (the file is already the same on both machines and syncing it as well would create
  false conflicts through a double path); with local copies it would turn Aldune into a file synchroniser that competes with OneDrive or Syncthing; it would force the sync format up to 5; and it would give
  what arrives through sync the ability to write files on another machine's disk. The link belongs to each
  machine: on the laptop the file is opened once and stays in its dock.
- **Linked protected notes**: see decision 7.
- **Letter-by-letter simultaneous editing** with another program (merging while both are being typed into): very
  complex; with decision 4 nothing is lost.
- Formats with another list syntax (`.org`, `.rst`).

## Decisions

### 1. A linked note is still a note

It has its row in `Note` (colour, tags, position in the deck, reminder, state) like any other.
Only where its text comes from and where it goes changes. `Note.Text` stores the **translation** of the file into
Aldune's format (`☐`, `☒`, `→`; see decision 3): it is the copy of the last content read, and that is why the dock, the
manager, search, the reminder and "Exportar a Markdown" work without special cases. The database
is encrypted as always; the only thing in plaintext is the file, which already was.

### 2. The link is local and does not travel

New table `NoteFileLink` (created with `CREATE TABLE IF NOT EXISTS`, like the others; an old database
gains it on opening and an old version ignores it):

| Column | What it stores |
|---|---|
| `NoteId` (PK) | The note. |
| `Path` | Full path of the file. |
| `SyncEnabled` | Whether the note takes part in sync. **0 by default.** |
| `KnownHash` | SHA-256 of the file's content the last time Aldune read or wrote it. |
| `KnownWriteTime` | The file's write date at that moment (for the cheap check). |
| `KnownTextHash` | SHA-256 of `Note.Text` at that same moment. |

**"Unwritten changes"** is defined as `hash(Note.Text) != KnownTextHash`. It lives in the database, so
it survives an unexpected shutdown: if Aldune closes before writing the file, on start-up it still
knows something is pending.

None of this goes into the sync envelope. **The sync format stays at 4.**

- **Sync off** (default): `SyncScopeFilter.Includes` leaves the note out. It is the single place
  already shared by sync and the ▂▄▆█ signal, so the signal will say "out of scope".
- **Sync on**: the note travels as a normal, encrypted note, and on the other machine it is a normal note.
  What arrives from another machine is stored in the note and **written to the file** by way of
  decision 4 (with its fingerprint check). "Sincronizar esta nota" is **added** to the general scope: if
  sync is limited to a tag or to chosen notes, the note has to satisfy that as well.

### 3. Translation between the file and the note (`MarkdownLink`, Core)

Two pure functions:

- `ToNoteText(string fileText) → string`: `- [ ] ` → `☐ `, `- [x] `/`- [X] ` → `☒ `, `- ` → `→ `, also with
  `*` and `+`, respecting indentation. It reuses what `TaskLists.ConvertMarkdownTasks` already does when pasting.
  Everything else (`#` headings, links, tables, YAML header, numbered lists) stays literal.
  **Inside a code block** (between ```` ``` ```` or `~~~` fences) nothing is translated.
- `ToFileText(string noteText, string originalFileText) → string`: rebuilds the file. It translates the
  original line by line into note format, pairs those lines with those of `noteText` (longest common
  subsequence) and **every paired line is written with its exact original bytes** (`*`/`+`/`-` marker,
  capital `[X]`, tabs, trailing spaces). New or edited lines are written in
  standard form: task `- [ ] `/`- [x] `; bullet with the marker most used in the file (or `-` if there is
  none). Lines that carry no glyph in the note are written as they are.
- **Guarantee**: `ToFileText(ToNoteText(f), f) == f` for any `f`, and editing one line changes only that
  line of the file.

File format (`LinkedFileFormat`, Core): detected when reading and preserved when writing.

- UTF-8 with or without BOM, UTF-16 LE/BE with BOM. A file that is not valid UTF-8 and has no BOM (old ANSI)
  **is not linked**, with a notice: saving it would damage its accents.
- Line breaks: the dominant one (CRLF or LF) is kept; paired lines keep their own. Whether the file ended
  with a line break or not is also kept.
- Cap of **2 MB**, like the editor. Larger: not linked, with a notice.
- Extensions: `.md`, `.markdown`, `.txt`. The same translation for all three (a new task in a `.txt` is
  written `- [ ]`, which reads fine in any editor).

### 4. Reading, saving and changes made from outside (`LinkedFileDecision`, Core)

A pure function decides, given `KnownHash`, the disk's current fingerprint (or "does not exist / not accessible") and whether the
note has changes not yet written to the file:

| Disk | Unwritten changes in Aldune | What is done |
|---|---|---|
| equal to `KnownHash` | yes | **Write** the file. |
| equal to `KnownHash` | no | Nothing. |
| different | no | **Reload**: the file wins; the note is updated. |
| different | yes | **Conflict**. |
| not accessible | any | **Unavailable** (decision 5). |

- **Write**: with the same autosave as notes. It is written to a hidden temporary file in the same folder
  (`.~aldune-<guid>.tmp`) and swapped in with `File.Replace`, so that a cut-off does not leave the file
  half-written; if the drive does not support it, it is written in place (the text stays safe in the database).
  Right before, the fingerprint is read again and compared: **it never writes over a file whose fingerprint is not
  the known one**. After writing, the new fingerprint and date are stored. An orphaned Aldune temporary file is
  deleted on that note's next save (it is the only thing Aldune deletes in a user folder, and
  only if its name follows the exact pattern).
- **Reload**: the note takes the translated text, with `UpdatedAt` = now (if sync is on, it is
  published). With the window open, the cursor stays on the same line if it exists.
- **Conflict**: the file is **not touched**. Aldune's text goes into a **new normal note** ("⚠
  Conflicto: \<título\>", same colour, no link), the linked note reloads the disk and a notice is shown. No
  files are created in the user's folders.
- **Watching** (WPF): one `FileSystemWatcher` per folder with linked files, plus a periodic check
  of the write date (every 3 s with the note open, every 30 s otherwise; on network drives the
  watcher fails). After a notification it waits for the file to stop changing (two equal reads
  300 ms apart) before deciding: editors save in several steps. Aldune's own writes
  are recognised by the fingerprint and do nothing. Archived notes or those in the trash are not watched;
  when restored, the decision is made again.
- **Automatic changes**: in a linked note, "hide done tasks" and "move done ones to
  the end" are **not** applied automatically (they would rewrite lines of the file without the user doing anything). What the
  user does by hand (ticking, deleting done ones, moving lines) is.

### 5. File unavailable

File deleted, folder moved, `Z:` disconnected or no permissions:

- The note becomes **read-only** and shows a strip: "Archivo no disponible: Z:\apuntes\tema3.md", with
  **Reintentar**, **Buscar…** (link again to another path: the known fingerprint is discarded, so
  it is decided as "different disk": with no pending changes the file wins; with them, conflict) and **Convertir en nota normal**.
- The periodic check continues: when the file comes back, the strip disappears on its own and it is decided as in
  decision 4.
- What is edited just before it disappears, or what arrives through sync while it is away, stays in
  the note as a pending change and is written when it returns (or gives a conflict if the file changed in the meantime).
- A rename within the same folder (the watcher's `Renamed` event) updates the path by itself.

### 6. Interface

Opening (the first three accept several files):

1. **"Abrir archivo…"** in the dock menu and in the tray menu (filter `.md`, `.markdown`, `.txt`).
2. **Dragging** files from Explorer onto the dock.
3. **"Abrir con → Aldune"** in Explorer: the installer registers Aldune in `OpenWithProgids` for the three
   extensions, **without becoming the default program**. `aldune.exe "<path>"` opens the file; if there is already
   an Aldune running, the new one passes the paths to it through a named pipe (`SingleInstance` today only
   signals it to show itself) and exits. The portable version registers nothing, but the command line works.
4. **"Guardar como archivo vinculado…"** in the "⋯" of a normal, unprotected note: creates the file with
   `ToFileText(text, "")` (UTF-8 without BOM, CRLF) and links the note.

Opening an already linked file (same full path, case-insensitive) **shows the existing note**.
A new one goes at the end of the deck, with the colour given by the assignment rules, and is opened.

In the linked note:

- **Mark on the tab** (page icon from Segoe Fluent Icons) and the path in the tooltip; the same in the manager.
- **Title**: the first line without the Markdown heading `#` characters (`# Tema 3` → "Tema 3"). Applied to all
  notes in `NoteTitleHelper`. If the first line is empty, the file name without extension.
- **"⋯" menu**: "Sincronizar esta nota" (checkbox), "Abrir en su editor" (Windows' default program
  for the extension), "Mostrar en el Explorador", "Convertir en nota normal" (removes the link,
  keeps the text and warns that the file stays on disk) and "Proteger" disabled with its
  explanation.
- "Exportar a Markdown" remains available and creates a separate copy.

Texts with `Strings.T` in the five languages; colours via `DynamicResource`.

### 7. Protected notes: they are not linked

"The note is your file" and the file is plaintext. Protecting only in Aldune would be false security
(anyone opens the `.md` with Notepad); encrypting the file would make it unreadable to the other
programs, which is what linking is for. So in a linked note "Proteger" is disabled, and to
protect it you first convert it into a normal note (with the notice that the file stays in plaintext on disk).
"Guardar como archivo vinculado…" is not offered on a protected note.

### 8. Archive, trash and delete

- Archiving keeps the link and stops watching; restoring reads again.
- In the trash the link is kept, so that restoring works.
- Emptying the trash (or its time limit) deletes the note and the `NoteFileLink` row. **Aldune never deletes a linked
  file**, wherever it is: deleting it cannot be undone and on a network drive it does not even go through the
  Windows Recycle Bin. To delete it, "Mostrar en el Explorador".

## Pieces

Core (TDD):

- `MarkdownLink`: `ToNoteText`, `ToFileText`.
- `LinkedFileFormat`: reading bytes → text + format (encoding, BOM, dominant line break, final line break);
  writing text + format → bytes; rejections (encoding, size).
- `LinkedFileDecision`: the table in decision 4.
- `NotesRepository`: `NoteFileLink` table and its operations; `SyncScopeFilter` with the per-note checkbox;
  `NoteTitleHelper` without `#`.

WPF:

- `LinkedFileWatcher`: per-folder watchers, periodic check, wait for the file to settle.
- `AppCoordinator`: open/link, apply decisions, conflict, convert into a normal note.
- `NoteWindow`: saving through the file path, unavailable strip and read-only mode, menus, not applying
  the automatic changes.
- `EdgeDockWindow`: dropping files; mark on the tab. Manager: mark and tooltip.
- `SingleInstance`: named pipe to pass paths; `App.OnStartup` reads the arguments.
- Installer (`Aldune.iss`): `OpenWithProgids` for `.md`, `.markdown`, `.txt`, removed on uninstall.

## Tests

- **Byte-for-byte round trip** (`ToFileText(ToNoteText(f), f) == f`) with a collection of difficult files:
  code blocks with `- [ ]` inside (with ```` ``` ```` and `~~~`), nested and numbered lists, `* [X]`, `+`,
  tabs, trailing spaces, CRLF, LF and a mix, with and without BOM, UTF-16, YAML header, empty file,
  no final line break, only line breaks.
- **Localised edits**: ticking a task, adding a line, deleting another, moving one with Alt+↑: only
  those lines of the file change.
- `LinkedFileDecision`: each row of the table; `LinkedFileFormat`: each encoding and each rejection.
- `SyncScopeFilter`: linked note without sync is out; with sync, subject to the general scope.
- **Probes** (outside the repo, temporary data): outside change that is reloaded; conflict ("⚠
  Conflicto" note and file intact); renamed folder (unavailable, read-only, comes back on its own); dragging
  two files onto the dock; second instance with a path; "Guardar como archivo vinculado…". Those that move
  the mouse or keyboard, with prior warning.
- Smoke test green before publishing.

## Order of work

One plan, in two batches:

1. Complete Core; open ("Abrir archivo…" and drag), save, watch, unavailable (with "Convertir en
   nota normal"), conflict, archive/trash, mark and title. With this the notes on `Z:` are already usable.
2. "Abrir con" (installer and pipe), "Guardar como archivo vinculado…", "Sincronizar esta nota",
   "Abrir en su editor" and "Mostrar en el Explorador".

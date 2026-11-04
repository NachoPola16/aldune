# Moving your notes to Aldune

Aldune can import your notes from Microsoft Sticky Notes, Google Keep, Evernote and plain Markdown or text files. This page says how, step by step, and exactly what comes across and what does not.

Short version of the rules:

- **Import only adds.** It creates new notes. It never edits, moves or deletes a note you already have.
- **You see a preview first.** Choosing a file reads it and shows what would happen. Nothing is written until you press **Import**.
- **Text only.** Aldune holds text. Images, audio, files and drawings are not imported, but the text of the note they were attached to is.
- **Importing the same export twice adds nothing** (see [Duplicates](#duplicates)).

Where to start:

| | Windows | Android |
|---|---|---|
| Open the importer | Manage notes → **Import** (button at the bottom; its tooltip is “Import notes from another app…”) | Settings → **Sync** tab → **Backup** section → **Import notes…** |
| Microsoft Sticky Notes | Yes | No (it is a Windows app: import on Windows and let sync carry the notes to the phone) |
| Google Keep (Takeout) | Yes | Yes |
| Evernote (.enex) | Yes | Yes |
| Markdown / text files | Yes (a whole folder) | Yes (files you pick one by one) |

## What is preserved

“Yes” means the importer reads it from that source. “–” means that source does not have it, or the importer does not read it.

| | Sticky Notes | Google Keep | Evernote | Markdown / text |
|---|---|---|---|---|
| Text | Yes | Yes | Yes | Yes |
| Title | The first line of the note | The Keep title becomes the first line | The Evernote title becomes the first line | The first `# heading`, otherwise the file name |
| Colour | Yes, if the database has a colour column | Yes (colour name) | – | – |
| Tags | – | Labels become tags | Tags are kept | – |
| Checklists | – (text only) | List items become tasks, ticked ones stay ticked | Check boxes become tasks | `- [ ]` / `- [x]` and bullets become Aldune tasks and bullets |
| Pinned | – | Yes | – | – |
| Archived | – | Yes | – | – |
| In the trash | Yes (deleted notes go to Trash) | Yes | – | – |
| Created / edited dates | Yes | Yes | Yes | The file's dates |
| Images, audio, drawings, ink, attachments | Not imported (counted and reported) | Not imported (counted and reported) | Not imported (counted and reported) | – |

Details that matter:

- **Colours are matched, not copied.** Aldune does not carry loose colours. The importer works out the colour family of the original (yellow, green, cyan, blue, pink or orange; Keep's purple is mapped to blue, red and brown to orange, grey and “default” have no family) and picks the closest colour of your **current note theme** and tone (Settings → Note colors on Windows; the colour settings on Android). A note with no colour, or a family too far from every colour in your theme, gets a colour by your normal rule for new notes. So the same import looks different under a different theme.
- **Dates.** Original creation and edit dates are kept when the source has them. A date in the future is pulled back to the moment of the import, and a note with no date gets the moment of the import. Notes imported **into the trash** keep their place there, but their trash clock restarts at the import, so the automatic purge (Settings → Notes → Trash, 30 days by default) does not erase them straight away just because they were deleted long ago in the other app.
- **Pinned** is Aldune's “pin to the top of the list”.
- **Left out of a preview, with the reason shown:** empty notes, notes or files that are too large (a note over about one million characters), files that are not plain text (binary, or an encoding Aldune cannot read), files that could not be read, and anything beyond **5,000 notes per import** (import again for the rest).
- Imported notes are normal notes. You can protect one afterwards (see [PROTECTED_NOTES.md](PROTECTED_NOTES.md)).

## Duplicates

Aldune does not mark imported notes (the sync format has no field for it and it is not going to change), so “already imported” is decided by comparing: a note in the file counts as **already there** when one of your existing notes (active, archived or in the trash) has the same text and, if the source gives a creation date, the same creation date. Those are not added again and the preview says how many.

Consequences:

- Importing the same export a second time adds nothing, so a failed or interrupted import can simply be run again.
- A note you **edited in Aldune after importing** no longer matches, so importing the export again would bring the original back as a new note.
- Password-protected notes are not compared (their text is encrypted).

## The preview, step by step

Every importer works the same way:

1. Pick the source in **Import from**.
2. Pick the file, folder or files. Aldune reads them (the original is only read) and the dialog shows the result:
   - “N note(s) will be added.” or “There is nothing new to add.”
   - Notes already in Aldune (same text and date), not added again.
   - Notes left out, grouped by reason (empty, too large, not text, could not be read, over the limit).
   - A warning if attachments are ignored: how many, and in how many notes. The text of those notes is imported.
   - A reminder that colours are matched to your current theme and original dates are kept.
3. Press **Import** (disabled while there is nothing to add). It shows “Done: N note(s) imported.” (plus how many could not be saved, if any).
4. **Close**. On Windows, Esc or Cancel before pressing Import leaves everything untouched. While it is reading or writing the window cannot be closed (a half-done import cannot be cancelled cleanly).

## Windows

Open **Manage notes** (tray icon → Manage notes…, the dock's Manage notes button, or the optional shortcut Ctrl+Alt+M) and press **Import**.

### From Microsoft Sticky Notes

1. Choose **Microsoft Sticky Notes** in *Import from*.
2. Press **Use the one from Windows**. Aldune looks for the Sticky Notes database of your account at `%LOCALAPPDATA%\Packages\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\LocalState\plum.sqlite`. If you never used Sticky Notes on this account it says so. If you have a copy of `plum.sqlite` (from another computer, for example), press **Choose file…** and pick it.
3. Check the preview and press **Import**.

Safe to try: Aldune never opens the live database. It copies `plum.sqlite` (and its `-wal` / `-shm` companions) to a private temporary folder, reads the copy and deletes it, so Sticky Notes can be open and the original is never locked or changed.

What is read: the text (each paragraph's internal `\id=…` marker is stripped), the creation and edit dates, the colour if there is a colour column, and the “deleted” stamp (a deleted note goes to Trash). Pictures and ink are counted as attachments and not imported. The old pre-2018 `StickyNotes.snt` format is not supported.

> **What was tested.** The importer was checked against a `plum.sqlite` written by the real Microsoft Sticky Notes 4.0 on Windows 11 (notes typed in the app: text, several lines, the yellow colour, empty notes). Paragraphs, dates and the colour name were read correctly and the app's own markup (`\id=…`) is removed. Not yet seen with a real database: deleted notes (the importer sends them to the trash if the app keeps them with a deletion date), images and ink drawings (never imported; Aldune is text only), and the other colours (Pink, Purple, Blue and Green are mapped by name; Gray and Charcoal have no hue and import with the default colour). If the preview looks wrong, do not import; the original is untouched. Please open an issue describing what you saw.

### From Google Keep

1. Go to Google Takeout, export only **Keep**, and download the `.zip`.
2. Choose **Google Keep (Takeout)** in *Import from*.
3. Press **Choose .zip…** and pick the file, or **Choose folder…** and pick the `Keep` folder inside the unzipped export.
4. Check the preview and press **Import**.

Aldune reads the `.json` file of each note (that is the source of truth). Nothing is extracted to disk. If a note only has an `.html` file and no `.json`, it is read as a best-effort fallback: text, labels and colour, but no dates, archive or pin. Other JSON in the export that is not a note is ignored.

### From Evernote

1. In Evernote, export your notes (or a notebook) as an **`.enex`** file.
2. Choose **Evernote (.enex)** in *Import from*.
3. Press **Choose file…**, pick the `.enex`, check the preview and press **Import**.

Titles, text, tags, creation and edit dates are read; check boxes become tasks. Attachments (“resources”) are skipped without being loaded and reported in the preview. Evernote has no colours, so imported notes get colours by your rule for new notes. The file is read safely (no external entities, nothing is fetched).

### From Markdown or text files

1. Choose **Folder of .md / .txt files** in *Import from*.
2. Press **Choose folder…**. Every `.md`, `.markdown` or `.txt` file in the folder **and its subfolders** (up to 6 levels) becomes a note.
3. Check the preview and press **Import**.

The title is the file's first `# heading`; if it has none, the file name (without extension) is used. Markdown tasks and bullets become Aldune tasks and bullets. The note's dates are the file's dates (the older of creation and modification is used as the creation date). A file that is binary or in an encoding Aldune cannot read is refused instead of imported as garbage. This is also the way back from Aldune's own Markdown export.

(To bring in a single Markdown file as a note without this dialog, use the dock's **New note** button, right-click → **Import Markdown as a note…**.)

## Android

Open **Settings → Sync** (the *Backup* section is on that tab) and press **Import notes…**. Android has no folder picker for this, so you choose files with the system picker (no storage permission is needed) and may pick several at once.

1. Choose the source: **Google Keep (Takeout)**, **Evernote (.enex)** or **Markdown / text files (.md, .txt)**.
2. Press **Choose files…**:
   - Keep: the Takeout `.zip`, or the `.json` files of your notes.
   - Evernote: the `.enex` file.
   - Markdown / text: the files. Android does not require an extension (the picker cannot filter reliably); what decides is the content, and a binary or unreadable file is refused. The file's modified time is used as both dates.
3. Check the preview and press **Import**. While it reads or imports a progress bar is shown and the dialog cannot be dismissed. The result is announced when it finishes.

Everything in the table above applies the same way. If you also use the Windows app, the simplest route for Sticky Notes is to import on Windows and let [sync](SYNC.md) bring the notes to the phone.

## Undo

There is no “undo import” button, and imported notes are not tagged as such. Your options, from safest to bluntest:

- **Before you import**, make a safety copy if the import is big: on Windows, Manage notes → **Export** (everything in the current list as a `.zip` of Markdown files), or turn on the automatic copy to a folder (Settings → Sync); on Android, Settings → **Export all notes**. With sync on, remember that the imported notes will be sent to your other devices too.
- **Found a problem in the preview?** Close the window. Nothing was written.
- **Imported something you did not want?** Select those notes and move them to the trash, then delete them forever from the Trash view if you want them gone. Manage notes helps: search for a word they share, filter by a tag that came with them (Keep labels and Evernote tags are kept), or sort by date, then tick the rows (the *Select all* box, Shift+click for a range) and use **Trash**. On Windows, in the Trash view the button is **Delete**, which asks for confirmation. Notes in the trash are also purged automatically after the retention period.
- **Remember the original is untouched.** Sticky Notes, Keep and Evernote still have their notes, and the files you imported are unchanged, so you can always start again.

## If something goes wrong

| What you see | Meaning |
|---|---|
| “That file no longer exists.” | The path is gone; pick it again. |
| “That file is too large to import safely.” | Over the size limits (about 256 MB for a Sticky Notes database, 1 GB for an `.enex`). |
| “That does not look like the right kind of file for this source.” | Wrong source for that file (for example an `.enex` chosen under Keep). |
| “No notes were found there.” | The file or folder has nothing the importer recognises as a note. |
| “That could not be read.” | The file is damaged or locked. |
| “Sticky Notes data was not found on this account…” | Sticky Notes was never used here; choose a copy of `plum.sqlite` instead. |

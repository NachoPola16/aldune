# Pinned notes (Windows 2.2.0 + Android 1.1.0)

Status: **spec, not implemented.** Decision by the owner (2026-10-31): the pin is synced between devices, even if it costs more, and it must not raise sync format 4.

## Naming: do not confuse it with "always on top"

On Windows a note already has a *pin* in the sense of **always on top of other windows** (`NoteUnpinned` table, local, never synced). This feature is a different thing: **pinned to the top of the list**. In code and in the sync payload it is `pinnedToTop` / `IsPinnedToTop`; in the interface the text is "Pin to top of the list" / "Unpin from the top" (never just "Pin"), and the Windows marker must not reuse the always-on-top pin icon.

## What the user sees

- A note can be **pinned**. Pinned notes are always shown first, in every list that shows notes: the Android list, the Android widget, the dock deck and the manager on Windows.
- **Order among pinned notes:** the same order the rest of the notes follow (the manual order of the dock, or the "most recently edited / oldest / by title" setting on Android). The pinned notes are a stable partition of the normal order, so there is no separate "pin date" to explain or to keep.
- **Dragging** a note only reorders it inside its own group (pinned or not). The "Move up / Move down" actions stop at the boundary. Pinning a note does **not** put it at the top of the pinned group: it keeps its place in the normal order, which is the same rule as above and the least surprising one when it is unpinned again.
- **Where it is done.** Android: the long-press menu and the options sheet ("Pin to top" / "Unpin"), and a small pin icon on the card. Windows: a pin button in the manager row and a marker on the dock tab, plus the entry in the note's menu.
- Pinning does not change the note's text, colour or reminders. It does count as an edit (see "Sync").

## Data model

| Where | Change |
|---|---|
| `Aldune.Core` `Note` | `bool IsPinnedToTop { get; set; }` (default `false`). Part of `NoteEquivalence`. |
| Core SQLite | `ALTER TABLE Note ADD COLUMN IsPinnedToTop INTEGER NOT NULL DEFAULT 0;` as a numbered migration (existing databases get `0`). |
| Android `Note` / `SyncNote` | `pinnedToTop: Boolean = false`. |
| Android Room | `NoteEntity.pinnedToTop` with a default of `0`, `Migrations.kt` 4 → 5 (`ALTER TABLE ... DEFAULT 0`), exported schema `5.json`, `MigrationTest` extended. |

## Sync

The pin travels inside the encrypted note JSON (`payloadJson`) as an **optional** field `pinnedToTop` (boolean). Absent means `false`. This is the same pattern as `protectedTitle` and `dockPosition`.

- **The sync format stays 4.** An older version never reads a format it does not know, and here nothing about the envelope changes: only a key is added inside the encrypted JSON. `System.Text.Json` (Core) and the key-by-key reader of `SyncEnvelopeCodec` (Kotlin) ignore keys they do not know; this is covered by a vector (below).
- **Pinning or unpinning counts as an edit**: it refreshes `UpdatedAt` (as `NotesRepository.TouchUpdatedAt` already does for reordering), otherwise the note would not be published again and the other device would never see the pin. Consequence: with "most recently edited first", pinning moves the note up inside its group. This is accepted and documented in the user-facing text.
- **Merging** is the existing rule (the most recent `UpdatedAt` wins, ties by `deviceId`); the winning version carries its own pin. There is no field-by-field merge, so two devices that pin and edit the same note at the same time resolve like any other edit.

### Compatibility with 2.1.x / 1.0.x (must be tested before publishing)

| Writer | Reader | Result |
|---|---|---|
| New, note pinned | Old | The old version reads the note normally and shows it unpinned. Nothing breaks. |
| Old edits that note and sends it back | New | The old version writes the note **without** `pinnedToTop`, so the pin is lost. Documented in `SYNC.md` and `STATUS.md`. It only happens when an old device *edits* the note (an unedited note is not re-sent). |
| New, note not pinned | Old | The field is written only when `true`, so the payload is byte-identical to today's. |
| Old, any note | New | No `pinnedToTop` means `false`. |

## Vectors (`tests/vectors/sync-format4.json`)

Regenerated with `ALDUNE_REGEN_VECTORS=1` once the Core change is in, after checking that **the existing vectors do not change** (only additions). New vectors:

1. A note with `"pinnedToTop": true` (the field present).
2. A note written by an old client (no `pinnedToTop`), which must read as `false` on both sides.
3. A scenario where an old device re-sends a pinned note (the pin disappears) so the documented loss is fixed by a test and not only by prose.

Checked by `SyncVectorsTests` (C#) and by the Kotlin tests in `android/core` (`SyncEnvelopeReadTest` / `SyncEnvelopeWriteTest`): C# writes and Kotlin reads, and the other way round.

## Settings parity

No setting is added. Add a line to `SETTINGS_PARITY.md` saying the pin exists on both platforms with the UI differences above.

## Out of scope

- Pinning from the Windows tray or a global shortcut.
- A separate "pinned" section header in the list (the pin icon is enough; a header would break the manual order drag).
- Pin date, or a different order for pinned notes.

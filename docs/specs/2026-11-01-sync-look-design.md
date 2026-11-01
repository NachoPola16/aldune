# Syncing the look between devices — design (2026-11-01)

Status: **built (server transport) in Windows 3.0.0 and Android 2.0.0** — `SyncLook` (Core) and `LookSync` (Android) share one document format, checked by a document sealed in C# that Kotlin opens; Folder and WebDAV are left for a later round. Opt-in: a person who wants the same look (appearance, note theme…) on all their devices turns it on; everyone else keeps a look per device, as today.

## The trap that decides the design

The sync store holds one object per note: an AES-GCM envelope whose decrypted payload is a note (`SyncNotePayload`). Every installed version reads **every** object it finds and
throws `FormatException` («The sync note metadata does not match its encrypted content») if the payload is not a note; over HTTP that **aborts the whole pass**
(better to stop than to sync half the data). So:

- A new kind of object in `/api/v1/objects` (or a look document inside the note listing) would break every device that still runs a version without this feature. Not acceptable.
- Raising the sync format (5) would make old versions refuse the store instead of failing: safer, but it forces everyone to update at once. The format is raised only when nothing else works.
- A hidden «note» with a reserved id would show up in old versions' trash as junk and sync back and forth. Rejected.

## Chosen design: a companion blob that old versions never list

One encrypted document per **profile**, stored **next to** the objects, not among them:

| Transport | Where | Why old versions ignore it |
|---|---|---|
| Server | `GET/PUT /api/v1/look` → `<space>/look.bin` (parent of `objects/`) | the listing reads only `objects/*.json` |
| Folder | `look.aldune` in the profile's folder | the folder transport reads `*.json` |
| WebDAV | `look.aldune` beside the objects | verify the old filter before relying on it (WebDAV lists a directory; the old one keeps `.json`) |

Same encryption as a note (the profile's sync key, `ContentCipher`), so the server learns nothing. Sync format stays **4**; a server without the endpoint answers 404 and the feature simply stays off.

## The document

```json
{ "v": 1, "stamp": "2026-11-01T12:00:00Z", "device": "<deviceId>", "look": {
    "appearance": 7, "aspectColors": "...", "squareCorners": null, "syncSignal": null,
    "fillNoteColor": null, "uniformNoteColor": null, "noteTheme": "…", "newNoteTone": 0, "colorAssignment": 0, "fixedNoteColor": null } }
```

Only what both platforms share (`docs/SETTINGS_PARITY.md`, «Look and look colors», «Colors»): the names are the **Android** ones, Windows maps them (`NoteColorAsStripe` is the inverse of `fillNoteColor`).
Never synced: language, text size, dock edge/alignment/monitor, hotkeys, paths, sounds, sync profiles, **custom themes** (Windows-only; an unknown `noteTheme` id falls back to the default theme).
Unknown fields are ignored and a missing one leaves that setting alone, so a later version can add fields.

## Which side wins, without hooking every setting

No hooks on the settings screens. Each device remembers `lastLookHash` (a hash of its own look subset) and `lastLookStamp` (the stamp of the document it last pulled or pushed). In every sync pass of a profile with the option on:

1. Read the remote document (404/absent = none).
2. If the **local** hash differs from `lastLookHash`, the person changed the look here: push a new document with `stamp = now`, store hash and stamp.
3. Else if the remote `stamp` is newer than `lastLookStamp`: apply it to the settings (refreshing windows live), store hash and stamp.
4. Else nothing.

If both changed between two passes, the later stamp wins (last writer wins; the look is a preference, not data). Clock skew over a day is ignored like in notes (`UpdatedAt > now + 1 day`).

## Interface

- A per-profile switch in Sync settings, off by default: «Sync the look with this profile» (hint: «Appearance and colours follow you across devices. Language, size, shortcuts and the dock stay per device»). Windows `SyncProfileSettings.SyncLook`; Android `SyncProfile.syncLook`.
- Turning it on does **not** overwrite either side blindly: the first pass pulls if the remote exists (the other device was first) and pushes if not. The person is told which happened («Look taken from the profile» / «Look sent to the profile»).
- Several profiles with the option on: the one with the newest stamp wins; the others receive it on their next pass.

## Server

`/api/v1/look`: same bearer middleware as the objects (so it lands in the token's space), `GET` → 200 with the blob or 404, `PUT` ≤ 64 KB → 204. Not touched by `registry`/invitations. A server older than this endpoint returns 404: GET reads as «none», PUT failure is silent and retried next pass.

## Tests

Core and `android/core`: document round trip, a vector made by C# read by Kotlin (like `tests/vectors/`), unknown fields ignored, a missing field leaves the setting, the three branches of the winner rule, the Windows↔Android mapping (`fillNoteColor` inverse), custom theme id falls back. Server: GET/PUT/404/size cap/space isolation. Probe: change the appearance on a temporary profile and see the second device take it.

## Out of scope

Custom themes, per-note colours (those are in the notes already), and syncing any other setting. Folder and WebDAV can follow the server transport in a later round.

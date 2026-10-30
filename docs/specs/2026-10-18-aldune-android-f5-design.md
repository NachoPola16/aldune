# Aldune for Android — F5: sync inside the app

> 2026-10-18. Details row F5 of [`2026-10-17-aldune-android-design.md`](2026-10-17-aldune-android-design.md) §5. That spec (especially §2.6,
> §3 and §3.1) takes precedence for anything not said here. Starting state: F0-F4 done and verified on the Pixel 10a and the Lenovo TB350FU tablet;
> the engine (`planSync`/`execute`) and the HTTP and WebDAV transports exist in `android/sync`
> with 119 tests. The decisions marked **(user)** were made by the user on 18-10.

## 0. Goal and limit

Make the phone and the tablet **sync with the same store as the PC** (Aldune server or WebDAV) without the user having to think about it, without
ever losing an edit and without changing format 4. When F5 is done: a note created on Android appears on the PC and vice versa; a provoked conflict is saved;
a signed deletion deletes and an unsigned one sends to the trash; all against the real server on CT 103 with a test profile.

Outside F5: opening/protecting/unprotecting protected notes and resolving conflicts (F7; in F5 they are **saved and flagged**, §5), more than one profile, shared folder,
starting a key rotation, QR, capture and widget (F6).

## 1. User decisions (18-10)

| Topic | Decision |
|---|---|
| Server that does not respond | **Silent** **(user)**: the app keeps going locally; the indicator changes to "Offline · last sync X ago"; it retries on the next trigger and with WorkManager when there is network. No pop-up notices nor aggressive retries. |
| What is shown | **Icon + detail** **(user)**: discreet icon in the list bar (not drawn if not configured; ok / syncing / offline / error). On tapping it: "Last sync 3 min ago" or the error in clear text and "Sync now". In Settings → Sync, the same plus the full last error. There is no notice after each sync. |
| Scope | **The code's** **(user)**: all / chosen / tag, as on Windows. It is shown in Settings and not edited on the phone. |
| First connection with local notes | **Ask** **(user)**: "Upload these N notes to the profile" or "Discard them from this device and keep the profile's". Without local notes, it connects without asking. |
| Protected notes arriving before F7 | **Lock, read-only** **(user)**: they are stored whole (`protectedContent` intact), shown with a lock and "Protected note" and, when opened, "It will be possible to open it in a future version". They can be archived, sent to the trash, colored and moved (that travels); the text is not touched. |
| Conflicts before F7 | **Save + notice** **(user)**: `sync_conflict` table (Room migration 1→2) and an icon on the note's row. Resolving them arrives in F7. The winner is already applied: nothing is lost. |

## 2. Split: a new pure module, `android/syncclient`

```
android/
  core/ sync/ text/   unchanged (except the typed exception of §4.6, only if needed)
  domain/             + protected-note fields in Note and their rules (§3)
  syncclient/         NEW. Pure Kotlin/JVM. Depends on :domain, :sync, :core. Bridge between the domain notes and the engine:
                      local snapshot → SyncInput, two-phase execution, LocalSyncStore contract (+ its reusable test suite),
                      triggers, state for the UI, profile code validation, first connection.
  app/                Room (implements LocalSyncStore), secrets in the Keystore, screens, WorkManager. Almost no rules of its own.
```

**Why a module and not `domain`:** `domain` stays free of OkHttp and the sync format, and the new work goes into a new folder with no risk to the current 414
tests. Package `app.aldune.syncclient`. Same compile options as the other pure modules (Java 11, JUnit 5).

**Where it is done:** all of `syncclient` and the `domain` change isolated, with TDD. `app` is done afterwards here, locally, with the devices (§7).

## 3. Changes in `domain`

- `Note` gains `isProtected: Boolean = false` and `protectedContentJson: String? = null`: the JSON of `ProtectedNoteContent` **exactly as it arrived**, opaque to `domain`
  (which does not depend on `core`). A protected note has `text == ""`. Room already has the `isProtected` and `protectedContent` columns (F4 reserved them).
- `NoteService`:
  - `edit` on a protected note returns an error (`NoteResult.Err`, new "protected" code) and does not write.
  - `setColor`, `setTags`, `archive`, `trash`, `restore`, `move`, `deleteForever`, `purgeTrashed`: same as with a normal note, and they **keep** `isProtected` and
    `protectedContentJson` without touching them.
  - `list(query = non-empty)`: protected notes do not appear (their text is not known); with an empty search they do appear.
  - They are not found by their tags either: a non-empty search never returns a protected note (simpler and more prudent).
- `NoteTitleHelper`/preview of a protected note: decided by `app` (placeholder title from `strings.tsv`), not `domain`.

## 4. `syncclient`

### 4.1 The local snapshot and building the `SyncInput`

`LocalSnapshot` (read in **one** transaction, §4.2): notes (`domain.Note`), tombstones (`domain.Tombstone`), bases (`noteId` → `SyncBase`).

`buildSyncInput(snapshot, settings, deviceId, now): SyncInput`:

- `Note` → `LocalNote(SyncNote, authorDeviceId = note.syncAuthorDeviceId)`.
- `String` ids → `UUID` (Core writes them lowercase with hyphens; `RandomIdFactory` too). An id that is not a UUID is a program bug: exception, it is not skipped.
- `Instant` → `OffsetDateTime` in **UTC** and **truncated to 100 ns** (`createdAt`, `updatedAt`, `deletedAt`, `now`). That way the header and the encrypted content match
  (comment on `toTicks` in `SyncPlanner.kt`) and the same note gives the same envelope on two consecutive passes.
- `text` = `note.wireText` (it brings back the CRLF of a Windows note; an Android note goes in LF). Protected: `text = ""` even if something else arrives (the engine
  also forces it in `forSync`; here it is pinned with a test from the input side), `isProtected = true`, `protectedContent` = the parsed JSON.
  Unreadable `protectedContentJson` = program bug (the app itself wrote it when applying).
- `state` = `NoteState.code`; `tags`, `color`, `dockPosition`, `screenOrigin` **as is** (not normalized: E28).
- Tombstones: `LocalTombstone(noteId, deletedAt, deviceId)` keeping the `"local"` marker (the engine replaces it with the `deviceId` when signing).
- `excludedIds` = always empty (Android has no notes linked to files).
- `scope`: from the stored profile (`SyncScope.All` / `SelectedNotes` / `Tag`).
- `deviceId`: 32 lowercase hex (`UUID` without hyphens, like Core's `Guid.ToString("N")`), generated **once per installation** and never changed (not even on
  disconnecting). `app` stores it; `syncclient` provides the function that generates and validates it.

And the other way round, `toDomainNote(SyncNote, authorDeviceId): Note?` for applying: `LineEnding.detect` + `normalize` on the text, `Instant` from the instant,
tags, color, position and origin as is, `syncAuthorDeviceId = author`, protected with its JSON (serialized with `core`, in the same key order as
Core so it can be compared). **Unknown state** (`NoteState.fromCode == null`, e.g. a future format): returns `null`; the action is skipped, its base is
not set and it is counted as "omitted" in the result (no state is invented, because it would be lost on uploading again).

### 4.2 `LocalSyncStore` contract (transactional)

```kotlin
interface LocalSyncStore {
    /** Notes, tombstones and bases read in a single transaction. */
    fun snapshot(): LocalSnapshot
    /** Applies everything local from a pass in ONE transaction; returns what was applied and what was skipped. */
    fun commit(commit: LocalCommit): CommitReport
    fun conflictedNoteIds(): Set<String>
    /** First connection → "discard": deletes notes, tombstones, bases and conflicts WITHOUT creating tombstones. */
    fun discardAllLocal()
    /** Disconnect: deletes bases and conflicts; the notes and tombstones stay. */
    fun forgetSyncState()
}
```

`LocalCommit` carries the plan's local actions (`ApplyNote`, `ApplyTombstone`, `TrashNote`, `SaveConflict`), the new bases and, per note, the
**expected version** that was seen in the snapshot (`updatedAt` + author, or "did not exist", or "had a tombstone").

Semantics of each action, same as Core:
- `ApplyNote`: inserts or replaces the note with the remote author and **removes the local tombstone** of that id if there is one (`NotesRepository.cs`, `DELETE FROM SyncTombstone`
  in `ApplySyncNote`).
- `ApplyTombstone`: deletes the note and stores the tombstone with the remote author's `deviceId` (so as not to sign it again as its own).
- `TrashNote`: trash state, `updatedAt = at`, author `null` ("this machine", like `SetState`).
- `SaveConflict`: stores the conflict (the whole losing version); if there was already one for that note, it replaces it.

**Optimistic concurrency** (the rule that avoids losing edits): sync takes seconds on the network and the user may edit in the meantime. Inside the
transaction, for each note with a local action the current state is compared with the expected version. If it **does not match**, all the local actions of that note
are skipped and **its base is not set**. With the old base, the next pass sees both sides changed and saves a real conflict if there is one, instead of silently
overwriting the edit. The bases of notes **without** local actions (upload only, or equal content) are always set: what they say is true in the store.
`CommitReport` says how many were skipped; if there is any, the trigger asks for another pass soon (§4.4).

**Contract test suite**: an abstract class (`LocalSyncStoreContract`) with all the tests of this section, in a `testFixtures` *source set*
(`java-test-fixtures` plugin) so that `app` inherits it in its instrumented tests with the Room implementation. `syncclient` passes it with
`InMemoryLocalSyncStore` (on top of `domain`'s `InMemoryNoteStore`, which is also the `NoteStore` of `NoteService`: a single store for both things, as
Room will do). If `testFixtures` does not work with AGP in `androidTest`, alternative: the suite in a `:syncclient-contract` module with JUnit in `implementation`.

### 4.3 Two-phase execution: `runSync`

```
snapshot  →  buildSyncInput  →  transport.readAll  →  planSync  →  PHASE 1: transport (Upload, DeleteRemote, in the plan's order)
                                                              →  PHASE 2: local commit (one transaction, with the bases)
```

- **The bases are never stored before the store has what they say.** If they were written first and the upload failed, the next pass would see the local note
  as "equal to the base" and would apply the store's old version on top: the edit would be lost. That is why phase 2 comes after phase 1.
- If phase 1 fails halfway: **nothing local is written**. The uploads already done are idempotent; the next pass converges.
- **There is never network inside the Room transaction** (it would block the editor's writes for up to 20 s).
- A plan with an error (`NOTHING_READABLE_ERROR`) writes nothing in either phase.
- `runSync` does not use `execute()` from `android/sync` (which interleaves and is not transactional); `execute` stays for the vectors and their tests.
- It returns `SyncOutcome`: counters (uploads, received, deletions applied, conflicts, omitted, skipped due to concurrent edit) or a `SyncFailure` (§4.5).
- One pass at a time: the caller guarantees it (§4.4); `runSync` is not reentrant and does not try to be.

### 4.4 When it syncs: `SyncTriggerPolicy`

A pure state machine, with an injected clock; `app` feeds it events and obeys its decisions (`RunNow`, `RunAt(instant)`, `Nothing`).

| Event | Decision |
|---|---|
| No profile configured | `Nothing` always |
| `AppOpened` | `RunNow` |
| `Foreground` (returning to the foreground) | `RunNow` if the last **attempted** pass was more than 30 s ago |
| `LocalChange` (every editor save or state/color/order change) | `RunAt(last change + 5 s)`: each new change delays it |
| `PullToRefresh`, `ManualSync` | `RunNow`, with no waits or limits |
| `Periodic` (WorkManager, ≥15 min, with network) | `RunNow` |
| `SyncFinished(outcome)` | if any event arrived during the pass, one repetition; if there were actions skipped due to concurrent edit, `RunAt(+5 s)` |

- **One pass at a time**: an event with a pass in progress does not launch another; it marks "repeat when finished" (a single repetition, however many events arrive).
- **After a failure** (any): the automatic ones (`AppOpened`, `Foreground`, `LocalChange`, `Periodic`) wait at least **60 s** from the failure. The manual ones
  never wait. There is no growing retry **(user: silent)**.
- The policy knows nothing about Android or WorkManager: `app` translates `RunAt` into a `Handler`/coroutine while in the foreground, and WorkManager requests `Periodic`.

### 4.5 Errors and state for the UI

`classifyFailure(Throwable | planError): SyncFailure`, with `kind`:

| `kind` | When | What the user will see (text in `strings.tsv`) |
|---|---|---|
| `OFFLINE` | `UnknownHostException`, `ConnectException`, `SocketTimeoutException`, `InterruptedIOException` from `callTimeout`, no route | "Offline" |
| `UNAUTHORIZED` | 401 (server: token; WebDAV: user or password) | "The server rejected the token" / "Wrong WebDAV user or password" |
| `KEY_CHANGED` | `NOTHING_READABLE_ERROR` | "This profile's key changed: paste the new code" |
| `SERVER_ERROR` | 5xx, 404 of the base path, response that is not JSON/XML | "The server responded with an error" |
| `TOO_LARGE` | 413 | "A note is too large for the server" |
| `UNSUPPORTED_FORMAT` | `SyncFormatException` for unknown format | "The store has notes from a newer version of Aldune: update the app" |
| `UNKNOWN` | everything else | "Could not sync" + technical detail only in Settings |

- Classification is **by exception type and, if needed, by the HTTP code in the message**, with tests that use the real transports against MockWebServer
  (not messages written by hand in the test). §4.6 if the message is not enough.
- `SyncStatus` (what `app` stores): `lastSuccessAt`, `lastAttemptAt`, `lastFailure`, `running`. `formatStatus(status, now): StatusLine` returns **keys and
  numbers**, not text: `icon` (`NONE`, `OK`, `SYNCING`, `OFFLINE`, `ERROR`), `message` (an enum that `app` translates to `R.string`) and a `RelativeTime` for "… ago":
  `JUST_NOW` (< 1 min), minutes (< 60), hours (< 24), days (< 7) and, from there on, the date (which `app` formats with the `Locale`). `OFFLINE` carries the offline
  icon, not the error one (it is not the user's fault). Without a profile: `NONE` (not drawn). Never synced: "Not synced yet".
- The technical detail of the last error (the transport's message in English) is only seen in Settings and in diagnostics, **without note contents**.

### 4.6 Typed exception in `android/sync` (only if needed)

Today HTTP's 401 is an `IllegalStateException` and the rest are `IOException` with the code in the message. If classifying by message turns out to be fragile (e.g. WebDAV does not
distinguish it), **one** change in `android/sync` is allowed: `SyncTransportException(val status: Int?, message)` inheriting from the type already thrown in each case,
**with the same message**, and touching nothing else. The 119 tests of `sync` and the vectors must stay green.

### 4.7 Validating the pasted profile code

`parseProfileInput(pasted: String): ProfileParse`:
- Cleans what copying from a chat or an email adds: spaces, line breaks and tabs **anywhere** (base64url has none), quotes or
  `<>` around it.
- `ProfileCodes.decode`. Result:
  - `Server(url, scope, tag, noteIds, profileName, key)`: asks for the token.
  - `WebDav(url, …)`: asks for user and app password.
  - `NeedsTransport(…)` if the code carries no `Transport` (old codes): the UI asks to choose server or WebDAV and type the URL.
  - Errors: `EMPTY`, `NOT_A_CODE` (does not start with a known prefix), `INVALID` (broken base64/JSON, unsupported version, bad key),
    `FOLDER_NOT_SUPPORTED` ("This code is for a shared folder; on the phone use server or WebDAV").
- `validateCredentials(transport, url, token | user+password)`: absolute `http`/`https` URL (`http` is allowed: the home server goes over the LAN), token or
  user non-empty after trimming; for the token, if the whole `ALDUNE_SYNC_TOKEN=…` line was pasted, what comes after the `=` is kept (it is the typical mistake that the
  401 message already warns about).
- The key is never logged or shown.
- "Test connection" (in `app`) = `transport.readAll()` + `classifyFailure`; it writes nothing.

### 4.8 First connection and disconnection

- `firstConnectDecision(snapshot): Proceed | AskUser(noteCount)`: asks if there is **any note** in any state (active, archived or trash).
  Tombstones without notes: they do not ask; they are discarded silently with `forgetSyncState` + deleting the tombstones (they were deletions of notes the profile does not know).
- "Upload": the profile is stored and a normal pass is launched (the engine uploads the local notes with no base: there is no conflict because the store does not have those ids).
- "Discard": `discardAllLocal()` **without creating tombstones** (if they were created, they would upload signed deletions to the profile) and then the pass.
- "Disconnect": deletes key, token and password (in `app`) and calls `forgetSyncState()`: the bases belong to that profile and are not valid for another. The notes stay.
  On reconnecting, with local notes, it asks again.

## 5. `app` (afterwards, locally)

1. Room v2: `sync_conflict` table (noteId, detectedAt, winning version: date + device + tombstone; losing: encrypted with `NoteCipher`, same model
   as the text) and migration 1→2 with its test. `RoomLocalSyncStore` (on the same database as `RoomNoteStore`) that passes `LocalSyncStoreContract` on the
   device.
2. Secrets: sync key, token and WebDAV password wrapped by the Keystore in `noBackupFilesDir` (parent spec §4.2); `deviceId` and profile (without secrets) in
   `SettingsStore`.
3. Settings → Sync: paste code, token or user+password, "Test connection", scope (read-only), last sync, last error, "Sync now",
   "Disconnect" (with confirmation). First-connection dialog. Notice that the system backup does not keep the notes: recovery is pasting the code again.
4. List: status icon in the bar with its detail; pull to sync; lock and conflict on the row; protected note on opening → read-only notice.
5. Triggers: `ProcessLifecycleOwner` (open, foreground), `NotesViewModel` saves (`LocalChange`), periodic 15-min WorkManager with
   `NetworkType.CONNECTED` that calls the same function and respects the single pass (a process `Mutex`).
6. New texts in `android/strings.tsv` with the five languages and `python android/scripts/gen_strings.py` (+ `--check`).
7. Test the editor with Gboard **and** a third-party keyboard after the changes (saving triggers sync: it must not be noticeable while typing).

## 6. Verification (gates)

1. `./gradlew test` green: the previous 414 (core 82, sync 119, text 100, domain 113) **without losing any** + the new ones of `domain` and `syncclient`.
   The vectors (`tests/vectors/`) are not regenerated or touched; `kotlin-output.json` changes with every run: discard it before committing.
2. `syncclient` (JVM): tests for §4.1-§4.8, including an **end-to-end** one with `InMemoryLocalSyncStore` + `NoteService` + the in-memory transport double
   from `android/sync`, that shows: a note created here uploads; a note from the "PC" is applied with its author and **is not re-uploaded** on the next pass; opening and closing without
   editing generates no upload (E28); CRLF comes back the same; a protected note uploads with `text ""` even if the `Note` carries text; the higher version wins with two of the same id;
   unsigned deletion → trash and never deletes; concurrent edit during phase 1 → not overwritten and the next pass saves a conflict; network failure in
   phase 1 → nothing local written; two consecutive passes without changes → the second does nothing.
3. `app` (instrumented): `LocalSyncStoreContract` with Room, migration 1→2.
4. **Probes** against the CT 103 server with a **test profile** (never the user's real profile; the debug app has its own database): a note
   created on Android appears on the PC (with a test Windows installation or a temporary database, never `%LOCALAPPDATA%\Aldune`) and vice versa; provoked
   conflict (edit on both with the network cut); signed deletion; unsigned deletion (envelope written by hand into the test store); server off →
   "Offline" without notices; bad token → clear message; airplane mode and back; on the Pixel and on the tablet.

## 7. Tasks

| # | Task | Where |
|---|---|---|
| 1 | `domain`: protected-note fields and rules (§3) | Pure JVM |
| 2 | `syncclient`: §4.1-§4.8 with TDD and `LocalSyncStoreContract` | Pure JVM |
| 3 | Room v2 + `RoomLocalSyncStore` + contract on the device | Local |
| 4 | Secrets, profile `SettingsStore`, Settings → Sync, first connection | Local |
| 5 | Indicator, pull to sync, lock and conflict on the row, triggers, WorkManager | Local |
| 6 | Probes against CT 103 on Pixel and tablet| Local |

## 8. Risks

1. **`testFixtures` in `androidTest`**: if AGP does not consume it well, a `:syncclient-contract` module (§4.2). It is discovered in task 3.
2. **Phone clock out of sync**: versions are ordered by date. A phone that is ahead always wins; one that is behind loses. It is the same as on Windows and is not
   corrected in F5; if it shows up, it is noted.
3. **WorkManager on the Lenovo tablet**: some manufacturers delay or kill background jobs. No periodicity is promised (parent spec §2.6); it is checked in
   the probe and noted.
4. **LF in notes created on Android** (F4 §1.2/§6.3): probe 4 really checks it. If Windows rewrites CRLF and creates new versions, it is fixed on Windows.

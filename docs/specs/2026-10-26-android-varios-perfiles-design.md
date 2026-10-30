# Android: multiple sync profiles

Date: 26-10-2026. Status: implemented on the `android-varios-perfiles` branch (unpublished). First block of the sync expansion (the next ones: QR pairing and
pasting/sharing codes; background sync transparency; a pass over gaps in errors and conflicts).

## Goal

Let a phone be joined to **more than one sync profile at a time**. The case that motivates it: the user syncs all their notes with their
server (the "all" profile) and also joins the family profile, which shares only one note (the shopping list), hosted by another
person. Windows already allows it; Android today stores a single profile (`SyncController._profile`, `SyncProfileStore.load()` returns one).

## Decisions made with the user

1. **Each profile syncs according to its scope, as on Windows.** A note that falls into two scopes (the family list is also in
   "all") is uploaded to both stores. There is no "note from another profile" marker.
2. **Join only.** Android imports as many codes as desired. Creating and sharing profiles from Android (note picker, generating a
   key and code) is left for a later block; those profiles keep being created on Windows.

## Out of scope

- Sync format: it stays 4. `tests/vectors/` and `Aldune.Core` are not touched.
- QR, pasting/sharing the code, battery notice, "shared note" indicator inside the note.
- Changes to the sync server.

## Design

### 1. Data

- Each profile has a `profileId` (UUID, generated on connecting; the legacy profile uses the fixed id `default`, `LEGACY_PROFILE_ID`). `SyncProfile` now carries it. Transport, URL, scope and name are
  stored per profile.
- **Secrets:** one file per profile (`SecretVault`, with the `profileId` in the name), with the same Keystore wrapping and the same
  atomic write. The file format does not change.
- **State** (last success, attempt, failure): per profile.
- **`sync_base` and `sync_conflict`:** become keyed by `(profileId, noteId)`. Room migration with a test.
- **Notes, tombstones and reminders** remain global: they belong to the device. A tombstone created when deleting a note is uploaded by each
  profile whose scope included the note (the planner decides it, as today).
- **`deviceId`** is unique per installation and still survives disconnecting.

### 2. Migrating what exists

The current single profile becomes the first one with the fixed id `default` (the Room migration is SQL and cannot invent a UUID; the usual secrets file `syncsecrets.bin` is not moved) and its preferences, its secrets file, its bases and its conflicts are moved to it. No manual steps. An installation with no profile stays without a profile. If the secrets file does not
open (`VaultState.Unreadable`), the migrated profile keeps that same state and it is resolved again by pasting the code, as today.

### 3. Passes

- `SyncController` keeps the list of profiles and goes through the existing ones **one by one**, inside the same `passLock`. One profile's
  failure stays in its state and does not stop the others.
- `LocalSyncStore` is instantiated per profile (the Room view receives the `profileId` and applies it to bases and conflicts). `runSync` and the
  planner do not change: they keep working with a single store.
- A single `SyncTriggerPolicy` for the whole app. Each trigger (open, foreground, local change, manual, periodic) makes one pass that
  goes through all the profiles. The background worker does not change its schedule.
- `SyncStatus` and `secretsUnreadable` become per profile; the list header uses an aggregate status (`aggregateStatus`) and each note's signal is the oldest base among the profiles that have it.

### 4. First connection of an additional profile

Today, when connecting with notes already on the device, it asks "upload or discard", and discarding **deletes all local notes** without
tombstones. With a second profile of "chosen" scope that would delete the personal notes.

- The first profile behaves as today.
- On an **additional profile** there is no discard option or question: only what the scope includes is uploaded and what is in the
  store is downloaded. `firstConnectDecision` receives whether it is the first profile.
- A local note with the same id as one in the store is resolved by the existing planner (conflict or the most recent wins), as in
  any pass.

### 5. Disconnecting

One profile is disconnected without touching the others: it deletes its secrets, bases and conflicts and leaves the notes and the tombstones. When disconnecting the
last one, the state goes back to the current no-profile one (`policy.setProfileConfigured(false)`, the periodic job is cancelled).

### 6. Interface

- Settings → Sync becomes a **list of profiles**: name, type, scope (`scopeText`) and last status, with a global "Sync
  now" and a per-profile "Disconnect".
- "Add profile" opens the current flow (paste code → choose transport → credential → Connect).
- A profile with unreadable credentials is marked in its row.
- The new strings go in `android/strings.tsv` (five languages) and are regenerated with `python android/scripts/gen_strings.py`; `--check`
  must pass.

### 7. Conflicts

The key of a stored conflict is `(profileId, noteId)`. If a note has a conflict in two profiles, the current interface (per note)
shows the most recent one, and **resolving it clears only that one**: the one for the note in the other profile stays marked and appears next.
Decision of 26-10 (delegated by the user): the losing versions from two stores can be different, and clearing both at once
would silently lose one. It costs at most one more resolution, and the next pass syncs the result in both profiles. A test
fixes this behavior before implementing it.

## Tests

New logic with test first, on the JVM (`android/syncclient`, `./gradlew test`):

- Two profiles over the same in-memory store with overlapping scopes (all + one note): the bases do not get mixed, each pass uploads
  and downloads its own, and a failure in one does not affect the other.
- Deleting a shared note: the tombstone reaches both stores.
- First connection of the additional profile: it does not offer to discard and does not delete notes.
- Disconnecting one profile leaves the others and the notes intact.
- Migration of the single profile's preferences and secrets (in `app`, instrumented or Robolectric depending on what is already there) and Room
  migration of `sync_base`/`sync_conflict`.

Interface: debug build on the Pixel with its own data and a test server/WebDAV (no real notes; no screenshots of the home screen
or of notifications). To check: adding a second profile, that the personal notes survive, disconnecting one and
keeping the other, and the migration from an installation with one profile.

## Documentation when finished

`docs/SYNC.md` (Android supports several profiles; step by step for the family note), `docs/STATUS.md`, `docs/MOBILE_PORT.md` if anything of what was
ruled out changes, `DECISIONS-2026-10-26.md` (QR) and the memory per the protocol.

# Aldune for Android — product design, architecture and phases

> 2026-10-17. Replaces the "phase 0 spec" that `docs/MOBILE_PORT.md` §7 asked for (that one was for MAUI). It takes precedence over §3 and §7 of
> that document; the technology and distribution review is in its §13. The product decisions marked **(user)**
> were made by the user when defining the spec; the rest are reasoned defaults that can be discussed.

## 0. In one sentence

A single **Kotlin + Jetpack Compose** app, adaptive (Pixel 10a with Android 17 (API 37; corrected on 2026-10-17, it used to say 16) and Lenovo Tab P11 with Android 14), `minSdk` 26,
`targetSdk` 36, GPL-3.0, publishable on F-Droid, that does three things: **capture in two seconds, browse and manage the PC's notes,
and remind**. It speaks sync format 4 without changing it. Phone and tablet have **the same features**; only the layout changes.

## 1. What Core does today and constrains the design (read from the code on 2026-10-17)

- **Note** (`Note.cs`): text, color (hex), creation/modification, state (active, archived, trash), tags (max. 20, no
  duplicates ignoring case), position in the deck (`DockPosition`, `double?`), `ScreenOrigin` (Android does not use it but **keeps it
  and returns it as is**: if it emptied it, the PC would lose which screen the note belongs on), protected + protected content.
  **All of that travels**; reminders, separately checked tasks (`TaskCompletion`), file link, window positions,
  conflicts and custom themes do not travel.
- **Text format**: plain text. Title = first line (1 to 6 `#` are stripped when displaying it; the stored text keeps them).
  Task = indentation + `☐`/`☒` (or `☑`, read-only) **followed by a space**; checking one writes `☒`. Bullet = indentation + `→ `.
  Enter continues the list and on an empty line ends it; Backspace removes the whole prefix; pasting `- [ ]`/`- [x]` gives checkboxes.
  There is no bold, no links and no rendered Markdown. Windows joins lines with CRLF.
- **Search** (`NoteSearch.cs`): substring over the full text, ignoring case. **Finding:** the comment says it ignores
  accents and the code does not (`CurrentCultureIgnoreCase`): "cafe" does not find "café". Tags are filtered separately.
- **Notes linked to files**: the link is local to the PC. Those with sync disabled (the default) **do not reach the store**;
  those with it enabled arrive as a normal note and, if Android edits them, the PC writes the change to the file with its fingerprint rule.
- **Reminders**: one per note, one-off, local (`NoteReminder`). Shortcuts: +1 h, tonight 20:00, tomorrow 9:00 (`ReminderPresets`).
- **Trash**: it is a state that travels. It is purged after 30 days by default. Deleting for good is only possible from the trash. Deleting or purging
  leaves a **local** tombstone (`SyncTombstone`); the signed envelope is created by sync on the next pass.
- **Version author**: every local edit (text, color, state, order, tags) sets `SyncAuthorDeviceId = NULL`, so that the
  version is uploaded with this machine's `deviceId` (`NotesRepository.cs`, e.g. :206, :594). Android has to do the same.
- **Tags in the comparison**: `NoteEquivalence.SameContent` compares them sorted and **case-sensitively** (ordinal). Android
  stores tags as they arrive (deduplicating case-insensitively when editing, like `SetTags`) so as not to create false conflicts.
- **Sync engine** (`SyncService.cs:300-451`): reads the **whole** store on every pass (no cursor), classifies (decrypts / authentic
  deletion / unsigned deletion), keeps the highest version per note, applies the scope (all, chosen, tag), handles
  unsigned deletions (trash, never delete), compares with the local state using **per-note bases** to tell a real conflict from a change
  on one side only, saves the losing version as a `SyncConflict` and sets the bases at the end. If there is at least one (non-deleted) note in
  the store and **none** decrypts, it aborts without writing (the key changed); if the store only has deletions, it does not abort
  (`SyncService.cs:556-584`). With the **same date** on both sides there is never a conflict: `deviceId` breaks the tie and that is it (:462).
  A conflict can also be **edit versus signed deletion**: if the deletion wins, the note disappears and the conflict is left with no
  note to hang from. Decision and I/O are mixed together; there is no injectable clock nor any way to inject the transport.

## 2. Screens: what each one does on phone and on tablet

The rule: **same features, different layout**. Material 3 Adaptive's `NavigableListDetailPaneScaffold` is used (stable 1.3.0):
in a compact window, one pane at a time; in medium/expanded, list and note side by side. The decision is not "is it a tablet" but the
**window size class** (a Pixel in landscape or in split screen behaves as it should).

### 2.1 List (home screen) **(user: list + big button)**

| | Phone (compact) | Tablet (expanded) |
|---|---|---|
| Layout | Full-screen list, search at the top, big "+" button at the bottom | List on the left (~360 dp), open note on the right |
| Rows | Color strip of the note + title + one-line preview + task counter (`done/total`) + icons (protected, reminder, conflict) | Same |
| Filters | Tabs or chips: Active · Archived · Trash; tag chip | Same, with room for the fixed tag list |
| Order | The deck's (`DockPosition`); reorder with long press and drag | Drag with finger or mouse; keyboard (Alt+↑/↓) |
| Per-note actions | Swipe: archive / trash; menu: color, tags, reminder, protect | Context menu (right click) in addition to swipe |

- **Search**: substring over text **and tags**, **case- and accent-insensitive** (on Android accents are folded with
  `Normalizer`; it is better than Windows today and is what Core's comment promises). Protected notes do not appear in search (their text is not
  known without the password). The Windows finding is noted in §9, not fixed here.
- A discreet **sync indicator** in the bar: last sync, error with clear text and a "Sync now" button. Pulling down syncs.

### 2.2 Note (editor)

| | Phone | Tablet |
|---|---|---|
| Layout | Full screen; back returns to the list | Right pane; the list stays visible |
| Bar | Color, tags, reminder, protect, archive, trash | Same, in the pane's top bar |
| Editing bar | Above the keyboard: checkbox, bullet, indent −/+, move line up/down | Same; and physical keyboard shortcuts |

- The editor works on the **text as is** with the rules of §1 (ports of `TaskLines`, `BulletLines`, `ListPrefix`,
  `TaskLists`, `NoteTitleHelper`). Tapping a `☐` checks it. Dictation is possible with the system keyboard.
- **Line breaks**: internally `\n`; on saving they are joined again with the style the note came with (CRLF if it came from Windows). And
  **`UpdatedAt` only changes if the user really edited**: opening and closing a note must not generate a new version, because it
  would trigger uploads and false conflicts (`NoteEquivalence.SameContent` compares the exact text).
- Autosave (when typing pauses and on leaving); there is no Save button.
- **Color**: palette of note themes from Core (`NoteThemes`); the border and the label are derived (`NoteColorDerivation`). A color that
  comes from a custom Windows theme is respected as is (its hex is painted and the rest is derived).
- **Tablet shortcuts with keyboard**: Ctrl+N new note, Ctrl+F search, Esc close the note; the line-editing ones, the same as on
  Windows (they are listed in phase F4 by reading `src/Aldune`).

### 2.3 Protected notes **(user: open and edit with a password; biometrics later)**

- In the list: lock icon, placeholder title ("Protected note"), no preview.
- Opening asks for the password; the derivation (PBKDF2 600,000 it.) runs **off the UI thread** with an indicator. Unlocked only
  in memory; it locks again on leaving the note and when the app goes to the background.
- A note can be protected (password ≥ 4 characters, twice) and unprotected (asks for the password).
- They never enter the widget, search or a reminder's text.
- **Outside v1:** biometric unlock (storing the derived key in the Keystore with authentication is a separate security design).

### 2.4 Conflicts **(user: notice + restore/discard)**

- The note shows a strip "This note changed in two places at once" with the other version visible (read-only) and two buttons:
  **Keep this one** (discards the conflict) and **Use the other** (restores the loser as a new version, like `RestoreConflict`).
- "Merge" stays in Windows (or in a later phase). Conflict icon in the list row.
- **Conflicts without a note** (a deletion won over an edit): the list shows an "N conflicts" notice that opens a short list with
  those cases and the same two buttons ("Recover the note" restores the edited version; "Discard"). Without this they would be unreachable.

### 2.5 Trash and archive

- Archived and Trash are list filters. In the trash: Restore and Delete forever (asks for confirmation; leaves a signed deletion).
- Automatic purge after 30 days, same rule as Windows (setting in Settings → Notes).

### 2.6 Settings

- **Sync** **(user: paste code + token; server and WebDAV)**: one profile in v1. Paste the profile code
  (`aldune-profile-v2:`, or the old ones), and depending on its transport: **server** (URL from the code + token) or **WebDAV** (URL + user +
  app password). The shared folder is not offered (if the code is a folder one, a clear message). "Test connection" button.
  Shows scope (all / chosen / tag), last sync and the last error. "Disconnect" deletes the key and token from the device
  (it does not touch the store).
- **When it syncs** (no periodicity is promised): on open, on returning to the foreground, a few seconds after stopping editing,
  on pulling the list, and in the background "when the system feels like it" (WorkManager, periodic ≥15 min with network).
- **Appearance** **(user: Aldune's palette)**: Dark / Light / Follow the system, with the `AppPalette` palette (Pastel and Midnight,
  if offered, are derived just like on Windows). No Material You.
- **Language** **(user: all five)**: en, es, de, fr, pt-BR. The system's by default; on Android 13+ the per-app language picker is used.
- **Notes**: default color of the new note, trash days.
- **Capture**: persistent notification with "+" (off by default); widget: "show more than the title" (off, with a warning).
- **Reminders**: "Notify at the exact time" (asks for the exact alarms permission; see §2.9).
- About: version, GPL-3.0 license, link to the code, diagnostics (copy the sync log, without note contents).

### 2.7 Quick capture

All of them open the editor with a new note and the keyboard out; the note is created when the first character is typed (if you leave without typing,
no empty note is left):

1. The list's "+" button.
2. **Icon shortcut** (long press on the icon: "New note"; static shortcut).
3. **Share to Aldune** (`ACTION_SEND` of text): new note with the shared text; if it is a link with a subject, the subject goes as the title.
4. **Quick Settings tile** "New note" (`TileService`).
5. **Persistent notification with "+"** (optional, off by default **(user)**; asks for notification permission on Android 13+).
6. **Widget** (next section).

### 2.8 Widget **(user: titles; text optional)**

- **Deck widget**: color strips with the title of each active note in deck order and a "+" button. Small on phone (a few
  strips), large on tablet (grid). Tapping a strip opens that note; "+" opens a new note.
- **Titles only by default**; the "show more text" setting adds the preview, with a warning that it will be visible on the home screen.
  Protected notes **never** appear (not even their title: a strip with a lock).
- **Correction to `MOBILE_PORT.md` §3.3**: on Android the widget (Glance) is painted from the **app's process**, which can
  decrypt with the Keystore key just like the app itself. **No cleartext snapshot on disk is needed.** The text that is
  painted does reach the launcher (unavoidable: it is visible), which is why the titles setting still makes sense.

### 2.9 Reminders (local in v1) and notifications

- One per note, as on Windows, with the same shortcuts (+1 h, tonight 20:00, tomorrow 9:00) and a date/time to choose.
- **Local to the device** and the interface says so ("This reminder only notifies on this device"). They do not travel to the PC (format 4).
- Notification with: title (or "Protected note"), up to 3 pending tasks if the note is not protected, and actions **Done**
  (removes the reminder), **Snooze** (+1 h / tomorrow 9:00) and **Open**.
- **Exact time** **(user: exact if allowed)**: by default an inexact alarm (it may arrive minutes late with the phone idle);
  the setting asks for `SCHEDULE_EXACT_ALARM` (on Android 14+ it comes denied and is granted in system Settings). `USE_EXACT_ALARM`
  is not used (Play reserves it for clock/calendar apps; F-Droid has no objection, but it is not wise to close the door on Play).
- They are rescheduled on reboot (`BOOT_COMPLETED`) and when the time/zone changes. If the note goes to archive or trash, it does not notify (like Windows).

### 2.10 What stays outside v1

Dock/fan/windows/screens (never), shared folder as a transport, more than one sync profile, **starting** a key rotation
or revoking access (Android only **suffers** it: if nothing decrypts, the message "this profile's key changed: paste the new code"), merging
conflicts, biometric unlock, locking the whole app **(user)**, pairing QR **(user: paste in v1)**, custom note
themes, theme editor, export to Markdown, notes linked to files on the phone, synced reminders (format 5),
iOS. System backup: **excluded** (see §4.3).

## 3. Architecture and modules

```
android/
  settings.gradle.kts        (Gradle root once the app exists: include :core, :sync, :text, :app)
  core/                      DONE. Pure Kotlin/JVM: format 4, encryption, protected notes, profile codes, version ordering
  sync/                      Pure Kotlin/JVM: sync engine (plan + execution), LocalStore and SyncTransport interfaces,
                             HTTP and WebDAV transports (OkHttp). JUnit tests against tests/vectors/sync-scenarios.json
  text/                      Pure Kotlin/JVM: text format (tasks, bullets, lists, title), color (themes, OKLCH,
                             derivation), deck order, search. Tests against tests/vectors/text-*.json
  app/                       Android: Compose + M3 Adaptive, Room, Keystore, WorkManager, Glance, notifications
  fastlane/metadata/android/<locale>/   F-Droid metadata (title ≤50, short ≤80, long ≤4000, changelogs ≤500)
```

- **Why `sync` and `text` are pure JVM**: they are tested with `gradle test` without the Android SDK (Linux CI, no emulator) and they are
  where the project's testing culture lives. `app` only glues pieces together and paints.
- **Until the app exists**, `android/sync` is its own build with `includeBuild("../core")` (coordinates
  `app.aldune:aldune-core`): there is no need to touch `android/core`. When `app` is created, it moves to a common root.
- **Dependencies** (all free and on Maven Central or Google Maven, which F-Droid accepts): AndroidX/Compose/M3 Adaptive, Room,
  WorkManager, Glance, OkHttp (+ MockWebServer in tests), kotlinx-serialization. **Forbidden**: Google Play Services, Firebase,
  ML Kit, Crashlytics or any analytics SDK (F-Droid rejects them). If there is ever a QR, ZXing.
- **Texts**: a single table (e.g. `android/strings.tsv` with key + five languages) from which a script generates
  `values*/strings.xml`; the script fails if a language is missing, just like the `Strings.T` rule on Windows.

### 3.1 The sync engine in Kotlin

Same behavior as `SyncService.SynchronizeCore`, but **separating decision from effects**, which is what allows testing it with vectors:

1. `SyncInput` = local state (notes, local deletions, bases, authors, excluded ids) + store objects + scope settings + `deviceId`.
2. `planSync(input, key): SyncPlan` — pure function: list of actions per note (upload note, upload deletion, apply note, apply deletion,
   trash for unsigned deletion, delete from the store, save conflict) + new bases, or an error (nothing decrypts, unknown format).
3. `execute(plan, local: LocalStore, transport: SyncTransport)` — applies them in order. In the app, `LocalStore` is implemented by Room inside a
   transaction; the bases are saved at the end, as in Core.

Core is replicated **including its quirks** (the vectors pin them down; they are not "fixed" in Kotlin): if `format` is missing it is 4; the scope
asymmetry of local deletions (§9); that an unreadable object aborts the pass over HTTP and is skipped over WebDAV. **The unknown format
is handled in the transport/deserialization**, not in `planSync`, which already receives valid envelopes.

Rules that must not be forgotten when building the local envelopes:
- A protected note uploads with `text: ""` **always**, even if it is unlocked in memory (in Core it is safe because the repository
  returns empty text; on Android it has to be forced, or the cleartext would travel inside the envelope).
- Author: `syncAuthorDeviceId` if the version came from outside and has not been touched; otherwise, the device's own `deviceId` (§1).
- Several versions of the same id in the store: the highest according to `SyncVersion.compare` wins (`SyncService.cs:325-329`). It does not come from a
  vector (the folder stores one file per id); it goes in a Kotlin test of its own.

## 4. Storage

### 4.1 Room, with the text encrypted at rest

- **Room** (the system's SQLite). Android's schema does **not** have to be that of Windows' `notes.db`: the contract between devices is
  sync, not the database. Room does have migrations (unlike the Windows rule): the schema is exported and each version carries its tested migration.
- Tables: `note` (id, `textCipher`/`nonce`/`tag`, color, createdAt, updatedAt, state, tags, dockPosition, screenOrigin, isProtected,
  `protectedContentJson`, `syncAuthorDeviceId`, `lineEnding`), `tombstone`, `sync_base`, `sync_conflict` (encrypted losing version),
  `reminder` (noteId, dueAt).
- **Per-row encrypted text** with AES-256-GCM and a random **data key** stored wrapped by an **Android Keystore** key
  (without user authentication, so that the widget and background sync work). Same model as Windows with DPAPI. SQLCipher is not needed.
- Search: notes are decrypted into an in-memory cache and searched there (there are hundreds, not millions; Windows does the same).

### 4.2 Secrets

Sync key, server token and WebDAV password: wrapped by the Keystore, in the app's private storage. Never in
`SharedPreferences` in cleartext. `EncryptedSharedPreferences` is deprecated: it is not used.

### 4.3 System backup

**Everything excluded** (`dataExtractionRules` and `fullBackupContent`): Keystore keys do not travel in the backup, so restoring the database
would give unreadable data. Recovery is **pasting the profile code again** and syncing. The sync wizard says so.

## 5. Phases and verification criterion

| # | Phase | Where | Deliverable | Verification (gate) | Sessions |
|---|---|---|---|---|---|
| F0 | **Scenario vectors** | This PC (Windows: `SyncService` uses DPAPI and does not run on Linux) | `tests/vectors/sync-scenarios.json` + generator in `Aldune.Core.Tests` (§6) | `dotnet test` green; each scenario cites the line of `SyncService.cs` it covers; vectors README updated | 1 |
| F1 | **Kotlin sync engine** | Isolated (pure JVM) | `android/sync`: `planSync`, `execute`, in-memory doubles | `gradle test` green with **all** the scenarios; `android/core` untouched | 1 |
| F2 | Transports | Local or isolated | HTTP and WebDAV with OkHttp | MockWebServer with the server's real responses (401, 404, 413, 5 MB); and a real sync against `Aldune.SyncServer` locally | 1 |
| F3 | Text, color, order, search | Isolated (pure JVM) | `android/text` + text vectors generated from Core (input, cursor, operation → output) | `gradle test` green against the text vectors | 1-2 |
| F4 | App: skeleton | Local (Android Studio) | Room + Keystore + adaptive list/note + editor, **without sync** | Compose tests on device + `adb` screenshots on Pixel and Tab (mobile probe, §7) | 3-4 |
| F5 | App: sync | Local | Sync settings, triggers, WorkManager | Probe: note created on Android appears on the PC and vice versa; provoked conflict; signed and unsigned deletion; real server on CT 103 with a test profile | 2 |
| F6 | Capture | Local | Share, shortcut, tile, "+" notification, Glance widget | Checklist on both devices, with the phone rebooted; test that the widget never paints a protected note | 2 |
| F7 | Reminders, protected notes, conflicts | Local | §2.3, §2.4, §2.9 | Notification with the app closed and the phone idle (exact and inexact); PBKDF2 measured on the tablet | 2 |
| F8 | Publish | Local | Signing, reproducible build, fastlane in 5 languages, IzzyOnDroid and F-Droid | `fdroid build` reproduces the signed APK; confirm with the user before publishing | 1-2 |

**Total ≈ 14-17 sessions** (F1 counts as one). It matches the range in `MOBILE_PORT.md` §13.2.

## 6. How the scenario vectors are generated from Aldune.Core (F0; not implemented)

**Principle:** Core's behavior is not touched. The real engine is observed, with a real repository and a real store, and what happens is
recorded. No reimplementing the logic in the test.

1. **Where**: `tests/Aldune.Core.Tests/SyncScenarioVectorsTests.cs`, with the same mechanism as `SyncVectorsTests`: each run
   **generates and compares** against `tests/vectors/sync-scenarios.json`; with `ALDUNE_REGEN_VECTORS=1` it rewrites it.
2. **Setup of each scenario**: temporary database (`NotesDatabase` in `%TEMP%`, never the user's), `NotesRepository`,
   temporary `SettingsService` with a fixed `deviceId` (`"android-pc-a"`…), fixed key `00…1f` (the one from the format vectors) and
   **folder** transport over a temporary folder. `SyncService` does not allow injecting the transport (`CreateTransport` is private,
   :728), so it is **not wrapped**: the `objects/` folder is photographed before and after. Each `Write` re-encrypts with a new nonce,
   so a changed file = upload, a new one = upload, a missing one = deletion from the store. That way no seam is needed in Core.
3. **Initial state** described with data, not code: local notes (with author and base), local deletions, excluded ids (linked without
   sync) and store objects (header + cleartext content + `signed` + which key: the vectors' or "another"). The generator writes
   those objects with `SyncEnvelopeCodec` and also stores their `envelopeJson`, so that Kotlin reads **exactly** the same bytes.
4. **Execution**: one `Synchronize` pass. **Observed result**: actions (the folder diff + the database diff before and
   after per note: applied, to trash, deleted, unchanged), saved conflicts (note, winner, losing version), final bases,
   final state of the store (headers + decrypted content) and the error if there was one.
5. **Clock**: `UtcNow` is not injectable and **no seam is added**. The scenario's dates are fixed and in the past; the "unsigned deletion
   with a future date" rule uses 2099; anything that depends on "now" (conflict date, `LastSyncAt`) **does not enter the vector**.
6. **File format** (per scenario): `name`, `covers` (lines of `SyncService.cs`), `settings` (`deviceId`, `scope`, `tag`,
   `noteIds`), `local` (`notes`, `tombstones`, `bases`, `excludedIds`), `remote` (`envelopeJson` + `plain` to read by eye),
   `expected` (`error`, `actions` ordered by `noteId`, `localAfter`, `remoteAfter`, `conflicts`, `basesAfter`). Dates: compare as instants.
7. **Minimum scenarios** (each one, one line of the engine):
   **E1** Local only → upload. **E2** Remote only → apply. **E3** Equal → nothing. **E4** Local changed, remote = base → upload without conflict.
   **E5** Remote changed, local = base → apply without conflict. **E6** Both changed with different content → conflict, the higher version wins.
   **E7** Both changed with the same content → no conflict. **E8** No base and different → conflict (caution, old installation).
   **E9** Date tie with different content → ordinal `deviceId` breaks the tie and there is **no** conflict. **E10** Base + 1 ms (`OnlyMigrated`) → no conflict.
   **E11** Signed remote deletion → apply. **E12** Unsigned remote deletion → trash (E12b: note that does not exist here → nothing; E12c: already in
   the trash → nothing, without touching `UpdatedAt`). **E13** Unsigned with a future date → deleted from the store.
   **E14** Unsigned with a signed object of the same id → ignored. **E15** Local deletion → upload signed deletion. **E16** Trash travels as a state.
   **E17** Tag scope: note without the tag → does not upload. **E18** Note that left the tag here and is more recent → the
   remote one is not applied (E18b: the remote one is equal or more recent → it is applied and the base is set).
   **E19** With a tag, a remote deletion goes through anyway. **E20** "Chosen notes" scope. **E21** Linked without sync → neither uploaded nor touched by an unsigned deletion.
   **E22** Protected: uploads and downloads with `protectedContent`; same protected note on both sides → no conflict. **E23** No note decrypts (another key) → error, zero writes
   (E23b: the store only has deletions from another key → does not abort; they go through the unsigned-deletion path).
   **E24** An object with `format` 5: with folder it **is skipped** (that is what the vector fixes); HTTP's "clear error" is tested in the transport (F2).
   **E25** Only `dockPosition` changes → upload. **E26** Asymmetry of local deletions with tag scope (§9): what it does today is pinned down.
   **E27** Local edit versus a more recent signed remote deletion → conflict saved with the edit as the loser and the note deleted (and the other way around).
   **E28** `ScreenOrigin` and tags with different case travel and come back unchanged (they generate neither an upload nor a conflict).
   Key rotation: **out** (Android does not start it in v1; the "nothing decrypts" case covers it from the receiving side).

## 7. UI verification on Android (the "mobile probe")

The equivalent of `docs/WPF_PROBES.md`: Compose tests (`createAndroidComposeRule`) run **on the two real devices**
over USB, with a temporary database, that make real taps and save screenshots (`adb exec-out screencap`). Plus a
diagnostics screen (debug only) that runs the script from §9 of `MOBILE_PORT.md` (decrypt, sync against a local server, notification,
widget, PBKDF2 measured). It is documented in `docs/ANDROID_PROBES.md` in phase F4.

## 8. Distribution (summary; detail in `MOBILE_PORT.md` §13.6)

A single developer signing key. **Reproducible build from day one**, so that F-Droid publishes the APK signed by the
developer (and not one with its own key): that way GitHub, IzzyOnDroid and F-Droid are interchangeable without uninstalling, and there is only one key to
register in Google's developer verification (2027 worldwide).

## 8.1 User decisions on distribution (2026-10-17)

- **`applicationId`: `io.github.nachopola16.aldune`** (permanent). The code's Kotlin package remains `app.aldune.*`
  (`namespace` and `applicationId` are independent).
- **Visible name: "Aldune".** Checked on 2026-10-17: F-Droid has no app with that name and a web search finds
  no Android app called that. On IzzyOnDroid the web search was inconclusive: check by hand when requesting inclusion.
- **Signing key:** a single one (RSA 4096, 30-year validity), generated in F8 with `keytool`; the keystore and its password in KeePass and an
  encrypted copy off the PC. Never in the repository nor in a shared continuous integration environment. Losing it = not being able to update the app.
- **Google account:** nothing for now; limited distribution account (free, 20 devices) when needed for the two own
  devices, and full verification only before publishing for others in 2027.
- **Publishing:** first a signed APK in the GitHub *releases* + IzzyOnDroid; F-Droid when the reproducible build works.
- **Tools:** Android Studio on this PC before F4 (it ships JDK 21, which also solves Gradle not running with the system's Java 26).

## 9. Findings in Windows/Core that came up while designing (not touched in this phase)

1. `NoteSearch` says it ignores accents and does not (`CurrentCultureIgnoreCase`). Fixing it on Windows is a separate change, with a test.
2. **Local** deletions are filtered by scope ids but not by tag (`SyncService.cs:368-370`), unlike notes.
   It may be intentional; scenario E26 pins it down as is until it is decided.
3. **Probable data loss on Windows, already today (found in an independent review, checked by reading the code; a test reproducing it is missing):**
   if a **protected** version arrives via sync of a note that on that PC is **linked** to a file,
   `ApplySyncNote` does not look at the link and saves empty text; afterwards `LinkedFileService.Reconcile` (:102-126) sees pending text
   (`""` ≠ known fingerprint), does not check `IsProtected` and **writes the file empty**. Android is not needed: another Windows PC
   that protects that note is enough (it is not linked there). Fix it on Windows with a test first (e.g. `Reconcile` never writes from
   a protected note and remote protection unlinks or is rejected) **before publishing the Android client**. **Fixed (2026-10-17):** `LinkedFileService.Reconcile` does nothing with a protected note (it neither writes nor reloads the file; the link is kept), with
   two tests in `LinkedFileServiceTests`. Android can offer "protect" without that restriction.
4. Tags: SQLite `NOCASE` only folds ASCII, `OrdinalIgnoreCase` folds more: "Ñ"/"ñ" may count as two tags on one side and one on the other.

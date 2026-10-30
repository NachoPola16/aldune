# Aldune for Android — F4: app skeleton (no sync)

> 2026-10-17. Details row F4 of [`2026-10-17-aldune-android-design.md`](../../specs/2026-10-17-aldune-android-design.md) §5. That spec governs everything
> not said here. Starting state: F0-F3 done (`android/core`, `android/sync`, `android/text`), Android Studio Rabbit 1 installed,
> SDK with platform 37 (36 missing), no device connected yet.

## 0. Goal and limit

An app that **opens, creates, edits, sorts, archives and deletes notes on the phone and the tablet**, with the text encrypted at rest, Aldune's text
format (tasks, bullets, indentation) and the note color palette, **without sync, without reminders, without protected notes, without a widget**. It is the base on
which F5-F7 hang everything else, so what is expensive to change later (Room schema, encryption, boundaries between modules) is decided properly here.

Out of F4 even if it seems close: sync (F5), share/tile/widget (F6), reminders, protected notes and conflicts (F7), languages other than English and
Spanish **for new strings** (the five languages are done with the table of §3.1 of the parent spec; in F4 the table is created with all five from the start).

## 1. Split decision: a new pure piece, `android/domain`

The risk of F4 is not drawing screens, it is the logic of "what happens when the user edits" (when `UpdatedAt` changes, how line breaks are preserved,
how reordering works, what trash/purge do). If it lives inside Room and Compose it can only be tested on a device. Therefore:

```
android/
  domain/   NEW. Pure Kotlin/JVM. Note model, NoteStore (interface), NoteService (rules), in-memory NoteStore for tests,
            NoteCipher (interface). Depends on :text. JUnit tests without Android.
  app/      Android. Room (implements NoteStore), Keystore (implements NoteCipher), Compose, ViewModels. Almost no rules of its own.
```

`domain` can be built in isolation (it needs neither the SDK nor Android Studio) with the same method as F1-F3. `app` is done here, locally.

### 1.1 `domain`: contents

- `Note` (immutable): `id` (UUID), `text` (always with `\n`), `lineEnding` (`LF` | `CRLF`, the one it came with), `color` (hex as it arrives), `createdAt`,
  `updatedAt`, `state` (`ACTIVE`, `ARCHIVED`, `TRASHED`; in sync they travel as 0, 1, 2), `tags`, `dockPosition: Double?`, `screenOrigin: String`
  (kept and returned intact), `syncAuthorDeviceId: String?`. No protected-note fields in F4 (they arrive in F7; the schema already reserves them, §2.1).
- `NoteStore`: `observeAll()`, `get(id)`, `put(note)`, `delete(id)` (leaves a local tombstone), `tombstones()`. All synchronous on an I/O thread; the
  `Flow` is added by `app`. `InMemoryNoteStore` for tests.
- `NoteService(store, clock, deviceId, idFactory)` — **the clock and the ids are injected** (unlike Windows' `SyncService`) so that it can be tested:
  - `create(text?, color?, themeSettings)` → color by `NoteColorAssigner` looking at the colors of the active deck; **it does not create the note until the first character**
    (spec §2.7): `create` is only called on that first edit.
  - `edit(id, newText)` → normalizes `\r\n`→`\n` **only to work**, and saves with the original `lineEnding` (for a new note, `LF`; decided in §1.2).
    **If the resulting text is equal to the stored one, it neither touches `UpdatedAt` nor writes anything** (opening and closing does not generate a version). If it changes, `UpdatedAt = clock.now`
    and `syncAuthorDeviceId = null` (the version belongs to this machine; parent spec §1).
  - `setColor`, `setTags` (max. 20, no duplicates ignoring case, **keeps the form in which they arrived**; same as `SetTags` in Core: read it), `archive`,
    `trash`, `restore`, `deleteForever` (only from the trash; leaves a local tombstone), `purgeTrashed(days)` (30 by default), `move(id, beforeId?, afterId?)` using
    `NoteOrdering.between`/`spaced` (renumbers if it returns `null`).
  - Every change of state, color, tags or order is also a local edit: it sets `UpdatedAt` and `syncAuthorDeviceId = null`.
  - `list(filter, query, tag)` → notes sorted like the deck (`dockPosition`, then `createdAt`; those without a position go last: **read `NotesRepository.cs`
    to copy the exact order**), with `NoteSearch` (text and tags).
- Text rules that are **not** reimplemented: everything comes from `:text`.

### 1.2 Line break of a new note

A note created on Android carries `LF`. Windows already reads and writes both (`NoteText` joins with CRLF only when putting title and body together; the stored content stays
as it arrives). **This must be checked with a sync vector before F5**, not here: if Windows rewrote CRLF over an LF note it would create a new version without anyone
editing it. Noted as a risk (§6); F4 does not depend on it.

## 2. `app`: architecture

- **A single `Activity`**, Compose, `ViewModel` + `StateFlow`, no injection framework (an `AppContainer` by hand in `Application`; less code and nothing
  for F-Droid to audit). `minSdk` 26, `targetSdk` 36, `compileSdk` that of Android Studio. Kotlin and the Android plugin: **a single version for all
  modules** in `android/gradle/libs.versions.toml`.
- **Common Gradle root** (first task): `android/settings.gradle.kts` with `:core`, `:sync`, `:text`, `:domain`, `:app`; `sync` stops using `includeBuild`
  and moves to `project(":core")`. The tests of the JVM modules keep running with `gradle test` from their folder or from the root; the current 87+119+82
  must not be lost (they are checked before and after).
- **Gradle on this PC**: Android Studio ships its JDK 21 (`C:\Program Files\Android\Android Studio\jbr`). Use **the JBR** for `app` and keep the Temurin JDK 21
  for the pure modules if preferred; in any case, export `JAVA_HOME` in each command. No Gradle wrapper committed with binaries: it is
  decided in task 1 (the wrapper is a small `.jar` and F-Droid requires it to be verified; see §6).

### 2.1 Storage (Room + encryption)

Tables of the parent spec §4.1, with **all the columns already** (also those of F5-F7, so as not to migrate right away): `note`, `tombstone`, `sync_base`,
`sync_conflict`, `reminder`. In F4 only `note` and `tombstone` are used. `exportSchema = true`, `app/schemas/` folder committed, and **a migration test
from v1** from day one (even if the first migration is trivial; the infrastructure is what matters).

Encryption, in two separate layers with their own tests:
1. **`DataKey`**: 32 random bytes, generated once, **stored wrapped** (AES-GCM) by a key from the **Android Keystore** (fixed alias, `setUserAuthenticationRequired(false)`,
   `StrongBox` if it exists and otherwise normal hardware; **without** `setUnlockedDeviceRequired`, so that sync and widget work with the phone locked). The wrapper lives in a
   file in private storage, not in `SharedPreferences`.
2. **`NoteCipher`** (interface in `domain`, implemented with `DataKey` in `app`): AES-256-GCM per field, random 12-byte nonce, 16-byte tag, it encrypts the text; the note's id
   goes as **associated data** (AAD) so that a row copied to another id does not decrypt. `textCipher`, `nonce`, `tag` in separate columns (as on Windows).
   It is **local** encryption, independent of the sync one; it does not share a format with it.
- **What happens if the Keystore loses the key** (restoring a backup, deleting fingerprints, some manufacturers): the wrapper does not decrypt → the app detects it, **does not delete
  the database**, shows a clear notice («No se puede leer la base: la clave del dispositivo cambió», i.e. "The database cannot be read: the device key changed") and offers "Start from scratch" with double confirmation. It is tested with a
  Keystore double that fails. The real recovery is sync (F5): the notice says so.
- **System backup excluded** (`dataExtractionRules` and `fullBackupContent`): parent spec §4.3.
- **Search**: in-memory cache with the already-decrypted notes (parent spec §4.1); it is emptied when the app goes to the background for more than 5 minutes.

### 2.2 F4 screens

Those of the parent spec §2.1, §2.2 and §2.5 **without what belongs to F5-F7**: adaptive list (`NavigableListDetailPaneScaffold`), editor, Active/Archived/Trash filters, tag chip,
search, color and tags from the note menu, reorder by dragging, swipe to archive/trash, automatic purge on opening the app. Minimal Settings: appearance
(Dark/Light/System), default color of the new note, trash days, language. The sync indicator and the F5+ buttons **are not drawn** (no dead screens).

**The editor is the hard part** and it is designed here because Compose's `BasicTextField` does not offer a low-level keyboard hook like WPF's `TextBox`:
- State: `TextFieldValue` (text + selection). Every `:text` operation receives `(text, caret)` and returns `(text, caret)`; it is applied with the selection collapsed at the caret.
- **Enter and Backspace of the virtual keyboard**: they do not arrive as reliable key events. They are detected **by comparing the previous value with the new one** (done with `TextFieldState` + `InputTransformation`, not with `onValueChange`: see task 7 and `ANDROID_PROBES.md`): an insertion
  of exactly `\n` at the caret is equivalent to Enter (it is undone and `EnterContinuation` is applied if the line is a list line and the caret was at the end of the line); a deletion of exactly
  one character right behind a list prefix is equivalent to Backspace (`RemoveOnBackspace` is applied). With a physical keyboard (tablet) `onPreviewKeyEvent` is additionally used for Tab, Shift+Tab,
  Alt+↑/↓ and Ctrl+Enter. **This is validated on the Pixel and on the tablet with real keyboards** (Gboard, and a third-party one) before considering the task good: it is the biggest technical risk of F4.
- **Tapping a `☐`/`☒` checks it**: the offset of the tap is obtained with `TextLayoutResult.getOffsetForPosition`; if it falls on a task's glyph, `ToggleCheckboxAt` and, if the setting
  "reorder on check" is active (off by default, as on Windows), `SettleToggled` with the cursor mapped by `mapIndex`.
- **Edit bar** above the keyboard (`imePadding`): checkbox, bullet, indent −/+, move line up/down, and with the task list: uncheck all and remove done ones («⋯» menu).
- Autosave: a 600 ms pause without typing, on leaving the note and when the app goes to the background. A single "writer" per note (queue of one); a late write cannot
  overwrite a newer one.
- Row preview: `NoteTitleHelper.getTitle` / `getPreview`, `done/total` counter from `TaskLines.count`.

### 2.3 Colors and appearance

The note colors: `NoteThemes` + `NoteColorDerivation` from `:text` (already done and verified). **The app palette (chrome)** needs `AppPalette`/`AspectPalettes`: its vectors are generated
from Core and ported **before the theme task** (F3b, §4 task 5); until then the app uses plain Material 3 so as not to block the other tasks. No Material You (user's decision).

### 2.4 Done (2026-10-17): tasks 1, 3 and 4

- **Task 1:** common Gradle root (wrapper with Gradle 9.6.0 pinned by hash, AGP 9.4.1, Kotlin 2.4.21, version catalog), `app` module that starts on the
  Pixel 10a (Android 17). The debug variant carries `applicationIdSuffix ".debug"`: instrumented tests uninstall their app when they finish, and so they never
  touch an installation with real notes.
- **Task 3:** Room v1 with `note`, `tombstone`, `sync_base`, `reminder`; **`sync_conflict` is left for F5** (its shape depends on the engine; it will come with migration
  1→2). Dates as ISO text (sync instants carry up to 100 ns). `RoomNoteStore` + 8 tests on the device and the migration test.
- **Task 4:** `AesGcmNoteCipher` and `DataKeyManager` in `:domain` (JVM, tested with a Keystore double); `AndroidKeystoreKeyWrapper` in `app`
  (StrongBox if it exists; on the Pixel, key in secure hardware and 29 ms to unwrap) and `AppContainer.open()` → `Ready` or `KeyLost`. Rule: without a recoverable key
  another one is **never** generated and nothing is deleted; only "start from scratch" (deletes data → file → Keystore key, in that order). The key file
  lives in `noBackupFilesDir`. The notice screen is missing (task 6-8).
- **Task 6 (2026-10-17):** adaptive list (`NavigableListDetailPaneScaffold`), Notes/Archived/Trash tabs with counter, search, tag chips, "+",
  swipe to archive/trash/restore with "Undo", permanent deletion with confirmation, Aldune theme (`AppPalette`), lost-key screen and a plain-text
  editor with autosave **in the `ViewModel`** (drafts that survive the pane; a new note does not exist until the first character). Tested by hand
  on the Pixel with real touches. **Pending from task 6:** reorder by dragging (it must be decided whether a library is added; meanwhile, the order is the domain's),
  color and tags menu per note, and checking the wide layout (tablet or emulator). Detail learned: the search box is the first text field and Android
  gave it focus and the keyboard on startup; it was solved with an invisible element that receives focus after the window's one (`NoteListPane`).
- **Task 7 (2026-10-17):** editor with checkboxes, bullets and indentation. Enter and Backspace of the virtual keyboard are deduced by difference in `ListKeys` (`:text`, 11 tests on the
  JVM); tested with a real Gboard on the Pixel, and the physical shortcuts with key events via adb. A third-party keyboard, a real physical one and the tablet are still pending. The technique
  and what was learned are in [`docs/ANDROID_PROBES.md`](../../ANDROID_PROBES.md).
  **Later correction (2026-10-18):** with AnySoftKeyboard (third-party keyboard) the first version, based on `onValueChange`/`TextFieldValue`, wrote the half-typed word again after Enter (the keyboard's
  composition region did not match the rewritten text). It moved to `TextFieldState` with an `InputTransformation`, which corrects within the keyboard's edit;
  verified with Gboard and AnySoftKeyboard on the Pixel. Only a real physical keyboard remains pending.
- **Task 8 (2026-10-18):** `android/strings.tsv` (key + en/es/de/fr/pt-BR) and `android/scripts/gen_strings.py`, which generates the five `values*/strings.xml` and fails if a
  language is missing. Settings (`SettingsScreen`, `SettingsStore` in SharedPreferences): appearance (System/Dark/Light/Pastel/Midnight), language (applied in `attachBaseContext` and recreates the
  activity; no AppCompat or system picker), tone of new notes, trash days and "move done tasks down" (**off by default**, decided on 18-10, like
  Windows; with Ctrl+Enter the cursor stays in place, on tap it follows its line). The per-theme color for new notes is missing (Core's theme names are in Spanish).

## 3. Verification (gates)

1. `gradle test` green in all JVM modules, **including the current ones without losing any**.
2. `domain`: tests with the clock and ids injected: opening and closing without editing does not change `UpdatedAt`; editing does, and sets `syncAuthorDeviceId = null`; CRLF is preserved; reorder and renumber;
   trash, purge at 30 days, permanent deletion leaves a tombstone; tags (limit, duplicates); search.
3. `app` (instrumented, on device): encryption round-trip and the "Keystore without key" case; v1 migration; the list does not decrypt everything each time a note changes.
4. **Mobile probes** (parent spec §7, `docs/ANDROID_PROBES.md`, which is written in task 7): Compose tests with a temporary database and captures via `adb exec-out screencap` on the Pixel and the
   tablet: create, write a task list, check, Enter that continues the list, Backspace that removes the prefix, move line, reorder, archive, trash, search, rotate, split screen.
5. Never against anyone's real data; the development app's database is that of the test device.

## 4. Tasks (in order) and where they are done

| # | Task | Where | Notes |
|---|---|---|---|
| 1 | Common Gradle root, version catalog, platform 36 installed, empty `app` module that builds and installs (`adb install`) | Local, Sonnet medium | Before: `gradle test` of core/sync/text. Decide wrapper (§6). |
| 2 | Complete `android/domain` with tests | Pure JVM | Can start now. |
| 3 | Room (entities, DAO, v1 migration) + real `NoteStore` + instrumented tests | Local | Needs a device or emulator. |
| 4 | Keystore + `DataKey` + `NoteCipher` + "lost key" case | Local, **Opus medium** (security design) | Read-only cross review. |
| 5 | F3b: `AppPalette`/`AspectPalettes` vectors (on Windows) and their port to `android/text` | Windows | Independent of the others. |
| 6 | Adaptive list + filters + search + reorder | Local | |
| 7 | Editor with `:text`, edit bar, tap on checkboxes; `docs/ANDROID_PROBES.md` and first probe | Local | The hard task (§2.2). Real keyboards. |
| 8 | Minimal Settings, language, appearance, strings table with the five languages | Local | Script that fails if a language is missing. |
| 9 | Full probes on both devices; report `INFORME-ANDROID-F4.md` | Local | |

Tasks 2 and 5 depend on nothing and can be done **in parallel** with 1 and 3.

## 5. What is NOT decided here (the user is asked when the time comes)

- Whether "reorder on check" should be active by default on Android (Windows: off).
- What rows with a long title show on a tablet (one line or two).
- Platform 36 or 37 as `targetSdk` (36 recommended; 37 changes behaviors that do not need to be absorbed now).

## 6. Risks

1. **Enter/Backspace with virtual keyboards** (§2.2): it may require a custom `InputConnection` if detection by difference fails on some keyboard. It is discovered in task 7, not before.
2. **Gradle wrapper and F-Droid:** F-Droid requires `gradle-wrapper.jar` to be verified; without a wrapper, the reproducible build depends on the installed Gradle version. Decide in task 1 with F-Droid's policy at hand.
3. **`LF` in notes created on Android** (§1.2): check with a sync scenario (F5) that Windows does not create new versions because of the line break.
4. **Platform 37 installed, 36 not:** F4 cannot be built against 36 until it is installed.
5. **Loss of the Keystore key** (§2.1): without sync there is no recovery; F4 makes it visible but does not solve it. Accepted until F5.

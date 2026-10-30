# Aldune — Mobile client (Android and iOS): study and plan

> **Status: plan decided on 2026-09-28 —Android first, on a tablet, see §0.1—. Android is already published; this study
> is the origin of the decisions and of what was ruled out.**
> **Revision of 2026-10-17 (§13): .NET MAUI is not suitable if F-Droid is wanted; the recommendation becomes native Android
> (Kotlin + Compose) with the sync format pinned by vectors generated from `Aldune.Core`. Decided by the user the
> same day. What the rest of the document says about MAUI (§3, §4, §6, §7) predates that revision; the current product and phases
> are in [`specs/2026-10-17-aldune-android-design.md`](specs/2026-10-17-aldune-android-design.md).**
> Date: 2026-09-28. This document exists so the port can be picked up again months from now,
> without repeating the research that has already been done. If anything here is
> implemented, what was implemented takes precedence: `STATUS.md` (what is done and why) and
> `ROADMAP.md` (what was decided and ruled out) must be updated and, if there is a new product design,
> `docs/specs/`.

## 0. The brief and the short answer

Requested by the user on 2026-09-28, in their words: build **the best possible port to Android and iOS**;
they are not convinced it should work the same way ("on mobile it should rather be an app that lets me
**manage the notes I have on the computer**, in case something occurs to me from the phone that I want to
jot down"); they ask to see **how to do the port, which features, something different**.

Short answer: **it should not be the same, and not out of taste, but because the desktop metaphor does not
exist on a phone.** Aldune on Windows is a desktop object: a deck stuck to the edge of the screen that
fans out when the mouse passes over it, notes as windows, several screens, a global shortcut. On a phone
there is no mouse, no edge, no windows, no several monitors: the "desktop" is the home screen and what is
"always visible" is notifications and widgets. Copying the fan would be planting a mouse gesture where
there is no mouse.

The mobile client has to do three things, and right away:

1. **Capture in two seconds** (the use case the user gives: something occurs to me and I want to jot it down).
2. **Browse and manage** what is already on the computer (search, read, edit, tag, archive,
   trash, reorder the deck).
3. **Remind** (reminders, with actions from the notification).

Everything else (dock, fan, pill, windows, per-screen position, tray, shortcuts, theme editor,
monitor management) **stays on Windows**.

### 0.1 Decision taken (2026-09-28): Android first, tested on a tablet

The user has an **iPhone and an Android tablet**. **Android first** is decided, for reasons of economy and
reach:

- **Cost of doing it: €0.** No Mac, no Apple account, no Xcode: Android is built and debugged
  from Windows with what is already there (Visual Studio installs the SDK and the JDK).
- **There is already a real device to test on**: the tablet. It matters more than it seems, because the probes of
  this project are done with real windows and devices, not simulations.
- **The tablet is the best test bench for the main use case**: managing and editing the notes that
  already exist, which is what the user asked for. And it accepts keyboard and mouse, so desktop
  interaction can be tested too.
- **For the product**: Android reaches more users per euro invested and publishing costs a **one-time $25**
  (Play) versus **$99/year** (Apple).
- **For personal use there is no need to publish**: a signed APK installed by hand on the tablet costs €0.
  Google Play is only needed for other people to use it or to have automatic updates.

**Design consequence: the tablet is the reference device and the phone is the subset.**
List and note can coexist in two panes, the large widget is the main one and a physical keyboard has to be
accounted for; the phone design comes from compressing that, not the other way around.

**The iPhone stays out until the iOS client exists**, and it is worth being clear about why: iOS
requires a Mac with Xcode 26 and a paid Apple account, even to install it on one's own phone. What
decides the priority is not money, it is the use case:

- **Reading, searching, editing and annotating** what is already on the computer → the tablet covers the whole case.
- **Jotting something down from your pocket** with the phone you carry → only iOS does that, and
  then the conversation starts with the Mac and the $99/year, not with the code. Meanwhile, on the
  tablet the short path is to open the app directly on "new note".

The decision does not close off iOS: the client is designed as **one MAUI project and two Swift extensions**, so
adding it later means adding the toolchain, not redoing anything (see §7, phase 3).

## 1. What exists today, verified in the repository (2026-09-28)

| Data point | Actual value |
|---|---|
| `src/Aldune.Core` | **69** `.cs` files, **6,663** lines, pure `net10.0`, no WPF |
| Core dependencies | `Microsoft.Data.Sqlite` 10.0.11 and `System.Security.Cryptography.ProtectedData` 10.0.11 |
| `src/Aldune` (WPF) | **60** `.cs`/`.xaml` files, **15,829** lines, `net10.0-windows`, `UseWPF` + `UseWindowsForms` (tray), version 1.4.1 |
| `tests/Aldune.Core.Tests` | **60** files, **6,260** lines, **850/850** tests green (these are *Core* tests, not WPF ones) |
| `src/Aldune.SyncServer` | ASP.NET Core minimal API, ~150 useful lines (`/health`, `GET /api/v1/objects`, `GET`/`PUT`/`DELETE /api/v1/objects/{id}`), `Bearer` with token rotation, 5 MB cap per object |
| UI texts | `src/Aldune/Resources/Strings.cs`, **1,268** lines, `T(en, es, de, fr, pt)`; languages in `Aldune.Core.UiLanguages` |
| CI | `.github/workflows/ci.yml`: `windows-latest`, `dotnet build` + `dotnet test` of `Aldune.slnx` |

**The only truly Windows code in Core is DPAPI.** Exhaustive search for
`ProtectedData|OperatingSystem.|Registry|DllImport|win-x64|Environment.` in `src/Aldune.Core/*.cs`:
outside `DatabaseKeyProvider.cs` everything else is comments about Windows, not API. The calls
are in:

- `SyncService.cs`: lines 69, 86, 110, 132, 144, 163, 254, 257, 428, 670-679.
- `LocalBackup.cs`: 153 (`DatabaseKeyRecovery.Opens`).
- `src/Aldune/App.xaml.cs`: 115-127 (startup: generate, wrap, unwrap the database key).
- Tests: `DatabaseKeyProviderTests`, `EnvelopeEncryptionTests`, `LocalBackupTests`.

**What travels in sync (format 4)**: text, color, tags, state (`Activa`,
`Archivada`, `Papelera`), `ScreenOrigin`, protected notes (salt + encrypted content) and
`DockPosition` (the deck order). The envelope is JSON with `CipherText`/`Nonce`/`Tag` in base64.

**What does NOT travel today** (it is local to each installation and this must be said out loud before designing the
mobile client): **reminders** (`NoteReminder`), **checked tasks** (`TaskCompletion`), **window
positions** (`NotePlacement`), **conflicts** (`SyncConflict`), **custom note
themes** (`CustomThemes`) and the appearance and language settings.

**Protected notes**: `ProtectedNoteContent` — PBKDF2-SHA256, **600,000 iterations**, 16 B salt,
AES-256-GCM, `Version = 1`. The iteration count **is in the code, not in the data**, so it is
a compatibility constant: the mobile client has to use exactly the same one (using the Core
class guarantees it).

**Existing transports** (all in Core, all portable in principle): `FolderSyncTransport`
(`objects/` in a folder), `HttpSyncTransport` (own server, `Bearer`) and `WebDavSyncTransport`
(PROPFIND + Basic, for Nextcloud/ownCloud). The invitation code (`SyncShareCodeCodec`) carries
key, scope, profile name, transport and server URL, **never the token**.

## 2. What is reused and what is not

| Reused as is (Core, with its 850 tests) | Why |
|---|---|
| `Note`, `NoteState`, `NotesDatabase`, `NotesRepository` | SQLite + encrypted text; the schema does not depend on Windows |
| `ContentCipher` (AES-256-GCM), `PasswordRules`, `ProtectedNoteContent` | Standard-library crypto; same constants = same compatibility |
| `SyncService`, `SyncModels`/`SyncEnvelopeCodec`, `ISyncTransport`, `HttpSyncTransport`, `WebDavSyncTransport`, `SyncProfileStore`, `SyncShareCodeCodec` | The format and the conflict resolution are the expensive part to duplicate |
| `SettingsService`, `AppSettings` (with their compatible defaults) | Same file, same numeric enums |
| `NoteSearch`, `NoteText`/`NoteTitleHelper`, `TaskLines`, `TaskLists`/`TaskCompletion`, `ListIndent`, `LineMovement`, `BulletLines`, `TextEdit`, `MarkdownExport` | This is the product's "text format": checkboxes, lists, titles |
| `NoteOrdering`, `NoteListing` | The deck order, which is already synced |
| `NoteThemes`, `NoteColorDerivation`, `OklchColor`, `NoteColorContrast`, `AppPalette` | PC↔mobile visual continuity and already-tested contrast |
| `ReminderPresets`, `LocalBackup`, `DiagnosticLog`, `BrandIdentity` | Portable utilities (with path caveats) |

| Left out on mobile | Why |
|---|---|
| Dock, fan, pill, `EdgeGeometry`, `DockPlacement`, `DockHover*`, `FanStateMachine`/`FanTiming`, `DpiConversion`, `MonitorInfo`/`MonitorLookup`/`MonitorPowerTracker`, `FullscreenDetection` | Desktop geometry and hover |
| `HotkeyBinding`, `TrayIcon`, `StartupRegistration`, own *toast* notifications | Windows concepts (or replaced by system notifications) |
| `ReleaseUpdateChecker` | On mobile the store updates, not GitHub |

## 3. The proposed mobile product

### 3.1 Translation of concepts

| Desktop (WPF) | Mobile (proposal) |
|---|---|
| Resting pill = color strips at the edge | Home screen **widget** (notes with their color). On iOS, also a lock screen / StandBy widget |
| Fan on mouse hover | **List** with search on top. You tap, you don't hover: the fan is not imitated |
| Global shortcut `Ctrl+Alt+H` | **Share sheet**; **Quick Settings tile** (Android); icon **"New note" action**; **persistent notification with "+"**; keyboard dictation |
| Note window (title = 1st line + body) | Full-screen note screen, with the same text (`☐`/`☒`, lists, indentation) |
| Reminder with Windows notification | Local notification with buttons: *Done*, *Snooze 10 min / 1 h / tomorrow*, *Open* |
| Notes manager (search, sort, filter) | It is the main screen |
| Note color theme | Same (same palette: the note looks the same color on the PC and on the phone) |
| Password-protected notes | Same, plus **biometric** unlock (the derived key is stored in Keychain/Keystore) |
| Dark/Light/Pastel/Midnight appearance | Dark/Light/follow the system (Pastel and Midnight are derived on their own, as on Windows). **Done on Android (2026-10-25, rc5-rc7):** also the retro appearances (XP, Telecommunications, Bash, Phosphor) with their colors and the monospaced font, without the Windows "skin" (see `ROADMAP.md` §13) |
| Daily local backup (`LocalBackup`) | Covered by the system backup; decide what is excluded (see risks) |

### 3.2 What is NOT ported, nor imitated

Dock, fan, pill, floating note windows, per-screen position, several screens, tray,
global shortcuts, full-screen game detection, theme editor, "Manage notes" as a
separate window. On mobile **there is no screen to stick a deck to**.

### 3.3 The widget, and the port's first privacy decision

A widget cannot ask for a password or unlock anything: it just paints. Proposal:

- The app writes a small **snapshot** (color, title = first line and, optionally, the
  first line of body) into a JSON in the shared container (App Group on iOS, own file on Android),
  and the widget paints that. It is refreshed when the note changes and in the background refresh.
- **By default, titles only.** Including more text is an explicit user setting, because it is a
  conscious exception to "the text is encrypted at rest": the snapshot is in the clear (protected by the
  file system's encryption, not by Aldune's key).
- **Protected notes never go** into the snapshot.

Advantage of this design: there is no need to reimplement AES-GCM in Swift for the widget, and Android and iOS use
the same mechanism (only one path to test).

### 3.4 Mobile ideas that are indeed "something different" (and cheap once the MVP exists)

- **Persistent notification with a "+" action** on Android: it is the exact equivalent of the global shortcut.
- **Deck widget**: the dock's color strip, on the home screen. It is the visual continuity
  of the product, not a generic list widget.
- **Share anything to the phone** (a link, a sentence from an article) → new note with that
  seed. On Android it is an `intent-filter` for `ACTION_SEND`; on iOS, a share extension (Swift) or,
  without writing Swift, a user shortcut that opens `aldune://nueva?texto=...`.
- **"Done" from the reminder notification**, without opening the app.
- **Keyboard dictation** in the editor: free, from the system, and it is the fastest capture there is.

## 4. Technology: options and recommendation

| Option | Reuses Core | Cost | Risk | Verdict |
|---|---|---|---|---|
| **A. .NET MAUI (C#/XAML)** | **Yes, almost entirely** (the 850 tests remain valid) | Medium-high: new XAML + Android/iOS toolchain | MAUI quality/perf, AOT/trimming on iOS | **Recommended** |
| B. Avalonia (Skia) | Yes, same | Medium | Less mature on mobile; widgets still need native code | No clear advantage over A |
| C. Flutter | **No**: crypto, sync and text would have to be rewritten in Dart | High | Two implementations of an **encrypted format** = silent divergence | No |
| D. Native (Kotlin + Swift) | No (two rewrites) | Double | The highest | No, unless maximum polish is sought and there is a time budget |
| E. Web/PWA | No | Medium | No widgets, unreliable notifications, text in the browser | No |

**The underlying reason for MAUI is not the framework, it is the format.** The sync envelope and the
encryption are implemented **only once**; duplicating them in Dart/Kotlin/Swift is the easiest way for
one device to write something the others cannot read. And MAUI allows keeping the project's
culture: logic in a library with tests, UI verified separately.

### 4.1 Verified platform facts (2026-09-28), with sources

- **AES-GCM**: on iOS it **did not exist**; it was added in **.NET 9** with minimum iOS 13
  ([dotnet/runtime#91523](https://github.com/dotnet/runtime/issues/91523), closed with milestone 9.0.0;
  the API appears marked `SupportedOSPlatform("ios13.0")`). Android is not listed as unsupported and the
  runtime has a native platform implementation. **With .NET 10, `ContentCipher` is portable as
  is**; even so, check `AesGcm.IsSupported` at startup and have it proven in the spike.
  ([Cross-platform cryptography in .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography))
- **SQLite**: `Microsoft.Data.Sqlite` is supported on Android/iOS (MAUI's own documentation
  lists it as an alternative to `sqlite-net-pcl`), but on **iOS Release (AOT + trimming)** is where
  the classic problems appear: `DllNotFoundException: e_sqlite3` and links that blow up in
  AOT-only. This has to be resolved **in the spike**, not in production:
  ([SQLite in MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite),
  [Resolving SQLite issues for .NET MAUI](https://www.banditoth.net/resolving-sqlite-issues-for-net-maui/)).
- **Secrets (the DPAPI replacement)**: MAUI's `SecureStorage` uses **Keystore +
  EncryptedSharedPreferences** on Android and **Keychain** on iOS. Two warnings that are already in the
  documentation and that affect the design:
  [Secure storage](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage)
  - **Android Auto Backup** restores encrypted preferences whose keys do **not** travel: MAUI deletes the
    value and every read has to be wrapped in `try/catch` (or those preferences excluded from the backup).
  - On iOS, the **Keychain survives uninstallation**; and if the user has **iCloud Keychain**
    enabled, values may sync to their other Apple devices with no control from the app.
- **Widgets**: on **Android they can be made in pure C#** with `AppWidgetProvider` + `RemoteViews`
  (even a widget with a scrollable list), **without Kotlin or Android Studio**
  ([.NET blog](https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/)).
  On **iOS it requires a Swift/SwiftUI extension + Xcode + App Group** and cannot be written in C#: it is the
  most manual part of the port ([.NET blog](https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/)).
- **Toolchain and stores**:
  - iOS: **Mac with Xcode 26** and a paid Apple Developer account, **$99/year**. From Windows iOS
    **cannot** be built ([MAUI installation](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation)).
    Since **28 Apr 2026** Apple requires building with Xcode 26 and the iOS 26 SDK
    ([upcoming requirements](https://developer.apple.com/news/upcoming-requirements/)).
  - Android: the SDKs/JDK are free and install from Visual Studio; Google Play requires
    **target API 36 (Android 16) from 31 Aug 2026** for new apps and updates
    ([target API requirements](https://support.google.com/googleplay/android-developer/answer/11926878)),
    and the account is a **one-time $25**.
- **Background (this conditions what can be promised)**: on iOS `BGAppRefreshTask` is
  **opportunistic** — `earliestBeginDate` is a floor, not a promise, and the system decides whether to let you
  run, how often, and whether to cut you off midway
  ([practical summary 2026](https://vburojevic.dev/blog/ios-background-tasks-2026/)). On Android,
  `WorkManager` has a minimum of **15 minutes** and exact alerts need the alarms permission
  (Android 12+), which Play policy restricts for apps that are not clock/calendar apps
  ([alarms on Android](https://developer.android.com/develop/background-work/services/alarms/schedule)).
  **Design conclusion: never promise "syncs every 15 minutes".** Sync always
  **on open, on return to the foreground and before editing**, and also "whenever the system allows" in the
  background. The sync engine is already idempotent (it uploads whole envelopes and breaks ties by date),
  so syncing too often breaks nothing.
- **Reminders**: on iOS local notifications are delivered by the system even if the app is
  closed (reliable, with permission); on Android it is wise to assume that without the exact-alarms permission the
  alert may arrive minutes late. It has to be tested **with the app closed** on Android 16 and
  iOS 26 (spike C).

## 5. What has to change first, in Core and in Windows

None of this breaks compatibility; it is bounded surgery, test first. **It is the only part that touches
the current repository**, so it can be done (and tested) without any mobile project yet.

1. **Take DPAPI out of Core.** Introduce
   `ISecretProtector { byte[] Protect(byte[]); byte[] Unprotect(byte[]); }` with an implementation
   `DpapiSecretProtector` (what `DatabaseKeyProvider` does today, plus `GenerateKey`) and inject it into
   `SyncService`, `LocalBackup`/`DatabaseKeyRecovery` and startup. On mobile it is implemented with
   Keystore/Keychain. The tests inject the Windows one and **keep passing as is** (the test
   files that use it: `DatabaseKeyProviderTests`, `EnvelopeEncryptionTests`, `LocalBackupTests`).
2. **Asynchronous sync.** Today `ISyncTransport` and `SyncService` are synchronous with
   `GetAwaiter().GetResult()` (see `HttpSyncTransport`, `WebDavSyncTransport`). On mobile that blocks
   thread-pool threads and on iOS it is simply a bad idea. Add an `async` API **keeping the synchronous one**,
   with the conflict, tombstone and format tests as a safety net.
3. **Serialization with a code generator (`JsonSerializerContext`).** iOS Release compiles AOT and
   trims: `System.Text.Json` via reflection gives IL2026/IL3050 warnings and may fail at runtime.
   Affected points: `SyncModels.cs` (lines 243, 255, 330, 337, 382, 415, 425, 429, 446),
   `SettingsService.cs` (19, 30, 44) and `NotesRepository.cs` (341, 363).
   **Before touching anything: a test that pins the exact JSON of an envelope** (golden file) to guarantee
   that format 4 remains byte for byte the same.
4. **Paths.** They are already passed as parameters (`SettingsService(path)`, `NotesDatabase(path)`,
   `LocalBackup.CreateDaily(appDataDir, ...)`), so it is enough for the mobile app to use
   `FileSystem.AppDataDirectory`. No new `IAppPaths` is needed.
5. **Windows, so that mobile is usable without friction** (two new features, both reuse what
   already exists):
   - **QR pairing.** Today the invitation does not carry the token (good), but typing it on a phone
     is awful. A QR generated by the PC with profile + URL + one-time token, scanned from the
     mobile app. *Careful: it touches the sync security model (`docs/SYNC.md` → "Security model"):
     it has to be decided with the user and documented.*
   - **Sync server inside the PC itself** (optional and cheap): start in the WPF process the same
     minimal API as `Aldune.SyncServer` over a LAN folder (the server is ~150 lines). The
     no-Docker scenario then becomes: install the app → scan the QR → synced, no cloud and no account.
6. **None of this is necessary for the mobile MVP, but it conditions v1**: reminders are not
   synced. If someday they must travel PC↔mobile, **the format has to go up to 5**, and by the
   project's rule (an old version rejects what it does not understand, with a clear error, applying nothing
   halfway) **Windows that accepts 5 must be published first and only afterwards the mobile client that writes it**.
   Recommendation: in mobile v1 reminders are local to the phone, and this is stated clearly in the
   interface. Format 5 is decided with real use in hand.

### 5.1 Formats and compatibility, in brief

- The mobile client implements **format 4 as is** (same envelopes, same validation, same
  tie-breaking by `UpdatedAt` and `DeviceId`). No new field is added in v1.
- The mobile `DeviceId` is generated once per installation and does not change (it takes part in tie-breaking).
- **Window positions remain local**: they are not used on mobile, but the `ScreenOrigin` column
  exists and does no harm (a synthetic value is stored, and that's it).
- **Custom themes and conflicts are left out of the mobile MVP** (they are local today): mobile reads the colors
  that come in each note and that's it. The conflicts screen can be a read-only list later
  on.

## 6. Proposed repository structure

```
Aldune.slnx                       (Windows + Core; no changes of substance)
src/Aldune.Core                   pure net10.0 (DPAPI removed)
src/Aldune.Core.Windows           new: DPAPI and whatever exists only on Windows
src/Aldune                        WPF (references Core + Core.Windows)
src/Aldune.Mobile.Core            new, net10.0: ViewModels, presentation services and abstract
                                  navigation → testable without UI (the test culture lives here)
src/Aldune.Mobile                 new, MAUI: net10.0-android (+ net10.0-ios)
src/Aldune.Mobile.iOS.Widget      Swift extension (WidgetKit + share sheet), outside the .NET solution
tests/Aldune.Core.Tests           the usual 850
tests/Aldune.Mobile.Tests         xUnit over Aldune.Mobile.Core
docs/MOBILE_PROBES.md             mobile "probes": diagnostics screen + per-device checklist
```

Conventions that are kept: texts with `Strings.T(en, es, de, fr, pt)` (so as not to duplicate the five
translations, the mobile project links `src/Aldune/Resources/Strings.cs` with a `<Compile Include>`
instead of copying it), comments in Spanish, UTF-8 BOM in `.cs`/`.xaml`, documentation without BOM, and
**test first** for anything that is logic.

## 7. Phases (estimate in work sessions like those of this project)

| # | Phase | Deliverable | Verification / gate | Sessions |
|---|---|---|---|---|
| 0 | **Spec and spikes** | `docs/specs/<date>-aldune-movil-design.md` (product, what is not ported, widget privacy) + four disposable probes | A: open `notes.db` and decrypt on Android. B: real sync against the Docker server, with a conflict and a deletion. C: notification with the app closed, Android 16 and iOS 26. D: minimal Swift widget reading from the App Group. **If A or D fail, the stack is reviewed before writing product** | 2 |
| 1 | **Seams in Core** | `ISecretProtector`, `async` sync API, `JsonSerializerContext`, `Aldune.Core.Windows` | The 850 tests + the Windows UI smoke test, **with no behavior changes** | 3 |
| 2 | **Android MVP** | Capture (new note, share, shortcut, "+" notification), list with search and filters, read/edit with the real text format, color/theme, tag/archive/trash, sync settings (profile code + token), automatic sync on open and on return to the foreground, local reminders, protected notes | Core tests + `Aldune.Mobile.Core` tests + **mobile probe** with a report from the device + manual checklist | 6 |
| 3 | **iOS MVP** | The same scope on the Mac pipeline (Xcode 26), TestFlight and the Swift share extension | Same as phase 2, with its own iOS checklist | 4 |
| 4 | **What makes it worthwhile** | Deck widget (Android in C#; iOS in Swift + App Group), lock screen widget, Quick Settings tile, notification actions (*Done*, *Snooze*, *Open*), biometric unlock, tablet design | Per-device checklist + screenshots; and a test that the widget snapshot **never** includes protected notes | 4-5 |
| 5 | **Frictionless pairing** | QR on Windows and, if wanted, sync server inside the PC | Probe: pair from scratch and see the note arrive on the PC; and an attempt with an invalid token | 2-3 |
| 6 | **Stores (only if published)** | Android: Play (AAB, target API 36, data safety, privacy policy). iOS, when its turn comes: App Store (privacy manifest, encryption declaration, App Privacy). CI: Android job on `windows-latest` | For personal use a **signed APK installed by hand** is enough (§0.1); if published, internal track first and check **availability of the name "Aldune"** | 1 (Android) + 2 (iOS) |

**Total: ~23-26 sessions.** It is a project the size of the original, not an add-on.

## 8. Costs and requirements

- **Apple Developer Program: $99/year** (mandatory to install on an iPhone and to publish).
- **Google Play: $25** one time.
- **A Mac with Xcode 26** for iOS, or a cloud macOS runner (GitHub Actions on a public repository,
  or a service like MacinCloud, ~$25-40/month). **iOS cannot be built from Windows.**
- Nothing new for the server: the `SyncServer` and the Docker compositions that already exist work just as well.

## 9. Verification: how the probe culture is maintained

The Core tests (850) are valid as they are, because **Core is the same assembly**. On top of that:

1. `tests/Aldune.Mobile.Tests`: xUnit over `Aldune.Mobile.Core` (ViewModels, filters, formatting, capture
   rules). No UI.
2. **Mobile probe**: a diagnostics screen (debug build only) that runs a script
   of checks over temporary data — open the database, decrypt N notes, sync against a
   local server, fire a notification, write the widget snapshot, time the PBKDF2 of a
   protected note — and saves a report. It is documented in `docs/MOBILE_PROBES.md`, along the lines of
   `docs/WPF_PROBES.md`.
3. **Manual per-device checklist** in each phase: widget after restarting the phone, notification with
   the app closed, sync with the screen off and with a network change, rotation and dark mode.
4. CI: the current job does not change; an Android one is added (builds `-f net10.0-android`) and, when there is
   an Apple account, another for iOS on a macOS runner.

## 10. Risks and open decisions

1. **iOS depends on a Mac.** Without a Mac, iOS is tied to CI + a physical iPhone to test on. It is the
   biggest practical blocker of the plan.
2. **Widget and privacy**: the text snapshot is a deliberate exception to "encrypted at rest".
   Proposal: titles only by default, with a clear warning when enabling the text.
3. **Reminders**: local on the phone (v1) or format 5 (later, with a mandatory publication order:
   first a Windows version that understands it).
4. **"Shared folder" on mobile: ruled out.** On iOS it depends on the provider (on-demand downloads)
   and on Android it forces SAF. The bet is on **own server or WebDAV**, which already work.
5. **Maintenance**: three applications × five languages × the tests. Cutting features on mobile is
   deliberate, not an oversight.
6. **Payment model (if it is ever charged for)**: Apple requires IAP for digital goods; the one-time desktop
   payment and the store one are not mixed without thinking. It has to be decided whether mobile is free
   (a companion to the desktop) or a one-time payment in each store.
7. **Brand**: check that "Aldune" is also free on Play and the App Store (`ROADMAP.md` §6 already warns
   about *FanNote* as a similar name).
8. **Decided on 2026-09-28** (§0.1): Android first, on the tablet and without publishing at the start. The only thing
   left to choose is the fine-grained start: do phase 0 (spec + spikes) or go straight to an Android
   MVP.

## 11. Ruled out, with the reason (so they are not proposed again)

- **Imitating the fan/dock on touch**: it is a mouse gesture without a mouse. Ruled out as a design.
- **Rewriting Core in another language** (Flutter, React Native, native): duplicates the encryption and the
  sync format, which is exactly the part where a divergence is paid for with unreadable data.
- **PWA as the main client**: no widgets, unreliable notifications on iOS, and the text would end up in
  the browser.
- **"Shared folder" on mobile**: see risk 4.
- **Syncing every N minutes in the background**: neither iOS nor Android guarantees it.
- **Storing the PC's database in the cloud and opening it from the phone**: the key is wrapped for the
  Windows user (DPAPI), so that file cannot be opened from another system. What is
  shared between devices is the **sync**, not `notes.db`.

## 12. How to pick this up again

1. Read this whole document and, if months have passed, **revalidate §4.1**: Xcode versions, the
   Play target API and MAUI versions change every season. Everything above dates from 2026-09-28.
2. If what is wanted is another desktop system (macOS, Linux), first read
   [`DESKTOP_PORT.md`](DESKTOP_PORT.md): the conclusion there is that **Core is worth it, the dock is not**.
3. Before starting, it is advisable that someone review the plan with fresh eyes and check the factual claims.
4. Write the phase 0 product spec and do the four spikes. Without the result of spike A
   (SQLite + AES-GCM on a real device) and of D (iOS widget), do not write product.
5. Before touching `src/Aldune.Core`, check that **all tests are still green** (850 when this was written; today ~1350) and that the
   envelope golden file (format 4) exists.
6. Platform: **already decided** on 2026-09-28 (§0.1): Android first, on the tablet, without publishing. What is
   left to choose is the fine-grained start: phase 0 (spec + spikes) or straight to the Android MVP.

## Sources

- [AES-GCM on iOS (dotnet/runtime#91523)](https://github.com/dotnet/runtime/issues/91523) ·
  [Cross-platform cryptography in .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography)
- [What is .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui) ·
  [What's new in MAUI in .NET 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10) ·
  [Installation: the Mac is mandatory for iOS](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation)
- [Android widgets with .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/) ·
  [iOS widgets with .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/)
- [MAUI SecureStorage](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage) ·
  [SQLite in MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite) ·
  [SQLite issues in MAUI](https://www.banditoth.net/resolving-sqlite-issues-for-net-maui/)
- [Apple upcoming requirements](https://developer.apple.com/news/upcoming-requirements/) ·
  [Google Play target API](https://support.google.com/googleplay/android-developer/answer/11926878) ·
  [Alarms on Android](https://developer.android.com/develop/background-work/services/alarms/schedule) ·
  [Background tasks on iOS, 2026](https://vburojevic.dev/blog/ios-background-tasks-2026/)
- [Avalonia](https://avaloniaui.net/) (comparison of options)

## 13. Revision of 2026-10-17: phone + tablet, F-Droid, and what is corrected in the plan

Requested by the user (2026-10-17): a **phone version in addition to the tablet** (their phone is now Android, a Pixel 10a),
**publishing on F-Droid too**, and reviewing whether the Android plan is correct. What was checked and what changes:

### 13.1 Findings (with sources)

1. **F-Droid and .NET MAUI: today there is no path in the main repository.** On the F-Droid forum
   ([«Why isn't C# .NET MAUI supported?»](https://forum.f-droid.org/t/why-isnt-c-net-maui-supported/24842),
   [page 2](https://forum.f-droid.org/t/why-isnt-c-net-maui-supported/24842?page=2)) a maintainer said in March 2024 that
   **there is no .NET app on F-Droid**; the obstacle in March 2026 is the **`maui-android`** workload, which is not built
   from source (F-Droid requires everything to be built from source, with the exceptions already listed), and the last
   visible entry (27 Sep 2026) is still **without an official reply**. Bitwarden, which is FOSS, ended up rewriting in Kotlin.
   *There is no published policy for or against: what is certain is that nobody has managed it.* Before deciding,
   it is worth looking at the thread again and at [the inclusion policy](https://f-droid.org/docs/FAQ_-_App_Developers/).
2. **The license is not the problem:** Aldune is **GPL-3.0** (`LICENSE`), which F-Droid and IzzyOnDroid accept.
3. **Distribution alternatives without main F-Droid**, valid with MAUI: signed APK in the GitHub *releases*, an
   **own F-Droid repository** (F-Droid allows it for what it cannot build) and
   [IzzyOnDroid](https://izzyondroid.org/docs/general/AppInclusionPolicy/) (accepts APKs already signed by the developer,
   open source, metadata with Fastlane; there are no special rules for .NET on record).
4. **Environment of this PC (checked on 2026-10-17):** .NET 10.0.400, **no** MAUI workloads, **no** Android SDK, **no**
   Visual Studio. The sentence in §0.1 ("Visual Studio installs the SDK and the JDK") does not hold here; with MAUI the
   workload and the SDK would have to be installed from the command line (several GB).
5. **Phone and tablet are not two apps:** Compose solves it with **a single adaptive app**
   ([ListDetailPaneScaffold and window size classes](https://developer.android.com/develop/ui/compose/layouts/adaptive/list-detail)):
   on a large screen list and note sit side by side; on a small one, one at a time.
6. **What has to be ported, measured:** the sync and encryption core is **~1,900 lines** of C# (`SyncModels` 513,
   `SyncService` 765, `ContentCipher`, `ProtectedNoteContent`, invitation codes, HTTP and WebDAV transports,
   `ConflictMerge`…), plus ~900 of text format (`TaskLines`, `TaskLists`, `BulletLines`, `ListIndent`, `TextEdit`…) and
   ~450 of color (`NoteThemes`, `OklchColor`…). All the cryptography is standard (AES-256-GCM, PBKDF2-SHA256, HMAC where applicable),
   and Android ships it in `javax.crypto`; SQLite comes with the system.

The **format 4 compatibility vectors** already exist (`tests/vectors/`, see its `README.md`) and `SyncVectorsTests` verifies them: they are the
contract that any client that does not use `Aldune.Core` has to meet. The **scenario** vectors of the sync engine (local state + store state →
actions) are missing, and have to be generated before a Kotlin client is considered compatible.

### 13.2 What is corrected in the plan

| Point of the original plan | Correction |
|---|---|
| §4: **MAUI recommended** | Only if main F-Droid is given up. With F-Droid as the goal, **native Android (Kotlin + Jetpack Compose)**. |
| §11: "Rewriting Core in Kotlin: ruled out" | The reason was the risk of **silent divergence** of the encrypted format. It is mitigated with **compatibility vectors**: `Aldune.Core` (the source of truth) generates JSON files with envelopes, keys and expected results, and the Kotlin client's tests consume them; and the other way round (what Kotlin writes, C# reads). With that, the cost of divergence is a red test, not unreadable data. |
| §5: **seams in Core** (`ISecretProtector`, `async`, `JsonSerializerContext`) | **They stop being a mobile requirement**: Kotlin does not use Core. They remain valid for Windows (and `ISecretProtector` fixes the 46 tests that fail on Linux), but they no longer block anything. What is needed is the **envelope golden file (format 4)** and the vectors. |
| §7: "6 sessions for the Android MVP" | Recounted: Kotlin core with vectors **≈ 6-8**, adaptive Compose UI **≈ 6-8**, widget/notifications/shortcuts **≈ 3-4**. Total **≈ 15-20 sessions**, not 23-26 with iOS, but without iOS. (Order of magnitude: if rewriting came out under 5 sessions the plan was wrong; it comes out higher, but in exchange there are no seams and no MAUI toolchain.) |
| §0.1: **tablet as the reference device** | There are now **two** real devices: the Pixel 10a (phone) and the tablet. The **phone is the reference for capture** and the **tablet for management**; the design is adaptive (see 13.3). |
| iOS | No change: out until there is a Mac. With Kotlin, iOS would be a separate Swift client consuming the same vectors (or Kotlin Multiplatform for the core if written with that in mind from the start; it need not be decided now). |

### 13.3 Phone and tablet: one app, same engine, different face

Same sync, same text format, same data and same settings. What changes is **the entry screen and the layout**,
not the product:

| | Phone (use case: jot down on the fly) | Tablet (use case: manage and edit) |
|---|---|---|
| Home screen | **Capture first**: "new note" one tap away, list below | List and note **side by side** |
| Quick entries | Widget, Quick Settings tile, notification with "+", share into the app | The same, plus keyboard and mouse shortcuts |
| Editing | Full screen | Right pane, several recent notes |
| Deck (dock order) | Reorder with long press | Reorder with drag or keyboard |
| Widget | Small (color strip / titles) | Large (deck grid) |

What is **not** done in either (same as before): dock, fan, floating windows, per-screen position.

### 13.4 Decisions (2026-10-17)

1. **Native Kotlin instead of MAUI: decided by the user on 2026-10-17.**
2. **Test devices:** Google Pixel 10a (Android 16, API 36) and **Lenovo Tab P11**. According to Lenovo's lifecycle table
   ([PSREF](https://psref.lenovo.com/WDProduct/Lenovo_Tablets/Tab_P11)) the TB-J606F shipped with Android 10 and is scheduled to
   receive the update to Android 11; **Android 12 is not on record**. That is, the tablet is probably Android 10 or 11 (API 29-30).
   **Corrected on 2026-10-17: the user checked that the tablet has Android 14 (API 34)**, so it is not the TB-J606F from the table above (exact model not noted down). `minSdk` 26 is kept for the sake of F-Droid users, not the tablet.
3. **`minSdk` 26 (Android 8.0), `targetSdk`/`compileSdk` 36.** Reasons:
   - It covers the tablet (API 29-30), the Pixel (API 36) and the old phones that F-Droid users tend to use
     (LineageOS 15 onwards).
   - **`PBKDF2WithHmacSHA256` only exists since API 26** (on 25 and earlier there is only `PBKDF2WithHmacSHA1`;
     [`SecretKeyFactory` reference](https://developer.android.com/reference/javax/crypto/SecretKeyFactory)). The format's protected notes
     use PBKDF2-SHA256 with 600,000 iterations, so below that it would have to be reimplemented by hand over HMAC,
     slower and riskier, to gain a tiny fraction of devices.
   - `java.time` (the envelope's ISO 8601 dates) ships with the system since API 26, without *desugaring*.
   - Compose requires API 21 at minimum, so 26 does not limit it.
   Going below 26 would only pay off if a real user with Android 5-7 shows up; until then, no.
4. **Pending:** `applicationId` and name (check that "Aldune" is free on F-Droid and IzzyOnDroid), and the tablet's exact Android version.
5. Before writing product: **a review of the plan with fresh eyes**, with the added question
   of whether Kotlin with vectors is really cheaper than MAUI.

### 13.5 Status as of 2026-10-17 and decisions

This was the status on the day of the revision; what was done afterwards (F4-F7, Android 1.0) is in `STATUS.md` and in the specs under `specs/`.

- **Done:** the product spec, the architecture, the storage and the phases in
  [`specs/2026-10-17-aldune-android-design.md`](specs/2026-10-17-aldune-android-design.md); the compatibility vectors
  (`tests/vectors/`, verified by `SyncVectorsTests`, with 34 scenarios of the real engine); the Kotlin core `android/core`
  (key, envelopes, deletions, protected notes, profile codes, version ordering). With each vector's nonce Kotlin reproduces
  C#'s encryption byte for byte, and Core reads what Kotlin writes.
- **Changes to the plan:** the Android widget does not need a clear-text snapshot (§13.6.8); key rotation, merging conflicts,
  biometrics, QR and shared folder are left out of v1; server + WebDAV transports; the Kotlin engine separates decision (`planSync`,
  pure) from effects; new distribution risk: Google developer verification (§13.6.3), which makes the reproducible build a
  requirement from day one. An independent review of the spec (14 findings, 13 confirmed in the code) added scenarios,
  envelope-construction rules (`ScreenOrigin`, author, protected notes with empty text) and a **probable Windows bug**: a protection
  arriving via sync empties the file of a linked note (spec §9.3).
- **Kotlin modules and their tests:** `android/sync` (planSync, execute, HTTP transport with OkHttp and WebDAV; the 34 scenarios pass in the
  plan and end to end, and the transports were run against the real `Aldune.SyncServer`), `android/text` (format, color, ordering and
  search against vectors generated from Core: ≈12,800 cases) and `android/domain`. Search without vectors (Android folds accents);
  `ColorFit`, `ColorMix` and `AppPalette` were left out of F3 (the app palette was decided in F4).
- **Decided (2026-10-17):** `applicationId` `io.github.nachopola16.aldune`, name "Aldune", a single signing key, GitHub + IzzyOnDroid
  first, Android Studio before F4; the tablet has Android 14. Detail in spec §8.1.

### 13.6 Second review with internet access (2026-10-17, afternoon)

Review requested by the user "with fresh eyes". What is confirmed, what is corrected and what is new:

1. **F-Droid and .NET: confirmed.** In the [forum thread](https://forum.f-droid.org/t/why-isnt-c-net-maui-supported/24842?page=2)
   the last entry (27 Sep 2026) asks whether a .NET SDK built from source would be acceptable and **F-Droid still has not replied**; no
   accepted .NET app is mentioned. The [inclusion policy](https://f-droid.org/docs/Inclusion_Policy/) (consulted on 2026-10-17)
   requires a "100% FLOSS" build chain and only admits precompiled binaries from a closed list (Debian, Maven Central and Google
   Maven if free, Android SDK, Flutter, Hermes, PyPI, Rust, Go, Node): **.NET/NuGet is not on it**. Kotlin remains the only route
   to the main repository.
2. **Kotlin versus MAUI, given what is already done: confirmed.** The format, the encryption, the protected notes and the profile codes are already
   ported (82 tests). What remains is the engine (`SyncService`, ~765 reference lines) and the transports. MAUI would additionally require installing the
   workloads and the SDK on this PC and giving up F-Droid.
3. **New: Google developer verification.** According to [the official page](https://developer.android.com/developer-verification)
   (undated; content from June 2026 or later): from **30 Sep 2026** in Brazil, Indonesia, Singapore and Thailand, on
   certified devices with Android 7+, only apps from verified developers can be installed, **also outside Play**; in **2027**
   it extends worldwide. There are "limited distribution" accounts (students and hobbyists, **up to 20 devices**, no
   document or fee) and an "advanced flow" for expert users (planned for August 2026, in preview). F-Droid considers it
   an [existential threat](https://thenewstack.io/f-droid-says-googles-android-developer-verification-plan-is-an-existential-threat-to-alternative-app-stores/)
   because it signs with its own key. The page says nothing about `adb install` or about how apps signed by a store will be treated:
   **not on record**. Consequences for Aldune: for personal use in Spain nothing changes until 2027 (and a limited account covers both
   devices); to publish, register the `applicationId` and the key before the global extension, and a **reproducible build from the
   first day** so that F-Droid serves the developer-signed APK (a single registered key for GitHub, IzzyOnDroid and F-Droid).
4. **Reproducible builds** ([documentation](https://f-droid.org/docs/Reproducible_Builds/)): F-Droid rebuilds, copies the APK signature from the
   developer and publishes it only if it matches byte for byte. Known pitfalls: timestamps, non-deterministic `baseline.prof`
   (disable the ArtProfile tasks), `vcsInfo` of AGP 8.3+ (clean tree at the tag or disable it), number of cores (R8 8.6.33+),
   same JDK and AGP versions, and signing with `apksigner` from build-tools 34 (the one from 35 is not verified by `apksigcopier`).
5. **Metadata** ([documentation](https://f-droid.org/docs/All_About_Descriptions_Graphics_and_Screenshots/)):
   `fastlane/metadata/android/<locale>/` with `title.txt` (≤50), `short_description.txt` (≤80), `full_description.txt` (≤4000),
   `images/icon.png`, `images/phoneScreenshots/`, `changelogs/<versionCode>.txt` (≤500). `en-US` is the fallback language.
6. **Dependencies**: AndroidX, Compose, M3 Adaptive, Room, WorkManager, Glance and OkHttp are on Google Maven / Maven Central and are free:
   accepted. Google Play Services, Firebase, ML Kit and analytics or ad SDKs are forbidden (a future QR, with ZXing).
7. **Compose Material 3 Adaptive** ([release notes](https://developer.android.com/jetpack/androidx/releases/compose-material3-adaptive),
   consulted on 2026-10-17): stable **1.3.0** (12 Aug 2026); 1.2.0 has been stable since 22 Oct 2025; in alpha 1.4.0-alpha03
   (7 Oct 2026). Use the stable one with `NavigableListDetailPaneScaffold` (it handles "back").
8. **Correction to §3.3 for Android:** the widget (Glance) is painted from the app's process, which can decrypt with the
   Keystore key; **there is no need to write a clear-text snapshot**.
9. **Not re-checked** in this review: IzzyOnDroid's policy and the anti-features list (Aldune should carry
   none: no tracking, free own server). What §13.1 says still holds.

# Aldune — Project status

> **Note (2026-10-17):** the step-by-step implementation plans (phases 1-3, 1.0, 1.5 and linked notes; all already published) were removed from the tree as residual; they remain in git history. The specs in `docs/specs/` are kept.

Continuity document: if you pick this project up again after some time, start
here. Everything important lives in the repository (specs, commits and these
documents).

## What Aldune is

A note app for Windows (WPF/.NET 10), inspired by Hold My Notes / Noty /
noty-sepia (macOS): notes anchored to the screen edge that fan out when the
mouse hovers over them. See the full design in
`docs/historia/specs/2026-08-30-aldune-v1-design.md` — that document is the
design authority; everything else (plans, code) is argued against it.

## What is done (merged into `main`)

- **Phase 1** (plan removed from the tree):
  window mechanics — pill anchored to the edge, hover with fan-out, note
  window that takes focus correctly, animation honoring the Windows
  accessibility setting. A single monitor, fake in-memory notes.
- **Phase 2** (plan removed from the tree):
  real persistence — SQLite, AES-GCM encryption with a DPAPI-wrapped key
  (bound to your Windows user), CRUD, trash/archive, autosave with
  debounce and forced save on close, startup error handling (inaccessible
  DPAPI key, corrupt database, wrong key for an existing database —
  the three cases the spec asked for, each with its own
  message and behavior).
- **Visual polish after Phase 2** (no formal plan, a round of direct tweaks):
  removed the default Windows blue highlight on the dock tabs
  (now the note's own color darkens on hover), varied colors for each new note
  (a palette of 6 pastels, rotating), and "title" treatment: the first line
  of each note is shown in bold on the dock tab and as the window title —
  no new title field, presentation only (`NoteTitleHelper.GetTitle`, in
  `Aldune.Core`, purely computed from the existing text).
- **Fix for the two pending visual bugs + modern visual polish**
  (bounded, no formal spec/plan — see details in "Visual bugs" and
  "History" below): scroll in the expanded panel, mitigation of the
  expansion glitch, resting pill with margin/rounded corners/native Windows 11
  shadow, note window tinted with the note's color, per-note color
  pills visible on the resting pill, and a selector to change the
  color of an already created note.
- **"Archived" button + `NotesManagerWindow`** (bounded — see details in its
  section below): "Restore" in `NoteWindow` for non-active notes,
  automatic trash purge after 30 days
  (`NotesRepository.PurgeExpiredTrash`), and a separate window
  (`NotesManagerWindow`) to archive/restore/delete in bulk with a
  filter by state. The "Archived"/"Active" toggle that was
  added **to the dock itself** was later removed by the fan-tab
  redesign (see below) — `NotesManagerWindow` is now the
  only way to see archived/trashed notes.
- **Fan-tab redesign in the Hold My Notes style** (architectural —
  see details in its section below): expanded dock panel
  rebuilt with staggered tabs, a vertically rotated text label per
  note, and the note opening by growing from the position of its own tab.
- **Phase 3a** (plan removed from the tree):
  first sub-delivery of Phase 3 — a real `EdgeDockWindow` for each
  connected monitor (before, only the primary one), with real Win32
  geometry and DPI (`MonitorEnumerator`, `app.manifest` with `PerMonitorV2`), and an
  app-level `AppCoordinator` so that notes and the management window
  are not duplicated across docks. Verified against the user's two
  real monitors (one in portrait), including the manual interaction
  checklist — it found and fixed a real refresh bug between docks
  (see history below) and, as a result of testing it, the
  pill/panel size was also made to adjust to the number of notes instead of being fixed.

Tests: 506/506 passing (`dotnet test tests/Aldune.Core.Tests --no-restore`).

- **Display changes, layout and synchronization**: when rebuilding the docks because of a change of
  monitors, the physical screen of each open note is kept and restored when it becomes
  available again; the rebuild is retried for a few seconds because Windows can publish the change
  before it finishes enumerating the screen. `Restore original positions` opens all notes
  of the view, recovers the saved positions and places the remaining ones near the dock edge,
  not in the center. `Normal cascade` was removed.
- **Updates from Settings**: the Application section includes `Check for updates…`, which
  uses the same release checker as the tray and lets you open the download when there is a
  newer version.
  Manage Notes uses adaptive text so it is also legible over dark notes. Sync allows
  limiting a link to a tag and keeps that filter in shared profiles.

- **Final logo (2026-10-26)**: the three-note «A» replaces the three tabs with a bar; a single drawing from which come
  all the formats (`scripts/branding/generar-iconos.py`: `.ico`, store listing, Android adaptive and monochrome icon,
  status icon, shortcut, widget, favicon, README). It ships in Windows 1.7.14 and Android rc10. Colors, usage
  rules and what was discarded (alternative C, stacked notes, is kept): [`BRANDING.md`](BRANDING.md).
- **Android, widgets and bubble (2026-10-26)**: the widget picker offers the deck in 4×2, 2×2 and 4×4 and a 1×1 new-note button
  (the three decks share `DockWidget`). The bubble is provided by the system since Android 17 (long press on the icon): there is nothing
  to build; decision and what remains untested in `DECISIONS-2026-10-26.md`. The note color picker no longer loses the
  color it was opened with (the black of a retro look) and warns when «same color on all notes» hides it.
- **Synchronization**: [`SYNC.md`](SYNC.md) begins with a comparison of the three types (shared folder, own server, WebDAV),
  what each platform supports, a step-by-step for each and the most common errors. Android only supports own server and WebDAV.
- **Android, editor bar and dragging tasks (2026-10-26, branch `android-varios-perfiles`)**: the bar above the keyboard no longer scrolls
  (undo, redo, checkbox, bullet, indents and «⋯»; move line up and down go to the «⋯» menu for TalkBack). Tasks carry a three-line
  handle on the right: dragging it moves the task (`LineMovement.moveTo`, `TaskDrag` in `android/text`, gesture and drawing in `TaskDragUi.kt`).
  Tested on the Pixel.
- **Checkboxes with a tick, large ones and welcome (2026-10-26, 1.7.15 and rc11)**: what is stored is still ☐ and ☒; what changes is how they are drawn. Own
  font AlduneCheck (`scripts/branding/generar-fuente-check.py`, Segoe box with ☒ = box + tick; a real ☑ was discarded because of Segoe's black square),
  behind the note font on Windows (line spacing is still set by the latter) and as a style for the checkboxes only in the Android editor.
  Setting «Larger task checkboxes» (+25 %, also the click area on Windows; in `ConfigFields` as `largeCheckboxes`). Android: first-time welcome,
  only on installs without notes (`welcomeSeen`), and a «Remove done» button visible above the bar when there are checked tasks.
  Sync on returning to the foreground already existed (minimum 30 s). Not done, on purpose: «add item» without opening the note (widget or notification),
  QR and pasting a profile code, background sync transparency.
- **Windows accessibility (2026-10-29, in `main`, released by 2.1.4 and 1.0.2):** code audit and fixes. (1) Visible keyboard focus: `AlduneFocusVisual` style (2 px ring, `AppPalette.FocusRing` color, computed from each palette with ≥ 3:1 over all its backgrounds, tested in the 4 palettes and the retro ones with extreme colors; it is NOT a palette key because that list is the contract shared with Android) in all button styles with their own template and as the default value of `Control`; in the note, a ring in the ink color of that face. (2) The color pills are `ToggleButton` (Tab enters once, arrows, Space/Enter, name and state for the screen reader). (3) The note manager: **Enter opens the note, Space checks it, arrows move up and down** (before, a row had focus and did nothing) and an optional global shortcut `Ctrl+Alt+M` (off by default; three new fields in `AppSettings` and `ConfigFields`). **Known limit (fixed afterwards, see «Dock by keyboard»):** the dock tabs were mouse-only (`WS_EX_NOACTIVATE` on purpose, so as not to steal focus when the cursor passes over). (4) Windows high contrast: `AppPalette.HighContrast` with the system colors (7:1, tests with five Windows themes), live on enabling or changing it; the retro looks are replaced by the plain skin. (5) «Text size» 100/115/130/150 % (`TextScale`, `AppSettings.TextScalePercent`, default 100): the type scale went from `StaticResource` to `DynamicResource` and `AlduneTextCaption` (12) and `AlduneTextLarge` (14) were added; fixed-size windows grow with it (`ThemeManager.ScaleWindowToText`); in the note, `Ctrl`+wheel, `Ctrl`+`+`/`-` and `Ctrl`+`0` zoom only that window. **Not scaled** (on purpose): icons and glyphs, the text of the dock tabs and the notice windows. (6) Buttons of the note search bar: 24 px and focusable.
- **Windows 2.1.5 — dock by keyboard (2026-10-30):** fixed global shortcut **Ctrl+Alt+D** (`HotkeyBinding.DockFocusDefault`, like Ctrl+Alt+H it is not configurable: it is the only way in, so it cannot hide behind a setting). `AppCoordinator.FocusDockWithKeyboard` shows the dock if hidden and calls `EdgeDockWindow.EnterKeyboardMode` on the one under the cursor: it opens the fan, lifts `WS_EX_NOACTIVATE` (`AllowActivation` + `ForceActivate`, the same path as the tag editor) and focuses the first tab; while the mode lasts the hover probe only holds the fan open. Up/Down move between tabs, Home/End jump to the ends, Enter or Space opens the note (`OnTabClick`), Tab reaches the footer buttons, Esc (or pressing Ctrl+Alt+D again) gives the keyboard back to the previous foreground window; clicking elsewhere or opening a note ends the mode without grabbing the focus back (`OnDeactivated`). The tab button style now uses `AlduneFocusVisual`, since the default dotted ring was almost invisible. Verified with a real-window probe (physical keys: enter, Down, Down, Up, Enter opens the second note, the dock folds again, Esc returns the focus). New text `QuickHelpKeyboardDock` in the five languages; `QuickHelpKeyboardManagerOff` no longer says the dock is mouse-only. Test: `DockKeyboardHotkeyTests`.
- **Windows 2.1.4 and Android 1.0.2 (2026-10-30):** Settings with the same layout on Windows and Android (General, Appearance, Notes, Colors, the one specific to each platform, Sync; table in `docs/SETTINGS_PARITY.md`; Windows gains the Appearance page, Android the Colors tab and language moves to General). Telecom look: the channel wave shares a row with the dock tab preview and the note grid leaves a margin on the left. Retro looks: dock and manager icon buttons with a 1 px bevel and no drop shadow, so the icon looks centered. Softer sounds, eraser sound, suggested sounds when turning on, silence notice on Android. New **grid notebook** look (light and dark, modes 15 and 16, Windows and Android; `text-color.json` vectors regenerated only with the new entries). Android Settings with chips that wrap onto lines instead of scrollable tabs. In bevelled looks the face of the icon buttons has a uniform outline and the shadow goes outside (the icon stays centered); rules and grid of the notes follow the text zoom and leave room for the cursor; «Text size» reaches rows created in code; Windows Settings with a maximum width; minimized windows come back to the front. `ALDUNE_DATA_DIR` environment variable to test a development build with separate data. `PRODUCT.md` and `DESIGN.md` at the root hold the design context.
- **Windows 2.1.3 (2026-10-30):** (1) in the XP looks the title of Settings and of the note manager is centered in the bar, at the height of the window buttons (`VerticalAlignment=Center` with the gradient bar). (2) The empty-note placeholder («Escribe algo…») was painted **below** the text box, which is opaque in the plain, XP and Bash looks, and was not visible; now it goes on top (`Panel.ZIndex`, without capturing clicks). Reviewed with no changes: «+» of the dock and of the manager (centered to ≤ 1 px in the 15 looks), bevel of the dock and note borders of dark XP, high contrast (own palette with 7:1 tests). No changes in Android or in the sync format.
- **Windows 2.1.2 (2026-10-30):** the resting dock is no longer covered by a note. Notes and docks are Topmost windows and among them the last activated wins, so a note dragged, activated or resized over the folded strip covered it and you could not see where the dock was (with the fan open it was already on top). Now `NoteWindow` calls `AppCoordinator.RaiseDocksAboveNotes()` (→ `EdgeDockWindow.RaiseAboveNotes()`, `EnsureTopmost` without activating) on activation and when a drag is released. Verified with a probe with a real mouse (drag by the handle, click, resize and fan): without the fix the note covered the strip 100 % of the time; with it, it is visible. **Trade-off:** a note stuck to the screen edge may have its close button under the strip (it can still be moved). **Update notice:** an unanswered new-version notice was replaced by a more recent one only on the next poll (every 24 h), and «Download» opened the page of the version it showed even if there was already another. Now, while a notice is pending it is checked again every hour (`UpdateNotifier.PendingRecheck`) and «Download» queries once more (5 s margin; without network it opens the notice's one) and opens the latest. Verified with a probe with a simulated query and browser (`UpdateNotifier` accepts both as optional parameters). Sync server unchanged. **`scripts/update-sync-server.sh`** (`37b4c3e`): the single `curl` to `/health` right after `up -d` exited with code 56 and made the script count as failed even though the server started fine; now it retries for up to 30 s (0 if it responds, 1 with a message if not).
- **«Text size» on text without its own size (2026-10-30, shipped in 2.1.4 and 1.0.2):** the `Window` style in App.xaml only matches the exact type and the `App.OnStartup` handler did not set `FontSize`, so everything that does not set its own (the manager's Export/Archive/Restore/Trash buttons, Sync labels, checkboxes) stayed at a fixed 12 px when raising the size. Now the handler sets `AlduneTextCaption` (12: at 100 % nothing changes) and the dock is fixed at 12 by hand because it does not scale. The numeric boxes in Settings (sync interval, hide-tasks delay) go from `Width` to `MinWidth` so the number is not cut off. Verified with a probe at 150 % in dark XP.
- **Note manager on large screens and documentation in English (2026-10-30, shipped in 2.1.4 and 1.0.2):** the manager body (tabs, search and list) stays at a maximum width of 1100 px and centered when the window is maximized (title bar and footer still go full width). README, SECURITY, CONTRIBUTING and the issue templates move to English by default (`README.md` in English, `README.es.md` in Spanish); `docs/` was translated to English the same day and afterwards. README GIF and screenshots redone with the current interface (Light and Dark looks, travel notebook, field notebook and grid notebook) and Fastlane screenshot 4 with the new Android Settings tabs; the Android sync help and SYNC.md say «Settings → Sync».
- **Polish of tray, bars and margins (2026-10-30, shipped in 2.1.4 and 1.0.2):** (1) The tray menu set `BackColor`/`ForeColor` when created while the renderer read the palette when painting: in light XP it came out with a dark body, beige strip and an illegible «Exit»; now they are refreshed on opening. (2) The scroll bar of Settings, manager and lists goes from a filled block with accent and border over a gray track to a thin pill with the look's muted ink, over a transparent track (accent only when dragging), like the notes'. (3) Text margins: in dark Telecom there were 22 px between the grid edge and the text on the left and 5 on the right; the Windows text box now carries 15 px of right padding (the bar shifts 15 px to stay glued to the edge) and the Android editor 12 dp (`TEXT_END_PAD`) on the right; when a line wraps up to one letter is left over, so the right gap oscillates around the left one (measured: 22/21 px on Windows, 40/36 px on Android with the longest line). Quick accessibility audit: no icon button without a name or tooltip.
- **Windows 2.1.1 (2026-10-30):** the note color stripe on the dock tabs (look with «color only as a stripe») now follows the rounded corners instead of sticking out straight (`StripeBar` is a border with the card's `CornerRadius`). No further changes from 2.1.0.
- **Windows 2.1.0 and Android 1.0.1 (2026-10-30):** «travel notebook» and «field notebook» looks (light and dark, with paper under the text on Windows and Android); paper and pen sounds (events open, close, check, delete and type, volume, off by default; `scripts/sounds/generar-sonidos.py`); automatic copy of notes to a folder (`AutoExportPlanner`, never deletes, protected ones left out; Android via SAF and WorkManager); on Android also an editor bar with icons, visual color picker, «Apply to existing notes», search/settings/«+» at the bottom and new-version notice. Design in `docs/specs/2026-10-30-libreta-viaje-sonidos-y-revision-design.md`, parity in `docs/SETTINGS_PARITY.md`.
- **Windows 2.0.0 and Android 1.0.0 (2026-10-29, published):** «definitive» versions after a round of polish. Optional display name for protected notes (`ProtectedTitle`/`protectedTitle`, in the clear, ≤ 60 characters, travels in the envelope without raising format 4; earlier versions lose it if they resend the note; `docs/PROTECTED_NOTES.md`); menu on long-pressing a note on Android (no archive or delete: that is swiping); Move up/Move down as an alternative to dragging; export all notes as a zip and a notice that without sync there is no backup; Android Settings in tabs (General, Appearance, Notes, Notices; the simplified mode stays on a single screen), more air under the rules and between groups, and the setting «Keyboard on opening a note» (only new by default / all / never; `KeyboardOnOpen` in domain); TalkBack semantics (headings, state, live regions); QR in landscape. Security (audit of 29-10, no blockers): name trimming without splitting emojis, release with minimal permissions (read-only `build` and a separate `publish`), Android job in CI, limit on failed token attempts on the server (`ALDUNE_SYNC_MAX_AUTH_FAILURES_PER_MINUTE`, `ALDUNE_SYNC_TRUST_PROXY`), a test that redirects do not forward `Authorization` to another host. **Known limits:** the Windows dock is still mouse-only (the keyboard route is the manager and its optional shortcut `Ctrl+Alt+M`); no real TalkBack with voice or Narrator tested; `usesCleartextTraffic` is still allowed on Android because `network_security_config` does not support IP ranges (the app's notice covers the case outside the local network); the display name is stored in the clear on the device, in the widget and in the notice.
- **Real-use polish (2026-10-27, in `main`, released by 2.1.4 and 1.0.2)**: (1) Windows synchronizes **all enabled profiles**, not just the one chosen in Settings; `SyncBase` and
  `SyncConflict` become keyed per profile (`ProfileKey`, migration that keeps the rows in the first profile), details in `SYNC.md`. (2) Android: the note color as the
  **background of the whole card** (as on Windows) by default, with a switch in Settings → Appearance; the terminal looks (Bash, Telecom, Phosphor)
  default to a stripe and choosing a look resets the setting, like corners and signal. (3) Windows: `NoteColorAsStripe` setting («color only as a stripe»,
  reuses Bash's Stripe card in the full-face looks); in Bash/Telecom/Phosphor the checkbox is off with a notice. Tested in the emulator of
  Then, the same day: sync downloads notes 8 at a time (before, one by one: with an own server or WebDAV the latency was multiplied by the number of notes),
  on Windows and Android; and **opening a note syncs first** (30 s rule, `NoteOpenSync` / `SyncEvent.NoteOpened`) with a «Syncing…» while
  the pass runs, without blocking editing (decision: without network you could not write, and conflicts are already saved). Android: the editor iterates over a copy as
  String when styling the checkboxes (held delete on long notes). The notice was tested with a WPF probe on Windows; on Android it has not been seen (the emulator has no sync profile).
  Android accessibility (27-10, emulator with 200 % font and dump of the accessibility tree): the list, the editor and Settings come out with all controls
  named and at least 48 dp, and at 200 % nothing breaks (tabs scroll). Changes: the symbol of the editor bar buttons (↶, ⇥…) is no longer
  read together with the name, and each note in the list carries accessibility actions (archive, trash, restore, delete) as an alternative to swiping. **Not tested with
  real TalkBack**; dragging tasks by the handle still has no accessible alternative (the text is a single field).
  Android (list, switch, looks, task checkboxes) and with a WPF probe on Windows (note window in Dark, Pastel and Midnight, with and without
  stripe: the stripe only appears with the setting, the background is the app's and the text is readable). Not tested with mouse (checkboxes) nor the dock.
- **Android, several sync profiles (2026-10-26, branch `android-varios-perfiles`, published)**: the phone can join more than one profile
  at a time (e.g. «all my notes» + the family's shared note). Each profile has its id, its secrets file, its state, and
  `sync_base`/`sync_conflict` per (profile, note) (Room v3, with migration); notes and tombstones are still per device. A pass goes through
  the profiles one by one and the failure of one does not stop the others; an additional profile never offers «discard». The single profile of
  earlier versions migrates by itself (id `default`). Creating and sharing profiles is still a Windows matter. Pending from the sync
  extension: QR and pasting/sharing the code, background sync transparency, a pass on errors and conflicts. Spec and plan in
  `docs/specs/`. Tested in the emulator (migration from one profile, second profile, duplicate, disconnecting one); not tested on the Pixel.

- **Simplified mode on Android and default settings (2026-10-27):** Settings has a «Use simplified mode» button that leaves in view only sync, look, language and the version (the rule lives in `SettingsSections`, `android/domain`, with a test; hidden settings do not change). Large task checkboxes come enabled on **new** Android installs (whoever already had the app keeps what they had). On Windows «Show pending tasks in the reminder» was moved from the Dock page to «Notes and tasks»; the rest of the default values was reviewed and left the same (see `docs/DECISIONS-2026-10-27.md`). Custom themes remains Windows-only.
- **Note shared with the family (2026-10-27):** works with a «Family» profile with tag scope (`familia`) and **its own sync service**
  (`aldune-sync-familia`, port 8100, `aldune-familia.polacabezon.com`): a profile with a different key cannot share a store with the personal one
  because Aldune refuses to write if it cannot read any note (`SyncService`, «sync code can't read the notes in the store»). See `SYNC.md` section D
  and `servidor-config/docs/actualizar-contenedores.md`.
- **Sync security audit (2026-10-27, in `main`, released by 2.1.4 and 1.0.2):** done: WebDAV on Android discards an `href` that changes host (before, it sent
  `Authorization` outside); a remote delete does not reach a note that here is out of tag scope (Core and Android); `ApplySyncTombstone` in one
  transaction; remote is not applied to a note that changed during the pass; 8 MB cap per response in the transports (Core and Android); the server
  rejects the sample token from `.env.example`; the password of a protected note on Android requires 8 (like Windows; unlocking still accepts from 4).
  A delete **without a signature** no longer sends the note to the trash (M2: with the token and without the key the notes could be emptied, and the purge at 30 days made it permanent;
  a local mark did not work because the «trash» state travels as a normal note): it is ignored and removed from the store, and the note is published again (Core and Android,
  scenarios E12a/E12c/E23b updated). **Open:** discarding authentic envelopes dated more than a day ago, `AppSettings` shared between the sync thread and the UI, non-root `USER` in the
  server Dockerfile (volume permissions must be checked in CT 103), token attempt limit, lockfiles and `dotnet list package --vulnerable`.
- **Second security pass (2026-10-27, in `main`, released by 2.1.4 and 1.0.2):** reviewed the WPF UI, `SecretVault`, `syncclient`, `QrScanActivity`, DPAPI/`SettingsService`,
  the installer and the workflows: nothing high. Done: a profile code with `user@` in the URL is rejected (Core and Android; before `https://good-host.com@attacker.tld/`
  looked trustworthy) and when connecting from a code the real host is shown in bold with a warning; `http` outside the local network asks to tick a checkbox before
  connecting (`isCleartextOutsideLan`); `FLAG_SECURE` on the code field only in production; credentials are not printed in `toString()`; `SettingsService.Save`
  serialized (lock, own temp file and flush to disk); the Windows release checks the signature before packaging and publishes `SHA256SUMS.txt`; Inno Setup pinned
  at 6.7.1; the sync server runs as non-root and without capabilities (**an already existing volume needs `chown -R 1654:1654`**, see `SYNC.md`); Gradle dependency
  verification (`android/gradle/verification-metadata.xml`, see `RELEASING.md`); CI fails on vulnerable NuGet packages; clean Android lint (the
  `@RequiresApi` was on `QuickListRow` instead of on `TileRow`). **Discarded, with reasons:** entropy in DPAPI (any process of the user reads it from the binary and the migration
  could leave the notes inaccessible), rejecting UNC paths and reparse points in linked notes (breaks a NAS or OneDrive), NuGet lockfiles (in locked mode they were not
  honored when tested), discarding old-dated envelopes on the client (would break a device switched off for several days: an unchanged note keeps its date), token
  attempt limit (the token is compared in constant time and those behind NPM share an IP; a long token is enough, the server warns if it is short). Dragging
  tasks on Android does have an accessible alternative: the «move up/down» buttons of the editor bar. Untested: Docker (daemon off), the release workflow
  (only runs with a tag) and the new interface on screen.
- **Code review of what changed since 1.7.17 (2026-10-27, `/code-review` high; in `main`, released by 2.1.4 and 1.0.2):** fixed with a test (red before): a remote edit newer than a **local tombstone** was never
  applied (regression of the «do not overwrite a note edited during the pass» change: the local snapshot only has live notes; on Android
  this does not happen, `ExpectedVersion.Tombstoned`); a WebDAV object above 8 MB brought down the whole pass (now it is skipped, Core and Android); a download failure still
  downloaded the whole store (now it cuts what is queued); opening a note synced even if the profile was manual-only. **Fixed afterwards:** deleting a sync profile purges its bases and conflicts (`DeleteSyncProfile`), so the signal of its notes no longer stays at «pending» (the case of a note that leaves the tag scope without deleting the profile remains, only cosmetic). **Open, on purpose:** the timer also syncs manual profiles when there is another
  automatic one (decision of the multi-profile sync); `SyncService` copies the loose fields to `AppSettings` from its thread while Settings may be editing them (the lock only
  orders the saves). Discarded as false: a delete without a signature cannot touch out-of-scope notes (it only removes its own object, and only if there is no live note with that id).
- **Verified on screen (2026-10-27):** Android emulator with the debug APK: a code with a public `http` server shows the host in bold, the HTTPS warning and the checkbox (Connect disabled until ticked); one on the LAN (`192.168.x`) asks for no checkbox; one with `user@` is rejected as damaged. Windows with real windows: the whole `Aldune.Ui.SmokeTests` (dock, menu with physical click, no white strip when growing) and a probe with a physical click on a task checkbox in Dark, Light, Pastel and Midnight (marks ☒ and reads well). `FLAG_SECURE` was not seen: it only applies in production.
- **Android: tapping a checkbox no longer puts the cursor there (2026-10-27, in `main`, released by 2.1.4 and 1.0.2):** the editor looked at the touch in the Initial pass «without consuming anything», so the field put the cursor (and opened the keyboard) as well as checking the task, and in a long list the keyboard covered what was being checked. Now, if the touch lands on a checkbox, the «down» is consumed (not the scroll); outside a checkbox everything stays the same. Verified in the emulator before and after (before, the cursor bar stayed next to the checkbox; now it stays where it was). No instrumentation test: pending confirmation on the real phone.
- **`/code-review ultra v1.7.17` (2026-10-27; 10 findings, all verified against the code and fixed in `main`, released by 2.1.4 and 1.0.2):**
  (1) deleting a sync profile **deleted its unresolved conflicts** (they hold the only copy of the lost version): now it only deletes the bases (my regression);
  (2) a WebDAV object above the cap was treated as «does not exist» and the local copy was **uploaded over it**, overwriting another device's content on every pass: now the transport
  records `UnreadableIds` and sync leaves them alone (Core and Android; on Android they go into `excludedIds`); (3) with `ResponseHeadersRead` the HttpClient timeout no longer covered the body
  and a server stopped midway **hung the sync and froze «Sync now»**: now the timeout covers the whole request (my regression); (4) the bases migration left all
  the old ones in the first profile even if another one synced: `SyncBaseMigration` moves them only once to the profile that was active (mark `SyncBasesKeyed`); (5) with several profiles,
  one without network marked the whole pass as failed and **windows were not repainted** even if another had brought changes: now it repaints if `SyncResult.AppliedChanges`; (6) the
  timer and opening a note also synced the **manual** profiles: `Synchronize(automaticOnly: true)` skips them («Sync now» goes through all); (7) the sync signal
  counted bases of disabled or out-of-scope profiles: `BaseKeysCovering` only counts the enabled ones that cover the note; (8) the non-root server with a root-owned volume
  started «healthy» and returned 500 to everyone: now it **fails at startup with the `chown` in the message** (tested in Docker); (9) on Android the checkbox read `hiddenSpans` from the first
  composition (my regression): `rememberUpdatedState`; (10) the bases key was read after the network and a profile change in Settings mid-pass contaminated it: it is fixed at the start.
  The one about the local tombstone and the one about the download-test timing were already fixed before.
- **Second local review (`/code-review` high on the ultra fixes, 2026-10-27; 9 findings, 6 fixed):** (1) `Synchronize()` returned a false success with sync off
  and nothing to do (it sounded «done» and enabled the pruning): now the error is silenced only in the automatic pass; (2) with a partially failed profile «done» and
  «failure» sounded at the same time and `_syncedSinceStart` was set: `AfterSync(fullySucceeded, …)`; (3) **the bases migration moved the first profile's to the chosen one on installs of
  1.7.18-rc1** (which already used per-profile keys): now it only moves if THAT opening migrated the old table (`LegacySyncBasesMigratedOnOpen`) and a failure does not prevent starting; (4) a profile
  that covers the note and never synced it does not count as up to date (`GetSyncBase` requires a base from each profile that covers it); (5) old settings without a profile list gave «pending»
  always; (6) BOM in the new files; and the scope is read directly from the profile, without creating a settings object for each refresh of the signal. **Discarded, with reasons:** conflicts that
  remain after deleting a profile (that is intended: they stay in the conflict list and are resolved from there); `TransportFactoryOverride` (internal, tests only); and an object with a
  **format this client does not understand (`FormatException`) is still replaced on upload** in the four transports: it is the documented design in `FolderSyncTransport.Write` («replacing an
  invalid file is the way to recover it») and it already existed; changing it (leaving the unreadable alone) touches all transports and Android and must be decided calmly in case a
  broken object gets stuck forever. **The published `v1.7.18-rc1` predates all these fixes.**
- **Versions (2026-10-27 night):** Windows **1.7.18** and Android **0.1.1** (`versionCode` 13) **published** (the Windows one with `SHA256SUMS.txt`; they include the sync security fixes, the cursorless checkbox, the host warning and the WebDAV timeout). Before: Windows 1.7.17 and Android 0.1.0 (the first stable one, `versionCode` 12, without `-rc`; published with `--latest=false`, see `RELEASING.md`).
- **Pairing QR** (2026-10-27, spec `specs/2026-10-27-qr-emparejamiento-design.md`): Windows shows the profile
  code as a QR (`ShareQr` in Core with QRCoder 1.8.1; `ShareQrWindow` with a warning and closing after 120 s) and Android scans it
  (`QrScanActivity` with CameraX 1.6.2 + `QrDecoder` over ZXing core 3.5.4, without Play Services). It was decided not to split the QR into several
  and that the token never travels. Zero changes to the sync format.

- **Published (2026-10-31): Windows 2.2.0 + Android 1.1.0 in a single release each** (nothing was published in between; list and reasons in `DECISIONS-2026-10-31.md`).
  Done so far: Android bugs (trash rows after restore + Undo, wrong checkbox ticked in a scrolled list, checkbox tap target now includes the left margin and the gap after the box, hanging indent for
  wrapped tasks, Settings tabs scroll to the top, auto-scroll when dragging a note to the list edge); Android look (no ghost card outlines, contrast outline for cards that blend into the
  background, balanced colour rows, aligned options sheet); Windows (centred X of the sync conflicts window, even margins of the manager's bar, no focus ring on the cogwheel after Settings, Ctrl+F bar that
  fits the note, New-note button moved to the filter row in accent, manager buttons follow "Text size", readable disabled text in every look, symmetric dock button bar with four 40 px buttons).
  **Pinned notes** (published in Windows 2.2.0 and Android 1.1.0; spec `specs/2026-10-31-pinned-notes-design.md`), done on both platforms: Core, Room (migration 4 to 5), the sync mapping, the vectors (C# and Kotlin), and the interface. Windows: entry in the note's menu and in the dock tab's right-click menu, a pin marker in the tab's badge row, a pin toggle on each manager row; the deck lists pinned notes first (`ORDER BY PinnedToTop DESC` in `GetByState`) and dragging a tab stays inside its group. Android: pin action in the long-press sheet, in the options sheet and in the editor's three-dots menu, a drawn pin on the row, `DockOrder` partitions pinned first (so the widget and the colour channel inherit it) and drag / Move up-down only act inside the group. **Compatibility:** the pin travels as the optional field `pinnedToTop` of the encrypted payload, written only when true, so the sync format stays 4; an older version (2.1.x / 1.0.x) shows the note unpinned and, if it *edits* and re-sends it, **drops the pin** (checked: both old versions read the new vectors, including the pinned note; only the old writer lacks the field). A key rotation on Windows used to drop the pin (and the visible name of protected notes) because the re-keyed copy did not carry the optional fields; fixed with a test. 25 sync tests cover the pin on both sides.

  **Round after 2.2.0 / 1.1.0 (2.3.0 / 1.2.0: built, tested and documented; waiting to be published)**: per-note done-tasks setting (Core, Windows, Android; Room 5 to 6), local version history (Core, Windows window, Android dialog), Android tags (All chip, all-tags dialog, manual order, `#` setting), bottom sheets following square corners, long-press menu about 350 ms sooner, release check over 100 releases, widget task progress and pin mark, dock view by wheel and an "All notes" view, menus ask for the skin's chrome font. **Checked without a mouse or screen (off-screen probes):** the dock wheel cycle on the four edges, the protected space behind a task box (caret, typing, paste) and the Android sync tick against a local server. The physical Left, Right and Home keys next to a task box were checked with real key events. The widget was seen on an emulator home screen (task progress and pin mark). README screenshots and the GIF were retaken for the new footer bar. **Italian**, **Japanese** and **Simplified Chinese** added as the sixth, seventh and eighth languages (Windows `Strings.T` takes an optional sixth argument, `UiLanguages`, the installer; Android `strings.tsv` column `it`, `values-it`, the language list, and `fastlane/.../it-IT` and `ja-JP` and `zh-CN`; Japanese also in the installer, Chinese is not because Inno Setup ships no Simplified Chinese file, so the Chinese installer shows English); a missing Italian, Japanese or Chinese text falls back to English. Machine-assisted translations that no native speaker has reviewed; both were rendered on screen (Windows Settings and manager, Android list). The welcome message now names the screen edge in each language (it used the English word in German, French and Portuguese). On Android a sync that overwrites a note now keeps the local text in the history first, like Windows. The dock tab menu's "Done tasks" item cycles and stays open, like the note menu. Android's editor and Settings were checked in sixteen looks on an emulator.
  **Windows 2.4.0 (2026-11-01, published):** view label in a fixed place, accent ring instead of the cogwheel dot, wheel session that keeps the dock window, 8 px gap above the button bar, pin colour, «Restart now» for the language, Settings header spacing, tray menu outline from the accent, Ctrl+click on `http(s)` links (`LinkText`, 22 tests). Decisions in `ROADMAP.md` (Round 2.4.0). Closing a note fades the whole window through `ClosingGhost`. Verified without touching the user's screen where possible (off-screen windows, `RenderTargetBitmap`) and with real-mouse probes for the wheel, the tray menu and the README GIF; measured before/after with the same probes against `6655c39` (Settings title x 25 to 21 px, equal to the manager). New smoke-test scenario `RunDockRound24` (cogwheel ring, view label placement, wheel session, closing ghost). The tray menu paints its hovered row itself (`AccentMenuRenderer`): the colour table's `MenuItemBorder` did not control the outline on current Windows. README screenshots and GIF regenerated for the tighter dock. Android 1.2.0 stays built but unpublished.
  **Android editor (2026-10-31, second session):** the note title lives in its own field in the editor header, like the Windows note window (`NoteText`: the note is still one text, first line = title, so nothing changes in storage or sync); the three-dots menu gets Pin, Archive and Trash; the long-press sheet offers Archive, Trash and Restore (the swipe actions, now discoverable), opens fully with no drag handle and scrolls in landscape; the options sheet lists the existing tags as one-tap chips; Settings tabs sit in one row in landscape and in an even 3-column grid in portrait. Decisions and the "do not" list in `docs/DECISIONS-2026-10-31.md`.
  New: Compose instrumented tests on a device (`./gradlew :app:connectedDebugAndroidTest`, see `ANDROID_PROBES.md`) and `ButtonContrastTests`.

## How we work

- **Design**: every large change starts with a specification in `docs/specs/`.
- **Code**: new logic in `Aldune.Core` with the test first; the interface is verified with disposable probes
  (`docs/WPF_PROBES.md`, `docs/ANDROID_PROBES.md`).
- **Integration**: every change is tested before being merged into `main` (`dotnet test` and, on Android, `./gradlew test`).
- **Manual verification**: interactive things (hover, focus, drag) are also checked by hand in the real app.
- The explanations of why something is decided (WPF, DPAPI, Win32) matter as much as the what: it is worth keeping that
  level of detail in the documents.

## Known technical debt, deliberately deferred

These things are identified, not surprises — decide whether to tackle them
when the corresponding phase comes:

- **Phase 1**: the spec asked for a low-frequency mouse position check as a safety net
  for the fan collapse (in case
  `MouseLeave` does not fire, e.g. another window covers the dock) — it was never
  implemented, only the simple delay. It works well in practice but it is a
  real gap against the spec.
- **`ContentCipher` is never disposed** (`Dispose()`) during the life of the
  app — deferred with a warning: a badly ordered shutdown hook could
  turn the save-on-close into an exception and lose the last
  keystrokes. If taken up again, it must go strictly after all
  note windows have closed.
- Several minor hygiene items (a comment explaining why
  `AuthenticationTagMismatchException` is not confused with the DPAPI case in
  `App.xaml.cs`, deduplication of the `SqliteConnection.ClearPool()` pattern in
  tests, etc.) — no functional impact, maintainability only.
- ~~**The dock's resting strip sometimes disappears completely, by itself, with nothing fullscreen**~~ — **RESOLVED (2026-10-27, confirmed by the user: it no longer happens).**
  Reported on 2026-09-09. The cause was a «layered» window (`AllowsTransparency`) that stopped recomposing after display changes (Windows moves
  windows when monitors are turned off or on). What fixed it, and **is kept**: `KeepWindowInPlace` (500 ms, puts the window back in place or rebuilds the
  docks), `KeepStripOnTop`, reading the strip color every 5 s with a repaint after two unpainted readings (`RepaintStripIfUnpainted`) and detection of
  monitors turned off via DDC/CI. The **dock diagnostic log was removed** (`logs\dock.log`: dock states, displays, power and session); only
  `logs\errores.log` (`AppLog`) remains, with the unexpected errors of background tasks (sync, reminders, linked files, sounds). History in
  `docs/DOCK_DIAGNOSTICS.md`. If the failure came back, the log must be restored from git history (`git log -S RecordDiagnostics`).

## Prerequisites for Phase 3, rest to do (sub-deliveries 2+)

Original points 1 and 2 (app-level coordinator, real Win32 geometry/DPI)
are already resolved by Phase 3a — see the history below.
What remains:

1. **`ScreenOrigin` is still fixed to the literal `"primary"` on all notes.**
   A future sub-delivery has to decide what to do with that sentinel value
   versus real, stable device IDs (Win32 today only
   gives `\\.\DISPLAY1`-style, which changes when monitors are reconnected — see
   `MonitorInfo.DeviceName` in `Aldune.Core`, documented there as "it is not
   the stable id").
2. **With several docks, all show the same complete list of notes**
   (decided on purpose in Phase 3a, while there are no
   stable IDs per monitor — see history). Once point 1 is resolved,
   each dock will be able to start filtering by its own origin.
3. **No Settings toggle** to restrict the app to a single monitor — no
   Settings UI exists yet (`AppSettings` only stores the
   encryption key). Needed if that control is wanted in addition to "on
   all connected screens" (the only mode there is now).
4. **No live hotplug**: connecting/disconnecting a monitor with the app
   already running is not handled (`WM_DISPLAYCHANGE`) — the app must be restarted
   to detect the change. A live DPI scale change without disconnecting the monitor
   is not handled either (WPF with
   `PerMonitorV2` rescales only the existing windows, but the fine
   repositioning of the edge docking has not been tested for that
   case).

## Prerequisites for Phase 5 (import/export)

- `GetByState` orders by `CreatedAt` as ISO-8601 text — it only works
  because all timestamps are written in UTC (`"+00:00"`). An
  import that wrote timestamps with a local offset would silently misorder the notes.
- The export has to decrypt via `ContentCipher`, never dump the encrypted
  BLOBs as they are.

## Design decisions from this last session (some already done, others only decided)

- **Note title**: a real title field is NOT added — the first line
  of the free text is treated as a title for display only (bold, on the
  dock tab and in the window title bar). Already implemented.
- **Viewing archived/trashed notes — DONE, but not as originally decided
  here.** A new list+detail window had been discarded in favor of an "Archived" button that
  temporarily replaced what is seen in the dock. It was implemented that way first, but
  the fan-tab redesign (see below) reverted it: now a separate window
  (`NotesManagerWindow`, see its own section) IS
  the only way to see archived/trashed notes — the dock only
  shows active notes.
- **Modern visual polish — DONE** (see "History: visual polish and visual
  bugs" below for the technical detail).
- **"Vintage paper" mode** (paper texture, per-note tilt, handwritten
  typography, sounds): stays exactly as it was in the original spec, as a separate optional
  package — still not implemented, not to be confused with the modern visual
  polish above (already closed).


## Session history

The chronological session diary (2026-09-02 to 2026-10-17: what was done in each round, with the why and the traps) was removed
from the tree; it remains in git history (`docs/historia/STATUS-2026-09.md` and `docs/historia/STATUS-2026-10.md`).

The specs of already published phases are in `historia/specs/`; the phase reports and
execution plans were removed from the tree (they remain in git history). The current ones remain in `specs/`.

# Android app probes

The tests of `android/*` (JVM) do not cover Compose or the keyboard. The interface is verified on the test device (Pixel 10a over USB)
with the **debug app** (`io.github.nachopola16.aldune.debug`: it has its own database and never touches an installation with real
notes) and real taps through `adb`. It is the counterpart of [`WPF_PROBES.md`](WPF_PROBES.md).

## Preparing

```bash
export JAVA_HOME="/c/Program Files/Eclipse Adoptium/jdk-21.0.12.101-hotspot" ANDROID_HOME="$LOCALAPPDATA/Android/Sdk"
cd android && ./gradlew :app:assembleDebug
A="$ANDROID_HOME/platform-tools/adb.exe"        # adb is not on the PATH
$A install -r app/build/outputs/apk/debug/app-debug.apk
$A shell am force-stop io.github.nachopola16.aldune.debug   # start from scratch
$A shell am start -n io.github.nachopola16.aldune.debug/app.aldune.MainActivity
```

With a phone and an emulator connected, add `-s <serial>` (`adb devices -l`).

## Looking and tapping

- Screenshot: `$A exec-out screencap -p > shot.png` and read it. Screen 1080x2424; **if it is shown reduced (891x2000), multiply the
  coordinates by 1.21** before tapping. In PowerShell, `>` corrupts the PNG: use Git Bash.
- Tap: `$A shell input tap X Y`, with `sleep 0.7` between taps (the keyboard and the recomposition take a while).
- **Virtual keyboard (Gboard): tap its keys by coordinates**, not `input text`. `input text` injects physical key events and does not exercise
  `onValueChange` the way the real keyboard does, which is exactly what is to be tested (Enter and Backspace by text difference).
- Physical keyboard: `input keyevent KEYCODE_TAB`, and with modifiers `input keycombination SHIFT_LEFT TAB`, `ALT_LEFT DPAD_UP`,
  `CTRL_LEFT ENTER`.
- Dragging after a long press (reordering notes): `$A shell input draganddrop X1 Y1 X2 Y2 1500`. A `swipe` is no good: it starts moving before the
  long press is satisfied.
- In Git Bash, `adb shell ... /sdcard/...` converts the path: export `MSYS_NO_PATHCONV=1`. `uiautomator dump` + `cat` gives the view tree (which field has focus).
- `dumpsys input_method | grep mInputShown` says whether the keyboard is open without taking a screenshot.
- State persists between tests (drafts and notes): taps at fixed coordinates fail if the list already has notes. Look at a
  screenshot before trusting a script; `force-stop` does not delete data.

## Reference positions (Pixel 10a, dark theme, Gboard open)

Original 1080x2424: the list's “+” (962, 2242); editing bar at y≈1395 (☐ x=63, ⋯ x=818); Gboard Enter (992, 2172); Backspace (992, 2005);
first editor line y≈332. They change with the keyboard, the language and the font size: a hint, not a contract.

## Editor probe (task 7)

Result on 2026-10-17, Pixel 10a, Android 17, Gboard in Spanish:

| Gesture | Expected | Result |
|---|---|---|
| ☐ in the bar and typing | `☐ a` | ok |
| Gboard Enter at the end of a task | another checkbox below | ok |
| Gboard Enter on an empty task | ends the list | ok |
| Gboard Backspace after `☐ ` | removes the whole prefix | ok |
| Tapping the checkbox | ☐ ↔ ☒ | ok (see “Learned”) |
| ⋯ → Remove done | deletes the done lines | ok |
| Tab / Shift+Tab | indents / outdents | ok |
| Alt+↑ | moves the line up | ok |
| Ctrl+Enter | ticks the line's task | ok |

Third-party keyboard: AnySoftKeyboard 1.13 (F-Droid; `adb install`, `ime enable`/`ime set`; when done `ime set` with Gboard and `adb uninstall`) also passes Enter,
Enter on empty and Backspace. Not tested yet: a real physical keyboard, the tablet, rotating and split screen.

## Learned

- **A change made in the same event as the tap is lost.** The field, on receiving that tap, emits the previous text with the new cursor
  and overwrites whatever was applied before. Ticking the checkbox is postponed one frame (`withFrameNanos`). If a `LaunchedEffect` depends on a
  state, that state cannot be set to `null` at the start: the new key cancels the effect; it is cleared at the end.
- Opening the ⋯ menu takes focus away from the field and hides the keyboard. It is acceptable; the cursor is kept.
- A temporary log (`Log.d` + `adb logcat -d -s TAG`) is the fastest way to know whether a gesture arrived; remove it before committing.
- **The keyboard at startup is a race.** Compose focuses the first focusable element (the search box) and asks for the keyboard; hiding it afterwards with
  `keyboard.hide()` is sometimes lost. The stable solution is for the search box not to be able to take focus until it is tapped (`focusProperties`).
  Check it by starting 4 times in a row (`force-stop` + `am start`) and looking at `mInputShown`.
- **A third-party keyboard can keep a composition region.** Fixing Enter/Backspace in `onValueChange` (with `TextFieldValue`) left the keyboard
  rewriting the half-typed word on the new line. With `TextFieldState` and an `InputTransformation` the fix goes inside the keyboard's own edit
  and there is no mismatch. Always test with Gboard **and** a third-party keyboard; Gboard alone would not have found it.

## Sync probe (F5)

The app talks to a **test** sync server (never the real one) and a simulated “PC” plays the other half. All with temporary data.

1. **Local server with temporary data**: `ALDUNE_SYNC_TOKEN=probe-token-123 ALDUNE_DATA_DIR=<temporary folder> dotnet run --project src/Aldune.SyncServer --urls http://127.0.0.1:5077`.
   The data folder is passed with **`ALDUNE_DATA_DIR`** (without it the server uses `/data`, which on Windows is `C:\data`). It reaches the device with
   `adb reverse tcp:5077 tcp:5077` (the profile URL is `http://127.0.0.1:5077/`); when done, `adb reverse --remove tcp:5077`.
2. **The “PC”**: `ProbePcSimulator` (in `android/syncclient/src/test`, skipped if there is no `ALDUNE_PROBE`). It is invoked with
   `./gradlew :syncclient:test --tests app.aldune.syncclient.ProbePcSimulator --rerun-tasks` and the variables `ALDUNE_PROBE` (`code`, `list`, `put`, `protected`,
   `delete`, `unsigned`), `ALDUNE_PROBE_URL`, `ALDUNE_PROBE_TOKEN`, `ALDUNE_PROBE_KEYFILE` (the probe's key; it creates itself), `ALDUNE_PROBE_OUT` (where it writes the
   result) and `ALDUNE_PROBE_ID` / `_TEXT` / `_STATE`. `code` prints the profile code; `list` shows the store **decrypted with the profile key**, which is
   what checks that what the app uploaded is readable from outside; `unsigned` writes a deletion signed with another key. Each call takes about 20 s (Gradle).
3. **Typing the profile code through adb**: `adb shell input text` loses characters with long strings; it is sent in chunks of 30 with `sleep 0.4`. A badly
   typed code comes out as “damaged”, which also tests that message.
4. **Do not use Back on the tablet in PC mode**: it closes the app and anything typed and unsaved is lost. And to type in the editor you have to tap it first (the keyboard
   only opens with focus).
5. **A conflict is provoked** like this: the “PC” writes the note, and **before** the tablet syncs (the policy waits 5 s after editing stops) something is written on the
   tablet. The pass sees both sides changed since the base: the most recent version wins and the other is kept as a conflict (icon in the row).
6. **Server down with `adb reverse`**: the tunnel accepts the connection and cuts it, so it is not “connection refused” but “unexpected end of stream”.
   It is a connection cut like any other and is classified “No connection”.
7. Pull to sync: `adb shell input swipe 250 450 250 1000 500` over the list. With an empty list it requires the “no notes” message to be scrollable.
8. Check what is left on the device: `adb shell run-as io.github.nachopola16.aldune.debug ls no_backup` (the `syncsecrets.bin` file exists when connected and
   does not exist when disconnected) and `adb shell dumpsys jobscheduler | grep aldune` (WorkManager's periodic job).

## Capture and widget probe (F6)

Results and findings: in git history (`INFORME-ANDROID-F6.md`, removed from the tree). Technique:

1. **Send intents by hand.** Own action: `am start -a app.aldune.action.NEW_NOTE -n io.github.nachopola16.aldune.debug/app.aldune.MainActivity`. Share:
   `am start -f 0x10008000 -a android.intent.action.SEND -t text/plain -n .../app.aldune.MainActivity --es android.intent.extra.TEXT '...' --es android.intent.extra.SUBJECT '...'`.
   **The `-f 0x10008000` (NEW_TASK + CLEAR_TASK) is mandatory when repeating:** after a reinstall or a previous test, the system sees an “equal” intent to the task's root
   (extras do not count for that) and only brings the task to the front: “Activity not started, its current task has been brought to the front”. The note is not created and it looks like an app bug.
2. **Put the widget without the launcher's picker**: Settings → Alerts → “Add the widget”. For the tile, “Add the tile” (it lands on the second page of the panel: swipe).
   Resize: long press on the widget (`input swipe x y x y 900`) and `input draganddrop` on the edge handle. The system picker's widget lives on the phone's **real**
   home screen: remove it when done.
3. **Fix a note as protected to see the widget** (protected ones only arrive through sync): stop the app, copy `databases/aldune.db*` with `adb exec-out run-as <pkg> cat ...`
   (binary output arrives fine this way), open the copy with Python's `sqlite3` (`PRAGMA wal_checkpoint(TRUNCATE)`; `UPDATE note SET isProtected = 1 WHERE rowid = ...`) and
   **return it with `adb push` to `/data/local/tmp` + `run-as cp`**, deleting `-wal` and `-shm` first. Passing the binary through a Git Bash pipe (`cat f | adb shell run-as ...`) ruins it.
   Only with the debug app's database.
4. Look at the notification: `cmd statusbar expand-notifications`; the tile panel: `cmd statusbar expand-settings`; theme: `cmd uimode night no|yes`.
5. **What to look at when testing a widget**: the time at which it is painted with the process alive and cold (Glance reuses the composition session); with 53 notes, the cap and the final row; with the
   preview on, that no strip is clipped; and, with a protected one, that its title does not appear anywhere (`uiautomator dump` does not see inside the widget: look at the screenshot).
6. **The keyboard of a new note**: start 4 times in a row with `force-stop` + `am start` and `dumpsys input_method | grep mInputShown`, as in the editor probe.
7. `logcat -d | grep AppWidgetHostView` says why a widget is not inflated (a class not supported in `RemoteViews`, e.g. a `<View>`).

## Reminders probe (F7)

Results: in git history (`INFORME-ANDROID-F7.md`, removed from the tree). Technique:

1. **Git Bash ruins `adb` paths**: `export MSYS_NO_PATHCONV=1` or `adb shell cat /sdcard/u.xml` fails. To read the screen, `uiautomator dump /sdcard/u.xml` and look for
   `bounds` with `grep`; a function that computes the rectangle's center (`ctr`) avoids tapping coordinates by hand. The tablet and the Pixel have different screens: never reuse coordinates.
2. **Set a reminder a few minutes ahead**: “⋯” menu → “Pick date and time” → OK (today) → the picker starts at the next full hour; tap the hour and the minutes
   (`content-desc="N minutes"`, only multiples of 5) → OK. With “Exact time” on the alarm has `window=0` in `dumpsys alarm | grep -A3 "REMINDER_FIRE$"`.
3. **Kill without cancelling alarms**: `input keyevent KEYCODE_HOME` and `am kill <pkg>` (**never `am force-stop`**). Wait for the notice with a loop over
   `dumpsys notification --noredact | grep -c "pkg=<pkg>.*id="` and note `adb shell date`. **Clear earlier notices first** (the loop exits at the first one it sees).
4. **Deep idle**: `dumpsys battery unplug` → `dumpsys deviceidle force-idle`; when done `dumpsys deviceidle unforce` and `dumpsys battery reset`.
5. **Notice buttons**: `cmd statusbar expand-notifications`; if there are several, they group and each has to be expanded (on the Pixel, `input swipe` downwards on the notice). On the
   tablet: tap the notice's chevron.
6. **Change the zone**: `settings put global auto_time_zone 0` + `service call alarm 3 s16 Europe/Lisbon`; restore with `Europe/Madrid` and `auto_time_zone 1`.
7. **Exact alarms permission**: the “Exact time” switch opens `Settings$AlarmsAndRemindersAppActivity`; tap the row and come back; check with
   `cmd appops get <pkg> SCHEDULE_EXACT_ALARM`.
8. **Swipe to archive does not work with `adb input swipe`** (nor `touchscreen swipe`): test it by hand.
9. On the user's phone there are real notes: create a test note of your own and do not touch the existing ones; delete it when done.

## Retro looks probe

25-10-2026, Pixel 10a with the debug version and test notes. Technique: choose the look in Settings → Look and look at the “Look colors” block; to go through many looks with extreme colors without touching the screen, write `shared_prefs/settings.xml` of the debug app (`appearance` 0-10 and `aspectColors` as JSON with `&quot;`) with `adb push` + `run-as <pkg> cp`, `am force-stop` (the debug one only) and relaunch.

Result: the 11 looks come out with their name; Bash changes the list, editor and dialogs to the terminal palette, with monospace text, and suggests the Serene theme (Using it applies it); the swatches, “Change”, the Ubuntu palette and “Reset colors” (back to Gruvbox) work; light Telecom with white ink and Phosphor with white phosphor are still readable (the text adjusts by itself); XP uses the usual font. No unreadable texts or overflows. The status bar was fixed after the review (the icons follow the look's lightness) and checked with the system in dark and XP light.

**Widget (rc5-rc7, real Pixel, 25-10-2026):** with Bash it looked like the usual Dark, because the widget only paints background, text and strips (`Ground`/`Text`/`Surface`, almost equal in Dark and Bash). The accent was added (the “Aldune” title and the “+”, with `ColorFit` against the background) and the monospace font in Bash, Telecom and Phosphor. Trick: the widget belongs to the real app, not the debug one; to see it a signed release has to be installed, and `adb shell dumpsys appwidget | grep aldune` says whether it is placed. With a monospace font long names are cut earlier with “…”.

## Accessibility probe (TalkBack)

2026-10-30, real Pixel 10a. Turn TalkBack on from adb: `settings put secure enabled_accessibility_services com.google.android.marvin.talkback/com.google.android.marvin.talkback.TalkBackService` and `settings put secure accessibility_enabled 1` (the first time it opens its tutorial: Back twice). **Turn it off when done**: `settings delete secure enabled_accessibility_services` and `settings put secure accessibility_enabled 0`. The speech does not show up in `logcat`; what TalkBack receives is read with `uiautomator dump` (text, `content-desc`, `clickable` and `bounds` of each node: in Compose, clickable containers merge the text of their children, which is what TalkBack reads). With TalkBack on, taps by `adb` are not reliable (a tap focuses, a double tap activates). Result: the tabs, rows and buttons carry a name (“Settings”, “New note”) or merged text, and all touch targets are at least 48 dp (126 px).

## Long checklist probe (checkbox taps)

2026-10-31, real Pixel 10a with the debug app. Create a long list without typing it: write the text with `☐ ` lines to a UTF-8 file, `adb push` it to `/sdcard/lista.txt` and run
`adb shell 'am start -f 0x10008000 -a android.intent.action.SEND -t text/plain -n io.github.nachopola16.aldune.debug/app.aldune.MainActivity --es android.intent.extra.TEXT "$(cat /sdcard/lista.txt)"'`
(the file avoids Git Bash and `adb` mangling the non-ASCII glyphs). Then scroll to the end with `input swipe 600 1800 600 500 400` and tap the checkboxes (x≈63) of rows visible in a screenshot. The counter in the top bar (`N/60`) says how many were
marked, and the screenshot says which: with the old code the counter went up but none of the visible rows was ticked (the taps ticked rows `scroll` pixels higher up, out of view).
`adb shell 'for y in ...; do input tap 63 $y; done'` taps as fast as `input` allows; the "editor stops accepting ticks until restart" symptom was **not** reproduced this way.

## Compose gesture tests

`app/src/androidTest/.../ui/*` use `createComposeRule` and run with `./gradlew :app:connectedDebugAndroidTest -Pandroid.testInstrumentationRunnerArguments.class=app.aldune.ui.TrashSwipeTest` (`ANDROID_SERIAL` selects the device).
Two traps: `swipeLeft()`/`swipeRight()` start at the very edge of the node and do not drag a `SwipeToDismissBox` (use `swipe(Offset, Offset, ms)` between 10 % and 90 % of the width), and Espresso 3.5 (the default dependency) crashes on Android 17, so 3.7.0 is pinned.

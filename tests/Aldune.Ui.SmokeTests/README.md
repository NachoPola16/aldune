# WPF dock regression smoke

Interactive Windows and the .NET 10 SDK. No test framework or extra packages: a WPF STA console that references the application and reuses its dependencies. Exit code 0 = PASS; 1 = failure or failed cleanup.

```powershell
dotnet run --project "C:\Users\nacho\Proyectos\aldune\tests\Aldune.Ui.SmokeTests\Aldune.Ui.SmokeTests.csproj" --configuration Debug
```

Quiet mode: `ALDUNE_SMOKE_QUIET=1` skips the two scenarios that use the physical mouse, grab the real screen and activate note windows (the note grow flash and the note menu toggle). Everything else (including the 2.4-2.6 dock checks) runs without touching the mouse, the keyboard focus or the screen. Use it whenever you are working while the suite runs.

## Priority scenario executed

1. A new SQLite DB in a GUID temporary directory; an ephemeral AES key; two notes, one tagged.
2. A real `EdgeDockWindow` and `AppCoordinator`, a fictitious monitor (100000, 100000), without activating a window or moving the cursor.
3. Opens `OpenDockViewPopup` by reflection; verifies the real WPF `Opened` event.
4. Raises `ButtonBase.ClickEvent` on the tag button built by production. It goes through `OnDockTagChoiceClick`, the coordinator and `Refresh` (two notes -> one and a geometry change).
5. Checks a grace period of exactly four seconds, renewed with respect to the opening.
6. Waits for the real `Closed`: the popup must close, but the dock stay expanded with the same deadline and the collapse timer stopped.
7. Pumps the dispatcher during the real grace period, sampling state/timer; after it expires, verifies collapse by real poll and timer with the cursor outside.
8. Closes the dock/uninstalls the hook through the coordinator, stops all `DispatcherTimer`s, disposes the coordinator/cipher, closes the SQLite pools and deletes the temporary directory.

Only the `Application.Resources` of the production `App.xaml`, embedded by link, are loaded; `Aldune.App` is never instantiated and its startup never runs. No user settings, DB, DPAPI keys or sync are read.

## Second scenario: the note's “⋯” with physical clicks

It creates a real note at (300, 200) and clicks it with `SetCursorPos` + `mouse_event`: open the menu, click
“⋯” again (it must close, not reopen), reopen and close by clicking outside. This one does move the mouse and
needs the window in the foreground: do not touch the mouse while it runs. It exists because the toggle bug
came back three times: with routed events it does not reproduce, only with a real click (see
`PopupToggle`).

## Limits

This delivery was limited, by priority, to **one** test of the specific cause: closing of the picker after changing a tag and refreshing geometry. It does not replace or only repeat `FanStateMachine` tests.

It does not yet cover creating a note directly, `CreateNoteFromMenu` with text, selecting another view, or expiry with the cursor inside. It does not touch the clipboard. The renewal tested is opening -> tag selection.

Real HWND, XAML, popup, WPF events and timers are used; the click is fired as a routed event, not as physical Win32 input. It does not verify mouse hit-testing, visual appearance, multi-monitor DPI or fullscreen. Fullscreen detection is stopped to avoid depending on the active application. The real popup keeps its events but its content is made transparent because Windows may reposition it on a real monitor. It may briefly capture the mouse while open (about 100 ms), although the harness does not move the cursor or generate physical clicks. It requires an interactive desktop; keep mouse buttons unpressed during the test, since production ignores hover during drags.

The test depends on private names through reflection: implementation changes may require updating it. It uses real wall-clock time, so debugger pauses, clock jumps or extreme load can produce failures. It was not added to the solution.

Validated with `dotnet run` Debug: PASS, 75 samples during the grace period and complete cleanup. The first build emitted the existing CA1416 warnings of `DatabaseKeyProvider` in Core; no build errors.

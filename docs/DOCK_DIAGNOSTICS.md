# Dock diagnostic log (retired)

**Status: retired on 2026-10-27.** The two bugs it was written for are fixed and have not been seen again:

1. **The dock's rest strip sometimes stopped being visible** and came back as soon as the mouse was moved over where it should be.
2. **The dock did not return to the main screen** when it was turned off and on.

## What fixed them (still in the code)

- **Windows moves the dock window when the screen arrangement changes** (seen on 2026-09-25 and reproduced with `DisplaySwitch.exe /internal` and
  `/extend`). Since 1.0.2 `EdgeDockWindow.KeepWindowInPlace` checks every 500 ms, at rest, that its window is where it calculated it (`DockPlacement`) and
  puts it back in place or rebuilds the docks if its screen changed.
- **Screens switched off with their button that Windows still sees**: since 1.0.3 `DockScreenWatcher` asks the monitor over DDC/CI whether it is on and, if not,
  moves the dock to another screen. With a monitor that does not answer, the option “On the screen where the mouse is” remains.
- **A normal window over the strip** (Firefox, Code…): `KeepStripOnTop` raises the dock again without waiting for the mouse. Those in the top layer (Start menu,
  snipping) are respected.
- **The unpainted strip** (a “layered” window that stops recomposing): `RepaintStripIfUnpainted` reads every 5 s the color shown on screen and, with two
  consecutive reads without the expected color, repaints and raises the dock again.

## What was removed

The `logs\dock.log` log (dock states each time they changed, screens seen, power and session, and the full-screen close). What remains is
`logs\errores.log`, only with unexpected errors of background tasks (`AppLog`, before in the same log). A `dock.log` or `dock.log.old` left in
`%LOCALAPPDATA%\Aldune\logs` can be deleted by hand.

If any dock bug came back: the full log and its reading guide are in git history (`git log -S RecordDiagnostics` and the version of this
document before 2026-10-27).

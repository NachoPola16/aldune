# Aldune Phase 3a — Real multi-monitor + per-monitor DPI (design spec)

First sub-delivery of Phase 3 (multi-monitor + DPI, see
`docs/STATUS.md`). The complete Phase 3 was split up because it mixes
independent pieces: this sub-delivery covers only the hard, isolated
technical part — real per-monitor geometry, DPI, and the app-level
window coordinator. Deliberately out of this delivery (see "Out of
scope" below): the Settings toggle to choose monitors, stable device
IDs, and the behavior of disconnecting/reconnecting a monitor.

## Context and motivation

Today Aldune creates a single `EdgeDockWindow`, always on the primary
monitor, using `SystemParameters.WorkArea` for its geometry — a WPF API
that **only returns the work area of the primary monitor**, no matter how
many monitors are connected. There is also no `app.manifest` declaring the
application as per-monitor DPI aware (`PerMonitorV2`), so today it runs
under Windows' default behavior for an undeclared process.

This is exactly the gap that the v1 spec (`2026-08-30-aldune-v1-design.md`)
points out in its "Core" section: "per-monitor DPI aware (Per-Monitor
V2)" and "multi-monitor: the app can be shown on all connected screens".
The prerequisites already identified in `docs/STATUS.md` after the final
review of Phase 2 are the starting point of this design:

1. `_openNoteWindows` (the dictionary that prevents opening the same note
   twice) and the `NoteWindow` → owner `EdgeDockWindow` relationship live
   today inside `EdgeDockWindow`, which will become one instance **per
   monitor**. If this is not moved to an application-level coordinator
   before the second dock is created, the "duplicate windows"/"stale
   label" bug that took several rounds to fix in Phase 2 is reproduced.
2. `SystemParameters.WorkArea` is no good for non-primary monitors —
   real geometry via Win32 is needed.
3. `ScreenOrigin` is still fixed to the literal `"primary"` in all notes —
   this delivery does **not** touch it (see "Out of scope").

## Scope of this sub-delivery

**In:**

- Declare the app as per-monitor DPI aware (`PerMonitorV2`) via
  `app.manifest`.
- Enumerate the connected monitors (Win32) with their real work area in
  pixels and their real DPI.
- Create **one `EdgeDockWindow` per connected monitor**, always — with no
  configuration toggle yet (that belongs to the next sub-delivery). This
  is already one of the two behaviors the v1 spec allows ("all connected
  screens").
- Each dock uses the real geometry and DPI of *its* monitor, not those of
  the primary monitor.
- Application-level coordinator (`AppCoordinator`) that centralizes: the
  already-open notes (avoids duplicates between docks), refreshing all
  the docks at once, and the single "Manage notes" window.
- Monitor detection **only at startup** — connecting or disconnecting a
  monitor while the app is already running is not handled (see "Out of
  scope").

**Out of scope** (for a later sub-delivery of Phase 3):

- Selecting which monitors to use from Settings (today there is no
  Settings UI at all; `AppSettings` only stores the encryption key).
- A different edge position per screen (the v1 spec already postpones it
  to v1.1; there is still a single global edge position).
- Stable device IDs for `ScreenOrigin` (it is still the literal
  `"primary"` for all notes).
- Connecting/disconnecting a monitor while the app is already running
  (`WM_DISPLAYCHANGE`), and the behavior of "temporarily show on the
  primary screen without rewriting the origin" which depends on having
  stable IDs first.
- Changing the DPI scale in Windows while the app is already running: WPF
  with `PerMonitorV2` rescales the existing windows on its own, so it
  should not break, but the fine repositioning specific to edge docking
  has not been tested for that case — a known limit.
- With several docks but `ScreenOrigin` still not differentiated per
  monitor: **all the docks show exactly the same full list of active
  notes** (they mirror each other). Creating or archiving a note from any
  dock updates all of them. When a later sub-delivery adds stable IDs per
  monitor, each dock will be able to start filtering by its own origin.

## Components

### `Aldune.Core.MonitorInfo` (new)

Pure data type, with no Win32/WPF dependencies — easy to test:

```csharp
public readonly record struct MonitorInfo(
    string DeviceName,      // e.g. "\\.\DISPLAY1" — NOT the future stable
                            // id, just what Win32 gives today; see
                            // "Out of scope"
    WorkingArea WorkArea,   // already converted to DIPs of THAT monitor
    double DpiScale,        // 1.0 = 96 DPI, 1.5 = 150%, etc.
    bool IsPrimary);
```

### Pixel → DIP conversion (new, `Aldune.Core`)

Win32 gives rectangles in physical pixels; WPF interprets
`Window.Left/Top/Width/Height` in DIPs relative to the DPI scale of that
specific monitor (once `PerMonitorV2` is declared). A pure, testable
function does the conversion:

```csharp
public static class DpiConversion
{
    public static WorkingArea ToWorkingArea(Rect pixelBounds, double dpiScale) =>
        new(pixelBounds.X / dpiScale, pixelBounds.Y / dpiScale,
            pixelBounds.Width / dpiScale, pixelBounds.Height / dpiScale);
}
```

With this, **`EdgeGeometry.cs` does not change at all** — its
`PillRect`/`ExpandedRect` calculations are pure width/height arithmetic,
the unit does not matter as long as it is consistent. Each dock already
receives the correct DIPs of its own monitor.

### Monitor enumeration (new, `Aldune.Interop`)

New methods next to `NativeMethods.cs` (or a new `MonitorEnumerator.cs`
file in the same namespace if `NativeMethods.cs` gets too large):
`EnumDisplayMonitors` + `GetMonitorInfoW` (work area in pixels + whether
it is the primary) + `GetDpiForMonitor` (real DPI). It exposes
`IReadOnlyList<MonitorInfo> EnumerateMonitors()`, using
`DpiConversion.ToWorkingArea` internally. It is called once, at startup
(`App.xaml.cs`).

Pure Win32 was chosen (not `System.Windows.Forms.Screen`) so as not to
bring in a WinForms dependency just for this, and because it matches the
pattern already established in the project (`NativeMethods.cs`) and the
technology stack rationale of the v1 spec ("full access to Win32 via
P/Invoke when it is necessary to drop down a level").

### `app.manifest` (new, `src/Aldune/`)

Declares `<dpiAwareness>PerMonitorV2</dpiAwareness>` (namespace
`http://schemas.microsoft.com/SMI/2016/WindowsSettings`). Referenced from
`Aldune.csproj`. Without this, none of the above takes effect: Windows
would keep treating the app as not per-monitor DPI aware and would scale
an already-rendered bitmap instead of letting each window render crisply
on its own monitor.

### `AppCoordinator` (new, `Aldune.Windowing`)

A single instance for the whole app (not one per dock). It replaces what
today lives inside `EdgeDockWindow`:

```csharp
public sealed class AppCoordinator
{
    private readonly NotesRepository _repository;
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private readonly List<EdgeDockWindow> _docks = new();
    private NotesManagerWindow? _notesManagerWindow;

    public AppCoordinator(NotesRepository repository) => _repository = repository;

    public void RegisterDock(EdgeDockWindow dock) => _docks.Add(dock);

    public int OpenNoteWindowCount => _openNoteWindows.Count;

    public void OpenOrActivateNote(Note note, EdgeDockWindow requestingDock)
    {
        if (_openNoteWindows.TryGetValue(note.Id, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            NativeMethods.ForceActivate(existing);
            return;
        }

        var noteWindow = new NoteWindow(note, _repository, this);
        requestingDock.PositionNoteWindow(noteWindow);
        _openNoteWindows[note.Id] = noteWindow;
        noteWindow.Closed += (_, _) => _openNoteWindows.Remove(note.Id);
        noteWindow.Show();
        NativeMethods.ForceActivate(noteWindow);
    }

    public void OpenOrActivateNotesManager()
    {
        if (_notesManagerWindow is not null)
        {
            _notesManagerWindow.Activate();
            NativeMethods.ForceActivate(_notesManagerWindow);
            return;
        }

        _notesManagerWindow = new NotesManagerWindow(_repository, this);
        _notesManagerWindow.Closed += (_, _) => _notesManagerWindow = null;
        _notesManagerWindow.Show();
        NativeMethods.ForceActivate(_notesManagerWindow);
    }

    public void RefreshAll()
    {
        foreach (var dock in _docks) dock.Refresh();
    }
}
```

`requestingDock.PositionNoteWindow(...)` stays in `EdgeDockWindow` (it
needs the position/bounds of **that** specific monitor), but the count of
open notes used to compute the cascade step is now read from
`coordinator.OpenNoteWindowCount` instead of a per-dock dictionary, so
that the cascade is consistent even if the notes are spread across
monitors.

### Changes to existing classes

- **`EdgeDockWindow`**: the constructor now receives `MonitorInfo`
  instead of a loose `WorkingArea` (internally it still keeps the
  `WorkingArea` from `MonitorInfo.WorkArea` for `EdgeGeometry`), and
  receives the `AppCoordinator`. `OnTabClick` delegates to
  `_coordinator.OpenOrActivateNote(note, this)` instead of managing
  `_openNoteWindows` itself. `OnManageArchiveClick` delegates to
  `_coordinator.OpenOrActivateNotesManager()`. It loses the
  `_openNoteWindows` and `_notesManagerWindow` fields (they go to the
  coordinator). `PositionNoteWindow` goes from `private` to `internal` —
  the coordinator (same library, different class) needs to be able to
  call it.
- **`NoteWindow`**: the constructor receives `AppCoordinator coordinator`
  instead of `EdgeDockWindow owner`; all the calls to `_owner.Refresh()`
  become `_coordinator.RefreshAll()`.
- **`NotesManagerWindow`**: same change — it receives the coordinator, and
  its bulk actions call `_coordinator.RefreshAll()`.
- **`App.xaml.cs`**: instead of creating a single `EdgeDockWindow` with
  `SystemParameters.WorkArea`, it enumerates monitors
  (`MonitorEnumerator.EnumerateMonitors()`), creates an `AppCoordinator`,
  creates an `EdgeDockWindow` for each `MonitorInfo` (all with
  `EdgePosition.Right` — the edge position is still global, unchanged),
  registers each one in the coordinator, calls `coordinator.RefreshAll()`
  once (replacing the current loose `dock.Refresh()` — the first real
  decryption attempt still happens here, the `try/catch` of
  `AuthenticationTagMismatchException` does not change), and shows all the
  docks.

## Error handling

- **Zero monitors detected** (extremely unlikely on real Windows, but it
  must be covered): it is treated as a fourth startup failure case, with
  the same pattern as the three already existing in `App.xaml.cs` (clear
  message + `Shutdown(1)`).
- **`GetDpiForMonitor` fails for a specific monitor**: 96 DPI (scale 1.0)
  is assumed for that monitor instead of propagating the exception — same
  spirit as `NativeMethods.ApplyRoundedCorners`, which breaks nothing on
  systems where the underlying API is not available.
- **Hot DPI scale change** (without disconnecting the monitor): it is not
  handled specially in this delivery; it remains a known limit documented
  above.

## Testing

- `DpiConversion.ToWorkingArea`: normal unit tests in
  `Aldune.Core.Tests`, with several scales (100%, 150%, 200%) — pure
  arithmetic, no Win32 involved.
- `MonitorInfo`: a data type, with no logic of its own to test beyond the
  above conversion.
- Real monitor enumeration, creation of several `EdgeDockWindow`s, and the
  `AppCoordinator` as a whole: pure Win32/WPF, verified by hand (like the
  rest of this app's window mechanics). The user has a second monitor
  available (in portrait) to test this in person. Suggested manual
  checklist:
  1. A single monitor connected — regression: it must look exactly as it
     does today.
  2. Two monitors — a dock appears on each, both on the right edge, each
     with the correct geometry/size for its own monitor (the portrait one
     included — `EdgeGeometry` is orientation-agnostic, but this is the
     first time it is tested with a portrait monitor).
  3. Create a note from the dock of monitor A → it also appears in the tab
     of the dock of monitor B (same list, see "Out of scope").
  4. Open the same note by clicking from both docks (if the note appears
     in both) → the same window is activated, never duplicated.
  5. Open "Manage notes" from the dock of monitor A and then from B → it
     is the same window (it is activated, a second one is not opened).
  6. Archive a note from `NotesManagerWindow` → both docks update their
     tab.

## Suggested implementation order

1. `Aldune.Core.MonitorInfo` + `DpiConversion` (TDD, no dependencies).
2. `app.manifest` + Win32 monitor enumeration (`Aldune.Interop`) —
   verifiable with a temporary `Console.WriteLine` or a manual test before
   touching the windows.
3. `AppCoordinator` (not yet used).
4. Refactor of `EdgeDockWindow`/`NoteWindow`/`NotesManagerWindow` to use
   the coordinator — behavior must remain identical with a single monitor
   (regression).
5. `App.xaml.cs`: create one dock per real monitor.
6. Full manual verification with the second monitor.

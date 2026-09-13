# Fanote Fase 3a: Multi-monitor real + DPI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Fanote's dock appear on every connected monitor with correct real geometry and DPI, instead of only ever appearing on the primary monitor via `SystemParameters.WorkArea`.

**Architecture:** Enumerate real monitors via Win32 (`EnumDisplayMonitors`/`GetMonitorInfoW`/`GetDpiForMonitor`), declare the app `PerMonitorV2` DPI-aware via `app.manifest`, and introduce an `AppCoordinator` that centralizes note-window/notes-manager-window lifecycle across however many `EdgeDockWindow` instances now exist (one per monitor) so they never duplicate windows.

**Tech Stack:** C# / WPF / .NET 10, Win32 P/Invoke (user32.dll, shcore.dll), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-02-fanote-phase3a-multimonitor-dpi-design.md`

## Global Constraints

- No new NuGet dependencies — pure Win32 P/Invoke, consistent with `Fanote.Interop.NativeMethods`.
- Monitor detection happens once at startup only — no live hotplug/`WM_DISPLAYCHANGE` handling in this plan (deferred, see spec "Fuera de alcance").
- `EdgePosition.Right` stays hardcoded for every dock — no per-screen edge position, no Settings UI in this plan.
- With `ScreenOrigin` still hardcoded `"primary"` for every note, **every dock must show the exact same full list of notes** — they mirror each other. Do not attempt any per-monitor note filtering in this plan.
- Every new pure-logic type goes in `Fanote.Core` and gets xUnit tests. Win32/WPF-only code (monitor enumeration, the coordinator, window classes) has no automated tests — verify manually, consistent with the rest of this codebase's window/interop code.
- Kill any running `Fanote.exe` before rebuilding (`tasklist //FI "IMAGENAME eq Fanote.exe"` then `taskkill //PID <pid> //F`) — the build will fail with a file-lock error otherwise.
- Run `dotnet test` after every task; all existing tests must keep passing (71 at the time of writing).

---

## Task 1: `MonitorInfo` + `DpiConversion`

**Files:**
- Create: `src/Fanote.Core/MonitorInfo.cs`
- Create: `src/Fanote.Core/DpiConversion.cs`
- Test: `tests/Fanote.Core.Tests/DpiConversionTests.cs`

**Interfaces:**
- Produces: `Fanote.Core.MonitorInfo` (record struct: `DeviceName: string`, `WorkArea: WorkingArea`, `DpiScale: double`, `IsPrimary: bool`); `Fanote.Core.DpiConversion.ToWorkingArea(Rect pixelBounds, double dpiScale) : WorkingArea`. Both consumed by Task 2 (`MonitorEnumerator`) and Task 4 (`AppCoordinator`/window constructors).

- [ ] **Step 1: Write the failing tests**

Create `tests/Fanote.Core.Tests/DpiConversionTests.cs`:

```csharp
using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class DpiConversionTests
{
    [Fact]
    public void ToWorkingArea_At100Percent_ReturnsSamePixelValues()
    {
        var pixels = new Rect(0, 0, 1920, 1080);

        var result = DpiConversion.ToWorkingArea(pixels, dpiScale: 1.0);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public void ToWorkingArea_At150Percent_DividesEverythingByScale()
    {
        var pixels = new Rect(100, 200, 1920, 1080);

        var result = DpiConversion.ToWorkingArea(pixels, dpiScale: 1.5);

        Assert.Equal(100 / 1.5, result.X, precision: 5);
        Assert.Equal(200 / 1.5, result.Y, precision: 5);
        Assert.Equal(1920 / 1.5, result.Width, precision: 5);
        Assert.Equal(1080 / 1.5, result.Height, precision: 5);
    }

    [Fact]
    public void ToWorkingArea_At200Percent_HalvesPixelDimensions()
    {
        var pixels = new Rect(0, 0, 3840, 2160);

        var result = DpiConversion.ToWorkingArea(pixels, dpiScale: 2.0);

        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd fanote && dotnet test tests/Fanote.Core.Tests --filter "FullyQualifiedName~DpiConversionTests"`
Expected: build error — `DpiConversion` does not exist. (This is the correct RED: the type is missing, not a typo.)

- [ ] **Step 3: Create `MonitorInfo`**

Create `src/Fanote.Core/MonitorInfo.cs`:

```csharp
namespace Fanote.Core;

/// <summary>
/// One connected monitor's real geometry and DPI. <see cref="DeviceName"/> is whatever Win32
/// hands back today (e.g. "\\.\DISPLAY1") — it is NOT a stable id across reconnects; that's a
/// later sub-delivery of Phase 3 (see docs/STATUS.md).
/// </summary>
public readonly record struct MonitorInfo(string DeviceName, WorkingArea WorkArea, double DpiScale, bool IsPrimary);
```

- [ ] **Step 4: Implement `DpiConversion`**

Create `src/Fanote.Core/DpiConversion.cs`:

```csharp
namespace Fanote.Core;

/// <summary>
/// Win32 gives monitor bounds in physical pixels; WPF's Window.Left/Top/Width/Height are
/// interpreted in DIPs relative to that specific monitor's own DPI scale once the app declares
/// PerMonitorV2 awareness (see app.manifest). This is the pixel-to-DIP conversion — kept as pure
/// math in Fanote.Core so it's unit-testable without any Win32/WPF dependency.
/// </summary>
public static class DpiConversion
{
    public static WorkingArea ToWorkingArea(Rect pixelBounds, double dpiScale) => new(
        pixelBounds.X / dpiScale,
        pixelBounds.Y / dpiScale,
        pixelBounds.Width / dpiScale,
        pixelBounds.Height / dpiScale);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd fanote && dotnet test tests/Fanote.Core.Tests --filter "FullyQualifiedName~DpiConversionTests"`
Expected: 3 passed.

Run the full suite too: `cd fanote && dotnet test`
Expected: all passing (74 = 71 existing + 3 new).

- [ ] **Step 6: Commit**

```bash
cd fanote
git add src/Fanote.Core/MonitorInfo.cs src/Fanote.Core/DpiConversion.cs tests/Fanote.Core.Tests/DpiConversionTests.cs
git commit -m "$(cat <<'EOF'
Add MonitorInfo and DpiConversion for per-monitor geometry (Phase 3a)

Pure Fanote.Core types: MonitorInfo is the plain per-monitor data
(device name, work area, DPI scale, is-primary); DpiConversion.ToWorkingArea
does the pixel->DIP math Win32 monitor enumeration will need, kept
here so it's unit-testable without touching Win32.

EOF
)"
```

---

## Task 2: Win32 monitor enumeration (`MonitorEnumerator`)

**Files:**
- Create: `src/Fanote/Interop/MonitorEnumerator.cs`
- Modify (temporarily, for manual verification, then reverted): `src/Fanote/App.xaml.cs`

**Interfaces:**
- Consumes: `Fanote.Core.MonitorInfo`, `Fanote.Core.DpiConversion.ToWorkingArea(Rect, double)`, `Fanote.Core.Rect` (Task 1).
- Produces: `Fanote.Interop.MonitorEnumerator.EnumerateMonitors() : IReadOnlyList<MonitorInfo>`. Consumed by Task 5.

This is pure Win32 P/Invoke — no automated test is possible (nothing to unit test that isn't Win32 itself). Verify it manually with a temporary call.

- [ ] **Step 1: Implement `MonitorEnumerator`**

Create `src/Fanote/Interop/MonitorEnumerator.cs`:

```csharp
using System.Runtime.InteropServices;
using Fanote.Core;

namespace Fanote.Interop;

/// <summary>
/// Real per-monitor bounds and DPI via Win32 — SystemParameters.WorkArea (WPF) only ever returns
/// the primary monitor's work area, which is the gap this fills. Called once at startup
/// (App.xaml.cs); no live hotplug handling (see Phase 3a spec, "Fuera de alcance").
/// </summary>
internal static class MonitorEnumerator
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class MONITORINFOEX
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice = string.Empty;
    }

    private const int MONITORINFOF_PRIMARY = 0x00000001;

    private enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0,
    }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

    internal static IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        var results = new List<MonitorInfo>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var info = new MONITORINFOEX();
            if (!GetMonitorInfo(hMonitor, info))
                return true; // couldn't read this one — skip it, keep enumerating the rest

            // GetDpiForMonitor needs Windows 8.1+ (shcore.dll); if it fails for any reason,
            // assume 96 DPI (100% scale) for that monitor rather than throwing.
            double dpiScale = 1.0;
            if (GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
            {
                dpiScale = dpiX / 96.0;
            }

            var pixelWorkArea = new Rect(
                info.rcWork.Left,
                info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left,
                info.rcWork.Bottom - info.rcWork.Top);

            results.Add(new MonitorInfo(
                info.szDevice,
                DpiConversion.ToWorkingArea(pixelWorkArea, dpiScale),
                dpiScale,
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0));

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return results;
    }
}
```

- [ ] **Step 2: Verify it manually**

Temporarily add this at the very top of `OnStartup` in `src/Fanote/App.xaml.cs` (right after `base.OnStartup(e);`), and add `using Fanote.Interop;` to the top of the file:

```csharp
var monitorDebugInfo = string.Join("\n", Fanote.Interop.MonitorEnumerator.EnumerateMonitors()
    .Select(m => $"{m.DeviceName}: {m.WorkArea} @ {m.DpiScale}x primary={m.IsPrimary}"));
MessageBox.Show(monitorDebugInfo, "Monitors detected");
```

Kill any running `Fanote.exe`, then run:
```bash
cd fanote
dotnet build src/Fanote/Fanote.csproj
dotnet run --project src/Fanote --no-build -c Debug
```

Expected: a message box lists every connected monitor with plausible work-area numbers (matching what Windows Display Settings shows) and the right one flagged `primary=True`. Close the box, close the app.

**Remove the temporary debug block and the `using Fanote.Interop;` line you added** — this was verification only, not part of the feature (`App.xaml.cs` gets its real integration in Task 5).

- [ ] **Step 3: Confirm the app still builds clean and tests still pass**

Run: `cd fanote && dotnet build src/Fanote/Fanote.csproj -v quiet`
Expected: Build succeeded, 0 errors.

Run: `cd fanote && dotnet test`
Expected: all still passing (no change in count — this task added no new automated tests).

- [ ] **Step 4: Commit**

```bash
cd fanote
git add src/Fanote/Interop/MonitorEnumerator.cs
git commit -m "$(cat <<'EOF'
Add Win32 monitor enumeration (Phase 3a)

MonitorEnumerator wraps EnumDisplayMonitors + GetMonitorInfoW +
GetDpiForMonitor to list every connected monitor with its real work
area and DPI scale — SystemParameters.WorkArea only ever returns the
primary monitor's, which is why this exists. Pure Win32 P/Invoke, no
new dependency. Verified manually (message box listing detected
monitors); not yet wired into App.xaml.cs, see Task 5.

EOF
)"
```

---

## Task 3: `app.manifest` declaring PerMonitorV2 DPI awareness

**Files:**
- Create: `src/Fanote/app.manifest`
- Modify: `src/Fanote/Fanote.csproj`

**Interfaces:** None (build/runtime configuration only, no code interface).

Without this, nothing else in this plan has any effect: Windows would keep treating Fanote as DPI-unaware and scale an already-rendered bitmap instead of letting each window render natively at its own monitor's DPI.

- [ ] **Step 1: Create the manifest**

Create `src/Fanote/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="Fanote.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 2: Reference it from the project file**

In `src/Fanote/Fanote.csproj`, add `<ApplicationManifest>` inside the existing `<PropertyGroup>`:

```xml
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
```

- [ ] **Step 3: Verify it builds and the manifest is embedded**

Kill any running `Fanote.exe`, then:
```bash
cd fanote
dotnet build src/Fanote/Fanote.csproj -v quiet
```
Expected: Build succeeded, 0 errors.

Run the app once (`dotnet run --project src/Fanote --no-build -c Debug`) and confirm it still looks and behaves exactly as before (pill docks to the right edge, expands on hover, notes open normally) — this is a pure regression check, nothing should look different yet on a single monitor. Close the app.

- [ ] **Step 4: Run the full test suite**

Run: `cd fanote && dotnet test`
Expected: all still passing, unchanged count.

- [ ] **Step 5: Commit**

```bash
cd fanote
git add src/Fanote/app.manifest src/Fanote/Fanote.csproj
git commit -m "$(cat <<'EOF'
Declare PerMonitorV2 DPI awareness via app.manifest (Phase 3a)

No app.manifest existed before this, so Fanote ran under whatever
Windows defaults to for a DPI-unaware process. This is the standard,
Microsoft-recommended way to opt a WPF app into per-monitor DPI —
without it, the Win32 monitor/DPI enumeration added in the previous
commit would have no effect on how windows actually render.

EOF
)"
```

---

## Task 4: `AppCoordinator` + refactor `EdgeDockWindow`/`NoteWindow`/`NotesManagerWindow`

**Files:**
- Create: `src/Fanote/Windowing/AppCoordinator.cs`
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml.cs`
- Modify: `src/Fanote/Windowing/NoteWindow.xaml.cs`
- Modify: `src/Fanote/Windowing/NotesManagerWindow.xaml.cs`
- Modify: `src/Fanote/App.xaml.cs`

**Interfaces:**
- Consumes: `Fanote.Core.MonitorInfo` (Task 1).
- Produces: `Fanote.Windowing.AppCoordinator` — constructor `AppCoordinator(NotesRepository repository)`; `RegisterDock(EdgeDockWindow dock)`; `OpenNoteWindowCount: int`; `OpenOrActivateNote(Note note, EdgeDockWindow requestingDock)`; `OpenOrActivateNotesManager()`; `RefreshAll()`. `EdgeDockWindow`'s new constructor signature `EdgeDockWindow(EdgePosition edge, MonitorInfo monitor, NotesRepository repository, AppCoordinator coordinator)` and its now-`internal` `PositionNoteWindow(NoteWindow)` are consumed by Task 5.

This is a pure refactor: behavior must be **identical** to today when there's a single monitor, since `App.xaml.cs` in this task still creates only one dock (from a `MonitorInfo` synthesized out of `SystemParameters.WorkArea`, exactly matching what it does today) — real multi-monitor enumeration is Task 5. Verify via regression, not new capability.

- [ ] **Step 1: Create `AppCoordinator`**

Create `src/Fanote/Windowing/AppCoordinator.cs`:

```csharp
using System.Windows;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

/// <summary>
/// One instance for the whole app (not one per dock/monitor). Owns what used to live inside
/// EdgeDockWindow: which notes already have an open NoteWindow (so clicking the same note's tab
/// from two different docks never opens it twice), the single shared NotesManagerWindow, and
/// telling every dock to refresh together (with several docks all mirroring the same note list —
/// see Phase 3a spec — archiving a note from any one of them has to update all of them).
/// </summary>
public sealed class AppCoordinator
{
    private readonly NotesRepository _repository;
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private readonly List<EdgeDockWindow> _docks = new();
    private NotesManagerWindow? _notesManagerWindow;

    public AppCoordinator(NotesRepository repository)
    {
        _repository = repository;
    }

    public int OpenNoteWindowCount => _openNoteWindows.Count;

    public void RegisterDock(EdgeDockWindow dock) => _docks.Add(dock);

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

- [ ] **Step 2: Refactor `EdgeDockWindow.xaml.cs`**

In `src/Fanote/Windowing/EdgeDockWindow.xaml.cs`, replace the field declarations and constructor:

```csharp
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly NotesRepository _repository;
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private bool _viewingArchive;
    private NotesManagerWindow? _notesManagerWindow;

    private const double NoteWindowCascadeStep = 30;
    private const int NoteWindowMaxCascadeSteps = 8;

    public EdgeDockWindow(EdgePosition edge, WorkingArea workingArea, NotesRepository repository)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = workingArea;
        _repository = repository;
```

with:

```csharp
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private bool _viewingArchive;

    private const double NoteWindowCascadeStep = 30;
    private const int NoteWindowMaxCascadeSteps = 8;

    public EdgeDockWindow(EdgePosition edge, MonitorInfo monitor, NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = monitor.WorkArea;
        _repository = repository;
        _coordinator = coordinator;
```

Replace `OnManageArchiveClick`:

```csharp
    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
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
```

with:

```csharp
    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenOrActivateNotesManager();
    }
```

Replace `OnTabClick`:

```csharp
    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
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
            PositionNoteWindow(noteWindow);
            _openNoteWindows[note.Id] = noteWindow;
            noteWindow.Closed += (_, _) => _openNoteWindows.Remove(note.Id);
            noteWindow.Show();
            NativeMethods.ForceActivate(noteWindow);
        }
    }
```

with:

```csharp
    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
        {
            _coordinator.OpenOrActivateNote(note, this);
        }
    }
```

Replace `PositionNoteWindow` (note: `private` becomes `internal` — `AppCoordinator` needs to call it):

```csharp
    private void PositionNoteWindow(NoteWindow noteWindow)
    {
        int step = _openNoteWindows.Count % NoteWindowMaxCascadeSteps;
```

with:

```csharp
    internal void PositionNoteWindow(NoteWindow noteWindow)
    {
        int step = _coordinator.OpenNoteWindowCount % NoteWindowMaxCascadeSteps;
```

- [ ] **Step 3: Refactor `NoteWindow.xaml.cs`**

In `src/Fanote/Windowing/NoteWindow.xaml.cs`, replace:

```csharp
    private readonly Note _note;
    private readonly NotesRepository _repository;
    private readonly EdgeDockWindow _owner;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    public NoteWindow(Note note, NotesRepository repository, EdgeDockWindow owner)
    {
        InitializeComponent();
        _note = note;
        _repository = repository;
        _owner = owner;
```

with:

```csharp
    private readonly Note _note;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    public NoteWindow(Note note, NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _note = note;
        _repository = repository;
        _coordinator = coordinator;
```

Then replace every occurrence of `_owner.Refresh();` with `_coordinator.RefreshAll();` — there are 5, in `Closing`, `OnColorSwatchClick`, `OnArchiveClick`, `OnTrashClick`, and `OnRestoreClick`. Use a project-wide find/replace scoped to this file, or edit each occurrence individually; either way, verify afterwards with:

```bash
cd fanote
grep -n "_owner" src/Fanote/Windowing/NoteWindow.xaml.cs
```

Expected: no output (no remaining references).

- [ ] **Step 4: Refactor `NotesManagerWindow.xaml.cs`**

In `src/Fanote/Windowing/NotesManagerWindow.xaml.cs`, replace:

```csharp
    private readonly NotesRepository _repository;
    private readonly EdgeDockWindow _owner;
    private List<NoteRow> _allRows = new();
    private List<NoteRow> _rows = new();
    private Filter _filter = Filter.All;

    public NotesManagerWindow(NotesRepository repository, EdgeDockWindow owner)
    {
        InitializeComponent();
        _repository = repository;
        _owner = owner;
```

with:

```csharp
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private List<NoteRow> _allRows = new();
    private List<NoteRow> _rows = new();
    private Filter _filter = Filter.All;

    public NotesManagerWindow(NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _repository = repository;
        _coordinator = coordinator;
```

Then replace the 3 occurrences of `_owner.Refresh();` (in `OnArchiveSelectedClick`, `OnRestoreSelectedClick`, `OnTrashSelectedClick`) with `_coordinator.RefreshAll();`. Verify:

```bash
cd fanote
grep -n "_owner" src/Fanote/Windowing/NotesManagerWindow.xaml.cs
```

Expected: no output.

- [ ] **Step 5: Update `App.xaml.cs`** (still single-dock — real enumeration is Task 5)

Replace:

```csharp
        var area = SystemParameters.WorkArea;
        var workingArea = new WorkingArea(area.Left, area.Top, area.Width, area.Height);

        var dock = new EdgeDockWindow(EdgePosition.Right, workingArea, repository);

        try
        {
            // The first place decryption of existing notes is actually attempted — this is where
            // case (c) (wrong key for this database) surfaces, not earlier in the bootstrap.
            dock.Refresh();
        }
        catch (AuthenticationTagMismatchException)
        {
```

with:

```csharp
        var area = SystemParameters.WorkArea;
        var monitor = new MonitorInfo(
            "primary",
            new WorkingArea(area.Left, area.Top, area.Width, area.Height),
            DpiScale: 1.0,
            IsPrimary: true);

        var coordinator = new AppCoordinator(repository);
        var dock = new EdgeDockWindow(EdgePosition.Right, monitor, repository, coordinator);
        coordinator.RegisterDock(dock);

        try
        {
            // The first place decryption of existing notes is actually attempted — this is where
            // case (c) (wrong key for this database) surfaces, not earlier in the bootstrap.
            coordinator.RefreshAll();
        }
        catch (AuthenticationTagMismatchException)
        {
```

And replace the final `dock.Show();` with the same call (it's still valid — `dock` still refers to the one `EdgeDockWindow` created above; no change needed there).

- [ ] **Step 6: Build and fix any compile errors**

Run: `cd fanote && dotnet build src/Fanote/Fanote.csproj -v quiet`
Expected: Build succeeded, 0 errors. If there are errors, they're almost certainly a missed `_owner` → `_coordinator` rename or a missing `using Fanote.Core;` for `MonitorInfo` in `App.xaml.cs` (it's already imported there) — fix and rebuild.

- [ ] **Step 7: Regression-test manually with one monitor**

Kill any running `Fanote.exe`, then:
```bash
cd fanote
dotnet run --project src/Fanote --no-build -c Debug
```

Walk through: pill docks to the right edge and expands on hover; click "+ Nueva nota" and confirm it appears; open it, type something, close it, confirm the tab shows the text; click "Archivadas" and confirm archived/trashed notes show with their state label; open "Gestionar notas" (gear icon), archive/restore/trash a note from there, confirm the dock's tab list updates; click a note tab twice in a row and confirm it activates the existing window rather than opening a second one. Everything should look and behave **exactly like before this task** — this is a pure refactor.

- [ ] **Step 8: Run the full test suite**

Run: `cd fanote && dotnet test`
Expected: all still passing, unchanged count (this task added no new automated tests — it's a WPF/coordinator refactor).

- [ ] **Step 9: Commit**

```bash
cd fanote
git add src/Fanote/Windowing/AppCoordinator.cs src/Fanote/Windowing/EdgeDockWindow.xaml.cs src/Fanote/Windowing/NoteWindow.xaml.cs src/Fanote/Windowing/NotesManagerWindow.xaml.cs src/Fanote/App.xaml.cs
git commit -m "$(cat <<'EOF'
Introduce AppCoordinator; move note-window ownership out of EdgeDockWindow

Pure refactor, no behavior change with a single monitor (regression-
tested manually). EdgeDockWindow no longer owns _openNoteWindows or the
NotesManagerWindow singleton — both move to a new app-level
AppCoordinator, since EdgeDockWindow is about to become one instance
per monitor (Task 5) and this is exactly the "duplicate windows across
docks" bug STATUS.md's Phase 3 prerequisites already flagged. NoteWindow
and NotesManagerWindow now depend on AppCoordinator instead of a
specific owning EdgeDockWindow, calling RefreshAll() so every dock
stays in sync (they currently all mirror the same note list — see
Phase 3a spec).

EOF
)"
```

---

## Task 5: One `EdgeDockWindow` per real connected monitor

**Files:**
- Modify: `src/Fanote/App.xaml.cs`

**Interfaces:**
- Consumes: `Fanote.Interop.MonitorEnumerator.EnumerateMonitors()` (Task 2), `AppCoordinator`/`EdgeDockWindow(EdgePosition, MonitorInfo, NotesRepository, AppCoordinator)` (Task 4).

This is the actual new capability — everything before this task was preparation. Verify with the real second monitor.

- [ ] **Step 1: Replace the single-monitor setup with real enumeration**

In `src/Fanote/App.xaml.cs`, add `using Fanote.Interop;` to the top of the file (alongside the existing `using Fanote.Core;` / `using Fanote.Windowing;`).

Replace:

```csharp
        var area = SystemParameters.WorkArea;
        var monitor = new MonitorInfo(
            "primary",
            new WorkingArea(area.Left, area.Top, area.Width, area.Height),
            DpiScale: 1.0,
            IsPrimary: true);

        var coordinator = new AppCoordinator(repository);
        var dock = new EdgeDockWindow(EdgePosition.Right, monitor, repository, coordinator);
        coordinator.RegisterDock(dock);

        try
        {
            // The first place decryption of existing notes is actually attempted — this is where
            // case (c) (wrong key for this database) surfaces, not earlier in the bootstrap.
            coordinator.RefreshAll();
        }
        catch (AuthenticationTagMismatchException)
        {
```

with:

```csharp
        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (monitors.Count == 0)
        {
            // Practically impossible on real Windows (there's always at least one display), but
            // treat it as a fourth bootstrap failure mode rather than crashing with no explanation.
            MessageBox.Show(
                "No se ha podido detectar ningún monitor conectado.",
                "Fanote — no se puede iniciar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var coordinator = new AppCoordinator(repository);
        var docks = new List<EdgeDockWindow>();
        foreach (var monitor in monitors)
        {
            var dock = new EdgeDockWindow(EdgePosition.Right, monitor, repository, coordinator);
            coordinator.RegisterDock(dock);
            docks.Add(dock);
        }

        try
        {
            // The first place decryption of existing notes is actually attempted — this is where
            // case (c) (wrong key for this database) surfaces, not earlier in the bootstrap.
            coordinator.RefreshAll();
        }
        catch (AuthenticationTagMismatchException)
        {
```

Then replace the final two lines of `OnStartup`:

```csharp
        // Cheap sweep so trash doesn't grow forever even for someone who never opens the
        // Archivadas view (which also purges on entry, see EdgeDockWindow.OnToggleArchiveClick).
        repository.PurgeExpiredTrash(TimeSpan.FromDays(NotesRepository.DefaultTrashRetentionDays));

        dock.Show();
    }
```

with:

```csharp
        // Cheap sweep so trash doesn't grow forever even for someone who never opens the
        // Archivadas view (which also purges on entry, see EdgeDockWindow.OnToggleArchiveClick).
        repository.PurgeExpiredTrash(TimeSpan.FromDays(NotesRepository.DefaultTrashRetentionDays));

        foreach (var dock in docks) dock.Show();
    }
```

- [ ] **Step 2: Build**

Run: `cd fanote && dotnet build src/Fanote/Fanote.csproj -v quiet`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Regression-test with the second monitor disconnected/disabled**

If possible, temporarily work with just the primary monitor active and confirm behavior is identical to Task 4's regression check (one dock, works exactly as before). This isolates "did the loop logic break the single-monitor case" from "does multi-monitor actually work."

- [ ] **Step 4: Manual verification with both monitors**

Kill any running `Fanote.exe`, then run with both monitors connected (the second one in portrait):
```bash
cd fanote
dotnet run --project src/Fanote --no-build -c Debug
```

Walk through the full checklist from the spec:
1. A dock pill appears on **each** monitor's right edge, including the portrait one — each with the correct size/position for its own screen (not stretched or misplaced).
2. Hovering each pill expands it correctly.
3. Create a note from the monitor-A dock → its tab appears in **both** docks' tab lists (they mirror each other — expected per this sub-delivery's scope).
4. Click that note's tab from dock A, then from dock B → the second click activates the same window rather than opening a duplicate.
5. Open "Gestionar notas" from dock A, then from dock B → same window both times (not two).
6. Archive a note from `NotesManagerWindow` → both docks' tab lists update.
7. Close the app and reopen — everything still works (no crash from monitor enumeration running fresh each launch).

- [ ] **Step 5: Run the full test suite one last time**

Run: `cd fanote && dotnet test`
Expected: all passing (74 — no new automated tests in this task; the deliverable is verified manually per the spec's testing section).

- [ ] **Step 6: Commit**

```bash
cd fanote
git add src/Fanote/App.xaml.cs
git commit -m "$(cat <<'EOF'
Create one EdgeDockWindow per connected monitor (Phase 3a)

App.xaml.cs now enumerates real monitors (MonitorEnumerator, Task 2)
instead of always using SystemParameters.WorkArea, and creates a dock
per monitor — one of the two behaviors the v1 design spec already
allows ("todas las pantallas conectadas"). Zero monitors detected is
treated as a fourth bootstrap failure mode alongside the three that
already existed. Verified manually with a real second (portrait)
monitor. No settings toggle yet to restrict to one monitor, no stable
per-monitor device ids, no live hotplug handling — all deferred to a
later Phase 3 sub-delivery per the design spec.

EOF
)"
```

---

## After this plan

Update `docs/STATUS.md`: mark this sub-delivery done, note what changed (real per-monitor geometry + DPI, `AppCoordinator`, docks now mirror the same note list), and record the next Phase 3 sub-delivery still open (Settings UI for monitor selection, stable per-monitor device ids, disconnect/reconnect fallback for `ScreenOrigin`, live hotplug/DPI-change handling) so a future session doesn't have to re-derive it from git history.

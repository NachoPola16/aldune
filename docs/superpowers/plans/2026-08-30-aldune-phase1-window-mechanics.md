# Aldune Phase 1: Window Mechanics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the core edge-docked, hover-fan-out window mechanism on a single monitor — the highest-uncertainty part of Aldune — as a runnable demo with fake in-memory notes. No persistence, no multi-monitor, no tray/hotkey yet; those are later plans (Phase 2-5 per the spec's suggested order).

**Architecture:** Two projects split by platform-dependence: `Aldune.Core` is a plain, platform-agnostic .NET class library holding pure logic (the hover/collapse state machine, edge-position geometry math, the note data shape) with zero WPF references, so it's fast to unit-test. `Aldune` is the WPF app (Windows-only) that turns that logic into real windows — a borderless, always-on-top, non-activating `EdgeDockWindow` that resizes itself in place between a small "pill" rect and a larger "expanded" rect (instead of using a transparent overlay with toggled click-through, which the spec's design review found to be self-contradictory), and a normal `NoteWindow` for the expanded single note.

**Tech Stack:** C# / .NET 10 (`net10.0` for Aldune.Core and its tests, `net10.0-windows` for the WPF app), WPF, Win32 interop via P/Invoke (`user32.dll`), xUnit for tests.

**Spec:** `docs/superpowers/specs/2026-08-30-aldune-v1-design.md`

## Global Constraints

- Windows-only app; the WPF project targets `net10.0-windows` with `<UseWPF>true</UseWPF>`.
- Pure logic (state machine, geometry math, data shapes) lives in `Aldune.Core` (`net10.0`, no `System.Windows.*` references) so it can be unit-tested without a UI thread. WPF-specific code (windows, interop) lives only in the `Aldune` project.
- The pill/dock window must never steal keyboard focus on hover (`WS_EX_NOACTIVATE`); the expanded note window must activate normally.
- No transparent-overlay-with-toggled-click-through design (rejected in the spec review as self-contradictory) — the dock window resizes itself in place instead.
- Respect the Windows accessibility setting for animations (`SystemParameters.ClientAreaAnimation`) — skip animating the resize when it's off.
- No placeholders: every task below ends in a concretely testable/verifiable state.

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `Aldune.sln`
- Create: `src/Aldune.Core/Aldune.Core.csproj`
- Create: `tests/Aldune.Core.Tests/Aldune.Core.Tests.csproj`
- Create: `src/Aldune/Aldune.csproj` (plus the default `App.xaml`/`App.xaml.cs`/`MainWindow.xaml`/`MainWindow.xaml.cs` from the WPF template — left untouched until Task 9)

**Interfaces:**
- Produces: a solution that builds, and a test project wired to run (0 real tests yet) — every later task depends on this existing and building.

- [ ] **Step 1: Create the class library, test project, and WPF app**

Run from the repository root:

```bash
dotnet new classlib -n Aldune.Core -o src/Aldune.Core --framework net10.0
dotnet new xunit -n Aldune.Core.Tests -o tests/Aldune.Core.Tests --framework net10.0
dotnet new wpf -n Aldune -o src/Aldune --framework net10.0-windows
```

- [ ] **Step 2: Wire project references**

```bash
dotnet add tests/Aldune.Core.Tests/Aldune.Core.Tests.csproj reference src/Aldune.Core/Aldune.Core.csproj
dotnet add src/Aldune/Aldune.csproj reference src/Aldune.Core/Aldune.Core.csproj
```

- [ ] **Step 3: Create the solution and add all three projects**

```bash
dotnet new sln -n Aldune
dotnet sln add src/Aldune.Core/Aldune.Core.csproj tests/Aldune.Core.Tests/Aldune.Core.Tests.csproj src/Aldune/Aldune.csproj
```

- [ ] **Step 4: Verify the solution builds and tests run**

Run: `dotnet build`
Expected: Build succeeds, 0 errors (the default classlib `Class1.cs` and the WPF template's default window are fine as-is for now).

Run: `dotnet test`
Expected: The default xUnit template's sample test (`UnitTest1`) passes — confirms the test project is correctly wired to `Aldune.Core`.

- [ ] **Step 5: Remove the template's placeholder class and test**

Delete `src/Aldune.Core/Class1.cs` and `tests/Aldune.Core.Tests/UnitTest1.cs` — they were only scaffolding to prove the wiring works.

- [ ] **Step 6: Commit**

```bash
git add Aldune.sln src/Aldune.Core src/Aldune tests/Aldune.Core.Tests
git commit -m "Scaffold Aldune solution: Core library, WPF app, test project"
```

---

### Task 2: Edge geometry math

**Files:**
- Create: `src/Aldune.Core/EdgePosition.cs`
- Create: `src/Aldune.Core/Geometry.cs`
- Create: `src/Aldune.Core/EdgeGeometry.cs`
- Test: `tests/Aldune.Core.Tests/EdgeGeometryTests.cs`

**Interfaces:**
- Produces: `EdgePosition` enum (`Top`, `Bottom`, `Left`, `Right`); `Rect` and `WorkingArea` record structs with `double X, Y, Width, Height`; `EdgeGeometry.PillRect(WorkingArea, EdgePosition) : Rect` and `EdgeGeometry.ExpandedRect(WorkingArea, EdgePosition) : Rect`; constants `EdgeGeometry.PillThickness`, `PillLength`, `ExpandedThickness`, `ExpandedLength` (all `double`).
- Consumes: nothing (pure math, no dependencies).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/EdgeGeometryTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class EdgeGeometryTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);

    [Fact]
    public void PillRect_Right_IsFlushAgainstRightEdge()
    {
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Right);
        Assert.Equal(Area.X + Area.Width - EdgeGeometry.PillThickness, rect.X);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Width);
    }

    [Fact]
    public void PillRect_Top_IsFlushAgainstTopEdge()
    {
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Top);
        Assert.Equal(Area.Y, rect.Y);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Height);
    }

    [Fact]
    public void ExpandedRect_SameEdge_IsLargerAndFlushLikePill()
    {
        var pill = EdgeGeometry.PillRect(Area, EdgePosition.Left);
        var expanded = EdgeGeometry.ExpandedRect(Area, EdgePosition.Left);
        Assert.True(expanded.Width > pill.Width);
        Assert.Equal(pill.X, expanded.X);
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void PillRect_TopOrBottom_IsHorizontallyCentered(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(Area, edge);
        double expectedCenter = Area.X + Area.Width / 2;
        double actualCenter = rect.X + rect.Width / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Theory]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void PillRect_LeftOrRight_IsVerticallyCentered(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(Area, edge);
        double expectedCenter = Area.Y + Area.Height / 2;
        double actualCenter = rect.Y + rect.Height / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `EdgePosition`, `WorkingArea`, `EdgeGeometry` don't exist yet.

- [ ] **Step 3: Implement the types**

```csharp
// src/Aldune.Core/EdgePosition.cs
namespace Aldune.Core;

public enum EdgePosition
{
    Top,
    Bottom,
    Left,
    Right
}
```

```csharp
// src/Aldune.Core/Geometry.cs
namespace Aldune.Core;

public readonly record struct Rect(double X, double Y, double Width, double Height);

public readonly record struct WorkingArea(double X, double Y, double Width, double Height);
```

```csharp
// src/Aldune.Core/EdgeGeometry.cs
namespace Aldune.Core;

public static class EdgeGeometry
{
    public const double PillThickness = 12;
    public const double PillLength = 160;
    public const double ExpandedThickness = 220;
    public const double ExpandedLength = 320;

    public static Rect PillRect(WorkingArea area, EdgePosition edge) => edge switch
    {
        EdgePosition.Top => new Rect(
            area.X + (area.Width - PillLength) / 2, area.Y, PillLength, PillThickness),
        EdgePosition.Bottom => new Rect(
            area.X + (area.Width - PillLength) / 2, area.Y + area.Height - PillThickness, PillLength, PillThickness),
        EdgePosition.Left => new Rect(
            area.X, area.Y + (area.Height - PillLength) / 2, PillThickness, PillLength),
        EdgePosition.Right => new Rect(
            area.X + area.Width - PillThickness, area.Y + (area.Height - PillLength) / 2, PillThickness, PillLength),
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };

    public static Rect ExpandedRect(WorkingArea area, EdgePosition edge) => edge switch
    {
        EdgePosition.Top => new Rect(
            area.X + (area.Width - ExpandedLength) / 2, area.Y, ExpandedLength, ExpandedThickness),
        EdgePosition.Bottom => new Rect(
            area.X + (area.Width - ExpandedLength) / 2, area.Y + area.Height - ExpandedThickness, ExpandedLength, ExpandedThickness),
        EdgePosition.Left => new Rect(
            area.X, area.Y + (area.Height - ExpandedLength) / 2, ExpandedThickness, ExpandedLength),
        EdgePosition.Right => new Rect(
            area.X + area.Width - ExpandedThickness, area.Y + (area.Height - ExpandedLength) / 2, ExpandedThickness, ExpandedLength),
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 8 tests pass (2 facts + 2 theories × 2 cases each = 6, plus the 2 single facts = 8 total).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/EdgePosition.cs src/Aldune.Core/Geometry.cs src/Aldune.Core/EdgeGeometry.cs tests/Aldune.Core.Tests/EdgeGeometryTests.cs
git commit -m "Add edge geometry math for pill/expanded window rects"
```

---

### Task 3: Hover/collapse state machine

**Files:**
- Create: `src/Aldune.Core/FanStateMachine.cs`
- Test: `tests/Aldune.Core.Tests/FanStateMachineTests.cs`

**Interfaces:**
- Produces: `FanStateMachine` class with `bool IsExpanded { get; }`, `event EventHandler? ExpansionChanged`, and methods `PointerEntered()`, `PointerLeft()`, `CollapseTimerElapsed()`. This class has no notion of real time or WPF timers — the WPF layer (Task 5) is responsible for starting/stopping a `DispatcherTimer` and calling `CollapseTimerElapsed()` when it fires, which is what makes this class trivially unit-testable.
- Consumes: nothing.

This directly implements the spec's fix for the "MouseLeave alone is unreliable" gap: a pointer-left event starts a pending collapse that only takes effect once `CollapseTimerElapsed()` is actually called, and a re-entry before that cancels it — so oscillation at the boundary of the expanded area doesn't cause flicker.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/FanStateMachineTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class FanStateMachineTests
{
    [Fact]
    public void StartsCollapsed()
    {
        var sut = new FanStateMachine();
        Assert.False(sut.IsExpanded);
    }

    [Fact]
    public void PointerEntered_Expands()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void PointerLeft_ThenTimerElapsed_Collapses()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        sut.PointerLeft();
        sut.CollapseTimerElapsed();
        Assert.False(sut.IsExpanded);
    }

    [Fact]
    public void PointerLeft_ThenReEntered_CancelsPendingCollapse()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        sut.PointerLeft();
        sut.PointerEntered();
        sut.CollapseTimerElapsed();
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void CollapseTimerElapsed_WithoutPriorPointerLeft_DoesNothing()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        sut.CollapseTimerElapsed();
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void ExpansionChanged_FiresOnlyOnActualTransition()
    {
        var sut = new FanStateMachine();
        int fireCount = 0;
        sut.ExpansionChanged += (_, _) => fireCount++;

        sut.PointerEntered();
        sut.PointerEntered();

        Assert.Equal(1, fireCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `FanStateMachine` doesn't exist yet.

- [ ] **Step 3: Implement `FanStateMachine`**

```csharp
// src/Aldune.Core/FanStateMachine.cs
namespace Aldune.Core;

public sealed class FanStateMachine
{
    public bool IsExpanded { get; private set; }
    public event EventHandler? ExpansionChanged;

    private bool _collapsePending;

    public void PointerEntered()
    {
        _collapsePending = false;
        if (!IsExpanded)
        {
            IsExpanded = true;
            ExpansionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void PointerLeft()
    {
        if (IsExpanded)
        {
            _collapsePending = true;
        }
    }

    public void CollapseTimerElapsed()
    {
        if (_collapsePending)
        {
            _collapsePending = false;
            IsExpanded = false;
            ExpansionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 6 new tests pass, plus the 8 from Task 2 (14 total).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/FanStateMachine.cs tests/Aldune.Core.Tests/FanStateMachineTests.cs
git commit -m "Add FanStateMachine with debounced collapse"
```

---

### Task 4: Note data shape

**Files:**
- Create: `src/Aldune.Core/NoteModel.cs`

**Interfaces:**
- Produces: `NoteModel` class with `Guid Id`, `string Text` (mutable), `string Color` (mutable) — mirrors the `Note` table from the spec's data model, minus the fields (`ScreenOrigin`, `CreatedAt`, `UpdatedAt`, `State`) that only matter once persistence exists in Phase 2. This is intentionally a placeholder shape for Phase 1's fake in-memory notes, not the final persisted model.
- Consumes: nothing.

No test file for this task — it's a plain data holder with no logic to verify; its correctness is exercised indirectly by Task 8's manual verification.

- [ ] **Step 1: Implement `NoteModel`**

```csharp
// src/Aldune.Core/NoteModel.cs
namespace Aldune.Core;

public sealed class NoteModel
{
    public required Guid Id { get; init; }
    public required string Text { get; set; }
    public required string Color { get; set; }
}
```

- [ ] **Step 2: Verify the library still builds**

Run: `dotnet build src/Aldune.Core`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Aldune.Core/NoteModel.cs
git commit -m "Add NoteModel placeholder data shape"
```

---

### Task 5: Non-activating window interop

**Files:**
- Create: `src/Aldune/Interop/NativeMethods.cs`

**Interfaces:**
- Produces: `internal static class NativeMethods` with `internal static void MakeNonActivating(IntPtr hwnd)`, applying `WS_EX_NOACTIVATE` to an existing window handle.
- Consumes: nothing (Task 6 calls `MakeNonActivating` from `EdgeDockWindow.SourceInitialized`).

This is Win32 interop with no automated test — it's verified manually as part of Task 6's checklist (clicking the pill must not steal focus from another app).

- [ ] **Step 1: Implement the interop wrapper**

```csharp
// src/Aldune/Interop/NativeMethods.cs
using System.Runtime.InteropServices;

namespace Aldune.Interop;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    internal static void MakeNonActivating(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }
}
```

- [ ] **Step 2: Verify the app project still builds**

Run: `dotnet build src/Aldune`
Expected: Build succeeds. (`MakeNonActivating` isn't called from anywhere yet — that's Task 6 — so there's nothing to run beyond confirming it compiles.)

- [ ] **Step 3: Commit**

```bash
git add src/Aldune/Interop/NativeMethods.cs
git commit -m "Add WS_EX_NOACTIVATE interop wrapper"
```

---

### Task 6: EdgeDockWindow — the pill that resizes on hover

**Files:**
- Create: `src/Aldune/Windowing/EdgeDockWindow.xaml`
- Create: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs`

**Interfaces:**
- Consumes: `Aldune.Core.EdgePosition`, `Aldune.Core.WorkingArea`, `Aldune.Core.EdgeGeometry` (Task 2), `Aldune.Core.FanStateMachine` (Task 3), `Aldune.Interop.NativeMethods.MakeNonActivating` (Task 5).
- Produces: `EdgeDockWindow(EdgePosition edge, WorkingArea workingArea)` constructor; this is what Task 7 extends (animation) and Task 8 extends (tabs/notes).

This is the task where the spec's design-review fix actually gets built: the window has a real, opaque background and resizes itself between the pill rect and the expanded rect — there is no transparent overlay and no click-through toggling.

- [ ] **Step 1: Create the XAML**

```xml
<!-- src/Aldune/Windowing/EdgeDockWindow.xaml -->
<Window x:Class="Aldune.Windowing.EdgeDockWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="False"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="NoResize"
        Background="#3A3A3A">
    <Grid />
</Window>
```

- [ ] **Step 2: Implement the code-behind**

```csharp
// src/Aldune/Windowing/EdgeDockWindow.xaml.cs
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;

    public EdgeDockWindow(EdgePosition edge, WorkingArea workingArea)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = workingArea;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            _fanState.CollapseTimerElapsed();
        };

        _fanState.ExpansionChanged += (_, _) => ApplyGeometry();

        MouseEnter += (_, _) => _fanState.PointerEntered();
        MouseLeave += (_, _) =>
        {
            _fanState.PointerLeft();
            _collapseTimer.Start();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNonActivating(hwnd);
        };

        ApplyGeometry();
    }

    private void ApplyGeometry()
    {
        var rect = _fanState.IsExpanded
            ? EdgeGeometry.ExpandedRect(_workingArea, _edge)
            : EdgeGeometry.PillRect(_workingArea, _edge);

        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }
}
```

- [ ] **Step 3: Wire a throwaway manual entry point to see it run**

Temporarily replace the body of `OnStartup` in `src/Aldune/App.xaml.cs` (generated by the template) with:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    var area = SystemParameters.WorkArea;
    var workingArea = new Aldune.Core.WorkingArea(area.Left, area.Top, area.Width, area.Height);
    new Aldune.Windowing.EdgeDockWindow(Aldune.Core.EdgePosition.Right, workingArea).Show();
}
```

Also remove `StartupUri="MainWindow.xaml"` from `src/Aldune/App.xaml` if the template generated it, since we're creating the window in code now.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Aldune`

Expected, checked by hand:
1. A thin dark strip appears flush against the right edge of the primary monitor, vertically centered.
2. Moving the mouse onto the strip makes it grow into a larger rectangle, still flush against the right edge (instantly — animation comes in Task 7).
3. Moving the mouse away from the expanded rectangle, then immediately back onto it before ~200ms pass, keeps it expanded without flickering closed (this is `FanStateMachine`'s cancel-on-re-entry behavior — the concrete case Opus's review flagged).
4. Moving the mouse away and waiting collapses it back to the thin strip after ~200ms.
5. Open Notepad, click into it, then hover the mouse over the Aldune strip without clicking — Notepad's title bar must stay highlighted as the active window (confirms `WS_EX_NOACTIVATE` is working; hovering must never steal focus).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune/Windowing/EdgeDockWindow.xaml src/Aldune/Windowing/EdgeDockWindow.xaml.cs src/Aldune/App.xaml src/Aldune/App.xaml.cs
git commit -m "Add EdgeDockWindow: resizing pill/fan mechanism"
```

---

### Task 7: Respect the Windows animation setting

**Files:**
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (the `ApplyGeometry` method from Task 6)

**Interfaces:**
- Consumes: `System.Windows.SystemParameters.ClientAreaAnimation` (built into WPF — no interop needed for this one, it already reflects the OS accessibility setting).
- No change to the public constructor signature.

- [ ] **Step 1: Replace `ApplyGeometry` with an animated version**

```csharp
// src/Aldune/Windowing/EdgeDockWindow.xaml.cs — replace the existing ApplyGeometry method
private void ApplyGeometry()
{
    var rect = _fanState.IsExpanded
        ? EdgeGeometry.ExpandedRect(_workingArea, _edge)
        : EdgeGeometry.PillRect(_workingArea, _edge);

    if (!SystemParameters.ClientAreaAnimation)
    {
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
        return;
    }

    var duration = new Duration(TimeSpan.FromMilliseconds(200));
    BeginAnimation(LeftProperty, new System.Windows.Media.Animation.DoubleAnimation(rect.X, duration));
    BeginAnimation(TopProperty, new System.Windows.Media.Animation.DoubleAnimation(rect.Y, duration));
    BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(rect.Width, duration));
    BeginAnimation(HeightProperty, new System.Windows.Media.Animation.DoubleAnimation(rect.Height, duration));
}
```

- [ ] **Step 2: Manual verification**

Run: `dotnet run --project src/Aldune`

Expected, checked by hand:
1. With Windows animations on (default): Settings → Accessibility → Visual effects → "Animation effects" enabled. Hovering the pill now grows it smoothly over ~200ms instead of snapping instantly.
2. Turn "Animation effects" off in that same Windows setting, restart the app, and hover again: the resize now snaps instantly, with no animation — confirms the app is reading the live OS setting rather than animating unconditionally.
3. Turn "Animation effects" back on afterward, since this is a system-wide setting and shouldn't be left changed on the dev machine.

- [ ] **Step 3: Commit**

```bash
git add src/Aldune/Windowing/EdgeDockWindow.xaml.cs
git commit -m "Animate dock resize, respecting the Windows animation accessibility setting"
```

---

### Task 8: NoteWindow — the expanded single note

**Files:**
- Create: `src/Aldune/Windowing/NoteWindow.xaml`
- Create: `src/Aldune/Windowing/NoteWindow.xaml.cs`

**Interfaces:**
- Consumes: `Aldune.Core.NoteModel` (Task 4).
- Produces: `NoteWindow(NoteModel note)` constructor. Unlike `EdgeDockWindow`, this window activates normally (no `WS_EX_NOACTIVATE`) — clicking a note tab must let the user start typing immediately.

- [ ] **Step 1: Create the XAML**

```xml
<!-- src/Aldune/Windowing/NoteWindow.xaml -->
<Window x:Class="Aldune.Windowing.NoteWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Aldune"
        Width="260" Height="260"
        WindowStyle="ToolWindow"
        Topmost="True">
    <TextBox x:Name="TextBody"
             AcceptsReturn="True"
             TextWrapping="Wrap"
             Padding="8"
             BorderThickness="0" />
</Window>
```

- [ ] **Step 2: Implement the code-behind**

```csharp
// src/Aldune/Windowing/NoteWindow.xaml.cs
using System.Windows;
using Aldune.Core;

namespace Aldune.Windowing;

public partial class NoteWindow : Window
{
    public NoteWindow(NoteModel note)
    {
        InitializeComponent();
        TextBody.Text = note.Text;
    }
}
```

- [ ] **Step 3: Manual verification**

Temporarily add, at the end of `OnStartup` in `src/Aldune/App.xaml.cs`:

```csharp
new Aldune.Windowing.NoteWindow(new Aldune.Core.NoteModel
{
    Id = Guid.NewGuid(),
    Text = "Nota de prueba",
    Color = "#F5E3B3"
}).Show();
```

Run: `dotnet run --project src/Aldune`

Expected, checked by hand: a second window opens showing "Nota de prueba" in an editable text box, and it has keyboard focus immediately (typing works without clicking into it first). Remove this temporary snippet after verifying — Task 9 wires `NoteWindow` properly via the tab click.

- [ ] **Step 4: Commit**

```bash
git add src/Aldune/Windowing/NoteWindow.xaml src/Aldune/Windowing/NoteWindow.xaml.cs
git commit -m "Add NoteWindow for the expanded single-note view"
```

---

### Task 9: Wire fake notes as clickable tabs, end-to-end demo

**Files:**
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml` (add the tabs `ItemsControl`)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (add `SetNotes` and the click handler)
- Modify: `src/Aldune/App.xaml` (remove `StartupUri`, since there's no `MainWindow` in this app)
- Modify: `src/Aldune/App.xaml.cs` (final `OnStartup` wiring)
- Delete: `src/Aldune/MainWindow.xaml`, `src/Aldune/MainWindow.xaml.cs` (template scaffolding, unused — `EdgeDockWindow` and `NoteWindow` are the app's only windows)

**Interfaces:**
- Consumes: everything from Tasks 2–8.
- Produces: a runnable Phase 1 demo — this is the task that makes the milestone's own definition of done ("mecánica de ventana en un solo monitor: pill, hover, expansión, nota abierta") concretely true end-to-end.

- [ ] **Step 1: Add the tabs list to the XAML**

```xml
<!-- src/Aldune/Windowing/EdgeDockWindow.xaml -->
<Window x:Class="Aldune.Windowing.EdgeDockWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="False"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="NoResize"
        Background="#3A3A3A">
    <ItemsControl x:Name="TabsList">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Button Content="{Binding Text}"
                        Background="{Binding Color}"
                        Margin="4"
                        Padding="6"
                        Click="OnTabClick"
                        Tag="{Binding}" />
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Window>
```

- [ ] **Step 2: Add `SetNotes` and the click handler**

```csharp
// src/Aldune/Windowing/EdgeDockWindow.xaml.cs — add these members to the existing class
using System.Collections.Generic;
// (add alongside the existing usings)

public void SetNotes(IReadOnlyList<NoteModel> notes)
{
    TabsList.ItemsSource = notes;
}

private void OnTabClick(object sender, RoutedEventArgs e)
{
    if (sender is FrameworkElement { Tag: NoteModel note })
    {
        new NoteWindow(note).Show();
    }
}
```

- [ ] **Step 3: Delete the unused MainWindow scaffolding**

```bash
rm src/Aldune/MainWindow.xaml src/Aldune/MainWindow.xaml.cs
```

- [ ] **Step 4: Finalize `App.xaml` and `App.xaml.cs`**

```xml
<!-- src/Aldune/App.xaml -->
<Application x:Class="Aldune.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnLastWindowClose">
</Application>
```

```csharp
// src/Aldune/App.xaml.cs
using System.Windows;
using Aldune.Core;
using Aldune.Windowing;

namespace Aldune;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var area = SystemParameters.WorkArea;
        var workingArea = new WorkingArea(area.Left, area.Top, area.Width, area.Height);

        var dock = new EdgeDockWindow(EdgePosition.Right, workingArea);
        dock.SetNotes(new[]
        {
            new NoteModel { Id = Guid.NewGuid(), Text = "Primera nota de prueba", Color = "#F5E3B3" },
            new NoteModel { Id = Guid.NewGuid(), Text = "Segunda nota", Color = "#C9E4DE" },
            new NoteModel { Id = Guid.NewGuid(), Text = "Tercera nota con más texto para probar el ajuste", Color = "#F2C6DE" },
        });
        dock.Show();
    }
}
```

- [ ] **Step 5: Full manual verification of the Phase 1 milestone**

Run: `dotnet run --project src/Aldune`

Expected, checked by hand — this is the complete Phase 1 acceptance checklist from the spec's Testing section, scoped to a single monitor:
1. The pill appears on the right edge; hovering fans it out into 3 colored tabs.
2. Clicking a tab opens a `NoteWindow` showing that note's text, with keyboard focus.
3. Opening a note does not close or collapse the dock.
4. Hovering the pill never steals focus from another app (repeat the Notepad check from Task 6, Step 4.5).
5. The boundary-oscillation case from Task 6 (leave and immediately re-enter) still doesn't flicker, now with real tabs visible.
6. Closing the `NoteWindow` leaves the dock and remaining tabs untouched.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Wire fake notes as clickable tabs: Phase 1 end-to-end demo"
```

---

## Self-Review Notes

- **Spec coverage** (Phase 1 slice only, per "Orden de implementación sugerido"): edge-anchored pill ✓, hover fan-out via native WPF events (no global hook) ✓, click-through/transparent-overlay design explicitly rejected in favor of window resizing ✓, pill never steals focus (`WS_EX_NOACTIVATE`) ✓, note window activates and gets focus ✓, collapse debounce for the boundary-oscillation case ✓, respects `SystemParameters.ClientAreaAnimation` ✓, all 4 edge positions supported by `EdgeGeometry` ✓. Deliberately out of this plan: persistence, multi-monitor/DPI, taskbar-collision handling, tray/hotkey/autostart, import/export, checkbox rendering, single-instance guard — each belongs to a later phase per the spec's suggested order and is called out there.
- **Placeholder scan:** no TBD/TODO; every step has real, complete code or a concrete manual-verification checklist.
- **Type consistency:** `EdgeGeometry.PillRect`/`ExpandedRect` signatures (Task 2) match their use in `EdgeDockWindow.ApplyGeometry` (Tasks 6-7); `FanStateMachine`'s three methods (Task 3) match the calls wired in `EdgeDockWindow`'s constructor (Task 6); `NoteModel` (Task 4) matches its use in `NoteWindow` (Task 8) and `EdgeDockWindow.SetNotes`/`OnTabClick` (Task 9).

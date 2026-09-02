# Fanote Fan-Tabs Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the dock's expanded panel from a scrollable list of tab buttons into individually staggered tabs with vertical rotated labels, matching the Hold My Notes-style reference — plus have a clicked note grow open from its own tab's screen position instead of appearing at a generic spot.

**Architecture:** Everything stays inside the existing single `EdgeDockWindow` (no new top-level windows per tab). The tab `DataTemplate` and its `Button` style change to a narrow vertical card with a rotated label; each realized tab plays its own entrance animation (offset by index) via its `Loaded` event; `AppCoordinator.OpenOrActivateNote` gains an optional origin rectangle that `NoteWindow` animates its appearance from.

**Tech Stack:** C# / WPF / .NET 10 (existing stack, no new dependencies).

**Spec:** `docs/superpowers/specs/2026-09-02-fanote-fan-tabs-redesign-design.md`

## Global Constraints

- Resting pill (`PillSwatches`, the color dashes) does not change at all.
- `EdgeGeometry.PillRect`/`ExpandedRect` do not change — the panel's outer rectangle is still computed from note count exactly as today.
- The dock's expanded panel shows **only active notes** — no more "Archivadas"/"Activas" toggle in the dock itself. `NotesManagerWindow` (unchanged) remains the only way to browse archived/trashed notes.
- No new `Fanote.Core` logic and no new automated tests — this is WPF presentation/animation, verified manually, consistent with how the rest of this app's window mechanics are tested (see spec's Testing section).
- Kill any running `Fanote.exe` before rebuilding (`tasklist //FI "IMAGENAME eq Fanote.exe"` then `taskkill //PID <pid> //F`) — the build fails with a file-lock error otherwise.
- Run `dotnet test` after every task; all 80 existing tests must keep passing (this plan adds none).

---

## Task 1: Simplify dock controls — drop Archivadas toggle, circular +/gear buttons

**Files:**
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml`
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml.cs`

**Interfaces:**
- Produces: `EdgeDockWindow` with no `_viewingArchive` field and no `OnToggleArchiveClick` handler; `Refresh()` unconditionally shows active notes. Consumed by nothing else (self-contained UI simplification) — sets up the layout Task 2 builds the new tab template into.

This only removes/restyles controls — the tab template itself doesn't change yet (still the current `NoteTabButtonStyle`), so this task's regression check is "the dock still works, just without the Archivadas button and with round +/gear buttons at the end of the list."

- [ ] **Step 1: Restructure `EdgeDockWindow.xaml`'s `PanelContent`**

Read the current file first — it should match what's in this plan's "current state" below (if it doesn't, the file has diverged; adapt accordingly rather than blindly overwriting).

Replace the whole `<Grid x:Name="PanelContent">...</Grid>` block (the one containing the `ScrollViewer`/`TabsList` and the bottom `StackPanel` of buttons) with:

```xml
        <Grid x:Name="PanelContent">
            <ScrollViewer Margin="0,0,4,0"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Disabled">
                <StackPanel>
                    <ItemsControl x:Name="TabsList">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Button Content="{Binding}"
                                        Background="{Binding Color}"
                                        Margin="4"
                                        Padding="6"
                                        Click="OnTabClick"
                                        Tag="{Binding}"
                                        Style="{StaticResource NoteTabButtonStyle}" />
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    <Button x:Name="NewNoteButton" Click="OnNewNoteClick"
                            Style="{StaticResource CircularIconButtonStyle}"
                            ToolTip="Nueva nota">
                        <TextBlock Text="+" FontSize="16" FontWeight="Bold" />
                    </Button>
                    <Button x:Name="ManageArchiveButton" Click="OnManageArchiveClick"
                            Style="{StaticResource CircularIconButtonStyle}"
                            ToolTip="Gestionar notas">
                        <TextBlock Text="&#xE713;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" />
                    </Button>
                </StackPanel>
            </ScrollViewer>
        </Grid>
```

Note this drops the two-row `Grid.RowDefinitions` layout entirely — everything (tabs + the two round buttons) now scrolls together as one column, which is also what lets the round buttons stay reachable even when there are enough notes to fill the panel's max height.

- [ ] **Step 2: Add `CircularIconButtonStyle` to `Window.Resources`**

In the same file's `<Window.Resources>`, add this style (anywhere among the existing styles, e.g. right after `NoteTabButtonStyle`'s closing `</Style>`):

```xml
        <Style x:Key="CircularIconButtonStyle" TargetType="Button">
            <Setter Property="Width" Value="32" />
            <Setter Property="Height" Value="32" />
            <Setter Property="Margin" Value="4" />
            <Setter Property="Foreground" Value="White" />
            <Setter Property="Background" Value="#33FFFFFF" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bg" Background="{TemplateBinding Background}" CornerRadius="16">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#4DFFFFFF" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

(`CornerRadius="16"` on a 32×32 `Border` makes it a full circle.)

- [ ] **Step 3: Remove the Archivadas toggle from `EdgeDockWindow.xaml.cs`**

Replace:

```csharp
    private readonly AppCoordinator _coordinator;
    private bool _viewingArchive;
    private int _noteCount;
```

with:

```csharp
    private readonly AppCoordinator _coordinator;
    private int _noteCount;
```

Replace:

```csharp
    public void Refresh()
    {
        if (_viewingArchive)
        {
            var archived = _repository.GetByState(NoteState.Archived);
            var trashed = _repository.GetByState(NoteState.Trashed);
            SetNotes(archived.Concat(trashed).ToList());
        }
        else
        {
            SetNotes(_repository.GetByState(NoteState.Active));
        }
    }
```

with:

```csharp
    public void Refresh()
    {
        SetNotes(_repository.GetByState(NoteState.Active));
    }
```

Replace:

```csharp
    private void OnToggleArchiveClick(object sender, RoutedEventArgs e)
    {
        _viewingArchive = !_viewingArchive;
        if (_viewingArchive)
        {
            _repository.PurgeExpiredTrash(TimeSpan.FromDays(NotesRepository.DefaultTrashRetentionDays));
        }
        ToggleArchiveButton.Content = _viewingArchive ? "Activas" : "Archivadas";
        NewNoteButton.Visibility = _viewingArchive ? Visibility.Collapsed : Visibility.Visible;
        Refresh();
    }

    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
```

with:

```csharp
    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
```

(This removes `OnToggleArchiveClick` entirely and leaves `OnManageArchiveClick` as-is — it already just calls `_coordinator.OpenOrActivateNotesManager()` and needs no change.)

The trash auto-purge that used to run inside `OnToggleArchiveClick` (`_repository.PurgeExpiredTrash(...)`) is not lost — `App.xaml.cs` already runs the same purge once at startup (see Phase 3a work), which is the only remaining trigger for it now that there's no in-dock archive view to enter. This is an accepted reduction in purge frequency (was: startup + every time Archivadas was opened; now: startup only) — fine, since the purge is about a 30-day window, not needing frequent re-checks.

- [ ] **Step 4: Build**

Kill any running `Fanote.exe`, then:
```bash
cd fanote
dotnet build src/Fanote/Fanote.csproj -v quiet
```
Expected: Build succeeded, 0 errors. (If `ToggleArchiveButton` or `_viewingArchive` show up in an error, a reference to them was missed — search the file for both names and remove/update.)

- [ ] **Step 5: Manual regression check**

Run: `dotnet run --project src/Fanote --no-build -c Debug`

Confirm: hovering the pill still expands the panel; the tab list still shows active notes and scrolls when there are many; clicking a tab still opens/activates its note; the "+" button (now a small circle) still creates a note; the gear button (now a small circle, no longer a bottom bar) still opens "Gestionar notas"; there is no "Archivadas" button anywhere in the dock; opening "Gestionar notas" and filtering by Archivadas/Papelera there still works (unchanged window).

- [ ] **Step 6: Run the full test suite**

Run: `cd fanote && dotnet test`
Expected: 80 passed (unchanged — this task touches no `Fanote.Core` code).

- [ ] **Step 7: Commit**

```bash
cd fanote
git add src/Fanote/Windowing/EdgeDockWindow.xaml src/Fanote/Windowing/EdgeDockWindow.xaml.cs
git commit -m "$(cat <<'EOF'
Drop dock's Archivadas toggle; +/gear become small circular buttons

The expanded panel now only ever shows active notes — NotesManagerWindow
(gear icon, unchanged) is the sole way to reach archived/trashed notes,
per the fan-tabs redesign spec. "+ Nueva nota" and the gear both become
small circular buttons at the end of the (now single, scrollable)
column of tabs and buttons, instead of a bottom bar of three text/icon
buttons. Sets up the layout for the tab-template redesign next.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Redesign the tab template — vertical rotated label

**Files:**
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml`

**Interfaces:**
- Consumes: `NoteTitleConverter` (existing, unchanged).
- Produces: `NoteTabButtonStyle` with a rotated-label `ContentTemplate`. Consumed visually by Task 3 (which animates the `Button` this style produces).

- [ ] **Step 1: Replace `NoteTabButtonStyle`**

Replace the whole `NoteTabButtonStyle` style (from `<Style x:Key="NoteTabButtonStyle" TargetType="Button">` to its closing `</Style>`) with:

```xml
        <Style x:Key="NoteTabButtonStyle" TargetType="Button">
            <Setter Property="ContentTemplate">
                <Setter.Value>
                    <DataTemplate>
                        <TextBlock Text="{Binding Text, Converter={StaticResource NoteTitleConverter}}"
                                   FontWeight="Bold"
                                   TextTrimming="CharacterEllipsis">
                            <TextBlock.LayoutTransform>
                                <RotateTransform Angle="-90" />
                            </TextBlock.LayoutTransform>
                        </TextBlock>
                    </DataTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Grid>
                            <Border Background="{TemplateBinding Background}" CornerRadius="4" />
                            <Border x:Name="HoverOverlay" Background="Black" Opacity="0" CornerRadius="4" />
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center"
                                              Margin="{TemplateBinding Padding}" />
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="HoverOverlay" Property="Opacity" Value="0.12" />
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="HoverOverlay" Property="Opacity" Value="0.22" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

This drops the old `HorizontalContentAlignment="Left"` setter (the label is now centered, since it's rotated) and the two-line title+state-label `StackPanel` (no more state label — the dock no longer shows archived/trashed notes at all, so it would always have been empty here anyway).

- [ ] **Step 2: Remove the now-unused `NoteStateLabelConverter` resource declaration**

In the same file's `<Window.Resources>`, remove this line (the converter class itself stays — it's still used by `NotesManagerWindow.xaml`, which is untouched):

```xml
        <local:NoteStateLabelConverter x:Key="NoteStateLabelConverter" />
```

- [ ] **Step 3: Give each tab a fixed height matching `ExpandedPerNoteLength`**

In the `TabsList.ItemTemplate`'s `DataTemplate` (from Task 1), add an explicit `Height="36"` to the `Button` (leaves a small margin within the 40px-per-note budget `EdgeGeometry.ExpandedPerNoteLength` already assumes, so the dynamic panel sizing from the previous session still roughly matches how much room the tabs actually take):

```xml
                                <Button Content="{Binding}"
                                        Background="{Binding Color}"
                                        Height="36"
                                        Margin="4"
                                        Padding="6"
                                        Click="OnTabClick"
                                        Tag="{Binding}"
                                        Style="{StaticResource NoteTabButtonStyle}" />
```

- [ ] **Step 4: Build**

Kill any running `Fanote.exe`, then:
```bash
cd fanote
dotnet build src/Fanote/Fanote.csproj -v quiet
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manual check**

Run the app. Expand the dock with a few notes present. Confirm: each tab shows its note's title rotated vertically (readable top-to-bottom or bottom-to-top — either is fine, whichever `Angle="-90"` produces, no need to fine-tune further unless it's genuinely hard to read), each keeps its own background color, clicking a tab still opens/activates the right note, and there's no leftover empty space where the old state-label line used to be.

- [ ] **Step 6: Run the full test suite**

Run: `cd fanote && dotnet test`
Expected: 80 passed.

- [ ] **Step 7: Commit**

```bash
cd fanote
git add src/Fanote/Windowing/EdgeDockWindow.xaml
git commit -m "$(cat <<'EOF'
Redesign tab template: vertical rotated label, no more state line

Each tab's label now runs vertically (RotateTransform -90°) instead of
horizontally, matching the fan-tabs reference design. Drops the
Archived/Papelera state-label line — no longer needed now that the
dock's expanded panel only ever shows active notes (previous commit).
The NoteStateLabelConverter class itself is untouched; only its now-
unused resource declaration in this file is removed.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Staggered per-tab entrance animation

**Files:**
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml`
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml.cs`

**Interfaces:**
- Produces: `EdgeDockWindow.OnTabLoaded(object, RoutedEventArgs)` — internal event handler, not consumed elsewhere.

**Known accepted quirk** (write this down so it doesn't look like a surprise bug later): `TabsList.ItemsSource` is reassigned to a brand-new list on every `Refresh()` (not an `ObservableCollection` with incremental updates), so WPF regenerates every tab's container from scratch each time — meaning if a note is created/archived/trashed *while the panel is already expanded*, **every** tab replays its entrance animation, not just the changed one. This plan accepts that as a minor, relatively rare cosmetic quirk rather than adding the extra state-tracking needed to animate only genuinely new tabs — YAGNI unless it turns out to bother in practice.

**Why `Loaded` instead of `ItemContainerGenerator.StatusChanged`**: the design spec's error-handling section worried about the animation code running before a container exists yet (racing `ItemContainerGenerator.Status`), requiring an explicit check to skip animating a not-yet-realized tab. Driving the animation from each `Button`'s own `Loaded` event sidesteps that race entirely — `Loaded` cannot fire before the element exists and is in the visual tree, so there's no "does this container exist yet" check to get wrong.

- [ ] **Step 1: Give each tab a starting (hidden) state and a `RenderTransform` to animate**

In `TabsList.ItemTemplate`'s `DataTemplate`, add `Opacity="0"`, `Loaded="OnTabLoaded"`, and a `RenderTransform`:

```xml
                            <DataTemplate>
                                <Button Content="{Binding}"
                                        Background="{Binding Color}"
                                        Height="36"
                                        Margin="4"
                                        Padding="6"
                                        Click="OnTabClick"
                                        Loaded="OnTabLoaded"
                                        Tag="{Binding}"
                                        Style="{StaticResource NoteTabButtonStyle}"
                                        Opacity="0">
                                    <Button.RenderTransform>
                                        <TranslateTransform X="20" />
                                    </Button.RenderTransform>
                                </Button>
                            </DataTemplate>
```

(`X="20"` — the tab starts 20px further out than its resting position, along the axis perpendicular to the screen edge, and slides in to X=0. This assumes a right-edge dock, which is the only edge the app actually creates today — see `App.xaml.cs`'s hardcoded `EdgePosition.Right`.)

- [ ] **Step 2: Implement `OnTabLoaded`**

Add this method to `EdgeDockWindow.xaml.cs` (e.g. right after `OnTabClick`):

```csharp
    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        int index = TabsList.Items.IndexOf(button.DataContext);
        if (index < 0) return;

        var delay = TimeSpan.FromMilliseconds(45 * index);

        var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)))
        {
            BeginTime = delay
        };
        button.BeginAnimation(OpacityProperty, opacityAnimation);

        var translate = (TranslateTransform)button.RenderTransform;
        var slideAnimation = new System.Windows.Media.Animation.DoubleAnimation(20, 0, new Duration(TimeSpan.FromMilliseconds(200)))
        {
            BeginTime = delay
        };
        translate.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
    }
```

Both animations use explicit `From`/`To` (0→1 and 20→0) rather than relying on the button's current value as an implicit origin — same lesson as the Phase 3a `AnimationException: ... default origin value of 'NaN'` fix in `ApplyGeometry`, even though here the literal values are already known constants rather than something read back from a possibly-uninitialized property.

Add `using System.Windows.Controls;` to the top of `EdgeDockWindow.xaml.cs` if it isn't already there (needed for the `Button` cast) — check the existing `using` block first; if `System.Windows.Controls` types were already accessible via another using or the generated XAML partial class, building will simply confirm one way or the other.

- [ ] **Step 3: Build**

Kill any running `Fanote.exe`, then:
```bash
cd fanote
dotnet build src/Fanote/Fanote.csproj -v quiet
```
Expected: Build succeeded, 0 errors. If `Button` or `TranslateTransform` are unresolved, add the missing `using System.Windows.Controls;` / `using System.Windows.Media;` (the latter is very likely already present, used elsewhere in this file for `DoubleAnimation`).

- [ ] **Step 4: Manual check**

Run the app with at least 3-4 notes. Hover the pill. Confirm: tabs slide/fade in one after another (not all at once), each keeping its own color, in the same order they're listed. Collapse and re-expand a few times rapidly — confirm it doesn't crash or leave tabs stuck invisible. Create a new note while the panel is already expanded — confirm the (accepted quirk) re-cascade happens but doesn't crash or look broken, just replays the reveal.

- [ ] **Step 5: Run the full test suite**

Run: `cd fanote && dotnet test`
Expected: 80 passed.

- [ ] **Step 6: Commit**

```bash
cd fanote
git add src/Fanote/Windowing/EdgeDockWindow.xaml src/Fanote/Windowing/EdgeDockWindow.xaml.cs
git commit -m "$(cat <<'EOF'
Add staggered entrance animation per tab (~45ms apart)

Each tab starts hidden (Opacity=0, translated 20px out from its
resting position) and fades + slides into place when its container
loads, with BeginTime offset by 45ms * its index in the list — the
"shingling" cascade from the fan-tabs reference design. Both animations
use explicit From/To rather than an implicit current-value origin, per
the Phase 3a NaN-origin lesson.

Accepted quirk (documented in the plan): since TabsList.ItemsSource is
replaced wholesale on every Refresh() rather than incrementally
updated, a note changing state while the panel is already expanded
replays every tab's entrance animation, not just the changed one — not
fixed here, YAGNI unless it proves annoying in practice.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Open a note growing from its own tab's position

**Files:**
- Modify: `src/Fanote/Windowing/AppCoordinator.cs`
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml.cs`
- Modify: `src/Fanote/Windowing/NoteWindow.xaml.cs`

**Interfaces:**
- Produces: `AppCoordinator.OpenOrActivateNote(Note note, EdgeDockWindow requestingDock, System.Windows.Rect? originRect = null)` (signature change — the two-argument call sites in `EdgeDockWindow` get updated in this same task); `NoteWindow.AnimateFrom(System.Windows.Rect origin)` (new, internal).

**DPI note** (read before writing this task): `Visual.PointToScreen` returns **physical pixel** coordinates, not the DIPs that `Window.Left/Top/Width/Height` expect. On a monitor at 100% scale these are numerically identical, which is exactly this machine's current setup (both monitors reported `DpiScale: 1x` during Phase 3a) — so a mistake here would build, run, and look correct on this hardware while still being wrong on any monitor with real scaling. Use `VisualTreeHelper.GetDpi(visual)` (available since .NET Core 3.0/WPF, no new dependency) to convert, the same category of fix as `DpiConversion.ToWorkingArea` in Phase 3a. This cannot be verified manually on this machine's current monitors (see Testing note in Step 5) — get the conversion right by reasoning, not by "it looked fine when I ran it."

- [ ] **Step 1: `AppCoordinator.OpenOrActivateNote` gains an optional origin**

Replace:

```csharp
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
```

with:

```csharp
    public void OpenOrActivateNote(Note note, EdgeDockWindow requestingDock, System.Windows.Rect? originRect = null)
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
        if (originRect is { } origin)
        {
            noteWindow.AnimateFrom(origin);
        }
        _openNoteWindows[note.Id] = noteWindow;
        noteWindow.Closed += (_, _) => _openNoteWindows.Remove(note.Id);
        noteWindow.Show();
        NativeMethods.ForceActivate(noteWindow);
    }
```

(`System.Windows.Rect` is qualified explicitly here — despite the `using System.Windows;` at the top of this file already covering it, `AppCoordinator.cs` also has `using Fanote.Core;`, which has its own `Rect` type. Bare `Rect` would be CS0104 ambiguous, the same mistake already made and caught twice earlier in this project — see `EdgeDockWindow._currentRect`'s declaration.)

- [ ] **Step 2: Capture the clicked tab's screen position in `EdgeDockWindow.OnTabClick`**

Replace:

```csharp
    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
        {
            _coordinator.OpenOrActivateNote(note, this);
        }
    }
```

with:

```csharp
    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note } element)
        {
            var screenPositionPixels = element.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(element);
            var tabRect = new System.Windows.Rect(
                screenPositionPixels.X / dpi.DpiScaleX,
                screenPositionPixels.Y / dpi.DpiScaleY,
                element.ActualWidth,
                element.ActualHeight);
            _coordinator.OpenOrActivateNote(note, this, tabRect);
        }
    }
```

(`System.Windows.Rect` is qualified explicitly here because this file also has `using Fanote.Core;`, which has its own `Rect` — same ambiguity already hit once in this file for `_currentRect`, see its declaration a few lines up. `element.ActualWidth`/`ActualHeight` are already in DIPs — WPF layout sizes always are — so only the `PointToScreen` pixel coordinates need dividing by the DPI scale, not the width/height.)

Add `using System.Windows.Media;` to the top of `EdgeDockWindow.xaml.cs` if it isn't already there (needed for `VisualTreeHelper`) — check first; `System.Windows.Media.Animation` is already imported via fully-qualified use elsewhere in this file, but `VisualTreeHelper` lives in the parent `System.Windows.Media` namespace specifically.

- [ ] **Step 3: `NoteWindow.AnimateFrom`**

Add this method to `NoteWindow.xaml.cs` (e.g. right after the constructor):

```csharp
    /// <summary>
    /// Makes the window appear to grow from <paramref name="origin"/> (a tab's on-screen rect)
    /// to whatever Left/Top/Width/Height are already set to (the final position PositionNoteWindow
    /// computed) — call this after that positioning and before Show(). Explicit From/To throughout,
    /// per the Phase 3a NaN-origin animation lesson.
    /// </summary>
    internal void AnimateFrom(System.Windows.Rect origin)
    {
        double targetLeft = Left;
        double targetTop = Top;
        double targetWidth = Width;
        double targetHeight = Height;

        Left = origin.X;
        Top = origin.Y;
        Width = origin.Width;
        Height = origin.Height;

        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        BeginAnimation(LeftProperty, new System.Windows.Media.Animation.DoubleAnimation(origin.X, targetLeft, duration));
        BeginAnimation(TopProperty, new System.Windows.Media.Animation.DoubleAnimation(origin.Y, targetTop, duration));
        BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(origin.Width, targetWidth, duration));
        BeginAnimation(HeightProperty, new System.Windows.Media.Animation.DoubleAnimation(origin.Height, targetHeight, duration));
    }
```

`System.Windows.Rect` is qualified explicitly here too — `NoteWindow.xaml.cs` also has `using Fanote.Core;`, same ambiguity.

Note `NoteWindow.xaml` sets `MinWidth="180" MinHeight="160"` — WPF clamps the window's actually-rendered size to those minimums regardless of the animated `Width`/`Height` DP value, so the window won't visibly shrink all the way down to a ~30×36px tab's exact size at the start of the animation; it'll still visibly grow, just from ~180×160 rather than the tab's literal dimensions. This is an accepted, harmless side effect of a pre-existing constraint — not a bug to chase.

- [ ] **Step 4: Build**

Kill any running `Fanote.exe`, then:
```bash
cd fanote
dotnet build src/Fanote/Fanote.csproj -v quiet
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manual check (with a documented verification gap)**

Run the app. Click a tab to open its note — confirm the note window visibly grows open from roughly where that tab was, rather than just appearing. Click a few different tabs (different vertical positions) and confirm each opens from its own position, not always the same spot. Confirm existing behavior is intact: clicking an already-open note's tab still just activates the existing window (no origin animation replay); the cascade offset for multiple simultaneously-open notes still keeps them from landing exactly on top of each other.

**Cannot be verified on this machine**: whether the `VisualTreeHelper.GetDpi` conversion is actually correct on a monitor with non-100% scaling — both of this machine's monitors report 100% (`DpiScale: 1x`, per the Phase 3a enumeration), so a missing or wrong DPI conversion here would look identical to a correct one in this environment. Note this gap in `docs/STATUS.md` after this plan (see "After this plan" below) rather than claiming it's fully verified.

- [ ] **Step 6: Run the full test suite**

Run: `cd fanote && dotnet test`
Expected: 80 passed.

- [ ] **Step 7: Commit**

```bash
cd fanote
git add src/Fanote/Windowing/AppCoordinator.cs src/Fanote/Windowing/EdgeDockWindow.xaml.cs src/Fanote/Windowing/NoteWindow.xaml.cs
git commit -m "$(cat <<'EOF'
Open a note growing from its own tab's screen position

AppCoordinator.OpenOrActivateNote takes an optional origin Rect;
EdgeDockWindow.OnTabClick captures the clicked tab's actual screen
position (via PointToScreen, converted from physical pixels to DIPs
with VisualTreeHelper.GetDpi — PointToScreen does NOT return DIPs,
easy to get wrong on a scaled monitor and not something this machine's
100%-scale monitors can catch) and passes it through. NoteWindow.AnimateFrom
sets Left/Top/Width/Height to that origin and animates to whatever
PositionNoteWindow already computed as the final cascade position, with
explicit From/To per the Phase 3a animation lesson.

Verification gap: the DPI conversion can't be exercised on this
machine's current monitors, both reporting 100% scale — noted in
STATUS.md rather than claimed as fully verified.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## After this plan

Update `docs/STATUS.md`: mark the fan-tabs redesign done, fold its former
"idea pendiente de brainstorming" entry into a implemented-history entry
(same style as the Phase 3a and Archivadas sections), and explicitly flag
the two open items so a future session doesn't have to rediscover them:
(1) the DPI-conversion-in-`OnTabClick` verification gap (needs a monitor
with real scaling to confirm), and (2) the accepted "whole fan replays its
entrance animation on any note-list change while already expanded" quirk
from Task 3, in case it's worth revisiting later.

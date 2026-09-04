using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _hoverPollTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private int _noteCount;
    private bool _pointerInside;

    // Tracked ourselves rather than read back from Left/Top/Width/Height: those can observe NaN
    // (WPF's uninitialized default) if something forces a resize/animation re-evaluation before
    // the window has ever been positioned — see ApplyGeometry's comment on why this matters.
    private Fanote.Core.Rect _currentRect;

    private IntPtr _hwnd;
    private readonly DispatcherTimer _regionApplyTimer;

    // ItemsControl.ItemContainerGenerator.ContainerFromIndex returns a ContentPresenter here, not
    // the Button from ItemTemplate — that's how a plain (unstyled) ItemsControl always generates
    // its containers, only Selector-derived controls hand back the templated element directly. A
    // `ContainerFromIndex(i) is Button` check (as this file's entrance/reset replay and the region
    // computation both used to do) therefore silently never matches — found empirically via
    // instrumentation while wiring up SetTabFanRegion (see the design spec) — 0 containers "found"
    // every time, even though ItemContainerGenerator.Status correctly reports ContainersGenerated.
    // Tracking the real Button references ourselves, populated as each one's own Loaded fires
    // (always the actual Button, since Loaded is wired directly on it in the DataTemplate),
    // sidesteps the container-type question entirely. Cleared on every SetNotes so a note being
    // archived/added can't leave a stale index→Button mapping pointing at a recycled container.
    private readonly Dictionary<int, Button> _tabButtons = new();

    private const double NoteWindowCascadeStep = 30;
    private const int NoteWindowMaxCascadeSteps = 8;

    public EdgeDockWindow(EdgePosition edge, MonitorInfo monitor, NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = monitor.WorkArea;
        _repository = repository;
        _coordinator = coordinator;
        _currentRect = EdgeGeometry.PillRect(_workingArea, _edge, _noteCount);

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            _fanState.CollapseTimerElapsed();
        };

        _fanState.ExpansionChanged += (_, _) => ApplyGeometry();

        // Deliberately not WPF's MouseEnter/MouseLeave: ApplyGeometry moves and resizes this
        // window on every expand/collapse (it slides the outer edge out while growing), and a
        // window moving/resizing out from under a stationary cursor makes Win32 fire a spurious
        // WM_MOUSELEAVE even though the pointer never actually left. Worse, once WPF has fired
        // MouseLeave once, it considers the pointer already "left" internally — a later handler
        // that merely ignores a spurious leave doesn't undo that, so WPF then never raises the
        // real MouseLeave when the pointer genuinely moves away afterward, leaving the panel
        // stuck expanded forever. Polling the true cursor position directly sidesteps this
        // Win32-tracking confusion entirely: hover state is derived from where the pointer
        // actually is right now, never from an event that may or may not reflect reality.
        _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _hoverPollTimer.Tick += (_, _) => PollHoverState();
        _hoverPollTimer.Start();

        // Recorta la forma del panel desplegado exactamente cuando su contenido empieza a hacerse
        // visible (el fadeIn de ApplyGeometry tiene BeginTime=120ms) — no antes, ni al terminar la
        // animación entera. Si se aplicara al terminar (Completed, t=200ms), el contenido ya llevaría
        // un rato totalmente visible dentro de un rectángulo sin recortar, y el recorte final se vería
        // como un "pop" — aplicado en el instante en que Opacity empieza a subir desde 0, en cambio,
        // no hay nada visible todavía que se vea mal recortado. Si el BeginTime del fadeIn cambia
        // alguna vez, este Interval tiene que moverse con él.
        _regionApplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _regionApplyTimer.Tick += (_, _) =>
        {
            _regionApplyTimer.Stop();
            ApplyTabFanRegion();
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNonActivating(_hwnd);
            NativeMethods.ApplyRoundedCornersAndShadow(_hwnd);
        };

        ApplyGeometry();
    }

    private void PollHoverState()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorScreen = NativeMethods.GetCursorScreenPosition();
        double cursorX = cursorScreen.X / dpi.DpiScaleX;
        double cursorY = cursorScreen.Y / dpi.DpiScaleY;

        // Width/Height (the animated DPs), not ActualWidth/ActualHeight — the latter only
        // update once WPF's layout system catches up with the animation's current frame, which
        // can lag a beat behind the value the animation clock has already reached.
        bool isInside = cursorX >= Left && cursorX <= Left + Width
            && cursorY >= Top && cursorY <= Top + Height;

        if (isInside && !_pointerInside)
        {
            _pointerInside = true;
            _fanState.PointerEntered();
        }
        else if (!isInside && _pointerInside)
        {
            _pointerInside = false;
            _fanState.PointerLeft();
            _collapseTimer.Start();
        }
    }

    private void ApplyTabFanRegion()
    {
        if (_hwnd == IntPtr.Zero) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var tabRects = new List<Fanote.Core.Rect>();
        foreach (var button in _tabButtons.Values)
        {
            var origin = button.TranslatePoint(new Point(0, 0), this);
            tabRects.Add(new Fanote.Core.Rect(
                origin.X * dpi.DpiScaleX, origin.Y * dpi.DpiScaleY,
                button.ActualWidth * dpi.DpiScaleX, button.ActualHeight * dpi.DpiScaleY));
        }

        var footerOrigin = FooterPanel.TranslatePoint(new Point(0, 0), this);
        var footerRect = new Fanote.Core.Rect(
            footerOrigin.X * dpi.DpiScaleX, footerOrigin.Y * dpi.DpiScaleY,
            FooterPanel.ActualWidth * dpi.DpiScaleX, FooterPanel.ActualHeight * dpi.DpiScaleY);

        var pieces = TabRegionShape.BuildRegion(tabRects, footerRect, cornerRadius: 9 * dpi.DpiScaleX);
        NativeMethods.SetTabFanRegion(_hwnd, pieces);

        // DWM's shadow tracks the window's full rectangular bounds, not this custom shape — left
        // on, it paints a translucent box across exactly the gutter/notches the region was meant
        // to remove (confirmed on a real run, see NativeMethods.ApplyShadow's remarks). Always
        // off while the shaped region is active; ClearTabFanRegion turns it back on.
        NativeMethods.ClearShadow(_hwnd);
    }

    private void ClearTabFanRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        NativeMethods.ClearWindowRegion(_hwnd);
        NativeMethods.ApplyShadow(_hwnd);
    }

    private void ApplyGeometry()
    {
        bool expanding = _fanState.IsExpanded;
        var rect = expanding
            ? EdgeGeometry.ExpandedRect(_workingArea, _edge, _noteCount)
            : EdgeGeometry.PillRect(_workingArea, _edge, _noteCount);

        _regionApplyTimer.Stop();

        // Clear any animation left running (with FillBehavior.HoldEnd, the default) by a
        // previous ApplyGeometry() call. A held animation outranks a plain local-value
        // assignment in WPF's property value precedence, so without this, once any
        // animation has ever run on this window, the instant-set branch below would
        // silently become a no-op and the window would freeze in place forever.
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        PanelContent.BeginAnimation(OpacityProperty, null);
        PillSwatches.BeginAnimation(OpacityProperty, null);

        // Opacity alone doesn't stop hit-testing in WPF — without this, whichever of the two
        // overlapping panels is merely invisible (not the active one) still swallows clicks
        // meant for the buttons underneath it.
        PanelContent.IsHitTestVisible = expanding;
        PillSwatches.IsHitTestVisible = !expanding;

        // The staggered per-tab entrance (see OnTabLoaded/PlayTabEntrance) only fires from a
        // Button's Loaded event, which happens once when SetNotes assigns TabsList.ItemsSource —
        // NOT every time the panel expands. Without this, tabs only ever cascade in once at
        // startup (while PanelContent is still invisible) and every subsequent hover just shows
        // them all already-visible. Replay it explicitly on every expand, and reset each tab back
        // to its hidden pre-entrance state on collapse so the next expand has something to reveal.
        if (expanding)
        {
            foreach (var (index, button) in _tabButtons)
            {
                PlayTabEntrance(button, index);
            }
        }
        else
        {
            foreach (var button in _tabButtons.Values)
            {
                ResetTabEntrance(button);
            }
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            Left = rect.X;
            Top = rect.Y;
            Width = rect.Width;
            Height = rect.Height;
            PanelContent.Opacity = expanding ? 1 : 0;
            PillSwatches.Opacity = expanding ? 0 : 1;
            _currentRect = rect;
            if (expanding) ApplyTabFanRegion(); else ClearTabFanRegion();
            return;
        }

        // Explicit From values, computed from our own tracked _currentRect rather than left null
        // (which would make WPF look up Left/Top/Width/Height's own current value as the origin).
        // That implicit lookup is what used to throw "DoubleAnimation cannot use default origin
        // value of NaN": under PerMonitorV2 (see app.manifest), window creation can trigger an
        // extra internal resize pass that re-evaluates this animation before Left/Top/Width/Height
        // have ever actually been set, observing WPF's uninitialized NaN default. Supplying From
        // ourselves sidesteps that lookup entirely.
        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var leftAnimation = new System.Windows.Media.Animation.DoubleAnimation(_currentRect.X, rect.X, duration);
        var topAnimation = new System.Windows.Media.Animation.DoubleAnimation(_currentRect.Y, rect.Y, duration);
        var widthAnimation = new System.Windows.Media.Animation.DoubleAnimation(_currentRect.Width, rect.Width, duration);
        var heightAnimation = new System.Windows.Media.Animation.DoubleAnimation(_currentRect.Height, rect.Height, duration);

        // Observed empirically: the animated Height in particular can finish its clock (WPF's own
        // Height getter reports the target value) without the real underlying window actually
        // being resized to match — Width doesn't show this, only Height does, for reasons that
        // didn't resolve under investigation (ruled out: the DWM shadow/rounded-corners call, and
        // ResizeMode). A direct assignment (the !ClientAreaAnimation branch above) always applies
        // correctly, so force-commit each property's final value as a plain local value once its
        // animation completes — this guarantees the settled state is correct even on the runs
        // where the animation alone doesn't visibly reach it.
        double targetX = rect.X, targetY = rect.Y, targetWidth = rect.Width, targetHeight = rect.Height;
        leftAnimation.Completed += (_, _) => { BeginAnimation(LeftProperty, null); Left = targetX; };
        topAnimation.Completed += (_, _) => { BeginAnimation(TopProperty, null); Top = targetY; };
        widthAnimation.Completed += (_, _) => { BeginAnimation(WidthProperty, null); Width = targetWidth; };
        heightAnimation.Completed += (_, _) => { BeginAnimation(HeightProperty, null); Height = targetHeight; };

        BeginAnimation(LeftProperty, leftAnimation);
        BeginAnimation(TopProperty, topAnimation);
        BeginAnimation(WidthProperty, widthAnimation);
        BeginAnimation(HeightProperty, heightAnimation);
        _currentRect = rect;

        // The pill is much smaller than the expanded panel (e.g. 12px vs 220px thick), so the
        // note buttons spend most of the resize crammed into a width/height they don't fit —
        // that's the "badly fit for an instant" glitch. Rather than chase every intermediate
        // layout, hide the content while the window is still mid-resize and only reveal it
        // once it's nearly at full size (and hide it again the instant a collapse starts). The
        // pill's own color-swatch preview (PillSwatches) does the mirror image of this: it's
        // what's showing while collapsed, so it fades out the instant expansion starts and
        // back in only in the closing moments of collapse.
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(80)))
        {
            BeginTime = TimeSpan.FromMilliseconds(120)
        };
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(60)));

        PanelContent.BeginAnimation(OpacityProperty, expanding ? fadeIn : fadeOut);
        PillSwatches.BeginAnimation(OpacityProperty, expanding ? fadeOut : fadeIn);

        if (expanding)
        {
            _regionApplyTimer.Start();
        }
        else
        {
            ClearTabFanRegion();
        }
    }

    public void Refresh()
    {
        SetNotes(_repository.GetByState(NoteState.Active));
    }

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        _tabButtons.Clear();
        TabsList.ItemsSource = notes;
        _noteCount = notes.Count;
        ApplyGeometry();
    }

    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenOrActivateNotesManager();
    }

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

    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        int index = TabsList.Items.IndexOf(button.DataContext);
        if (index < 0) return;

        _tabButtons[index] = button;
        PlayTabEntrance(button, index);

        // On a cold start, SetNotes/ApplyGeometry(expanding: true) can both run before any tab's
        // Loaded has fired yet, which means _regionApplyTimer's 120ms callback can find an empty
        // _tabButtons and apply a region that excludes every tab, not just the gaps between them.
        // Recomputing here too, once each tab genuinely finishes loading, is a self-correcting
        // safety net: harmless if the timer already got it right, the only thing that fixes it if
        // it didn't. Only recompute while actually expanded — Loaded can fire at any time (e.g.
        // right after SetNotes while the panel is still collapsed), and the pill never uses regions.
        if (_fanState.IsExpanded)
        {
            ApplyTabFanRegion();
        }
    }

    private void PlayTabEntrance(Button button, int index)
    {
        // Increasing width by index + negative top margin overlap, validated in mockup
        // (https://claude.ai/code/artifact/25194ada-e021-4222-bc03-722f78250544) — a fanned-out
        // stack of cards rather than one full-width column. Set here (not in XAML) because both
        // depend on the item's index, which XAML has no clean way to express; this already runs
        // on every container both the first time it loads and on every replay of this method
        // (see ApplyGeometry's expanding-branch loop), so re-assigning the same values each call
        // is harmless. Width stays exactly the mockup's validated formula; the overlap is scaled
        // from the mockup's ~19px (on a 64px-tall tab there) to this app's real 80px tab height.
        button.Width = 32 + index * 14;
        button.Margin = new Thickness(0, index == 0 ? 4 : -28, 0, 4);

        var delay = TimeSpan.FromMilliseconds(45 * index);

        button.BeginAnimation(OpacityProperty, null);
        var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)))
        {
            BeginTime = delay
        };
        button.BeginAnimation(OpacityProperty, opacityAnimation);

        var translate = new TranslateTransform(20, 0);
        button.RenderTransform = translate;
        var slideAnimation = new System.Windows.Media.Animation.DoubleAnimation(20, 0, new Duration(TimeSpan.FromMilliseconds(200)))
        {
            BeginTime = delay
        };
        translate.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
    }

    private static void ResetTabEntrance(Button button)
    {
        button.BeginAnimation(OpacityProperty, null);
        button.Opacity = 0;

        if (button.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 20;
        }
    }

    internal void PositionNoteWindow(NoteWindow noteWindow)
    {
        int step = _coordinator.OpenNoteWindowCount % NoteWindowMaxCascadeSteps;

        var left = Left - noteWindow.Width - 12 - step * NoteWindowCascadeStep;
        var top = Top + step * NoteWindowCascadeStep;

        // Clamp to the visible working area so later cascade steps (or a left-anchored dock)
        // can't land a note window partially or fully off-screen on a narrow/short display.
        noteWindow.Left = Math.Max(left, _workingArea.X);
        noteWindow.Top = Math.Min(top, _workingArea.Y + _workingArea.Height - noteWindow.Height);
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        var existingCount = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorPalette.Colors[existingCount % NoteColorPalette.Colors.Length];
        _repository.Create(string.Empty, color, screenOrigin: "primary");
        _coordinator.RefreshAll();
    }
}

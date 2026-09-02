using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private bool _viewingArchive;
    private int _noteCount;

    // Tracked ourselves rather than read back from Left/Top/Width/Height: those can observe NaN
    // (WPF's uninitialized default) if something forces a resize/animation re-evaluation before
    // the window has ever been positioned — see ApplyGeometry's comment on why this matters.
    private Fanote.Core.Rect _currentRect;

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
            NativeMethods.ApplyRoundedCornersAndShadow(hwnd);
        };

        ApplyGeometry();
    }

    private void ApplyGeometry()
    {
        bool expanding = _fanState.IsExpanded;
        var rect = expanding
            ? EdgeGeometry.ExpandedRect(_workingArea, _edge, _noteCount)
            : EdgeGeometry.PillRect(_workingArea, _edge, _noteCount);

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

        if (!SystemParameters.ClientAreaAnimation)
        {
            Left = rect.X;
            Top = rect.Y;
            Width = rect.Width;
            Height = rect.Height;
            PanelContent.Opacity = expanding ? 1 : 0;
            PillSwatches.Opacity = expanding ? 0 : 1;
            _currentRect = rect;
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
        BeginAnimation(LeftProperty, new System.Windows.Media.Animation.DoubleAnimation(_currentRect.X, rect.X, duration));
        BeginAnimation(TopProperty, new System.Windows.Media.Animation.DoubleAnimation(_currentRect.Y, rect.Y, duration));
        BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(_currentRect.Width, rect.Width, duration));
        BeginAnimation(HeightProperty, new System.Windows.Media.Animation.DoubleAnimation(_currentRect.Height, rect.Height, duration));
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
    }

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

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        TabsList.ItemsSource = notes;
        _noteCount = notes.Count;
        ApplyGeometry();
    }

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
    {
        _coordinator.OpenOrActivateNotesManager();
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
        {
            _coordinator.OpenOrActivateNote(note, this);
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

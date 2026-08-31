using System.Collections.Generic;
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
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();

    private const double NoteWindowCascadeStep = 30;
    private const int NoteWindowMaxCascadeSteps = 8;

    public EdgeDockWindow(EdgePosition edge, WorkingArea workingArea, NotesRepository repository)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = workingArea;
        _repository = repository;

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

        // Clear any animation left running (with FillBehavior.HoldEnd, the default) by a
        // previous ApplyGeometry() call. A held animation outranks a plain local-value
        // assignment in WPF's property value precedence, so without this, once any
        // animation has ever run on this window, the instant-set branch below would
        // silently become a no-op and the window would freeze in place forever.
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);

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

    public void Refresh()
    {
        SetNotes(_repository.GetByState(NoteState.Active));
    }

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        TabsList.ItemsSource = notes;
    }

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

    private void PositionNoteWindow(NoteWindow noteWindow)
    {
        int step = _openNoteWindows.Count % NoteWindowMaxCascadeSteps;

        var left = Left - noteWindow.Width - 12 - step * NoteWindowCascadeStep;
        var top = Top + step * NoteWindowCascadeStep;

        // Clamp to the visible working area so later cascade steps (or a left-anchored dock)
        // can't land a note window partially or fully off-screen on a narrow/short display.
        noteWindow.Left = Math.Max(left, _workingArea.X);
        noteWindow.Top = Math.Min(top, _workingArea.Y + _workingArea.Height - noteWindow.Height);
    }

    private static readonly string[] NoteColorPalette =
    {
        "#F5E3B3", // pale yellow
        "#C9E4DE", // mint
        "#F2C6DE", // pink
        "#B8D8E8", // pale blue
        "#D9C9E8", // pale lavender
        "#F2D9B8", // pale peach
    };

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        var existingCount = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorPalette[existingCount % NoteColorPalette.Length];
        _repository.Create(string.Empty, color, screenOrigin: "primary");
        Refresh();
    }
}

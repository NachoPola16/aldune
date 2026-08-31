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
    private int _noteWindowsOpened;

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
            _openNoteWindows[note.Id] = noteWindow;
            noteWindow.Closed += (_, _) => _openNoteWindows.Remove(note.Id);
            PositionNoteWindow(noteWindow);
            noteWindow.Show();
            NativeMethods.ForceActivate(noteWindow);
        }
    }

    private void PositionNoteWindow(NoteWindow noteWindow)
    {
        int step = _noteWindowsOpened % NoteWindowMaxCascadeSteps;
        _noteWindowsOpened++;

        noteWindow.Left = Left - noteWindow.Width - 12 - step * NoteWindowCascadeStep;
        noteWindow.Top = Top + step * NoteWindowCascadeStep;
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        _repository.Create(string.Empty, "#F5E3B3", screenOrigin: "primary");
        Refresh();
    }
}

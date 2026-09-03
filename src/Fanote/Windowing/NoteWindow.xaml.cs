using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

public partial class NoteWindow : Window
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(500);

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

        ApplyColor(note.Color);
        PopulateColorSwatches();

        if (note.State != NoteState.Active)
        {
            ActiveActions.Visibility = Visibility.Collapsed;
            InactiveActions.Visibility = Visibility.Visible;
        }

        TextBody.Text = note.Text;
        Title = NoteTitleHelper.GetTitle(note.Text);
        Loaded += (_, _) =>
        {
            TextBody.Focus();
            TextBody.CaretIndex = TextBody.Text.Length;
            TextBody.ScrollToEnd();
        };

        _autosaveTimer = new DispatcherTimer { Interval = AutosaveDelay };
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer.Stop();
            Flush();
        };

        TextBody.TextChanged += (_, _) =>
        {
            Title = NoteTitleHelper.GetTitle(TextBody.Text);
            _hasPendingEdit = true;
            _autosaveTimer.Stop();
            _autosaveTimer.Start();
        };

        Closing += (_, _) =>
        {
            Flush();
            _coordinator.RefreshAll();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };
    }

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
        var leftAnimation = new System.Windows.Media.Animation.DoubleAnimation(origin.X, targetLeft, duration);
        var topAnimation = new System.Windows.Media.Animation.DoubleAnimation(origin.Y, targetTop, duration);
        var widthAnimation = new System.Windows.Media.Animation.DoubleAnimation(origin.Width, targetWidth, duration);
        var heightAnimation = new System.Windows.Media.Animation.DoubleAnimation(origin.Height, targetHeight, duration);

        // FillBehavior.HoldEnd (the default) leaves these animations latched on Left/Top/
        // Width/Height forever once they finish, outranking any later plain assignment — the
        // same hazard already documented and guarded against in EdgeDockWindow.ApplyGeometry.
        // Unlike the dock, this window is user-draggable/resizable (see NoteWindow.xaml's
        // WindowChrome), so clear each animation and commit its final value as a plain local
        // value once it completes, or a drag/resize right after opening could fight a still-
        // active animation clock.
        leftAnimation.Completed += (_, _) => { BeginAnimation(LeftProperty, null); Left = targetLeft; };
        topAnimation.Completed += (_, _) => { BeginAnimation(TopProperty, null); Top = targetTop; };
        widthAnimation.Completed += (_, _) => { BeginAnimation(WidthProperty, null); Width = targetWidth; };
        heightAnimation.Completed += (_, _) => { BeginAnimation(HeightProperty, null); Height = targetHeight; };

        BeginAnimation(LeftProperty, leftAnimation);
        BeginAnimation(TopProperty, topAnimation);
        BeginAnimation(WidthProperty, widthAnimation);
        BeginAnimation(HeightProperty, heightAnimation);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void Flush()
    {
        if (!_hasPendingEdit) return;
        _hasPendingEdit = false;
        _repository.UpdateText(_note.Id, TextBody.Text);
    }

    private void ApplyColor(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        Background = brush;
        TextBody.Background = brush;
    }

    private void PopulateColorSwatches()
    {
        foreach (var color in NoteColorPalette.Colors)
        {
            var swatch = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(color)!,
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(color == _note.Color ? 2 : 0),
                Cursor = Cursors.Hand,
                Tag = color
            };
            swatch.MouseLeftButtonUp += OnColorSwatchClick;
            ColorSwatches.Children.Add(swatch);
        }
    }

    private void OnColorSwatchClick(object sender, MouseButtonEventArgs e)
    {
        var color = (string)((Border)sender).Tag;
        if (color == _note.Color) return;

        _note.Color = color;
        _repository.SetColor(_note.Id, color);
        ApplyColor(color);

        foreach (Border swatch in ColorSwatches.Children)
        {
            swatch.BorderThickness = new Thickness((string)swatch.Tag == color ? 2 : 0);
        }

        _coordinator.RefreshAll();
    }

    private void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        Flush();
        _repository.SetState(_note.Id, NoteState.Archived);
        _coordinator.RefreshAll();
        Close();
    }

    private void OnTrashClick(object sender, RoutedEventArgs e)
    {
        _hasPendingEdit = false; // discard any pending edit — the note is being trashed, not saved
        _repository.SetState(_note.Id, NoteState.Trashed);
        _coordinator.RefreshAll();
        Close();
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        Flush();
        _repository.SetState(_note.Id, NoteState.Active);
        _coordinator.RefreshAll();
        Close();
    }
}

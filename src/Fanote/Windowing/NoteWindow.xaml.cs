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
    private readonly EdgeDockWindow _owner;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    public NoteWindow(Note note, NotesRepository repository, EdgeDockWindow owner)
    {
        InitializeComponent();
        _note = note;
        _repository = repository;
        _owner = owner;

        ApplyColor(note.Color);
        PopulateColorSwatches();

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
            _owner.Refresh();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };
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

        _owner.Refresh();
    }

    private void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        Flush();
        _repository.SetState(_note.Id, NoteState.Archived);
        _owner.Refresh();
        Close();
    }

    private void OnTrashClick(object sender, RoutedEventArgs e)
    {
        _hasPendingEdit = false; // discard any pending edit — the note is being trashed, not saved
        _repository.SetState(_note.Id, NoteState.Trashed);
        _owner.Refresh();
        Close();
    }
}

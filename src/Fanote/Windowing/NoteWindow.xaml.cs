using System.Windows;
using System.Windows.Threading;
using Fanote.Core;

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

        TextBody.Text = note.Text;
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
            _hasPendingEdit = true;
            _autosaveTimer.Stop();
            _autosaveTimer.Start();
        };

        Closing += (_, _) =>
        {
            Flush();
            _owner.Refresh();
        };
    }

    private void Flush()
    {
        if (!_hasPendingEdit) return;
        _hasPendingEdit = false;
        _repository.UpdateText(_note.Id, TextBody.Text);
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

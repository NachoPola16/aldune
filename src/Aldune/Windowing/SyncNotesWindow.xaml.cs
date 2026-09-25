using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

public partial class SyncNotesWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly List<SyncNoteOption> _options;

    public SyncNotesWindow(
        NotesRepository repository,
        AppSettings settings,
        SettingsService settingsService)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        _settings = settings;
        _settingsService = settingsService;
        _options = repository.GetAllForSync()
            .OrderBy(note => note.State)
            .ThenBy(note => NoteTitleHelper.GetTitle(note.Text), StringComparer.CurrentCultureIgnoreCase)
            .Select(note => new SyncNoteOption(
                note,
                settings.SyncScope != SyncScopeKind.SelectedNotes || settings.SyncNoteIds.Contains(note.Id)))
            .ToList();

        NotesList.ItemsSource = _options;
        EmptyText.Visibility = _options.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelectAllState();

        SourceInitialized += (_, _) =>
            NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    private void OnHeaderMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        bool allSelected = _options.Count > 0 && _options.All(option => option.IsSelected);
        foreach (var option in _options) option.IsSelected = !allSelected;
        UpdateSelectAllState();
    }

    private void UpdateSelectAllState()
    {
        SelectAllCheck.IsChecked = _options.Count > 0 && _options.All(option => option.IsSelected);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _settings.SyncScope = SyncScopeKind.SelectedNotes;
        _settings.SyncNoteIds = _options
            .Where(option => option.IsSelected)
            .Select(option => option.Note.Id)
            .ToList();
        _settingsService.Save(_settings);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private sealed class SyncNoteOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public SyncNoteOption(Note note, bool isSelected)
        {
            Note = note;
            _isSelected = isSelected;
            var state = note.State switch
            {
                NoteState.Archived => $" · {Strings.Archived}",
                NoteState.Trashed => $" · {Strings.Trash}",
                _ => string.Empty
            };
            DisplayText = NoteTitleHelper.GetTitle(note.Text) + state;
        }

        public Note Note { get; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public string DisplayText { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>Esc cierra, como en el resto de diálogos de la app.</summary>
    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        OnCancelClick(this, new RoutedEventArgs());
        e.Handled = true;
    }
}

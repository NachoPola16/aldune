using System.Windows;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

public partial class SyncConflictsWindow : Window
{
    private readonly SyncService _syncService;
    private readonly AppCoordinator _coordinator;

    public SyncConflictsWindow(SyncService syncService, AppCoordinator coordinator)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        _syncService = syncService;
        _coordinator = coordinator;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };
        LoadRows();
    }

    private void LoadRows()
    {
        var rows = _syncService.GetConflicts()
            .Select(conflict => new ConflictRow(conflict, _syncService.GetActiveNote(conflict.NoteId)))
            .ToList();
        ConflictsList.ItemsSource = rows;
        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid conflictId } &&
            _syncService.RestoreConflict(conflictId))
        {
            _coordinator.RefreshNoteAppearance();
            LoadRows();
        }
    }

    private void OnMergeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid conflictId } && _syncService.MergeConflict(conflictId))
        {
            _coordinator.RefreshNoteAppearance();
            LoadRows();
        }
    }

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid conflictId }) return;

        // Igual que "Descartar todo": la versión perdedora se tira para siempre.
        var row = ((IEnumerable<ConflictRow>)ConflictsList.ItemsSource).FirstOrDefault(r => r.Conflict.Id == conflictId);
        var choice = AppDialog.Show(this, Strings.SyncConflictDismissConfirm(row?.LosingTitle ?? ""), Strings.SyncConflictTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes) return;

        if (_syncService.DismissConflict(conflictId))
        {
            // La nota sale de la cola de conflictos: su señal de sync deja de decir "conflicto".
            _coordinator.RefreshNoteAppearance();
            LoadRows();
        }
    }

    private void OnDismissAllClick(object sender, RoutedEventArgs e)
    {
        var count = _syncService.GetConflicts().Count;
        if (count == 0) return;

        // Descartar todo tira a la vez todas las versiones perdedoras, así que se pide confirmación:
        // con unos cientos de conflictos acumulados es demasiado fácil pulsarlo sin querer.
        var choice = AppDialog.Show(
            this,
            Strings.SyncConflictDismissAllConfirm(count),
            Strings.SyncConflictTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes) return;

        _syncService.DismissAllConflicts();
        _coordinator.RefreshNoteAppearance();
        LoadRows();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        // Kept as a named handler so the custom chrome remains consistent with Settings and the
        // notes manager when Windows restores/maximizes this auxiliary window.
    }

    private sealed class ConflictRow
    {
        public ConflictRow(SyncConflict conflict, Note? active)
        {
            Conflict = conflict;
            LosingTitle = conflict.Losing.Note is { } note
                ? NoteTitleHelper.GetTitle(note.Text)
                : Strings.SyncConflictDeleted;
            Details = $"{conflict.Losing.DeviceId} · {conflict.Losing.UpdatedAt.ToLocalTime():g}  →  " +
                      $"{conflict.Winner.DeviceId} · {conflict.Winner.UpdatedAt.ToLocalTime():g}";
            // La ganadora es la nota viva; si ya no está (o está en la papelera) la versión activa es un borrado.
            MergeVisibility = active is { State: not NoteState.Trashed } && conflict.Losing.Note is { } other
                              && ConflictDiff.Summarize(active.Text, other.Text) is not null
                ? Visibility.Visible : Visibility.Collapsed;
            Difference = active is null || active.State == NoteState.Trashed
                ? Strings.SyncConflictWinnerDeleted
                : conflict.Losing.Note is { } losing && ConflictDiff.Summarize(active.Text, losing.Text) is { } diff
                    ? Strings.SyncConflictDiffAt(diff.Line, ConflictDiff.Clip(diff.LosingLine, 40), ConflictDiff.Clip(diff.WinnerLine, 40), diff.DifferingLines)
                    : "";
        }

        public SyncConflict Conflict { get; }

        /// <summary>Dónde cambia la versión perdedora respecto a la activa (vacío si no se puede comparar).</summary>
        public string Difference { get; }
        /// <summary>Combinar solo tiene sentido con las dos versiones en texto y distintas.</summary>
        public Visibility MergeVisibility { get; }
        public Visibility DifferenceVisibility => Difference.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        public string LosingTitle { get; }
        public string Details { get; }
    }

    /// <summary>Esc cierra, como en el resto de diálogos de la app.</summary>
    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        OnCloseClick(this, new RoutedEventArgs());
        e.Handled = true;
    }
}

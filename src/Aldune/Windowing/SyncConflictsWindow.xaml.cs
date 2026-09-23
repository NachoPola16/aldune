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
            .Select(conflict => new ConflictRow(conflict))
            .ToList();
        ConflictsList.ItemsSource = rows;
        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid conflictId } &&
            _syncService.RestoreConflict(conflictId))
        {
            _coordinator.RefreshAll();
            LoadRows();
        }
    }

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid conflictId } &&
            _syncService.DismissConflict(conflictId))
        {
            LoadRows();
        }
    }

    private void OnDismissAllClick(object sender, RoutedEventArgs e)
    {
        var count = _syncService.GetConflicts().Count;
        if (count == 0) return;

        // Descartar todo tira a la vez todas las versiones perdedoras, así que se pide confirmación:
        // con unos cientos de conflictos acumulados es demasiado fácil pulsarlo sin querer.
        var choice = MessageBox.Show(
            this,
            Strings.SyncConflictDismissAllConfirm(count),
            Strings.SyncConflictTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes) return;

        _syncService.DismissAllConflicts();
        LoadRows();
    }

    public void DismissAll()
    {
        var count = _syncService.GetConflicts().Count;
        if (count == 0) return;

        // Descartar todo tira a la vez todas las versiones perdedoras, así que se pide confirmación:
        // con unos cientos de conflictos acumulados es demasiado fácil pulsarlo sin querer.
        var choice = MessageBox.Show(
            this,
            Strings.SyncConflictDismissAllConfirm(count),
            Strings.SyncConflictTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes) return;

        _syncService.DismissAllConflicts();
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
        public ConflictRow(SyncConflict conflict)
        {
            Conflict = conflict;
            LosingTitle = conflict.Losing.Note is { } note
                ? NoteTitleHelper.GetTitle(note.Text)
                : Strings.SyncConflictDeleted;
            Details = $"{conflict.Losing.DeviceId} · {conflict.Losing.UpdatedAt.ToLocalTime():g}  →  " +
                      $"{conflict.Winner.DeviceId} · {conflict.Winner.UpdatedAt.ToLocalTime():g}";
        }

        public SyncConflict Conflict { get; }
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

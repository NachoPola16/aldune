using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

/// <summary>
/// General bulk note management (archive/restore/trash several at once, filtered by state) —
/// split out from the dock's compact fan panel because checkboxes plus a toolbar plus a list
/// don't fit a 320px-wide panel without overlapping or clipping.
/// </summary>
public partial class NotesManagerWindow : Window
{
    private enum Filter { All, Active, Archived, Trashed }

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private List<NoteRow> _allRows = new();
    private List<NoteRow> _rows = new();
    private Filter _filter = Filter.All;

    public NotesManagerWindow(NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _repository = repository;
        _coordinator = coordinator;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };

        FilterAll.IsChecked = true;
        LoadRows();
    }

    private void LoadRows()
    {
        var active = _repository.GetByState(NoteState.Active);
        var archived = _repository.GetByState(NoteState.Archived);
        var trashed = _repository.GetByState(NoteState.Trashed);
        _allRows = active.Concat(archived).Concat(trashed).Select(n => new NoteRow(n)).ToList();

        // Los botones de acción se habilitan según haya o no selección, así que hay que enterarse
        // de cada marca. NoteRow ya es INotifyPropertyChanged para el binding del CheckBox; esto
        // solo se engancha a la misma notificación.
        foreach (var row in _allRows)
        {
            row.PropertyChanged += (_, _) => UpdateSelectionState();
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        // _rows is a filtered VIEW over _allRows (same NoteRow instances, not copies), so a
        // selection made under one filter is still there if the user switches filters and back.
        _rows = _filter switch
        {
            Filter.Active => _allRows.Where(r => r.Note.State == NoteState.Active).ToList(),
            Filter.Archived => _allRows.Where(r => r.Note.State == NoteState.Archived).ToList(),
            Filter.Trashed => _allRows.Where(r => r.Note.State == NoteState.Trashed).ToList(),
            _ => _allRows
        };
        RowsList.ItemsSource = _rows;
        UpdateSelectionState();
    }

    /// <summary>
    /// Mantiene al día el subtítulo, el estado vacío y qué acciones están disponibles.
    ///
    /// Las acciones se deshabilitan sin selección en vez de dejarlas pulsables sin efecto: un
    /// botón que no hace nada es peor que uno que dice que no puede.
    /// </summary>
    private void UpdateSelectionState()
    {
        int selected = _allRows.Count(r => r.IsSelected);
        bool any = selected > 0;

        ArchiveButton.IsEnabled = any;
        RestoreButton.IsEnabled = any;
        TrashButton.IsEnabled = any;
        SelectAllButton.IsEnabled = _rows.Count > 0;

        SubtitleText.Text = selected switch
        {
            0 => _rows.Count == 1 ? "1 nota" : $"{_rows.Count} notas",
            1 => "1 seleccionada",
            _ => $"{selected} seleccionadas"
        };

        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Text = _filter switch
        {
            Filter.Active => "No hay notas activas. Crea una con el botón + del borde de la pantalla.",
            Filter.Archived => "No has archivado ninguna nota todavía.",
            Filter.Trashed => "La papelera está vacía. Lo que envíes aquí se borra solo a los 30 días.",
            _ => "Todavía no hay notas. Crea una con el botón + del borde de la pantalla."
        };
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        _filter = sender == FilterActive ? Filter.Active
            : sender == FilterArchived ? Filter.Archived
            : sender == FilterTrashed ? Filter.Trashed
            : Filter.All;
        ApplyFilter();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        bool allSelected = _rows.Count > 0 && _rows.All(r => r.IsSelected);
        foreach (var row in _rows) row.IsSelected = !allSelected;
    }

    private void OnArchiveSelectedClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows.Where(r => r.IsSelected))
        {
            _repository.SetState(row.Note.Id, NoteState.Archived);
        }
        LoadRows();
        _coordinator.RefreshAll();
    }

    private void OnRestoreSelectedClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows.Where(r => r.IsSelected))
        {
            _repository.SetState(row.Note.Id, NoteState.Active);
        }
        LoadRows();
        _coordinator.RefreshAll();
    }

    private void OnTrashSelectedClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows.Where(r => r.IsSelected))
        {
            _repository.SetState(row.Note.Id, NoteState.Trashed);
        }
        LoadRows();
        _coordinator.RefreshAll();
    }
}

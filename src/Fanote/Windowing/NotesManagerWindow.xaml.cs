using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using Fanote.Core;
using Fanote.Interop;
using Fanote.Resources;
using Microsoft.Win32;

namespace Fanote.Windowing;

/// <summary>
/// General bulk note management (archive/restore/trash several at once, filtered by state) —
/// split out from the dock's compact fan panel because checkboxes plus a toolbar plus a list
/// don't fit a 320px-wide panel without overlapping or clipping.
/// </summary>
public partial class NotesManagerWindow : Window
{
    private enum Filter { Active, Archived, Trashed }

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private List<NoteRow> _allRows = new();
    private List<NoteRow> _rows = new();
    private Filter _filter = Filter.Active;
    private string _searchText = "";

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

        FilterActive.IsChecked = true;
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
        IEnumerable<NoteRow> byState = _filter switch
        {
            Filter.Archived => _allRows.Where(r => r.Note.State == NoteState.Archived),
            Filter.Trashed => _allRows.Where(r => r.Note.State == NoteState.Trashed),
            _ => _allRows.Where(r => r.Note.State == NoteState.Active)
        };

        // La búsqueda se queda dentro del filtro activo, no lo sustituye: mezclar estados en los
        // resultados dejaría "Eliminar" (más abajo) actuando sobre notas que no están en la
        // papelera, y ese botón existe justo para no poder saltarse la papelera.
        _rows = byState.Where(r => NoteSearch.Matches(r.Note.Text, _searchText)).ToList();
        RowsList.ItemsSource = _rows;

        // Borrar del todo solo tiene sentido sobre lo que ya esta en la papelera.
        DeleteButton.Visibility = _filter == Filter.Trashed ? Visibility.Visible : Visibility.Collapsed;
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
        // Exportar no depende de haber marcado algo: sin selección exporta el filtro entero.
        ExportButton.IsEnabled = _rows.Count > 0;

        SelectAllCheck.IsEnabled = _rows.Count > 0;
        // Indeterminada cuando hay algo pero no todo: es justo lo que una casilla de tres estados
        // existe para decir. Se fija aquí y no en el clic porque también cambia al marcar filas
        // sueltas o al cambiar de filtro.
        SelectAllCheck.IsChecked = _rows.Count > 0 && _rows.All(r => r.IsSelected) ? true
            : _rows.Any(r => r.IsSelected) ? null
            : false;

        SubtitleText.Text = selected switch
        {
            0 => _rows.Count == 1 ? Strings.OneNote : Strings.NotesCount(_rows.Count),
            1 => Strings.OneSelected,
            _ => Strings.SelectedCount(selected)
        };

        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        // Con búsqueda activa, el hueco vacío es del texto escrito, no del filtro: decir "no hay
        // notas activas" cuando en realidad sí las hay, solo que ninguna contiene lo buscado, sería
        // mentir sobre la causa.
        EmptyState.Text = _searchText.Trim().Length > 0
            ? Strings.NoSearchResults(_searchText.Trim())
            : _filter switch
            {
                Filter.Active => Strings.EmptyActive,
                Filter.Archived => Strings.EmptyArchived,
                Filter.Trashed => Strings.EmptyTrashed,
                _ => Strings.EmptyNone
            };
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        SearchPlaceholder.Visibility = _searchText.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchClearButton.Visibility = _searchText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    private void OnSearchClearClick(object sender, RoutedEventArgs e)
    {
        // Vaciar el cuadro ya dispara OnSearchTextChanged, que vuelve a aplicar el filtro.
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        _filter = sender == FilterArchived ? Filter.Archived
            : sender == FilterTrashed ? Filter.Trashed
            : Filter.Active;

        ApplyFilter();
        PlayListEntrance();
    }

    /// <summary>
    /// Qué filas seleccionadas dejan de pertenecer a la vista actual al pasar a
    /// <paramref name="newState"/>.
    ///
    /// En el filtro "Todas" no se va ninguna: solo cambia su chip de estado. Animarlas saliendo
    /// ahí seria mentir sobre lo que pasa, y ademas volverian a aparecer de inmediato.
    /// </summary>
    private IReadOnlyList<NoteRow> RowsLeavingView(NoteState newState)
    {
        var stays = _filter switch
        {
            Filter.Archived => NoteState.Archived,
            Filter.Trashed => NoteState.Trashed,
            _ => NoteState.Active
        };

        return newState == stays
            ? Array.Empty<NoteRow>()
            : _rows.Where(r => r.IsSelected).ToList();
    }

    private static readonly Duration ListFade = new(TimeSpan.FromMilliseconds(160));

    /// <summary>
    /// La lista entra apareciendo y subiendo un poco al cambiar de filtro. Sin esto, pasar de
    /// "Todas" a "Papelera" cambiaba el contenido de golpe y no quedaba claro que fuera otra vista
    /// y no un refresco.
    /// </summary>
    private void PlayListEntrance()
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var slide = new TranslateTransform();
        RowsList.RenderTransform = slide;

        RowsList.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ListFade));
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, ListFade)
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    /// <summary>
    /// Anima la salida de las filas que dejan de pertenecer a la vista actual y, al terminar,
    /// recarga. <paramref name="reload"/> corre igual si no hay nada que animar.
    ///
    /// Sirve para que archivar o enviar a la papelera se vea como que la nota <b>se va a otra
    /// sección</b>, en vez de desaparecer sin más de una lista que se rehace.
    /// </summary>
    private void AnimateOut(IReadOnlyList<NoteRow> leaving, Action reload)
    {
        var containers = leaving
            .Select(row => RowsList.ItemContainerGenerator.ContainerFromItem(row) as UIElement)
            .Where(c => c is not null)
            .Cast<UIElement>()
            .ToList();

        if (containers.Count == 0 || !SystemParameters.ClientAreaAnimation)
        {
            reload();
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        bool reloaded = false;

        foreach (var container in containers)
        {
            var slide = new TranslateTransform();
            container.RenderTransform = slide;

            // Hacia la derecha: es el lado por el que estan el dock y el resto de secciones, asi
            // que la nota "se va" hacia donde va a estar, no a un sitio cualquiera.
            slide.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(0, 40, duration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                });

            var fade = new DoubleAnimation(1, 0, duration);
            fade.Completed += (_, _) =>
            {
                // Varias filas terminan a la vez; recargar una sola vez.
                if (reloaded) return;
                reloaded = true;
                reload();
            };
            container.BeginAnimation(OpacityProperty, fade);
        }
    }

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// Mismo fundido + crecimiento desde el 95% que <c>NoteWindow.PlayOpenAnimation</c> y
    /// <c>SettingsWindow.PlayOpenAnimation</c> — las tres ventanas de la app se abren igual, en vez
    /// de que solo la nota y Ajustes se sientan "vivas" y esta aparezca de golpe. Llamada desde
    /// <see cref="AppCoordinator"/> justo después de <c>Show()</c>.
    /// </summary>
    internal void PlayOpenAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var content = (UIElement)Content;
        content.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.95, 0.95);
        content.RenderTransform = scale;

        var duration = new Duration(OpenDuration);
        IEasingFunction Ease() => new QuinticEase { EasingMode = EasingMode.EaseOut };

        content.Opacity = 0;
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = Ease() });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.95, 1, duration) { EasingFunction = Ease() });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.95, 1, duration) { EasingFunction = Ease() });
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _coordinator.OpenSettings();


    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        // Se decide por el estado de las filas, no por el de la casilla: una casilla de tres
        // estados cicla sola al pulsarla (marcada -> indeterminada -> vacía) y eso daría un tercer
        // clic que no hace nada. Aquí siempre alterna entre todo y nada.
        bool allSelected = _rows.Count > 0 && _rows.All(r => r.IsSelected);
        foreach (var row in _rows) row.IsSelected = !allSelected;
        UpdateSelectionState();
    }

    private void OnArchiveSelectedClick(object sender, RoutedEventArgs e)
    {
        var leaving = RowsLeavingView(NoteState.Archived);
        AnimateOut(leaving, () =>
        {
            foreach (var row in _rows.Where(r => r.IsSelected))
            {
                _repository.SetState(row.Note.Id, NoteState.Archived);
            }
            LoadRows();
            _coordinator.RefreshAll();
        });
    }

    private void OnRestoreSelectedClick(object sender, RoutedEventArgs e)
    {
        var leaving = RowsLeavingView(NoteState.Active);
        AnimateOut(leaving, () =>
        {
            foreach (var row in _rows.Where(r => r.IsSelected))
            {
                _repository.SetState(row.Note.Id, NoteState.Active);
            }
            LoadRows();
            _coordinator.RefreshAll();
        });
    }

    /// <summary>
    /// Borrado permanente. Unica accion irreversible de la app, asi que pide confirmacion y dice
    /// cuantas notas se lleva por delante — un "¿seguro?" sin cifra no informa de nada.
    /// </summary>
    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = _rows.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0) return;

        var message = selected.Count == 1
            ? Strings.ConfirmDeleteOne
            : Strings.ConfirmDeleteMany(selected.Count);

        var answer = MessageBox.Show(this, message, Strings.DeletePermanentlyTitle,
            MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;

        AnimateOut(selected, () =>
        {
            foreach (var row in selected) _repository.Delete(row.Note.Id);
            LoadRows();
            _coordinator.RefreshAll();
        });
    }

    private void OnTrashSelectedClick(object sender, RoutedEventArgs e)
    {
        var leaving = RowsLeavingView(NoteState.Trashed);
        AnimateOut(leaving, () =>
        {
            foreach (var row in _rows.Where(r => r.IsSelected))
            {
                _repository.SetState(row.Note.Id, NoteState.Trashed);
            }
            LoadRows();
            _coordinator.RefreshAll();
        });
    }

    /// <summary>
    /// Exporta lo marcado, o todo el filtro actual si no hay ninguna selección. Pregunta primero
    /// carpeta con .md sueltos o un único .zip — mutuamente excluyentes, para no dejar el mismo
    /// contenido dos veces en el mismo sitio (ver la decisión en docs/STATUS.md).
    /// </summary>
    private void OnExportSelectedClick(object sender, RoutedEventArgs e)
    {
        var toExport = _rows.Where(r => r.IsSelected).ToList();
        if (toExport.Count == 0) toExport = _rows;
        if (toExport.Count == 0) return;

        var choice = MessageBox.Show(this, Strings.ExportAsZipPrompt, Strings.ExportFormatTitle,
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (choice == MessageBoxResult.Cancel) return;

        if (choice == MessageBoxResult.Yes) ExportAsZip(toExport);
        else ExportAsFolder(toExport);
    }

    private void ExportAsFolder(List<NoteRow> toExport)
    {
        var dialog = new OpenFolderDialog { Title = Strings.ExportFolderDialogTitle };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in toExport)
            {
                var fileName = UniqueName(MarkdownExport.SuggestedFileName(row.Note.Text), used,
                    name => File.Exists(Path.Combine(dialog.FolderName, name)));
                File.WriteAllText(Path.Combine(dialog.FolderName, fileName), MarkdownExport.ToMarkdown(row.Note.Text));
            }
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    private void ExportAsZip(List<NoteRow> toExport)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "Fanote export.zip",
            Filter = Strings.ZipFileFilter,
            DefaultExt = ".zip"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            // Las entradas se escriben directamente en el .zip, sin pasar por ficheros .md
            // sueltos en disco — así el zip no deja nada más a su lado.
            using var archive = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in toExport)
            {
                var fileName = UniqueName(MarkdownExport.SuggestedFileName(row.Note.Text), used);
                using var writer = new StreamWriter(archive.CreateEntry(fileName).Open());
                writer.Write(MarkdownExport.ToMarkdown(row.Note.Text));
            }
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    private void ShowExportError(Exception ex) =>
        MessageBox.Show(this, Strings.UnexpectedErrorMessage(ex.Message), Strings.UnexpectedErrorTitle,
            MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>
    /// <paramref name="fileName"/> si no choca con nada en <paramref name="used"/> (otra nota de esta
    /// misma exportación) ni con <paramref name="alsoTaken"/> (un fichero que ya existiera de una
    /// exportación anterior — solo aplica al exportar como carpeta, el .zip siempre es nuevo); si no,
    /// añade " (2)", " (3)"... hasta encontrar uno libre.
    /// </summary>
    private static string UniqueName(string fileName, HashSet<string> used, Func<string, bool>? alsoTaken = null)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = fileName;
        for (int suffix = 2; used.Contains(candidate) || (alsoTaken?.Invoke(candidate) ?? false); suffix++)
        {
            candidate = $"{stem} ({suffix}){extension}";
        }

        used.Add(candidate);
        return candidate;
    }
}

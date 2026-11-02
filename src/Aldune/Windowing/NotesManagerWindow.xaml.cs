using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;
using Microsoft.Win32;

namespace Aldune.Windowing;

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
    private string? _tagFilter;
    private bool _loadingTagFilter;
    private NoteListOrder _order;
    private readonly PopupToggle _tagManagerToggle;
    private readonly PopupToggle _tagEditorToggle;
    private AutoScrollManager? _autoScroll;
    private NoteRow? _tagEditRow;
    private NoteRow? _selectionAnchor;

    public NotesManagerWindow(NotesRepository repository, AppCoordinator coordinator, string? tagFilter = null)
    {
        InitializeComponent();
        MaximizeHook.Install(this);
        // Con «Tamaño del texto» al 130 o 150 % la ventana crece con él (ver ThemeManager.ScaleWindowToText).
        ThemeManager.ScaleWindowToText(this);
        NativeMethods.CloakUntilFirstFrame(this);
        WindowCloseAnimation.Attach(this);
        KeyDown += OnWindowKeyDown;
        _repository = repository;
        _coordinator = coordinator;
        _tagFilter = tagFilter;
        _order = coordinator.NotesManagerOrder;
        LoadSortOptions();
        // The rows read "note colour only on the edge" when they are built: rebuild them if the look changes while the window is open.
        Action onLookChanged = () => Dispatcher.BeginInvoke(new Action(LoadRows));
        ThemeManager.Changed += onLookChanged;
        Closed += (_, _) => ThemeManager.Changed -= onLookChanged;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };

        FilterActive.IsChecked = true;
        _autoScroll = new AutoScrollManager(RowsHost, RowsScroll, null);
        _tagManagerToggle = new PopupToggle(TagManagerPopup);
        _tagEditorToggle = new PopupToggle(TagEditorPopup);
        Closed += (_, _) => { _autoScroll.Stop(); };
        LoadRows();
    }

    /// <summary>
    /// Aplica un filtro de etiqueta desde fuera: el botón de gestionar del dock llega aquí cuando el
    /// dock está en la vista de una etiqueta. Recargar también restablece la selección, que es lo
    /// esperable al cambiar de filtro. Si ya estaba ese filtro, no se toca nada.
    /// </summary>
    public void ApplyTagFilter(string? tag)
    {
        if (string.Equals(_tagFilter, tag, StringComparison.OrdinalIgnoreCase)) return;
        _tagFilter = tag;
        LoadRows();
    }

    private void LoadRows()
    {
        _selectionAnchor = null;
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

        LoadTagFilterOptions();
        ApplyFilter();
    }

    private void LoadTagFilterOptions()
    {
        _loadingTagFilter = true;
        try
        {
            var tags = _repository.GetAllTags().ToList();
            TagFilterBox.Items.Clear();
            TagFilterBox.Visibility = tags.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (tags.Count == 0)
            {
                _tagFilter = null;
                return;
            }

            TagFilterBox.Items.Add(new ComboBoxItem { Content = Strings.AllTags, Tag = null });
            foreach (var tag in tags)
            {
                TagFilterBox.Items.Add(new ComboBoxItem { Content = tag, Tag = tag });
            }

            int selectedIndex = 0;
            if (_tagFilter is not null)
            {
                for (int i = 1; i < TagFilterBox.Items.Count; i++)
                {
                    if (string.Equals(((ComboBoxItem)TagFilterBox.Items[i]).Tag as string,
                            _tagFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            TagFilterBox.SelectedIndex = selectedIndex;
        }
        finally
        {
            _loadingTagFilter = false;
        }
    }

    /// <summary>Recarga la lista cuando una operación externa cambia el repositorio.</summary>
    public void Refresh() => LoadRows();

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
        if (!string.IsNullOrWhiteSpace(_tagFilter))
        {
            byState = byState.Where(r => r.Note.Tags.Any(tag =>
                string.Equals(tag, _tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        _rows = NoteListing.Sort(byState.Where(r => NoteSearch.Matches(ProtectedTitleRules.Visible(r.Note) ?? r.Note.Text, _searchText)), r => r.Note, _order)
            .ToList();
        RowsList.ItemsSource = _rows;

        // Borrar del todo solo tiene sentido sobre lo que ya esta en la papelera.
        DeleteButton.Visibility = _filter == Filter.Trashed ? Visibility.Visible : Visibility.Collapsed;
        // En la papelera "Mover a la papelera" no hace nada y se confunde con eliminar.
        TrashButton.Visibility = _filter == Filter.Trashed ? Visibility.Collapsed : Visibility.Visible;
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
                Filter.Active when !string.IsNullOrWhiteSpace(_tagFilter) => Strings.NoTagResults(_tagFilter!),
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
        // Con muchas notas, filtrar tras cada tecla congela el cuadro mientras se escribe: se espera a que se deje de teclear.
        // Al vaciar el cuadro se aplica al instante.
        _searchDebounce.Stop();
        if (_searchText.Length == 0) ApplyFilter();
        else _searchDebounce.Start();
    }

    private DispatcherTimer? _searchDebounceTimer;

    private DispatcherTimer _searchDebounce => _searchDebounceTimer ??= CreateSearchDebounce();

    private DispatcherTimer CreateSearchDebounce()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ApplyFilter();
        };
        return timer;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control) return;

        SearchBox.Focus();
        SearchBox.SelectAll();
        e.Handled = true;
    }

    /// <summary>
    /// Esc deshace lo último abierto antes de cerrar la ventana: primero un panel de etiquetas, luego
    /// la búsqueda y, sin nada de eso, cierra el gestor, como el resto de ventanas de la app. Va en
    /// KeyDown y no en Preview: si un control ya usó el Esc (un desplegable abierto), no llega aquí.
    /// </summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        e.Handled = true;

        if (TagEditorPopup.IsOpen) TagEditorPopup.IsOpen = false;
        else if (TagManagerPopup.IsOpen) TagManagerPopup.IsOpen = false;
        else if (SearchBox.Text.Length > 0) SearchBox.Clear();
        else Close();
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

    private void LoadSortOptions()
    {
        foreach (var (order, label) in new[]
                 {
                     (NoteListOrder.Dock, Strings.SortDock),
                     (NoteListOrder.Newest, Strings.SortNewest),
                     (NoteListOrder.Oldest, Strings.SortOldest),
                     (NoteListOrder.Title, Strings.SortTitle)
                 })
        {
            var item = new ComboBoxItem { Content = label, Tag = order };
            SortBox.Items.Add(item);
            if (order == _order) SortBox.SelectedItem = item;
        }
        SortBox.SelectedItem ??= SortBox.Items[0];
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        // Rellenar el desplegable en el constructor ya dispara esto, antes de que haya filas.
        if (SortBox.SelectedItem is not ComboBoxItem { Tag: NoteListOrder order } || order == _order) return;

        _order = order;
        _coordinator.NotesManagerOrder = order;
        ApplyFilter();
        PlayListEntrance();
    }

    private void OnTagFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingTagFilter || TagFilterBox.SelectedItem is not ComboBoxItem item) return;

        _tagFilter = item.Tag as string;
        ApplyFilter();
        PlayListEntrance();
    }

    private void OnRestorePlacementsClick(object sender, RoutedEventArgs e)
    {
        var answer = AppDialog.Show(this, Strings.RestoreAllPlacementsConfirm, Title,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) _coordinator.RestoreAllNotePlacements();
    }

    private void OnManageTagsClick(object sender, RoutedEventArgs e)
    {
        if (_tagManagerToggle.ShouldConsumeOpen())
        {
            e.Handled = true;
            return;
        }

        PopulateTagManager();
        TagManagerPopup.PlacementTarget = ManageTagsButton;
        _tagManagerToggle.SetTrigger(ManageTagsButton);
        TagManagerPopup.IsOpen = true;
        TagManagerPopup.Focus();
        e.Handled = true;
    }

    private void PopulateTagManager()
    {
        TagManagerItems.Children.Clear();
        var tags = _repository.GetAllTags();
        TagManagerEmptyText.Visibility = tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            var row = new Border
            {
                CornerRadius = ThemeManager.Radius(6),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 0, 0, 6)
            };
            // Por referencia a la paleta: estas filas se crean en código y antes llevaban el fondo
            // oscuro y el texto blanco fijos, iguales en cualquier aspecto.
            row.SetResourceReference(Border.BackgroundProperty, "AlduneRaisedHoverBrush");
            var content = new DockPanel();
            var label = new TextBlock
            {
                Text = tag,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextBrush");
            var delete = new Button
            {
                Content = "\uE738",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Width = 28,
                Height = 28,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                Tag = tag,
                ToolTip = Strings.DeleteTagTooltip,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("DangerToolbarButtonStyle")
            };
            System.Windows.Automation.AutomationProperties.SetName(delete, Strings.DeleteTagTooltip);
            delete.Click += OnDeleteTagClick;
            // The trash can is the bigger action: the tag goes away together with its notes (to the trash, after a confirmation).
            var deleteWithNotes = new Button
            {
                Content = "\uE74D",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Width = 28,
                Height = 28,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                Tag = tag,
                ToolTip = Strings.DeleteTagWithNotesTooltip,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("DangerToolbarButtonStyle")
            };
            System.Windows.Automation.AutomationProperties.SetName(deleteWithNotes, Strings.DeleteTagWithNotesTooltip);
            deleteWithNotes.Click += OnDeleteTagWithNotesClick;
            DockPanel.SetDock(deleteWithNotes, Dock.Right);
            content.Children.Add(deleteWithNotes);
            DockPanel.SetDock(delete, Dock.Right);
            content.Children.Add(delete);
            // Own order (per device, not synced): up and down arrows, as on Android. The ends are disabled instead of hidden so the rows align.
            var down = MakeMoveButton("", Strings.MoveTagDownTooltip(tag), tag, +1, enabled: i < tags.Count - 1);
            var up = MakeMoveButton("", Strings.MoveTagUpTooltip(tag), tag, -1, enabled: i > 0);
            DockPanel.SetDock(down, Dock.Right);
            DockPanel.SetDock(up, Dock.Right);
            content.Children.Add(down);
            content.Children.Add(up);
            // The colour of the tag: a swatch that opens the theme's palette under the row. Picking one repaints the notes that carry the tag.
            var palette = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 2) };
            var swatchButton = MakeTagColorButton(tag, palette);
            DockPanel.SetDock(swatchButton, Dock.Right);
            content.Children.Add(swatchButton);
            var edit = MakeRenameButton(tag, content, label);
            DockPanel.SetDock(edit, Dock.Right);
            content.Children.Add(edit);
            content.Children.Add(label);
            var stack = new StackPanel();
            stack.Children.Add(content);
            stack.Children.Add(palette);
            row.Child = stack;
            TagManagerItems.Children.Add(row);
        }
    }

    /// <summary>A pencil that turns the name into a text box: Enter renames the tag, Esc (or leaving the box) leaves it as it was.</summary>
    private Button MakeRenameButton(string tag, DockPanel content, TextBlock label)
    {
        var button = new Button
        {
            Content = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Width = 28, Height = 28,
            Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0), ToolTip = Strings.TagRenameTooltip,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource("ToolbarButtonStyle")
        };
        System.Windows.Automation.AutomationProperties.SetName(button, Strings.TagRenameTooltip);
        button.Click += (_, _) =>
        {
            var box = new TextBox { Text = tag, MaxLength = 40, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(4, 2, 4, 2) };
            bool done = false;
            void Finish(bool commit)
            {
                if (done) return;
                done = true;
                if (commit && _coordinator.RenameTag(tag, box.Text)) LoadTagFilterOptions();
                PopulateTagManager();
            }
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
                else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
            };
            box.LostKeyboardFocus += (_, _) => Finish(false);
            content.Children.Remove(label);
            content.Children.Add(box);
            box.Focus();
            box.SelectAll();
        };
        return button;
    }

    private Button MakeTagColorButton(string tag, StackPanel palette)
    {
        var current = _coordinator.TagColor(tag);
        // No colour yet: a palette glyph (what the button is for); with one, the colour itself as a dot.
        FrameworkElement dot;
        if (current is null)
        {
            var glyph = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextBrush");
            dot = glyph;
        }
        else
        {
            var filled = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(current)!
            };
            filled.SetResourceReference(Border.BorderBrushProperty, "AlduneTextFaintBrush");
            dot = filled;
        }
        var button = new Button
        {
            Content = dot, Width = 28, Height = 28, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0), ToolTip = Strings.TagColorTooltip,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource("ToolbarButtonStyle")
        };
        System.Windows.Automation.AutomationProperties.SetName(button, Strings.TagColorTooltip);
        button.Click += (_, _) =>
        {
            if (palette.Visibility == Visibility.Visible) { palette.Visibility = Visibility.Collapsed; return; }
            palette.Children.Clear();
            var swatches = new StackPanel();
            NoteSwatchPanel.Fill(swatches, _coordinator.ActiveNoteTheme, _coordinator.TagColor(tag), (sender, _) =>
            {
                _coordinator.SetTagColor(tag, (string)((FrameworkElement)sender).Tag);
                PopulateTagManager();
                LoadRows();
            });
            var none = new Button { Content = Strings.TagColorNone, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4, 10, 4), Style = (Style)FindResource("ToolbarButtonStyle") };
            none.Click += (_, _) => { _coordinator.SetTagColor(tag, null); PopulateTagManager(); };
            palette.Children.Add(swatches);
            palette.Children.Add(none);
            // The tag colour lands in each note's own colour, which not every look shows: say so here, as the note's own colour menu does.
            var kind = SkinColorNotice.For(ThemeManager.Skin.Card, "#A8E0B4", NoteColorDisplay.Uniform);
            string? notice = NoteDisplayColor.IsActive(NoteColorDisplay.Uniform) ? Strings.UniformColorNotice
                : kind is SkinColorNoticeKind.ColorsNotShown or SkinColorNoticeKind.ChannelColors ? Strings.SkinColorNotice(kind) : null;
            if (notice is not null)
            {
                var text = new TextBlock { Text = notice, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 8, 0, 0), MaxWidth = 250, Cursor = Cursors.Hand, ToolTip = Strings.NoticeOpenSetting };
                text.SetResourceReference(TextBlock.ForegroundProperty, "AlduneMutedTextBrush");
                var uniform = NoteDisplayColor.IsActive(NoteColorDisplay.Uniform);
                text.MouseLeftButtonUp += (_, _) => _coordinator.OpenColorNoticeSetting(uniform);
                palette.Children.Add(text);
            }
            palette.Visibility = Visibility.Visible;
        };
        return button;
    }

    private Button MakeMoveButton(string glyph, string tooltip, string tag, int delta, bool enabled)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 11,
            Width = 28,
            Height = 28,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Tag = (tag, delta),
            ToolTip = tooltip,
            IsEnabled = enabled,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource("ToolbarButtonStyle")
        };
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
        button.Click += OnMoveTagClick;
        return button;
    }

    private void OnMoveTagClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ValueTuple<string, int> (var tag, var delta) }) return;

        _coordinator.MoveTag(tag, delta);
        PopulateTagManager();
        LoadTagFilterOptions();
    }

    private void OnCreateTagClick(object sender, RoutedEventArgs e)
    {
        if (!_repository.CreateTag(NewTagTextBox.Text)) return;

        NewTagTextBox.Clear();
        PopulateTagManager();
        LoadTagFilterOptions();
        _coordinator.RefreshAll();
    }

    private void OnDeleteTagClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;

        _repository.DeleteTag(tag);
        PopulateTagManager();
        LoadRows();
        _coordinator.RefreshAll();
    }

    private void OnDeleteTagWithNotesClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;

        int count = _repository.GetByTag(tag, NoteState.Active).Count + _repository.GetByTag(tag, NoteState.Archived).Count;
        if (count > 0 && AppDialog.Show(this, Strings.DeleteTagWithNotesConfirm(tag, count), Strings.DeleteTagWithNotesTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        _repository.DeleteTagAndNotes(tag);
        PopulateTagManager();
        LoadTagFilterOptions();
        LoadRows();
        _coordinator.RefreshAll();
    }

    private void OnPinToTopClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: NoteRow row }) return;
        _repository.SetPinnedToTop(row.Note.Id, !row.Note.IsPinnedToTop);
        LoadRows();
        _coordinator.RefreshAll();
        e.Handled = true;
    }

    private void OnEditTagsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: NoteRow row } button) return;

        // Mismo interruptor que el resto de popups: si el descarte del popup cerró por este botón,
        // este clic es el que cierra, no hay que volver a rellenar ni a abrir.
        if (_tagEditorToggle.ShouldConsumeOpen())
        {
            e.Handled = true;
            return;
        }

        _tagEditRow = row;
        ManagerTagPanel.Bind(_repository, row.Note);

        TagEditorPopup.PlacementTarget = button;
        _tagEditorToggle.SetTrigger(button);
        TagEditorPopup.IsOpen = true;
        TagEditorPopup.Focus();
        e.Handled = true;
    }

    /// <summary>El panel guarda al marcar; la lista y el dock se refrescan una sola vez, al cerrarlo.</summary>
    private void OnTagEditorClosed(object? sender, EventArgs e)
    {
        if (ManagerTagPanel.Changed)
        {
            LoadRows();
            PopulateTagManager();
            _coordinator.RefreshAll();
        }
        _tagEditRow = null;
    }

    /// <summary>
    /// Qué filas seleccionadas dejan de pertenecer a la vista actual al pasar a
    /// <paramref name="newState"/>.
    ///
    /// En el filtro "Todas" no se va ninguna: solo cambia su chip de estado. Animarlas saliendo
    /// ahí seria mentir sobre lo que pasa, y ademas volverian a aparecer de inmediato.
    /// </summary>
    /// <summary>Rows a right-click menu acts on when the clicked row is not part of the selection (the selection itself is left alone); null otherwise.</summary>
    private List<NoteRow>? _menuTargets;

    /// <summary>The rows an action works on: the menu's target if there is one, else what is selected.</summary>
    private List<NoteRow> ActionRows() => _menuTargets ?? _rows.Where(r => r.IsSelected).ToList();

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
            : ActionRows();
    }

    private static readonly Duration ListFade = new(TimeSpan.FromMilliseconds(140));

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

        var duration = new Duration(TimeSpan.FromMilliseconds(150));
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

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(150);

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

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        ToggleMaximized();

    private void ToggleMaximized()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            RestoreNormalHeightLimit();
            return;
        }

        MaxHeight = double.PositiveInfinity; MaxWidth = double.PositiveInfinity;
        WindowState = WindowState.Maximized;
    }

    private void RestoreNormalHeightLimit()
    {
        // The initial size leaves room (90 %) but the cap is the whole work area: with 90 % as the cap too, a snap that does not go through
        // «maximize» (Win+arrow, PowerToys FancyZones) left a strip uncovered when the window was fitted to half of the screen.
        // Plus the invisible resize borders that Windows puts around a snapped window (about 7 px a side): a cap of exactly the work area cut
        // them and left a strip uncovered on the right and at the bottom.
        (MaxWidth, MaxHeight) = MaximizeHook.SnapCap();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaxHeight = double.PositiveInfinity; MaxWidth = double.PositiveInfinity;
            }
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized
                ? Strings.RestoreWindowTooltip
                : Strings.MaximizeWindowTooltip;
            MaximizeGlyph.IsRestore = WindowState == WindowState.Maximized;
        }

        if (WindowState == WindowState.Normal) RestoreNormalHeightLimit();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        // El engranaje conserva el foco mientras Ajustes está abierta; al cerrarla (casi siempre con Esc) WPF se lo devuelve y dibuja su anillo
        // de foco, que se lee como «seleccionado». Con el foco en la propia ventana no queda nada resaltado al volver.
        Focus();
        // Hay que fijar también el foco lógico: al reactivarse la ventana WPF restaura el del ámbito de foco (el engranaje), no el de teclado.
        FocusManager.SetFocusedElement(this, this);
        _coordinator.OpenSettings();
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        // Una nota nueva es activa: desde Archivadas o Papelera no se vería aparecer en la lista.
        if (_filter != Filter.Active) FilterActive.IsChecked = true;
        _coordinator.CreateAndOpenNote(_tagFilter);
    }


    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        // Se decide por el estado de las filas, no por el de la casilla: una casilla de tres
        // estados cicla sola al pulsarla (marcada -> indeterminada -> vacía) y eso daría un tercer
        // clic que no hace nada. Aquí siempre alterna entre todo y nada.
        bool allSelected = _rows.Count > 0 && _rows.All(r => r.IsSelected);
        foreach (var row in _rows) row.IsSelected = !allSelected;
        _selectionAnchor = null;
        UpdateSelectionState();
    }

    /// <summary>
    /// What a click on a row does. The checkbox selects; the rest of the row opens the note, except that Ctrl or Shift select instead (Shift picks the
    /// range from the last marked row, Ctrl+Shift adds it to the selection) and that, once anything is selected, a click toggles the row, as taps do
    /// on Android. So a click never does two things at once, and the note can always be opened from here (the only way in when the dock is hidden).
    /// </summary>
    private void OnRowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteRow row }) return;

        // If the click started on the checkbox, its binding already toggles it: here the row only becomes the anchor for the next Shift+click.
        if (FindVisualAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            _selectionAnchor = row;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        bool selecting = modifiers.HasFlag(ModifierKeys.Shift) || modifiers.HasFlag(ModifierKeys.Control) || _rows.Any(r => r.IsSelected);
        if (selecting) SelectFromRow(row, modifiers);
        else _coordinator.OpenNoteById(row.Note.Id);
        e.Handled = true;
    }

    /// <summary>
    /// Right-click on a row: the same actions as the buttons at the bottom, on that row. As in Explorer, a row that is not part of the selection
    /// becomes the selection first, so the menu never acts on rows the person is not pointing at; on a selected row it acts on the whole selection.
    /// </summary>
    private void OnRowMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteRow row } rowElement) return;

        // The menu acts on the whole selection if this row is part of it, and only on this row otherwise; right-click never changes the selection.
        var targets = row.IsSelected ? _rows.Where(r => r.IsSelected).ToList() : [row];

        var menu = new ContextMenu { PlacementTarget = rowElement, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        void Add(string header, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, args) =>
            {
                _menuTargets = row.IsSelected ? null : targets;
                try { handler(s, args); }
                finally { _menuTargets = null; }
            };
            menu.Items.Add(item);
        }

        Add(Strings.OpenNote, (_, _) => _coordinator.OpenNoteById(row.Note.Id));
        Add(Strings.Export, OnExportSelectedClick);
        if (_filter == Filter.Active) Add(Strings.Archive, OnArchiveSelectedClick);
        if (_filter != Filter.Active) Add(Strings.Restore, OnRestoreSelectedClick);
        if (_filter == Filter.Trashed) Add(Strings.Delete, OnDeleteSelectedClick);
        else Add(Strings.Trash, OnTrashSelectedClick);
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// Lo que el ratón hace con una fila, con teclado: Intro abre la nota, Espacio la marca (con Mayús, el intervalo) y las flechas
    /// pasan a la fila de arriba o de abajo. Sin esto una fila recibía el foco con Tab pero no hacía nada, y el gestor, la ruta
    /// por teclado a cualquier nota, no servía sin ratón.
    /// </summary>
    private void OnRowKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteRow row } rowElement) return;
        // Las teclas de la casilla de dentro (Espacio) son suyas.
        if (FindVisualAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null && e.Key == Key.Space) return;

        switch (e.Key)
        {
            case Key.Enter:
                _coordinator.OpenNoteById(row.Note.Id);
                break;
            case Key.Space:
                SelectFromRow(row, Keyboard.Modifiers);
                break;
            case Key.Down or Key.Up:
                rowElement.MoveFocus(new TraversalRequest(e.Key == Key.Down ? FocusNavigationDirection.Down : FocusNavigationDirection.Up));
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnRowCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox { DataContext: NoteRow row }) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            SelectRange(row, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            e.Handled = true;
            return;
        }

        _selectionAnchor = row;
    }

    private void SelectFromRow(NoteRow row, ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            SelectRange(row, modifiers.HasFlag(ModifierKeys.Control));
            return;
        }

        row.IsSelected = !row.IsSelected;
        _selectionAnchor = row;
        UpdateSelectionState();
    }

    private void SelectRange(NoteRow row, bool append)
    {
        int targetIndex = _rows.IndexOf(row);
        int anchorIndex = _selectionAnchor is null ? -1 : _rows.IndexOf(_selectionAnchor);
        if (targetIndex < 0) return;

        if (anchorIndex < 0)
        {
            row.IsSelected = true;
            _selectionAnchor = row;
            UpdateSelectionState();
            return;
        }

        if (!append)
        {
            foreach (var visibleRow in _rows) visibleRow.IsSelected = false;
        }

        int first = Math.Min(anchorIndex, targetIndex);
        int last = Math.Max(anchorIndex, targetIndex);
        for (int i = first; i <= last; i++) _rows[i].IsSelected = true;

        _selectionAnchor = row;
        UpdateSelectionState();
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OnArchiveSelectedClick(object sender, RoutedEventArgs e)
    {
        var leaving = RowsLeavingView(NoteState.Archived);
        var targets = ActionRows();
        var selectedIds = targets.Select(r => r.Note.Id).ToList();
        AnimateOut(leaving, () =>
        {
            foreach (var row in targets)
            {
                _repository.SetState(row.Note.Id, NoteState.Archived);
            }
            _coordinator.NotifyLinkedFilesKept(selectedIds, AppCoordinator.LinkedLeave.Archived);
            _coordinator.PlaySound(SoundEvent.NoteArchived);   // una vez, aunque sean varias notas
            LoadRows();
            _coordinator.RefreshAll();
        });
    }

    private void OnRestoreSelectedClick(object sender, RoutedEventArgs e)
    {
        var leaving = RowsLeavingView(NoteState.Active);
        var targets = ActionRows();
        AnimateOut(leaving, () =>
        {
            foreach (var row in targets)
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
        var selected = ActionRows();
        if (selected.Count == 0) return;

        var message = selected.Count == 1
            ? Strings.ConfirmDeleteOne
            : Strings.ConfirmDeleteMany(selected.Count);

        var answer = AppDialog.Show(this, message, Strings.DeletePermanentlyTitle,
            MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;

        // Before deleting: the link row goes with the note, and the message names the file that stays.
        _coordinator.NotifyLinkedFilesKept(selected.Select(r => r.Note.Id).ToList(), AppCoordinator.LinkedLeave.Deleted);
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
        var targets = ActionRows();
        var selectedIds = targets.Select(r => r.Note.Id).ToList();
        AnimateOut(leaving, () =>
        {
            foreach (var row in targets)
            {
                _repository.SetState(row.Note.Id, NoteState.Trashed);
            }
            _coordinator.NotifyLinkedFilesKept(selectedIds, AppCoordinator.LinkedLeave.Trashed);
            _coordinator.PlaySound(SoundEvent.NoteDeleted);   // una vez, aunque sean varias notas
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
        var toExport = ActionRows();
        if (toExport.Count == 0 && _menuTargets is null) toExport = _rows;
        if (toExport.Count == 0) return;

        var choice = AppDialog.Show(this, Strings.ExportAsZipPrompt, Strings.ExportFormatTitle,
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
            FileName = $"{BrandIdentity.AppName} export.zip",
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
        AppDialog.Show(this, Strings.UnexpectedErrorMessage(ex.Message), Strings.UnexpectedErrorTitle,
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

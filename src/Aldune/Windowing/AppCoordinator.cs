using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// One instance for the whole app (not one per dock/monitor). Owns what used to live inside
/// EdgeDockWindow: which notes already have an open NoteWindow (so clicking the same note's tab
/// from two different docks never opens it twice), the single shared NotesManagerWindow, and
/// telling every dock to refresh together (with several docks all mirroring the same note list —
/// see Phase 3a spec — archiving a note from any one of them has to update all of them).
/// </summary>
public sealed class AppCoordinator
{
    private const int MaxTemplateNotes = 12;
    private const double TemplateMargin = 24;
    private const double TemplateGap = 18;

    private readonly NotesRepository _repository;
    private readonly AppSettings? _settings;
    private readonly SettingsService? _settingsService;
    private readonly SyncService? _syncService;
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private readonly List<EdgeDockWindow> _docks = new();
    private Dictionary<Guid, bool>? _noteTopmostBeforeDockMenu;
    private NotesManagerWindow? _notesManagerWindow;
    private SettingsWindow? _settingsWindow;
    private bool _openingSettings;
    private SyncConflictsWindow? _syncConflictsWindow;
    private System.Threading.Timer? _autoSyncTimer;

    /// <summary>Fabrica de la ventana de ajustes, inyectada por App: el coordinador no tiene por
    /// que saber de SettingsService ni del atajo global, solo de que hay una ventana unica.</summary>
    public Func<SettingsWindow>? SettingsWindowFactory { get; set; }

    /// <summary>
    /// Accion para reconstruir los docks en caliente (inyectada por App), usada al cambiar
    /// la pantalla elegida en Ajustes sin necesidad de reiniciar la app.
    /// </summary>
    public Action? RebuildDocksAction { get; set; }

    public void RebuildDocks() => RebuildDocksAction?.Invoke();

    public AppCoordinator(
        NotesRepository repository,
        AppSettings? settings = null,
        SyncService? syncService = null,
        SettingsService? settingsService = null)
    {
        _repository = repository;
        _settings = settings;
        _syncService = syncService;
        _settingsService = settingsService;
    }

    public int OpenNoteWindowCount => _openNoteWindows.Count;

    public void OpenSyncConflicts()
    {
        if (_syncService is null) return;
        if (_syncConflictsWindow is { IsVisible: true })
        {
            _syncConflictsWindow.Activate();
            return;
        }

        _syncConflictsWindow = new SyncConflictsWindow(_syncService, this);
        _syncConflictsWindow.Closed += (_, _) => _syncConflictsWindow = null;
        _syncConflictsWindow.Show();
    }

    /// <summary>
    /// Si esta nota ya tiene ventana abierta. El dock lo consulta para ocultar su pestaña: al
    /// abrirse, la nota se lleva esa pestaña consigo como lomo, así que dejarla también en el mazo
    /// mostraría la misma etiqueta dos veces.
    /// </summary>
    public bool IsNoteOpen(Guid noteId) => _openNoteWindows.ContainsKey(noteId);

    public void RegisterDock(EdgeDockWindow dock) => _docks.Add(dock);

    /// <summary>
    /// Fuerza el guardado inmediato (texto + posición) de todas las notas abiertas, sin pasar por su
    /// animación de cierre — usado por <c>App.OnSessionEnding</c> cuando Windows avisa de que la
    /// sesión va a terminar (apagar, reiniciar, cerrar sesión), porque no da tiempo a esperar a que
    /// cada nota complete su cierre normal.
    /// </summary>
    public void FlushAllOpenNotes()
    {
        foreach (var window in _openNoteWindows.Values)
        {
            window.FlushForShutdown();
        }
    }

    /// <summary>
    /// Cierra todos los docks actuales. Lo usa <c>App</c> al cambiar la configuración de pantallas
    /// para reconstruirlos contra los monitores que haya ahora.
    ///
    /// Las ventanas de nota abiertas <b>no</b> se tocan: no guardan referencia a ningún dock (solo
    /// al coordinador), así que sobreviven al recambio. Lo único que se pierde es que una nota
    /// abierta ya no vuelve a "su" dock, cosa que tampoco tenía sentido si su monitor ha
    /// desaparecido.
    /// </summary>
    public void CloseAllDocks()
    {
        foreach (var dock in _docks.ToList())
        {
            dock.PrepareForClose();
            dock.Close();
        }
        _docks.Clear();
    }

    private void RefreshOpenState()
    {
        foreach (var dock in _docks) dock.RefreshOpenState();
    }

    /// <summary>
    /// El dock que vive en el monitor donde está el cursor ahora mismo, o el primero registrado si
    /// no se encuentra ninguno (arranque sin movimiento de ratón, o el cursor ya no está en ningún
    /// monitor conocido). Lo usan los tres caminos que no tienen "su" monitor propio porque entran
    /// desde la bandeja, no desde un dock concreto: Ajustes, "Gestionar notas" y la nota nueva por
    /// atajo global — antes los tres usaban siempre <c>_docks[0]</c>, así que en un sistema
    /// multimonitor podían abrirse en la pantalla equivocada.
    /// </summary>
    private EdgeDockWindow? DockNearCursor()
    {
        var cursorMonitor = NativeMethods.MonitorFromCursor();
        return _docks.FirstOrDefault(d => d.IsOnMonitor(cursorMonitor)) ?? _docks.FirstOrDefault();
    }

    /// <summary>
    /// Opens <paramref name="note"/> in a new window, or activates its already-open one.
    /// <paramref name="originRect"/> (the clicked tab's on-screen rect) is where the note slides
    /// out from, and also what its vertical position is aligned to. Ignored when the note is
    /// already open — that path just activates the existing window, wherever the user put it.
    /// </summary>
    public void OpenOrActivateNote(Note note, EdgeDockWindow requestingDock, System.Windows.Rect? originRect = null)
    {
        if (_openNoteWindows.TryGetValue(note.Id, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;

            existing.Activate();
            NativeMethods.ForceActivate(existing);
            return;
        }

        string? protectionPassword = null;
        if (note.IsProtected)
        {
            var password = PasswordPromptWindow.Show(requestingDock, Strings.UnlockNote,
                Strings.ProtectedNoteHint, confirm: false);
            if (password is null) return;
            if (!_repository.TryUnlock(note.Id, password, out var unlocked) || unlocked is null)
            {
                MessageBox.Show(requestingDock, Strings.WrongPassword, Strings.AppName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            note = unlocked;
            protectionPassword = password;
        }

        var noteWindow = new NoteWindow(note, _repository, this, _settings, protectionPassword);

        // Si la nota tiene una posición guardada PARA ESTA PANTALLA (la del dock que la pidió) y
        // sigue a la vista, reaparece ahí directamente — sensación de post-it real, no de "otra
        // ventana que se abre desde el dock". Por pantalla y no una posición única: abrirla desde
        // el dock del monitor vertical no debe traerla desde donde se dejó en el horizontal (o
        // viceversa) — cada pantalla tiene su propio recuerdo. Sin posición guardada para esta
        // pantalla, sigue el camino de siempre: cascada junto a la pestaña que se pulsó.
        bool restoredPlacement = TryRestorePlacement(noteWindow, note.Id, requestingDock.MonitorKey);
        if (!restoredPlacement)
        {
            requestingDock.PositionNoteWindow(noteWindow, originRect);
        }

        _openNoteWindows[note.Id] = noteWindow;
        noteWindow.Closed += (_, _) =>
        {
            _openNoteWindows.Remove(note.Id);
            // Devuelve la pestaña a su hueco en el mazo. Va aquí y no en el Closing de NoteWindow
            // porque Closing se dispara *antes* de que esta entrada se quite del diccionario, así
            // que un refresco desde allí seguiría viendo la nota como abierta.
            RefreshOpenState();
        };

        // El orden importa: la pestaña tiene que desaparecer del mazo antes de que la ventana se
        // muestre, o durante la aparición se vería la etiqueta duplicada (en el lomo y en el mazo).
        RefreshOpenState();
        noteWindow.Show();
        noteWindow.PlayOpenAnimation();

        NativeMethods.ForceActivate(noteWindow);
    }

    /// <summary>
    /// Abre (o activa) una nota a partir de solo su Id, sin que lo pida un dock concreto — el mismo
    /// patrón que <see cref="CreateAndOpenNote"/>/<see cref="OpenNotesManager"/>, usado por
    /// <see cref="ReminderScheduler"/> cuando un recordatorio se dispara: solo conoce el NoteId, no
    /// una referencia al dock que originó la petición.
    /// </summary>
    public void OpenNoteById(Guid noteId)
    {
        var dock = DockNearCursor();
        if (dock is null) return;

        var note = _repository.GetById(noteId);
        if (note is null) return; // la nota se borró entre que sonó el recordatorio y el clic

        OpenOrActivateNote(note, dock);
    }

    /// <summary>
    /// Si el ajuste está activado y hay una posición guardada para <paramref name="noteId"/> en
    /// <paramref name="monitorKey"/> que siga siendo visible ahora mismo, la aplica a
    /// <paramref name="noteWindow"/> y devuelve <c>true</c>. La validez de "sigue siendo visible"
    /// se comprueba contra todos los monitores actuales (no solo <paramref name="monitorKey"/>) por
    /// si ese monitor cambió de resolución; la búsqueda en sí sí es específica de esa pantalla — ver
    /// el comentario en <see cref="OpenOrActivateNote"/>.
    /// </summary>
    private bool TryRestorePlacement(NoteWindow noteWindow, Guid noteId, string monitorKey)
    {
        if (_settings is not { RememberNotePositions: true }) return false;

        var placement = _repository.GetPlacement(noteId, monitorKey);
        if (placement is null) return false;

        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (!PlacementValidation.IsVisibleOnMonitors(
                placement.Left, placement.Top, placement.Width, placement.Height, monitors))
        {
            return false;
        }

        noteWindow.Left = placement.Left;
        noteWindow.Top = placement.Top;
        noteWindow.Width = placement.Width;
        noteWindow.Height = placement.Height;
        return true;
    }

    /// <summary>
    /// Abre el gestor de notas, o activa el que ya haya.
    ///
    /// <paramref name="requestingDock"/> decide en qué pantalla sale. Sin él, la ventana no fijaba
    /// posición y Windows la ponía en (0,0) — o sea, siempre en el monitor principal, aunque
    /// hubieras pulsado el engranaje en el otro.
    /// </summary>
    public void OpenOrActivateNotesManager(EdgeDockWindow requestingDock)
    {
        if (_notesManagerWindow is not null)
        {
            if (_notesManagerWindow.WindowState == WindowState.Minimized)
                _notesManagerWindow.WindowState = WindowState.Normal;

            _notesManagerWindow.Activate();
            NativeMethods.ForceActivate(_notesManagerWindow);
            return;
        }

        _notesManagerWindow = new NotesManagerWindow(_repository, this);
        requestingDock.CenterOnThisMonitor(_notesManagerWindow);
        _notesManagerWindow.Closed += (_, _) => _notesManagerWindow = null;
        _notesManagerWindow.Show();
        _notesManagerWindow.PlayOpenAnimation();
        NativeMethods.ForceActivate(_notesManagerWindow);
    }

    /// <summary>
    /// Abre el gestor sin que lo pida un dock — desde el menu de la bandeja. Se coloca sobre el
    /// primer dock disponible, que es lo mas parecido a "donde vive la app" cuando la peticion no
    /// viene de una pantalla concreta.
    /// </summary>
    public void OpenNotesManager()
    {
        var dock = DockNearCursor();
        if (dock is null) return;
        OpenOrActivateNotesManager(dock);
    }

    /// <summary>
    /// Crea una nota y la abre. Es el camino del atajo global y del menu de la bandeja: los dos
    /// sitios donde se pide una nota sin haber pulsado el "+" de ningun dock concreto.
    /// </summary>
    public void CreateAndOpenNote()
    {
        var dock = DockNearCursor();
        if (dock is null) return;

        var existing = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorPalette.Colors[existing % NoteColorPalette.Colors.Length];
        var note = _repository.Create(string.Empty, color, screenOrigin: "primary");

        RefreshAll();

        // Se abre para escribir directamente: crear una nota y tener que buscarla luego en el
        // abanico no ahorra nada frente a no tener atajo.
        OpenOrActivateNote(note, dock);
    }

    /// <summary>
    /// Abre todas las notas activas de golpe, para verlas a todas a la vez como una mesa de
    /// post-its — pedido explícito del usuario. Las que ya están abiertas no se tocan: activarlas
    /// una a una de paso no ganaría nada, solo dejaría el foco en la última. Las que no, se
    /// cascadean solas: <see cref="EdgeDockWindow.PositionNoteWindow"/> ya calcula su paso de
    /// cascada a partir de cuántas ventanas de nota hay abiertas en cada momento, así que no hace
    /// falta ningún cálculo nuevo aquí para que no queden todas exactamente superpuestas.
    /// </summary>
    public void OpenAllNotes(NoteLayoutTemplate layout = NoteLayoutTemplate.Normal)
    {
        var dock = DockNearCursor();
        if (dock is null) return;

        foreach (var note in _repository.GetByState(NoteState.Active))
        {
            if (IsNoteOpen(note.Id)) continue;
            OpenOrActivateNote(note, dock);
        }

        // TambiÃ©n se ejecuta para Normal: si ya habÃ­a notas abiertas, elegir "Normal cascade"
        // debe tener un efecto visible y no limitarse a guardar una preferencia para el siguiente
        // ciclo de abrir/cerrar.
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            ArrangeOpenNotes(layout, dock);
        }));
    }

    public void OpenSyncNotesSelector(Window owner)
    {
        if (_settings is null || _settingsService is null) return;

        var selector = new SyncNotesWindow(_repository, _settings, _settingsService)
        {
            Owner = owner
        };
        selector.ShowDialog();
    }

    private void ArrangeOpenNotes(NoteLayoutTemplate layout, EdgeDockWindow preferredDock)
    {
        var monitors = MonitorEnumerator.EnumerateMonitors();
        var fallbackMonitor = monitors.FirstOrDefault(m => m.DeviceName == preferredDock.MonitorKey);
        var windows = _openNoteWindows.Values.ToList();

        foreach (var window in windows)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.UpdateLayout();
        }

        var groups = windows
            .Select(window =>
            {
                var monitor = MonitorLookup.MonitorAt(
                    window.Left, window.Top, window.Width, window.Height, monitors);
                return (Window: window, Monitor: monitor ?? fallbackMonitor);
            })
            .Where(item => item.Monitor.DeviceName is not null)
            .GroupBy(item => item.Monitor.DeviceName);

        foreach (var group in groups)
        {
            var monitor = group.First().Monitor;
            ArrangeWindowsOnMonitor(group.Select(item => item.Window).ToList(), monitor.WorkArea, layout);
        }
    }

    private static void ArrangeWindowsOnMonitor(
        IReadOnlyList<NoteWindow> windows,
        WorkingArea area,
        NoteLayoutTemplate layout)
    {
        if (windows.Count == 0) return;

        double availableWidth = Math.Max(1, area.Width - TemplateMargin * 2);
        double availableHeight = Math.Max(1, area.Height - TemplateMargin * 2);
        var templateWindows = windows.Take(MaxTemplateNotes).ToList();
        var overflowWindows = windows.Skip(MaxTemplateNotes).ToList();

        switch (layout)
        {
            case NoteLayoutTemplate.Grid:
                int gridColumns = GridColumnsFor(
                    templateWindows.Count,
                    availableWidth,
                    availableHeight,
                    maxColumns: 4);
                PlaceInCells(templateWindows, area, TemplateMargin, availableWidth, availableHeight, gridColumns);
                break;

            case NoteLayoutTemplate.Columns:
                int columnCount = GridColumnsFor(
                    templateWindows.Count,
                    availableWidth,
                    availableHeight,
                    maxColumns: availableWidth >= availableHeight ? 3 : 2);
                PlaceInCells(templateWindows, area, TemplateMargin, availableWidth, availableHeight, columnCount);
                break;

            default:
                double maxWidth = windows.Max(window => Math.Max(window.Width, window.MinWidth));
                double maxHeight = windows.Max(window => Math.Max(window.Height, window.MinHeight));
                double step = Math.Min(
                    TemplateGap,
                    Math.Min(
                        availableWidth > maxWidth ? (availableWidth - maxWidth) / Math.Max(1, windows.Count - 1) : 0,
                        availableHeight > maxHeight ? (availableHeight - maxHeight) / Math.Max(1, windows.Count - 1) : 0));
                double totalWidth = maxWidth + step * (windows.Count - 1);
                double totalHeight = maxHeight + step * (windows.Count - 1);
                double startLeft = area.X + Math.Max(TemplateMargin, (area.Width - totalWidth) / 2);
                double startTop = area.Y + Math.Max(TemplateMargin, (area.Height - totalHeight) / 2);

                for (int index = 0; index < windows.Count; index++)
                {
                    SetWindowPosition(windows[index], area, startLeft + index * step, startTop + index * step);
                }
                break;
        }

        if (overflowWindows.Count > 0)
        {
            // Más allá del límite la plantilla deja de intentar comprimir la pantalla. Las notas
            // restantes siguen abiertas y se colocan en una pequeña cascada, con sus cabeceras
            // visibles, para que nunca parezca que han desaparecido.
            double overflowLeft = area.X + area.Width - TemplateMargin - overflowWindows.Max(window => window.Width);
            double overflowTop = area.Y + TemplateMargin;
            for (int index = 0; index < overflowWindows.Count; index++)
            {
                SetWindowPosition(
                    overflowWindows[index],
                    area,
                    overflowLeft - index * TemplateGap,
                    overflowTop + index * TemplateGap);
            }
        }
    }

    private static int GridColumnsFor(int count, double availableWidth, double availableHeight, int maxColumns)
    {
        if (count <= 1) return 1;

        bool landscape = availableWidth >= availableHeight;
        int columns = (int)Math.Ceiling(Math.Sqrt(count * availableWidth / availableHeight));

        // En una pantalla vertical conviene conservar una columna mientras haya pocas notas. En
        // una horizontal, dos notas ya forman una fila natural. A partir de ahí la raíz cuadrada
        // mantiene las celdas equilibradas sin crear filas excesivamente altas o anchas.
        if (count == 2) columns = landscape ? 2 : 1;
        else if (count == 3) columns = landscape ? 3 : 1;

        return Math.Clamp(columns, 1, Math.Min(maxColumns, count));
    }

    private static void PlaceInCells(
        IReadOnlyList<NoteWindow> windows,
        WorkingArea area,
        double margin,
        double availableWidth,
        double availableHeight,
        int columnCount)
    {
        int rowCount = (int)Math.Ceiling(windows.Count / (double)columnCount);
        double cellWidth = availableWidth / columnCount;
        double cellHeight = availableHeight / rowCount;

        for (int index = 0; index < windows.Count; index++)
        {
            int column = index % columnCount;
            int row = index / columnCount;
            var window = windows[index];
            double left = area.X + margin + column * cellWidth + (cellWidth - window.Width) / 2;
            double top = area.Y + margin + row * cellHeight + (cellHeight - window.Height) / 2;
            SetWindowPosition(window, area, left, top);
        }
    }

    private static void SetWindowPosition(NoteWindow window, WorkingArea area, double left, double top)
    {
        double targetLeft = Math.Clamp(
            left,
            area.X,
            Math.Max(area.X, area.X + area.Width - window.Width));
        double targetTop = Math.Clamp(
            top,
            area.Y,
            Math.Max(area.Y, area.Y + area.Height - window.Height));
        window.MoveToLayoutPosition(targetLeft, targetTop);
    }

    /// <summary>
    /// Cierra todas las notas que estén abiertas ahora mismo, sin importar cómo se abrieran (por el
    /// botón, o una a una a mano). <c>ToList()</c> antes de recorrer: cerrar cada ventana dispara su
    /// <c>Closed</c>, que se quita a sí misma de <c>_openNoteWindows</c> — recorrer el diccionario
    /// en directo mientras se modifica lanzaría.
    /// </summary>
    public void CloseAllNoteWindows()
    {
        foreach (var window in _openNoteWindows.Values.ToList())
        {
            window.Close();
        }
    }

    /// <summary>
    /// El botón "abrir todas" del dock, convertido en interruptor: si hay alguna nota abierta ahora
    /// mismo (todas o solo algunas — no importa cómo se llegara a ese estado), cierra todas; si no
    /// hay ninguna, las abre todas. Más predecible que un estado de tres vías ("ninguna/algunas/
    /// todas"): la pregunta que responde siempre es la misma, "¿hay algo abierto ahora mismo?".
    /// </summary>
    public void ToggleAllNotes()
    {
        if (OpenNoteWindowCount > 0) CloseAllNoteWindows();
        else OpenAllNotes(_settings?.DefaultNoteLayout ?? NoteLayoutTemplate.Normal);
    }

    public void SetDefaultNoteLayout(NoteLayoutTemplate layout)
    {
        if (_settings is null) return;

        _settings.DefaultNoteLayout = layout;
        _settingsService?.Save(_settings);
    }

    internal void SuspendNotesAboveDockMenu()
    {
        if (_noteTopmostBeforeDockMenu is not null) return;

        _noteTopmostBeforeDockMenu = _openNoteWindows.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Topmost);

        foreach (var window in _openNoteWindows.Values)
        {
            window.Topmost = false;
        }
    }

    internal void RestoreNotesAboveDockMenu()
    {
        if (_noteTopmostBeforeDockMenu is null) return;

        foreach (var pair in _noteTopmostBeforeDockMenu)
        {
            if (_openNoteWindows.TryGetValue(pair.Key, out var window))
            {
                window.Topmost = pair.Value;
            }
        }

        _noteTopmostBeforeDockMenu = null;
    }

    public void OpenSettings()
    {
        if (_openingSettings) return;

        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            NativeMethods.ForceActivate(_settingsWindow);
            return;
        }

        if (SettingsWindowFactory is null) return;

        _openingSettings = true;
        try
        {
            var settingsDock = DockNearCursor();
            _settingsWindow = SettingsWindowFactory();
            settingsDock?.CenterOnThisMonitor(_settingsWindow);
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                _openingSettings = false;
            };
            _settingsWindow.Show();
            var shownSettingsWindow = _settingsWindow;
            shownSettingsWindow.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (ReferenceEquals(_settingsWindow, shownSettingsWindow) && shownSettingsWindow.IsVisible)
                {
                    // SizeToContent termina de medir el ScrollViewer al mostrar la ventana. Recentrar
                    // en Loaded evita calcular Top con la altura antigua y cortar la cabecera por arriba.
                    settingsDock?.CenterOnThisMonitor(shownSettingsWindow);
                    shownSettingsWindow.PlayOpenAnimation();
                }
            }));
            NativeMethods.ForceActivate(_settingsWindow);
        }
        catch
        {
            _settingsWindow = null;
            _openingSettings = false;
            throw;
        }
    }

    public void RefreshAll()
    {
        foreach (var dock in _docks) dock.Refresh();
        _notesManagerWindow?.Refresh();
    }

    public void SetDockView(DockViewKind view, string? tag = null)
    {
        if (_settings is null) return;
        _settings.DockView = view;
        _settings.DockTagFilter = view == DockViewKind.Tag ? tag : null;
        _settingsService?.Save(_settings);
        RefreshAll();
    }

    /// <summary>Activa o desactiva el sondeo automático según los ajustes actuales.</summary>
    public void ConfigureAutomaticSync()
    {
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = null;

        if (_syncService is null || _settings is not { SyncEnabled: true, SyncAutomatically: true }) return;

        int minutes = Math.Clamp(_settings.SyncIntervalMinutes, 1, 1440);
        _autoSyncTimer = new System.Threading.Timer(_ =>
        {
            var result = _syncService.Synchronize();
            if (result.Succeeded)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshAll));
            }
        }, null, TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(minutes));
    }

    /// <summary>Sincroniza y repinta las ventanas abiertas con los datos que haya ganado el merge.</summary>
    public SyncResult Synchronize()
    {
        if (_syncService is null) return new(0, 0, 0, 0, "Sync is not available.");
        var result = _syncService.Synchronize();
        if (result.Succeeded)
        {
            if (Application.Current.Dispatcher.CheckAccess()) RefreshAll();
            else Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshAll));
        }
        return result;
    }

    public void Dispose()
    {
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = null;
    }
}

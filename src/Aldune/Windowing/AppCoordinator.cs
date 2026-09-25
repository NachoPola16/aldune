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
    private Dictionary<Guid, bool>? _noteTopmostBeforeSuspension;

    /// <summary>
    /// Cuántas ventanas han pedido a la vez que las notas dejen de estar "siempre encima". Es un
    /// contador y no un booleano porque los dueños se solapan: el menú de una pestaña del dock puede
    /// estar abierto mientras también lo está el gestor de notas, y el primero en cerrarse no debe
    /// devolver la capa superior a las notas del segundo. Con el contador, la instantánea de estados
    /// se toma al entrar el primero y se restaura al salir el último.
    /// </summary>
    private int _alwaysOnTopSuspensions;
    private readonly HashSet<Window> _windowsAboveNotes = new();
    private NotesManagerWindow? _notesManagerWindow;
    private SettingsWindow? _settingsWindow;
    private bool _openingSettings;
    private SyncConflictsWindow? _syncConflictsWindow;
    private System.Threading.Timer? _autoSyncTimer;
    private readonly Dictionary<Guid, string?> _noteMonitorsBeforeDisplayChange = new();

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

    /// <summary>
    /// Cuántos docks hay ahora mismo (uno por pantalla que deba mostrarlo). El menú de "abrir todas"
    /// solo ofrece elegir pantalla cuando alguna se queda sin dock: con un dock en cada monitor, la
    /// pantalla ya la decide el dock que se pulsa; donde no hay dock no habría forma de pedirlo.
    /// </summary>
    public int DockCount => _docks.Count;

    /// <summary>
    /// El mismo ajuste que la casilla de Ajustes, a mano desde el botón derecho del "+". Guarda el
    /// valor y repinta docks y ajustes: si el usuario lo fija desde ese menú con Ajustes abierto, la
    /// casilla tiene que reflejarlo en el acto y no solo al reabrir la ventana.
    /// </summary>
    public void SetKeepDockOpen(bool keepOpen)
    {
        if (_settings is null) return;

        _settings.KeepDockOpen = keepOpen;
        _settingsService?.Save(_settings);
        RefreshAll();
    }

    /// <param name="owner">Quien lo abre como parte de su tarea (Ajustes): la ventana queda por
    /// encima de ella y se cierra con ella, en vez de quedarse suelta al cerrar Ajustes.</param>
    public void OpenSyncConflicts(Window? owner = null)
    {
        if (_syncService is null) return;
        if (_syncConflictsWindow is { IsVisible: true })
        {
            RaiseAppWindow(_syncConflictsWindow);
            return;
        }

        _syncConflictsWindow = new SyncConflictsWindow(_syncService, this) { Owner = owner };
        _syncConflictsWindow.Closed += (_, _) => _syncConflictsWindow = null;
        _syncConflictsWindow.Show();

        // Igual que el gestor de notas: los docks y las notas son Topmost, así que una ventana normal
        // puede quedar por debajo aunque se acabe de abrir. Sin esto, sus botones "no dejan clicar"
        // cuando el dock de ese canto se cruza con la ventana.
        RaiseAppWindow(_syncConflictsWindow);
    }

    /// <summary>
    /// Si esta nota ya tiene ventana abierta. El dock lo consulta para ocultar su pestaña: al
    /// abrirse, la nota se lleva esa pestaña consigo como lomo, así que dejarla también en el mazo
    /// mostraría la misma etiqueta dos veces.
    /// </summary>
    public bool IsNoteOpen(Guid noteId) => _openNoteWindows.ContainsKey(noteId);

    /// <summary>
    /// Si la ventana de esa nota está abierta pero minimizada.
    ///
    /// El dock lo necesita para decidir si enseña su pestaña: minimizar una nota es sacarla de la mesa
    /// sin cerrarla, así que la ficha tiene que volver al mazo. Antes solo miraba si la ventana
    /// existía, y minimizarla la hacía desaparecer del dock sin forma de recuperarla desde ahí.
    /// </summary>
    public bool IsNoteMinimized(Guid noteId) =>
        _openNoteWindows.TryGetValue(noteId, out var window) && window.WindowState == WindowState.Minimized;

    /// <summary>
    /// Si el dock está a la vista. No se guarda en los ajustes a propósito: ocultarlo es una acción
    /// de un momento (una película a pantalla completa), no una preferencia, y persistirla significaría
    /// arrancar algún día sin dock y sin recordar por qué.
    /// </summary>
    public bool DocksVisible { get; private set; } = true;

    /// <summary>
    /// Oculta o vuelve a mostrar todos los docks sin cerrarlos: al volver, cada uno recupera su
    /// sitio, su monitor y su capa superior.
    ///
    /// Hace falta porque la detección de pantalla completa no cubre todo: el caso que lo pidió es
    /// poner un vídeo a pantalla completa en el navegador, que a veces Windows no reporta como tal
    /// (pasa con la pantalla completa sin bordes de algunos reproductores web), y entonces el dock
    /// se queda encima del vídeo. Con esto el usuario lo aparta y lo devuelve cuando quiere.
    /// </summary>
    public void SetDocksVisible(bool visible)
    {
        if (DocksVisible == visible) return;

        DocksVisible = visible;
        foreach (var dock in _docks)
        {
            dock.SetUserHidden(!visible);
        }
    }

    /// <summary>Interruptor para el atajo global y la bandeja.</summary>
    public void ToggleDocksVisible() => SetDocksVisible(!DocksVisible);

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

    public void RememberOpenNoteMonitors()
    {
        _noteMonitorsBeforeDisplayChange.Clear();
        foreach (var pair in _openNoteWindows)
            _noteMonitorsBeforeDisplayChange[pair.Key] = pair.Value.CurrentMonitorKey;
    }

    public void RestoreOpenNotesAfterDisplayChange()
    {
        foreach (var pair in _noteMonitorsBeforeDisplayChange.ToList())
        {
            if (pair.Value is null || !_openNoteWindows.TryGetValue(pair.Key, out var window)) continue;
            var placement = _repository.GetPlacement(pair.Key, pair.Value);
            if (placement is null) continue;
            if (PlacementValidation.IsVisibleOnMonitors(
                    placement.Left, placement.Top, placement.Width, placement.Height,
                    MonitorEnumerator.EnumerateMonitors()))
                window.ApplyPlacement(placement.Left, placement.Top, placement.Width, placement.Height);
        }
        _noteMonitorsBeforeDisplayChange.Clear();
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
    ///
    /// <paramref name="targetMonitorKey"/> abre la nota en otra pantalla que la de
    /// <paramref name="requestingDock"/> — solo lo usan las opciones del menú que se pueden mandar a
    /// otra pantalla (ver <see cref="OpenAllNotes"/>).
    /// </summary>
    public void OpenOrActivateNote(
        Note note,
        EdgeDockWindow requestingDock,
        System.Windows.Rect? originRect = null,
        string? targetMonitorKey = null,
        bool arrangeAfterOpen = true)
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
            Note? unlocked = null;
            var password = PasswordPromptWindow.Show(requestingDock, PasswordPromptMode.Unlock,
                candidate => _repository.TryUnlock(note.Id, candidate, out unlocked) && unlocked is not null);
            if (password is null || unlocked is null) return;

            note = unlocked;
            protectionPassword = password;
        }

        var noteWindow = new NoteWindow(note, _repository, this, _settings, protectionPassword);

        // Si la nota tiene una posición guardada PARA ESA PANTALLA y sigue a la vista, reaparece ahí
        // directamente — sensación de post-it real, no de "otra ventana que se abre desde el dock".
        // Por pantalla y no una posición única: abrirla desde el dock del monitor vertical no debe
        // traerla desde donde se dejó en el horizontal (o viceversa) — cada pantalla tiene su propio
        // recuerdo.
        string placementKey = targetMonitorKey ?? requestingDock.MonitorKey;
        bool restoredPlacement = TryRestorePlacement(noteWindow, note.Id, placementKey);
        if (!restoredPlacement)
        {
            // Sin posición guardada, el camino de siempre es la cascada junto a la pestaña que se
            // pulsó, pero eso solo vale si la nota sale en la pantalla de ESE dock. Si se pidió otra,
            // el reparto exacto lo hace ArrangeOpenNotes un momento después: aquí basta con que nazca
            // dentro de la pantalla elegida, en vez de asomar por la del dock.
            if (placementKey == requestingDock.MonitorKey) requestingDock.PositionNoteWindow(noteWindow, originRect);
            else PositionNearMonitorEdge(noteWindow, placementKey);
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

        // Si hay una disposición elegida (cuadrícula, columnas o cascada), la nota entra en ella en
        // vez de caer en cascada junto a su pestaña: se redistribuyen las abiertas de esa pantalla
        // para que quepa. Es lo que convierte "abrir todas en columnas" en un estado del escritorio y
        // no en una acción de un instante — al abrir cualquiera después, encaja donde toca. Con la
        // disposición Normal (la de fábrica) no se toca nada: manda la cascada de siempre.
        // arrangeAfterOpen=false cuando quien abre es un lote (abrir todas, cascada): el lote hace UN
        // reparto al final. Con uno por nota, N repartos concurrentes sobre ventanas que aún se
        // animan pisaban las posiciones unos a otros y el orden final dependía de las carreras.
        if (arrangeAfterOpen
            && _settings?.DefaultNoteLayout is { } layout && layout != NoteLayoutTemplate.Normal)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                ArrangeOpenNotes(layout, requestingDock, targetMonitorKey)));
        }
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

        noteWindow.ApplyPlacement(placement.Left, placement.Top, placement.Width, placement.Height);
        return true;
    }

    /// <summary>
    /// Abre el gestor de notas, o activa el que ya haya.
    ///
    /// <paramref name="requestingDock"/> decide en qué pantalla sale. Sin él, la ventana no fijaba
    /// posición y Windows la ponía en (0,0) — o sea, siempre en el monitor principal, aunque
    /// hubieras pulsado el engranaje en el otro.
    /// </summary>
    public void OpenOrActivateNotesManager(EdgeDockWindow requestingDock, string? tagFilter = null)
    {
        // Uno que se está cerrando (su animación dura un instante) cuenta como cerrado: pulsar otra
        // vez justo entonces abre uno nuevo en vez de "activar" el que se va.
        if (_notesManagerWindow is not null && !WindowCloseAnimation.IsClosing(_notesManagerWindow))
        {
            if (_notesManagerWindow.WindowState == WindowState.Minimized)
                _notesManagerWindow.WindowState = WindowState.Normal;

            // Pedir "gestionar" desde la vista de una etiqueta tiene que enseñar esa etiqueta aunque
            // el gestor ya estuviera abierto enseñando otra cosa.
            if (tagFilter is not null) _notesManagerWindow.ApplyTagFilter(tagFilter);

            // Por encima de todo, no solo activado: una nota activada después tapa el gestor, y eso
            // es justo lo que obligaba a pulsar dos veces (ver RaiseAppWindow).
            RaiseAppWindow(_notesManagerWindow);
            return;
        }

        var manager = new NotesManagerWindow(_repository, this, tagFilter);
        _notesManagerWindow = manager;
        requestingDock.CenterOnThisMonitor(manager);
        manager.Closed += (_, _) =>
        {
            if (ReferenceEquals(_notesManagerWindow, manager)) _notesManagerWindow = null;
        };
        _notesManagerWindow.Show();
        _notesManagerWindow.PlayOpenAnimation();
        RaiseAppWindow(_notesManagerWindow);
    }

    public IReadOnlyList<string> GetSyncTags() => _repository.GetAllTags();
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

        var note = _repository.Create(string.Empty, NextNoteColor(), screenOrigin: "primary");

        // En la vista de una etiqueta la nota nace ya con ella, igual que con el "+" del dock: si no,
        // desaparecería del dock nada más crearla y su color se habría calculado con otras vecinas.
        if (_settings is { DockView: DockViewKind.Tag, DockTagFilter: { } tag } && !string.IsNullOrWhiteSpace(tag))
            _repository.SetTags(note.Id, new[] { tag });

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
    ///
    /// <paramref name="targetMonitorKey"/> manda la disposición a otra pantalla que la del dock que
    /// pidió la acción; sin él, a la de ese dock (ver <see cref="ArrangeOpenNotes"/>).
    /// </summary>
    public void OpenAllNotes(
        EdgeDockWindow requestingDock,
        NoteLayoutTemplate layout = NoteLayoutTemplate.Normal,
        string? targetMonitorKey = null)
    {
        // La cascada junto al dock es una plantilla más (se recuerda como las demás): el clic izquierdo
        // del botón vuelve a abrir en ella, no en la cascada por defecto de cada pestaña.
        if (layout == NoteLayoutTemplate.DockCascade)
        {
            CascadeNotesNearDock(requestingDock, targetMonitorKey);
            return;
        }

        foreach (var note in NotesForCurrentDockView())
        {
            if (IsNoteOpen(note.Id)) continue;
            OpenOrActivateNote(note, requestingDock, targetMonitorKey: targetMonitorKey, arrangeAfterOpen: false);
        }

        // Con Normal no se reparte nada mas: cada nota ya cayo donde tocaba dentro del bucle de arriba
        // (su posicion recordada por pantalla via TryRestorePlacement, o si no la tenia, en cascada
        // junto a su pestana - ver OpenOrActivateNote). Antes esto llamaba a ArrangeOpenNotes tambien
        // para Normal, que reparte TODAS las notas abiertas (incluidas las que ya estaban) en una
        // cascada diagonal centrada en la pantalla, pisando sin excepcion cualquier posicion movida a
        // mano - justo lo que "Desplegar todas" no debia hacer. Grid/Columns/DockCascade si necesitan
        // este reparto porque colocan varias notas a la vez segun su hueco disponible, algo que no se
        // puede calcular nota a nota segun se van abriendo.
        if (layout == NoteLayoutTemplate.Normal) return;

        var dock = requestingDock;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            ArrangeOpenNotes(layout, dock, targetMonitorKey);
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

    /// <summary>
    /// Las ventanas de nota abiertas, en el orden del MAZO (el de las pestañas del dock), no en el de
    /// apertura. `_openNoteWindows` es un diccionario y su orden es el de inserción: repartir en ese
    /// orden hacía que la nota que caía en cada celda de una disposición pareciera aleatoria y que la
    /// cuadrícula no se correspondiera con el abanico. Con el mismo orden que el dock, la primera celda
    /// es la primera pestaña. Las que no estén en la vista actual (no pueden, porque las cierra
    /// <c>CloseNotesOutsideCurrentView</c>) quedarían al final, pero se deja la defensa por si algún
    /// día cambia esa regla.
    /// </summary>
    private List<NoteWindow> OpenWindowsInDeckOrder()
    {
        var deckOrder = NotesForCurrentDockView()
            .Select((note, index) => (note.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index);

        return _openNoteWindows
            .OrderBy(pair => deckOrder.TryGetValue(pair.Key, out int index) ? index : int.MaxValue)
            .Select(pair => pair.Value)
            .ToList();
    }

    /// <summary>
    /// Reparte las notas abiertas con la plantilla elegida, todas en la misma pantalla: la del dock
    /// que pidió la acción, o <paramref name="targetMonitorKey"/> si se pidió otra de las que no
    /// tienen dock (ver <see cref="DockCount"/>).
    ///
    /// Antes agrupaba las ventanas por el monitor en el que estuvieran y repartía cada grupo en el
    /// suyo, así que con notas en las dos pantallas "cuadrícula" dejaba dos cuadrículas de una columna
    /// y no se veía ninguna disposición. Una disposición es una: si se pide desde un dock, o para una
    /// pantalla concreta, todas van ahí.
    /// </summary>
    private void ArrangeOpenNotes(
        NoteLayoutTemplate layout,
        EdgeDockWindow preferredDock,
        string? targetMonitorKey = null)
    {
        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (MonitorLookup.TargetOrFallback(targetMonitorKey, preferredDock.MonitorKey, monitors) is not { } target)
        {
            return; // ni la pantalla pedida ni la del dock existen ahora mismo
        }

        if (layout == NoteLayoutTemplate.DockCascade)
        {
            CascadeOpenNotesNearDock(
                target.WorkArea, _settings?.DockEdge ?? EdgePosition.Right, NotesForCurrentDockView().Count);
            return;
        }

        var windows = OpenWindowsInDeckOrder();

        foreach (var window in windows)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.UpdateLayout();
        }

        // Con el dock en un lateral el abanico se lee de arriba abajo, así que las celdas se rellenan
        // por columnas para que la primera columna sea la primera tanda de pestañas. Con el dock
        // arriba o abajo, la lectura natural es por filas.
        bool columnMajor = (_settings?.DockEdge ?? EdgePosition.Right) is EdgePosition.Left or EdgePosition.Right;
        ArrangeWindowsOnMonitor(windows, target.WorkArea, layout, columnMajor);
    }

    private static void ArrangeWindowsOnMonitor(
        IReadOnlyList<NoteWindow> windows,
        WorkingArea area,
        NoteLayoutTemplate layout,
        bool columnMajor)
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
                PlaceInCells(templateWindows, area, TemplateMargin, availableWidth, availableHeight, gridColumns, columnMajor);
                break;

            case NoteLayoutTemplate.Columns:
                int columnCount = GridColumnsFor(
                    templateWindows.Count,
                    availableWidth,
                    availableHeight,
                    maxColumns: availableWidth >= availableHeight ? 3 : 2);
                PlaceInCells(templateWindows, area, TemplateMargin, availableWidth, availableHeight, columnCount, columnMajor);
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
        int columnCount,
        bool columnMajor)
    {
        int rowCount = (int)Math.Ceiling(windows.Count / (double)columnCount);
        double cellWidth = availableWidth / columnCount;
        double cellHeight = availableHeight / rowCount;

        for (int index = 0; index < windows.Count; index++)
        {
            int column = columnMajor ? index / rowCount : index % columnCount;
            int row = columnMajor ? index % rowCount : index / columnCount;
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
    /// Coloca una nota recién creada cerca del borde del dock de la pantalla indicada. Es el reparto por defecto
    /// cuando la nota se abre en una pantalla que no es la del dock que la pidió:
    /// <see cref="EdgeDockWindow.PositionNoteWindow"/> la pega a ESE dock, que está en otra. El reparto
    /// definitivo (cascada, cuadrícula, columnas) lo hace <see cref="ArrangeOpenNotes"/> un momento
    /// después.
    /// </summary>
    private void PositionNearMonitorEdge(NoteWindow noteWindow, string monitorKey)
    {
        if (MonitorLookup.ForDeviceName(monitorKey, MonitorEnumerator.EnumerateMonitors()) is not { } monitor) return;

        var area = monitor.WorkArea;
        const double gap = 42;
        var edge = _settings?.DockEdge ?? EdgePosition.Right;
        double left = edge == EdgePosition.Left ? area.X + gap
            : edge == EdgePosition.Right ? area.X + area.Width - noteWindow.Width - gap
            : area.X + (area.Width - noteWindow.Width) / 2;
        double top = edge == EdgePosition.Top ? area.Y + gap
            : edge == EdgePosition.Bottom ? area.Y + area.Height - noteWindow.Height - gap
            : area.Y + (area.Height - noteWindow.Height) / 2;
        noteWindow.Left = Math.Clamp(left, area.X, Math.Max(area.X, area.X + area.Width - noteWindow.Width));
        noteWindow.Top = Math.Clamp(top, area.Y, Math.Max(area.Y, area.Y + area.Height - noteWindow.Height));
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

    /// <summary>El tema de color elegido en Ajustes, o Clásico si no hay ajustes o el id no existe.</summary>
    internal NoteTheme ActiveTheme => NoteThemes.Resolve(_settings?.ActiveThemeId, _settings?.CustomThemes);

    /// <summary>
    /// El color de una nota que se va a crear. Mira la vista actual del dock y no todas las notas:
    /// en la vista de una etiqueta, las vecinas de la nota nueva son las de esa etiqueta.
    /// </summary>
    internal string NextNoteColor() => NoteColorAssigner.Assign(
        ActiveTheme,
        _settings?.NewNoteTone ?? NoteTone.Light,
        _settings?.ColorAssignment ?? NoteColorAssignment.RotateAvoidNeighbors,
        _settings?.FixedNoteColor,
        NotesForCurrentDockView().Select(note => note.Color).ToList());

    /// <summary>
    /// Vuelve a colorear todas las notas activas con el tema, el tono y la regla actuales, en el
    /// orden del dock y como si se crearan una tras otra: así "rotar sin repetir el de al lado"
    /// deja un dock sin dos vecinas iguales. Archivadas y papelera no se tocan. Cada cambio es un
    /// SetColor normal, así que se sincroniza como si se hubiera hecho a mano.
    /// </summary>
    internal int ApplyThemeToActiveNotes()
    {
        var theme = ActiveTheme;
        var tone = _settings?.NewNoteTone ?? NoteTone.Light;
        var rule = _settings?.ColorAssignment ?? NoteColorAssignment.RotateAvoidNeighbors;
        var assigned = new List<string>();
        int changed = 0;

        foreach (var note in _repository.GetByState(NoteState.Active))
        {
            var color = NoteColorAssigner.Assign(theme, tone, rule, _settings?.FixedNoteColor, assigned);
            assigned.Add(color);
            if (string.Equals(color, note.Color, StringComparison.OrdinalIgnoreCase)) continue;

            _repository.SetColor(note.Id, color);
            if (_openNoteWindows.TryGetValue(note.Id, out var window)) window.ApplyExternalColor(color);
            changed++;
        }

        RefreshAll();
        return changed;
    }

    /// <summary>
    /// Una nota ha cambiado de color fuera de su propia ventana (menú de la pestaña del dock). Si está
    /// abierta se repinta: si no, seguiría con la cara, el borde y la tinta viejos hasta reabrirla.
    /// </summary>
    internal void NotifyNoteColorChanged(Guid noteId, string color)
    {
        if (_openNoteWindows.TryGetValue(noteId, out var window)) window.ApplyExternalColor(color);
    }

    /// <summary>Cuántas notas activas hay, para el mensaje de confirmación de Ajustes.</summary>
    internal int ActiveNoteCount => _repository.GetByState(NoteState.Active).Count;

    /// <summary>
    /// El botón "abrir todas" del dock, convertido en interruptor: si hay alguna nota abierta ahora
    /// mismo (todas o solo algunas — no importa cómo se llegara a ese estado), cierra todas; si no
    /// hay ninguna, las abre todas. Más predecible que un estado de tres vías ("ninguna/algunas/
    /// todas"): la pregunta que responde siempre es la misma, "¿hay algo abierto ahora mismo?".
    /// </summary>
    /// <summary>
    /// Notas que el dock está enseñando en este momento. En la vista por etiqueta son solo las de esa
    /// etiqueta, así que "abrir todas" desde ahí abre esas y no el mazo entero: desparramar por la
    /// pantalla notas que el usuario acaba de decidir no ver rompería el filtro que acaba de elegir.
    /// </summary>
    private IReadOnlyList<Note> NotesForCurrentDockView()
    {
        var tag = _settings?.DockTagFilter;
        if (_settings?.DockView == DockViewKind.Tag && !string.IsNullOrWhiteSpace(tag))
            return _repository.GetByTag(tag, NoteState.Active);

        return _repository.GetByState(NoteState.Active);
    }

    /// <summary>
    /// El botón "abrir todas" del dock, convertido en interruptor: si hay alguna nota abierta ahora
    /// mismo (todas o solo algunas — no importa cómo se llegara a ese estado), cierra todas; si no
    /// hay ninguna, las abre todas. Más predecible que un estado de tres vías ("ninguna/algunas/
    /// todas"): la pregunta que responde siempre es la misma, "¿hay algo abierto ahora mismo?".
    ///
    /// "Todas" significa las de la vista en curso: en la vista de una etiqueta el interruptor abre y
    /// cierra solo esas, igual que el resto de botones del pie. "Cerrar todas" del menú contextual
    /// sigue siendo global, porque ahí el usuario está pidiendo cerrarlo todo explícitamente.
    ///
    /// El dock que pide decide la pantalla: abrir desde la pestaña de un dock tiene que pintar en
    /// esa pantalla, no en la del cursor. Con dos docks, uno en cada monitor, el cursor casi nunca
    /// está sobre el dock que se acaba de pulsar — está sobre la pantalla donde se está trabajando —
    /// y adivinar por cursor abre las notas en el monitor equivocado.
    /// </summary>
    public void ToggleAllNotes(EdgeDockWindow requestingDock)
    {
        var view = NotesForCurrentDockView();
        var openInView = view.Where(note => IsNoteOpen(note.Id)).ToList();

        if (openInView.Count > 0)
        {
            // ToList antes de cerrar: cada Close dispara el Closed que la quita del diccionario, y
            // recorrerlo en directo mientras se modifica lanzaría.
            foreach (var window in openInView.Select(note => _openNoteWindows[note.Id]).ToList())
            {
                window.Close();
            }
            return;
        }

        OpenAllNotes(requestingDock, _settings?.DefaultNoteLayout ?? NoteLayoutTemplate.Normal);
    }

    public void SetDefaultNoteLayout(NoteLayoutTemplate layout)
    {
        if (_settings is null) return;

        _settings.DefaultNoteLayout = layout;
        _settingsService?.Save(_settings);
    }

    /// <summary>
    /// Aviso de que el usuario acaba de arrastrar una nota a mano (ver <see cref="NoteWindow.OnGripMouseDown"/>).
    /// Si la disposición global activa era una plantilla automática (cascada, cuadrícula, columnas), deja de
    /// describir dónde está de verdad esa nota: sin este aviso, "Desplegar todas" volvía a recalcular esa
    /// disposición desde cero la próxima vez y sobrescribía la posición recién elegida a mano.
    /// </summary>
    internal void NoteMovedManually()
    {
        if (_settings is { DefaultNoteLayout: not NoteLayoutTemplate.Normal })
        {
            SetDefaultNoteLayout(NoteLayoutTemplate.Normal);
        }
    }

    internal void SuspendNotesAboveDockMenu()
    {
        _alwaysOnTopSuspensions++;
        if (_alwaysOnTopSuspensions > 1) return;

        _noteTopmostBeforeSuspension = _openNoteWindows.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Topmost);

        foreach (var window in _openNoteWindows.Values)
        {
            window.Topmost = false;
        }
    }

    internal void RestoreNotesAboveDockMenu()
    {
        if (_alwaysOnTopSuspensions == 0) return;

        _alwaysOnTopSuspensions--;
        if (_alwaysOnTopSuspensions > 0) return;

        if (_noteTopmostBeforeSuspension is not null)
        {
            foreach (var pair in _noteTopmostBeforeSuspension)
            {
                if (_openNoteWindows.TryGetValue(pair.Key, out var window))
                {
                    window.Topmost = pair.Value;
                }
            }
        }

        _noteTopmostBeforeSuspension = null;
    }

    /// <summary>
    /// Deja una ventana propia (gestor de notas, Ajustes, conflictos) por encima de todo durante su
    /// vida útil.
    ///
    /// Las notas y los docks son <c>Topmost</c> porque ese es el comportamiento de un post-it, así que
    /// una ventana de la app que no lo fuera quedaba siempre debajo y había que pulsarla dos veces:
    /// la primera la abría detrás de las notas, la segunda la activaba. Aquí se apartan las notas
    /// mientras la ventana vive (mismo mecanismo que ya usaba el menú de una pestaña del dock) y se
    /// sube la ventana al frente de la capa superior.
    ///
    /// La suspensión se toma una sola vez por ventana y se devuelve sola al cerrarse. Antes se tomaba
    /// en cada llamada, y esto se llama también al pulsar "Gestionar notas" o "Ajustes" con la
    /// ventana ya abierta: el contador subía dos veces, bajaba una al cerrar, y las notas se quedaban
    /// sin "siempre encima" hasta reiniciar la app.
    /// </summary>
    internal void RaiseAppWindow(Window window)
    {
        if (_windowsAboveNotes.Add(window))
        {
            SuspendNotesAboveDockMenu();
            window.Closed += (_, _) => ReleaseAppWindow(window);
        }
        window.Activate();
        NativeMethods.RaiseTopmostWindow(window);
        NativeMethods.ForceActivate(window);
    }

    private void ReleaseAppWindow(Window window)
    {
        if (_windowsAboveNotes.Remove(window)) RestoreNotesAboveDockMenu();
    }

    /// <summary>
    /// Abre todas las notas de la vista en cascada junto al dock que pidió la acción — solapadas en
    /// diagonal desde el canto del dock, en el orden del mazo. Es la opción "Cascada junto al dock"
    /// del menú del dock: la forma rápida de recoger el escritorio sin decidir una disposición (para
    /// una disposición pensada están "Cuadrícula" y "Columnas").
    ///
    /// Va en la pantalla del dock que la pidió (o <paramref name="targetMonitorKey"/> si se eligió
    /// otra), igual que el resto de disposiciones del menú.
    /// </summary>
    internal void CascadeNotesNearDock(EdgeDockWindow requestingDock, string? targetMonitorKey = null)
    {
        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (MonitorLookup.TargetOrFallback(targetMonitorKey, requestingDock.MonitorKey, monitors) is not { } target)
        {
            return; // ni la pantalla pedida ni la del dock existen ahora mismo
        }

        foreach (var note in NotesForCurrentDockView())
        {
            if (!IsNoteOpen(note.Id))
                OpenOrActivateNote(note, requestingDock, targetMonitorKey: target.DeviceName, arrangeAfterOpen: false);
        }

        int deckSize = NotesForCurrentDockView().Count;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            CascadeOpenNotesNearDock(target.WorkArea, _settings?.DockEdge ?? EdgePosition.Right, deckSize)));
    }

    /// <summary>
    /// Cascada junto al dock que indica <paramref name="edge"/>, dejando libre el hueco donde se
    /// despliega su abanico (ver <see cref="NoteCascade"/>). Cada ventana deja asomar la cabecera de la
    /// anterior (el paso es menor que la ventana), así que es fácil coger cualquiera sin adivinar qué
    /// hay debajo. Con muchas notas la cascada se comprime a partir de la sexta: no hay pantalla donde
    /// quepa una en escalera eterna de una en una.
    /// </summary>
    private void CascadeOpenNotesNearDock(WorkingArea area, EdgePosition edge, int deckSize)
    {
        // ToList antes de mover: fijar Left/Top dispara LocationChanged, que puede tocar la posición
        // mientras se recorre el diccionario. Orden del mazo, igual que las plantillas: la cascada lee
        // la primera pestaña más cerca del dock y así sucesivamente.
        var windows = OpenWindowsInDeckOrder();
        if (windows.Count == 0) return;

        for (int index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            if (window.WindowState != WindowState.Normal) window.WindowState = WindowState.Normal;

            var (left, top) = NoteCascade.Position(area, edge, deckSize, window.Width, window.Height, index);
            SetWindowPosition(window, area, left, top);
        }

        // El apilado también es parte de la cascada: cada nota tapa el cuerpo de la anterior y deja
        // asomar su cabecera. Abriéndolas desde cerradas sale solo (cada una se activa al abrirse, en
        // orden del mazo), pero si ya estaban abiertas —por ejemplo en columnas, donde se tocó alguna—
        // conservaban el apilado de antes y la última activada tapaba las cabeceras de las demás.
        // Se fija aquí para que la cascada sea idéntica venga de donde venga.
        foreach (var window in windows)
            NativeMethods.BringToTopWithoutActivating(window);
        NativeMethods.ForceActivate(windows[^1]);
    }

    public void OpenSettings()
    {
        if (_openingSettings) return;

        if (_settingsWindow is not null && !WindowCloseAnimation.IsClosing(_settingsWindow))
        {
            RaiseAppWindow(_settingsWindow);
            return;
        }

        if (SettingsWindowFactory is null) return;

        _openingSettings = true;
        try
        {
            var settingsDock = DockNearCursor();
            var created = SettingsWindowFactory();
            _settingsWindow = created;
            settingsDock?.CenterOnThisMonitor(created);
            created.Closed += (_, _) =>
            {
                if (ReferenceEquals(_settingsWindow, created)) _settingsWindow = null;
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
            RaiseAppWindow(_settingsWindow);
            // Solo protege la construcción: antes se quedaba en true mientras Ajustes estuviera
            // abierta, y pulsar "Ajustes" otra vez no la traía al frente.
            _openingSettings = false;
        }
        catch
        {
            // Si llegó a subirse, la suspensión de las notas se devuelve aquí: la ventana no se va a
            // cerrar por el camino normal. Si estalló antes, no hay nada que devolver.
            if (_settingsWindow is not null) ReleaseAppWindow(_settingsWindow);
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

        CloseNotesOutsideCurrentView();
        RefreshAll();
    }

    /// <summary>
    /// Cierra las notas abiertas que ya no pertenecen a la vista en curso del dock. Cambiar de vista o
    /// de etiqueta es cambiar de mesa de trabajo: una nota abierta que ya no está en lo que el dock
    /// enseña cerraría sola su ventana — se guarda al cerrar, no se pierde nada — en vez de quedarse en
    /// la pantalla representando un filtro que el dock ya no aplica. Lo pidió el usuario para el cambio
    /// entre etiquetas; la regla es la misma para cualquier vista.
    ///
    /// ToList antes de cerrar: cada Close dispara el Closed que la quita del diccionario.
    /// </summary>
    private void CloseNotesOutsideCurrentView()
    {
        var inView = NotesForCurrentDockView().Select(note => note.Id).ToHashSet();

        foreach (var window in _openNoteWindows.Values
                     .Where(window => !inView.Contains(window.Note.Id))
                     .ToList())
        {
            window.Close();
        }
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

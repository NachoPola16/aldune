using System.Linq;
using System.Windows;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

/// <summary>
/// One instance for the whole app (not one per dock/monitor). Owns what used to live inside
/// EdgeDockWindow: which notes already have an open NoteWindow (so clicking the same note's tab
/// from two different docks never opens it twice), the single shared NotesManagerWindow, and
/// telling every dock to refresh together (with several docks all mirroring the same note list —
/// see Phase 3a spec — archiving a note from any one of them has to update all of them).
/// </summary>
public sealed class AppCoordinator
{
    private readonly NotesRepository _repository;
    private readonly AppSettings? _settings;
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private readonly List<EdgeDockWindow> _docks = new();
    private NotesManagerWindow? _notesManagerWindow;
    private SettingsWindow? _settingsWindow;

    /// <summary>Fabrica de la ventana de ajustes, inyectada por App: el coordinador no tiene por
    /// que saber de SettingsService ni del atajo global, solo de que hay una ventana unica.</summary>
    public Func<SettingsWindow>? SettingsWindowFactory { get; set; }

    /// <summary>
    /// Accion para reconstruir los docks en caliente (inyectada por App), usada al cambiar
    /// la pantalla elegida en Ajustes sin necesidad de reiniciar la app.
    /// </summary>
    public Action? RebuildDocksAction { get; set; }

    public void RebuildDocks() => RebuildDocksAction?.Invoke();

    public AppCoordinator(NotesRepository repository, AppSettings? settings = null)
    {
        _repository = repository;
        _settings = settings;
    }

    public int OpenNoteWindowCount => _openNoteWindows.Count;

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

        var noteWindow = new NoteWindow(note, _repository, this, _settings);

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
    public void OpenAllNotes()
    {
        var dock = DockNearCursor();
        if (dock is null) return;

        foreach (var note in _repository.GetByState(NoteState.Active))
        {
            if (IsNoteOpen(note.Id)) continue;
            OpenOrActivateNote(note, dock);
        }
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
        else OpenAllNotes();
    }

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            NativeMethods.ForceActivate(_settingsWindow);
            return;
        }

        if (SettingsWindowFactory is null) return;

        _settingsWindow = SettingsWindowFactory();
        DockNearCursor()?.CenterOnThisMonitor(_settingsWindow);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.PlayOpenAnimation();
        NativeMethods.ForceActivate(_settingsWindow);
    }

    public void RefreshAll()
    {
        foreach (var dock in _docks) dock.Refresh();
    }
}

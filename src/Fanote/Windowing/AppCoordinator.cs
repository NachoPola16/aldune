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
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private readonly List<EdgeDockWindow> _docks = new();
    private NotesManagerWindow? _notesManagerWindow;

    public AppCoordinator(NotesRepository repository)
    {
        _repository = repository;
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

        var noteWindow = new NoteWindow(note, _repository, this);
        double slideFrom = requestingDock.PositionNoteWindow(noteWindow, originRect);

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
        // muestre, o durante la deslizada se vería la etiqueta duplicada (en el lomo y en el mazo).
        RefreshOpenState();
        noteWindow.Show();

        if (originRect is not null)
        {
            noteWindow.SlideInFrom(slideFrom);
        }

        NativeMethods.ForceActivate(noteWindow);
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
        NativeMethods.ForceActivate(_notesManagerWindow);
    }

    /// <summary>
    /// Abre el gestor sin que lo pida un dock — desde el menu de la bandeja. Se coloca sobre el
    /// primer dock disponible, que es lo mas parecido a "donde vive la app" cuando la peticion no
    /// viene de una pantalla concreta.
    /// </summary>
    public void OpenNotesManager()
    {
        if (_docks.Count == 0) return;
        OpenOrActivateNotesManager(_docks[0]);
    }

    public void RefreshAll()
    {
        foreach (var dock in _docks) dock.Refresh();
    }
}

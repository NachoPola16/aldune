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

    public void RegisterDock(EdgeDockWindow dock) => _docks.Add(dock);

    /// <summary>
    /// Opens <paramref name="note"/> in a new window, or activates its already-open one.
    /// <paramref name="originRect"/> (the clicked tab's on-screen rect, used for the grow-from-tab
    /// entrance) is ignored when the note is already open — that path just activates the existing
    /// window, which is already at wherever the user put it.
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
        requestingDock.PositionNoteWindow(noteWindow);
        if (originRect is { } origin)
        {
            noteWindow.AnimateFrom(origin);
        }
        _openNoteWindows[note.Id] = noteWindow;
        noteWindow.Closed += (_, _) => _openNoteWindows.Remove(note.Id);
        noteWindow.Show();
        NativeMethods.ForceActivate(noteWindow);
    }

    public void OpenOrActivateNotesManager()
    {
        if (_notesManagerWindow is not null)
        {
            _notesManagerWindow.Activate();
            NativeMethods.ForceActivate(_notesManagerWindow);
            return;
        }

        _notesManagerWindow = new NotesManagerWindow(_repository, this);
        _notesManagerWindow.Closed += (_, _) => _notesManagerWindow = null;
        _notesManagerWindow.Show();
        NativeMethods.ForceActivate(_notesManagerWindow);
    }

    public void RefreshAll()
    {
        foreach (var dock in _docks) dock.Refresh();
    }
}

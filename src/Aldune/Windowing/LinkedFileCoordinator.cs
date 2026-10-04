using System.Collections.Concurrent;
using System.IO;
using System.Threading.Channels;
using System.Windows.Threading;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Vigila los archivos de las notas vinculadas activas y pasa cada comprobación a
/// <see cref="LinkedFileService"/> en un hilo de fondo, de una en una (spec, decisión 4). El vigilante de
/// Windows falla en unidades de red, así que además se sondea la fecha: cada 3 s las notas abiertas, cada
/// 30 s las demás. Los resultados vuelven al hilo de la interfaz por <c>onResult</c>.
/// </summary>
internal sealed class LinkedFileCoordinator : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private const int ClosedNotePollEvery = 10;   // 10 × 3 s = 30 s

    private readonly LinkedFileService _service;
    private readonly NotesRepository _repository;
    private readonly Dispatcher _dispatcher;
    private readonly Func<Guid, bool> _isOpen;
    private readonly Action<Guid> _flushOpenWindow;
    private readonly Action<Guid, ReconcileResult> _onResult;
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _queued = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private volatile IReadOnlyList<NoteFileLink> _activeLinks = [];
    private HashSet<Guid> _activeIds = [];
    private Timer? _pollTimer;
    private int _pollTick;

    public LinkedFileCoordinator(LinkedFileService service, NotesRepository repository, Dispatcher dispatcher,
        Func<Guid, bool> isOpen, Action<Guid> flushOpenWindow, Action<Guid, ReconcileResult> onResult)
    {
        _service = service;
        _repository = repository;
        _dispatcher = dispatcher;
        _isOpen = isOpen;
        _flushOpenWindow = flushOpenWindow;
        _onResult = onResult;
    }

    public void Start()
    {
        _ = Task.Run(ConsumeAsync);
        Refresh();
        foreach (var link in _activeLinks) RequestCheck(link.NoteId);   // al arrancar: lo que cambió con Aldune cerrada
        _pollTimer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
    }

    /// <summary>En el hilo de la interfaz: vacía lo escrito en la ventana abierta y encola la comprobación.</summary>
    public void RequestCheck(Guid noteId)
    {
        _flushOpenWindow(noteId);
        if (_queued.TryAdd(noteId, 0)) _queue.Writer.TryWrite(noteId);
    }

    /// <summary>
    /// Rehace la lista de vínculos activos y los vigilantes por carpeta. Se llama en cada refresco del
    /// coordinador; las notas que acaban de volver a activas (restauradas del archivo o la papelera) se
    /// comprueban, porque mientras no estaban activas nadie las vigilaba.
    /// </summary>
    public void Refresh()
    {
        var active = _repository.GetByState(NoteState.Active).Select(note => note.Id).ToHashSet();
        var links = _repository.GetFileLinks().Where(link => active.Contains(link.NoteId)).ToList();
        var newlyActive = links.Where(link => !_activeIds.Contains(link.NoteId)).Select(link => link.NoteId).ToList();
        _activeLinks = links;
        _activeIds = links.Select(link => link.NoteId).ToHashSet();

        var folders = links.Select(link => Path.GetDirectoryName(link.Path)!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in _watchers.Keys.Except(folders, StringComparer.OrdinalIgnoreCase).ToList())
        {
            _watchers[gone].Dispose();
            _watchers.Remove(gone);
        }
        foreach (var folder in folders.Where(folder => !_watchers.ContainsKey(folder)))
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                };
                watcher.Changed += (_, e) => OnFileEvent(e.FullPath);
                watcher.Created += (_, e) => OnFileEvent(e.FullPath);
                watcher.Deleted += (_, e) => OnFileEvent(e.FullPath);
                watcher.Renamed += (_, e) => OnRenamed(e.OldFullPath, e.FullPath);
                watcher.EnableRaisingEvents = true;
                _watchers[folder] = watcher;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Carpeta que no existe o unidad desconectada: el sondeo lo cubre.
            }
        }

        foreach (var id in newlyActive) RequestCheck(id);
    }

    private void OnFileEvent(string path)
    {
        foreach (var link in _activeLinks.Where(link => string.Equals(link.Path, path, StringComparison.OrdinalIgnoreCase)))
            _dispatcher.BeginInvoke(() => RequestCheck(link.NoteId));
    }

    // Un renombrado dentro de la misma carpeta se sigue solo (spec, decisión 5). Los editores que guardan con
    // "temporal + renombrar" producen un Renamed hacia la ruta vinculada: eso es un cambio, no un renombrado.
    private void OnRenamed(string oldPath, string newPath)
    {
        OnFileEvent(newPath);
        foreach (var link in _activeLinks.Where(link => string.Equals(link.Path, oldPath, StringComparison.OrdinalIgnoreCase)))
        {
            if (!LinkedFileFormat.IsSupportedExtension(newPath)) continue;
            _dispatcher.BeginInvoke(() =>
            {
                _repository.SaveFileLink(link with { Path = newPath });
                LinkedNoteDisplay.Set(_repository.GetFileLinks());
                Refresh();
                RequestCheck(link.NoteId);
            });
        }
    }

    // En el hilo del temporizador: solo mira fechas (puede tardar en una unidad de red) y pide comprobar.
    private void Poll()
    {
        bool all = Interlocked.Increment(ref _pollTick) % ClosedNotePollEvery == 0;
        foreach (var link in _activeLinks)
        {
            if (!all && !_isOpen(link.NoteId)) continue;
            if (LinkedFileService.LooksChanged(link) || LinkedNoteDisplay.IsUnavailable(link.NoteId))
                _dispatcher.BeginInvoke(() => RequestCheck(link.NoteId));
        }
    }

    private async Task ConsumeAsync()
    {
        await foreach (var noteId in _queue.Reader.ReadAllAsync())
        {
            _queued.TryRemove(noteId, out _);
            ReconcileResult result;
            try
            {
                result = _service.Reconcile(noteId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                result = new ReconcileResult(ReconcileOutcome.Unavailable);
            }
            await _dispatcher.BeginInvoke(() => _onResult(noteId, result));
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _queue.Writer.TryComplete();
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
    }
}

using System.IO;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// La copia automática de las notas en una carpeta (fase 4): vigila la huella de cambios cada pocos segundos, espera a que se calme (así
/// una ráfaga de teclas es una sola escritura) y escribe en segundo plano con <see cref="AutoExportRunner"/>. También escribe al cerrar.
/// Lee los ajustes en vivo, de modo que encender, apagar o cambiar de carpeta surte efecto sin reiniciar. Un fallo de disco o carpeta
/// se apunta y se reintenta en la siguiente vuelta; nunca interrumpe la edición.
/// </summary>
internal sealed class AutoExportService : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);

    private readonly NotesRepository _repository;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer = new() { Interval = Tick };
    private readonly object _gate = new();
    private string? _doneKey;       // huella + ajustes de la última escritura buena
    private string? _pendingKey;    // lo visto en la vuelta anterior: se escribe cuando se repite (ya no cambia)
    private Task _running = Task.CompletedTask;

    /// <summary>El último fallo de la copia, o <c>null</c> si la última vuelta fue bien. Lo enseña Ajustes.</summary>
    public static string? LastError { get; private set; }

    public AutoExportService(NotesRepository repository, AppSettings settings)
    {
        _repository = repository;
        _settings = settings;
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
    }

    private bool Active => _settings.AutoExportEnabled && !string.IsNullOrWhiteSpace(_settings.AutoExportFolder);

    private string KeyNow() => $"{_repository.ChangeStamp()}#{_settings.AutoExportFolder}#{_settings.AutoExportArchived}";

    private void OnTick()
    {
        if (!Active || !_running.IsCompleted) return;
        try
        {
            var key = KeyNow();
            if (key == _doneKey) { _pendingKey = null; return; }
            if (key != _pendingKey) { _pendingKey = key; return; }   // acaba de cambiar: esperar una vuelta más
            _running = Task.Run(() => Write(key));
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or IOException) { }
    }

    /// <summary>Al cerrar la app: una escritura final y síncrona (con un tope de tiempo) para no perder lo último tecleado.</summary>
    public void FlushNow()
    {
        if (!Active) return;
        try
        {
            _running.Wait(TimeSpan.FromSeconds(5));
            var key = KeyNow();
            if (key != _doneKey) Write(key);
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or IOException or AggregateException) { }
    }

    private void Write(string key)
    {
        lock (_gate)
        {
            try
            {
                var folder = _settings.AutoExportFolder!;
                var includeArchived = _settings.AutoExportArchived;
                var notes = new List<ExportNote>();
                foreach (var note in _repository.GetByState(NoteState.Active)) notes.Add(ToExport(note));
                if (includeArchived)
                    foreach (var note in _repository.GetByState(NoteState.Archived)) notes.Add(ToExport(note));

                AutoExportRunner.Run(folder, notes, includeArchived);
                _doneKey = key;
                LastError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or ArgumentException or NotSupportedException)
            {
                LastError = ex.Message;
                AppLog.Write("copia automatica", $"no se pudo escribir: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static ExportNote ToExport(Note note) => new(note.Id, note.Text, note.State, note.IsProtected);

    public void Dispose() => _timer.Stop();
}

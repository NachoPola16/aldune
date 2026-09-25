using System.Windows.Threading;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Avisa cuando vence el recordatorio de una nota. Sondea cada 30s
/// mientras la app corre (<see cref="PollInterval"/>) y hace una pasada de catch-up al arrancar
/// (<see cref="CheckDueReminders"/>, llamada explícitamente desde App.OnStartup) para que un
/// recordatorio vencido con la app cerrada avise igual, en vez de perderse en silencio — ver
/// docs/superpowers/specs/2026-09-11-aldune-reminders-design.md.
///
/// El aviso es propio (<see cref="ToastCenter"/>), no un toast de Windows: estos exigen un acceso
/// directo con ruta fija, que rompería el uso portable (ver la spec). Hasta la 1.0.0 era un globo de
/// la bandeja, que no decía qué aviso se había pulsado: uno antiguo podía abrir la nota equivocada.
/// Cada aviso propio sabe cuál es su nota, y se queda hasta que se cierra.
/// </summary>
internal sealed class ReminderScheduler : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly ToastCenter _toasts;
    private readonly DispatcherTimer _timer;

    internal ReminderScheduler(NotesRepository repository, AppCoordinator coordinator, ToastCenter toasts)
    {
        _repository = repository;
        _coordinator = coordinator;
        _toasts = toasts;

        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => CheckDueReminders();
        _timer.Start();
    }

    /// <summary>Revisa y avisa de los recordatorios vencidos ahora mismo. Público porque App.xaml.cs
    /// la llama una vez explícitamente al arrancar, además del sondeo periódico normal.</summary>
    internal void CheckDueReminders()
    {
        try
        {
            var due = _repository.GetDueReminders(DateTimeOffset.UtcNow);
            if (due.Count == 0) return;

            foreach (var (noteId, _) in due)
            {
                _repository.ClearReminder(noteId);
            }

            if (due.Count == 1)
            {
                var noteId = due[0].NoteId;
                var note = _repository.GetById(noteId);
                var text = note is null ? Strings.AppName : NoteTitleHelper.GetTitle(note.Text);
                _toasts.Show(new ToastContent(Strings.ReminderToastTitle, text,
                    [new ToastAction(Strings.ReminderOpenNote, () => _coordinator.OpenNoteById(noteId), Primary: true)]));
            }
            else
            {
                _toasts.Show(new ToastContent(Strings.ReminderToastTitle, Strings.ReminderManyDue(due.Count),
                    [new ToastAction(Strings.ReminderShowNotes, _coordinator.OpenNotesManager, Primary: true)]));
            }

            _coordinator.RefreshAll();
        }
        catch
        {
            // Un fallo persistente (BD inaccesible, NotifyIcon ya no válido, etc.) no debe repetirse
            // cada 30s para siempre — el timer seguiría disparando el mismo error sin parar y, como
            // las excepciones del tick sí llegan al DispatcherUnhandledException global (a diferencia
            // de esta misma llamada durante el arranque), eso significa un MessageBox modal nuevo
            // cada 30 segundos sin forma de pararlo salvo matar el proceso. Frenar el timer aquí deja
            // como mucho un único aviso en vez de un bucle infinito. La excepción se deja subir (no
            // se traga) para que quien llame — el handler global en steady-state, o el catch-up
            // diferido de App.OnStartup — se entere de que algo falló.
            _timer.Stop();
            throw;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}

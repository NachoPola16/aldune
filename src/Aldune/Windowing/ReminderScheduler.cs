using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;
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
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;

    internal ReminderScheduler(NotesRepository repository, AppCoordinator coordinator, ToastCenter toasts, AppSettings settings)
    {
        _settings = settings;
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
        // Un fallo (base de datos ocupada, aviso que no se puede mostrar) no puede parar el sondeo para siempre:
        // antes se frenaba el temporizador y no volvía a avisarse nada hasta reiniciar. Se apunta y se reintenta
        // en el siguiente sondeo. Tampoco se lanza: una excepción por tick abriría un MessageBox cada 30 s.
        try
        {
            var due = _repository.GetDueReminders(DateTimeOffset.UtcNow);
            if (due.Count == 0) return;

            // Primero se avisa y después se borra el recordatorio: si el aviso falla, sigue pendiente.
            _coordinator.PlaySound(SoundEvent.Reminder);
            if (due.Count == 1)
            {
                ShowSingle(due[0].NoteId);
            }
            else
            {
                _toasts.Show(new ToastContent(Strings.ReminderToastTitle, Strings.ReminderManyDue(due.Count),
                    [new ToastAction(Strings.ReminderShowNotes, _coordinator.OpenNotesManager, Primary: true)]));
            }

            foreach (var (noteId, _) in due) _repository.ClearReminder(noteId);
            _coordinator.RefreshAll();
        }
        catch (Exception ex)
        {
            DockDiagnostics.Write("recordatorios", $"comprobación fallida, se reintenta: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ShowSingle(Guid noteId)
    {
        var note = _repository.GetById(noteId);
        var text = note is null ? Strings.AppName : NoteTitleHelper.GetTitle(note.Text);
        // Lo que queda por hacer, si el usuario lo quiere en el aviso: el recordatorio es de la nota entera (una
        // línea no tiene identidad estable para llevar el suyo, ver la spec de recordatorios), pero así se ve qué
        // tareas faltan. Una nota protegida llega sin texto y no enseña nada.
        if (note is not null && _settings.ReminderIncludesTasks)
        {
            var pending = TaskLists.PendingTasks(note.Text, int.MaxValue);
            if (pending.Count > 0)
                text += "\n" + Strings.ReminderPendingTasks(pending.Count, pending.Take(3).ToList());
        }
        _toasts.Show(new ToastContent(Strings.ReminderToastTitle, text,
        [
            new ToastAction(Strings.ReminderOpenNote, () => _coordinator.OpenNoteById(noteId), Primary: true),
            new ToastAction(Strings.ReminderSnooze10, () => Snooze(noteId, TimeSpan.FromMinutes(10))),
            new ToastAction(Strings.ReminderSnooze60, () => Snooze(noteId, TimeSpan.FromHours(1))),
        ]));
    }

    // Pospone: el recordatorio vuelve a quedar fijado más adelante, como si se hubiera puesto de nuevo.
    private void Snooze(Guid noteId, TimeSpan by)
    {
        _repository.SetReminder(noteId, DateTimeOffset.UtcNow + by);
        _coordinator.RefreshAll();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}

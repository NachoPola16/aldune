using System.Windows.Forms;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Resources;

namespace Fanote.Windowing;

/// <summary>
/// Dispara el aviso nativo de Windows cuando un recordatorio de nota vence. Sondea cada 30s
/// mientras la app corre (<see cref="PollInterval"/>) y hace una pasada de catch-up al arrancar
/// (<see cref="CheckDueReminders"/>, llamada explícitamente desde App.OnStartup) para que un
/// recordatorio vencido con la app cerrada avise igual, en vez de perderse en silencio — ver
/// docs/superpowers/specs/2026-09-11-fanote-reminders-design.md.
///
/// Usa el NotifyIcon que ya crea TrayIcon (inyectado, no uno nuevo) y ShowBalloonTip en vez de un
/// toast interactivo de verdad: la razón (no depender de un acceso directo con ruta fija, que
/// rompería el uso portable) está documentada en la spec.
/// </summary>
internal sealed class ReminderScheduler : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BalloonTimeoutMs = 10000;

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly NotifyIcon _icon;
    private readonly DispatcherTimer _timer;

    /// <summary>El NoteId del último aviso de UN solo recordatorio mostrado — null si el último
    /// aviso fue el agrupado de varios a la vez, o si ese aviso ya se cerró/expiró (ver
    /// OnBalloonTipClosed). Lo usa OnBalloonTipClicked para decidir si abre esa nota o el gestor.
    ///
    /// OJO: en Windows 10/11 ShowBalloonTip en realidad renderiza un toast que se apila en el
    /// Centro de actividades y sigue siendo clicable después de que llegue uno nuevo — no
    /// reemplaza al anterior como hacía el globo clásico de versiones viejas de Windows. Eso
    /// significa que este campo NO siempre corresponde al toast en el que el usuario hace clic: si
    /// el aviso A se ignora y luego llega el aviso B, un clic tardío sobre el toast de A todavía
    /// visible abre B, no A. La API de NotifyIcon.BalloonTipClicked no expone qué toast concreto
    /// se pulsó, así que no hay forma de recuperar la identidad real — esto es una mitigación, no
    /// una solución: al cerrarse/expirar un toast (BalloonTipClosed) este campo se limpia, para que
    /// un clic posterior sobre OTRO toast obsoleto caiga al gestor de notas (inofensivo) en vez de
    /// abrir silenciosamente la nota equivocada (activamente incorrecto).</summary>
    private Guid? _lastSingleNoteId;

    internal ReminderScheduler(NotesRepository repository, AppCoordinator coordinator, NotifyIcon icon)
    {
        _repository = repository;
        _coordinator = coordinator;
        _icon = icon;
        _icon.BalloonTipClicked += OnBalloonTipClicked;
        _icon.BalloonTipClosed += OnBalloonTipClosed;

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
                _lastSingleNoteId = noteId;
                var text = note is null ? Strings.AppName : NoteTitleHelper.GetTitle(note.Text);
                _icon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, text, ToolTipIcon.None);
            }
            else
            {
                _lastSingleNoteId = null;
                _icon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, Strings.ReminderManyDue(due.Count), ToolTipIcon.None);
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

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_lastSingleNoteId is { } noteId)
        {
            _coordinator.OpenNoteById(noteId);
        }
        else
        {
            _coordinator.OpenNotesManager();
        }
    }

    private void OnBalloonTipClosed(object? sender, EventArgs e)
    {
        _lastSingleNoteId = null;
    }

    public void Dispose()
    {
        _timer.Stop();
        _icon.BalloonTipClicked -= OnBalloonTipClicked;
        _icon.BalloonTipClosed -= OnBalloonTipClosed;
    }
}

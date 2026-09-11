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
internal sealed class ReminderScheduler
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BalloonTimeoutMs = 10000;

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly NotifyIcon _icon;
    private readonly DispatcherTimer _timer;

    /// <summary>El NoteId del último aviso de UN solo recordatorio mostrado — null si el último
    /// aviso fue el agrupado de varios a la vez. Lo usa OnBalloonTipClicked para decidir si abre esa
    /// nota o el gestor. Como ShowBalloonTip reemplaza cualquier globo anterior por uno nuevo, el
    /// que esté visible en cada momento siempre corresponde al valor guardado más reciente.</summary>
    private Guid? _lastSingleNoteId;

    internal ReminderScheduler(NotesRepository repository, AppCoordinator coordinator, NotifyIcon icon)
    {
        _repository = repository;
        _coordinator = coordinator;
        _icon = icon;
        _icon.BalloonTipClicked += OnBalloonTipClicked;

        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => CheckDueReminders();
        _timer.Start();
    }

    /// <summary>Revisa y avisa de los recordatorios vencidos ahora mismo. Público porque App.xaml.cs
    /// la llama una vez explícitamente al arrancar, además del sondeo periódico normal.</summary>
    internal void CheckDueReminders()
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
}

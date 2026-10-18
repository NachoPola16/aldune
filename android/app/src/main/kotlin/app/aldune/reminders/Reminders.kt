package app.aldune.reminders

import android.content.Context
import app.aldune.domain.Clock
import app.aldune.domain.Note
import app.aldune.domain.NoteState
import app.aldune.domain.NoteStore
import app.aldune.domain.Reminder
import app.aldune.domain.ReminderRules
import app.aldune.domain.ReminderStore
import app.aldune.domain.SnoozeKind
import java.time.Instant
import java.time.ZoneId
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Los recordatorios del dispositivo: la base ([store]), las alarmas y las notificaciones, siempre de acuerdo. Todo pasa por un cerrojo propio y no
 * por el de la interfaz: la alarma, los botones de la notificación y el arranque del móvil llegan por receptores, sin interfaz, y una alarma que
 * salta a la vez que una reconciliación no puede avisar dos veces. Todo es E/S: se llama fuera del hilo de interfaz.
 */
class Reminders(
    context: Context,
    private val notes: NoteStore,
    private val store: ReminderStore,
    private val clock: Clock,
) {
    private val scheduler = ReminderScheduler(context)
    private val notifier = ReminderNotifier(context)
    private val lock = Any()

    private val _changes = MutableStateFlow(0L)

    /** Sube cada vez que cambia algún recordatorio guardado: la lista lo observa para poner o quitar la campana (también si lo cambió un receptor). */
    val changes: StateFlow<Long> = _changes.asStateFlow()

    fun get(noteId: String): Reminder? = store.get(noteId)

    fun all(): List<Reminder> = store.all()

    /** Pone (o cambia) el recordatorio de la nota. Una fecha ya pasada avisa en el acto, como uno vencido. */
    fun set(noteId: String, dueAt: Instant) = synchronized(lock) {
        store.set(Reminder(noteId, dueAt))
        reconcileLocked()
        changed()
    }

    fun clear(noteId: String) = synchronized(lock) {
        store.clear(noteId)
        scheduler.cancel(noteId)
        changed()
    }

    /** Posponer desde la notificación: el aviso ya quitó el recordatorio, así que se crea uno nuevo; la notificación se cierra. */
    fun snooze(noteId: String, kind: SnoozeKind) {
        notifier.cancel(noteId)
        set(noteId, ReminderRules.snooze(kind, clock.now().atZone(ZoneId.systemDefault())))
    }

    /** «Hecho»: solo cierra la notificación. El recordatorio ya se quitó al avisar; si se puso otro después, no se toca. */
    fun dismiss(noteId: String) = notifier.cancel(noteId)

    /**
     * Saltó la alarma de [noteId] programada para [dueAt]. Se comprueba todo otra vez, porque desde que se programó la nota pudo archivarse o
     * borrarse (por sync) y el recordatorio cambiar de hora: solo avisa si sigue siendo el mismo, de una nota activa y ya vencido. Después se
     * reconcilia, que también deja programado el que se cambió de hora.
     */
    fun fired(noteId: String, dueAt: Instant) = synchronized(lock) {
        val reminder = store.get(noteId)
        val note = notes.get(noteId)
        if (reminder != null && reminder.dueAt == dueAt && note?.state == NoteState.ACTIVE && !dueAt.isAfter(clock.now())) {
            notify(reminder, note)
            changed()
        }
        reconcileLocked()
    }

    /**
     * Deja las alarmas de acuerdo con la base: borra los recordatorios sin nota activa (archivada, en la papelera o borrada, también por sync),
     * avisa de los vencidos sin avisar (móvil apagado, app muerta) y programa los demás. Se llama tras cualquier cambio que pueda dejar una nota
     * fuera del mazo, al abrir la app y con los avisos del sistema (arranque, hora, zona, permiso de alarma exacta).
     */
    fun reconcile() = synchronized(lock) {
        if (reconcileLocked()) changed()
    }

    private fun reconcileLocked(): Boolean {
        val reminders = store.all()
        if (reminders.isEmpty()) return false
        val all = notes.all()
        val now = clock.now()
        var removed = false
        for (orphan in ReminderRules.orphans(reminders, all)) {
            store.clear(orphan.noteId)
            scheduler.cancel(orphan.noteId)
            removed = true
        }
        val byId = all.associateBy { it.id }
        for (due in ReminderRules.overdue(reminders, all, now)) {
            notify(due, byId.getValue(due.noteId))
            removed = true
        }
        // Programar dos veces el mismo recordatorio reemplaza la alarma (mismo PendingIntent): no hace falta saber cuáles ya estaban.
        ReminderRules.toSchedule(reminders, all, now).forEach(scheduler::schedule)
        return removed
    }

    // Como en Windows, el recordatorio se quita al avisar: la notificación queda hasta que se descarta.
    private fun notify(reminder: Reminder, note: Note) {
        store.clear(reminder.noteId)
        scheduler.cancel(reminder.noteId)
        notifier.show(note)
    }

    private fun changed() = _changes.value++
}

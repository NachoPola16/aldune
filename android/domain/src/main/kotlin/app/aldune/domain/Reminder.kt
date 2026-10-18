package app.aldune.domain

import java.time.Instant

/**
 * El recordatorio de una nota: uno por nota, puntual y local al dispositivo (el formato 4 de sync no lo lleva).
 * [dueAt] es un instante: la zona sólo importa al mostrarlo ([ReminderLabel]) y al calcular atajos ([ReminderPresets]).
 */
data class Reminder(val noteId: String, val dueAt: Instant)

/** Dónde viven los recordatorios. Lo implementa Room (en `app`) y [InMemoryReminderStore] (en los tests). */
interface ReminderStore {
    fun get(noteId: String): Reminder?

    /** Inserta o reemplaza: una nota sólo tiene un recordatorio. */
    fun set(reminder: Reminder)

    /** No hace nada si la nota no tenía. */
    fun clear(noteId: String)

    fun all(): List<Reminder>
}

class InMemoryReminderStore : ReminderStore {
    private val reminders = LinkedHashMap<String, Reminder>()

    @Synchronized override fun get(noteId: String): Reminder? = reminders[noteId]

    @Synchronized override fun set(reminder: Reminder) {
        reminders[reminder.noteId] = reminder
    }

    @Synchronized override fun clear(noteId: String) {
        reminders.remove(noteId)
    }

    @Synchronized override fun all(): List<Reminder> = reminders.values.toList()
}

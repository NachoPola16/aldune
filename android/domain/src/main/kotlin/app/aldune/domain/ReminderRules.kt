package app.aldune.domain

import java.time.Duration
import java.time.Instant
import java.time.ZonedDateTime

/** Cuánto se pospone desde la notificación («+1 h» o «mañana 9:00», los dos atajos del menú Posponer). */
enum class SnoozeKind { OneHour, TomorrowMorning }

/**
 * Qué hacer con los recordatorios guardados. Sin reloj interno: `now` entra como parámetro, así que cada regla se prueba a los dos
 * lados de su límite. Sólo una nota **activa** avisa: archivada, en la papelera o borrada (por sync) no.
 */
object ReminderRules {
    /** Los que hay que programar en la alarma: de una nota activa y todavía por venir. */
    fun toSchedule(reminders: Collection<Reminder>, notes: Collection<Note>, now: Instant): List<Reminder> {
        val active = activeIds(notes)
        return reminders.filter { it.noteId in active && it.dueAt > now }
    }

    /**
     * Los que vencieron mientras nadie los avisó (móvil apagado, app muerta) y de una nota activa: se avisan al arrancar, los más
     * antiguos primero. El que vence justo ahora cuenta como vencido, el mismo límite por el que [toSchedule] lo deja fuera.
     */
    fun overdue(reminders: Collection<Reminder>, notes: Collection<Note>, now: Instant): List<Reminder> {
        val active = activeIds(notes)
        return reminders.filter { it.noteId in active && it.dueAt <= now }.sortedWith(compareBy<Reminder> { it.dueAt }.thenBy { it.noteId })
    }

    /**
     * Los de una nota que no existe o ya no está activa. Windows los salta al avisar y no los borra; en Android se borran (al arrancar
     * y tras cada sync) porque no hay otra pantalla desde la que verlos o quitarlos.
     */
    fun orphans(reminders: Collection<Reminder>, notes: Collection<Note>): List<Reminder> {
        val active = activeIds(notes)
        return reminders.filter { it.noteId !in active }
    }

    /** La nueva hora de un recordatorio pospuesto; [now] lleva la zona para que «mañana 9:00» sea hora de pared. */
    fun snooze(kind: SnoozeKind, now: ZonedDateTime): Instant = when (kind) {
        SnoozeKind.OneHour -> now.toInstant().plus(Duration.ofHours(1))
        SnoozeKind.TomorrowMorning -> ReminderPresets.tomorrowMorning(now).toInstant()
    }

    private fun activeIds(notes: Collection<Note>): Set<String> =
        notes.filter { it.state == NoteState.ACTIVE }.mapTo(HashSet()) { it.id }
}

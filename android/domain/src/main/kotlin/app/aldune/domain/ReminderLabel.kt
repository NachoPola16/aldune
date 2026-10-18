package app.aldune.domain

import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId

/**
 * La hora del recordatorio tal como la enseña la fila de la lista. Sellada y sin cadenas: la interfaz decide cómo se escribe
 * «hoy 20:00» en cada idioma (este módulo no tiene textos de interfaz).
 */
sealed interface ReminderLabel {
    data class Today(val time: LocalTime) : ReminderLabel

    data class Tomorrow(val time: LocalTime) : ReminderLabel

    data class Date(val date: LocalDate, val time: LocalTime) : ReminderLabel

    /** Ya venció (o vence ahora) y sigue sin avisar. */
    data object Overdue : ReminderLabel

    companion object {
        /** «Hoy», «mañana» y «la fecha» se deciden por el día **en [zone]**, no en UTC; la hora se muestra al minuto. */
        fun of(dueAt: Instant, now: Instant, zone: ZoneId): ReminderLabel {
            if (dueAt <= now) return Overdue
            val due = dueAt.atZone(zone)
            val today = now.atZone(zone).toLocalDate()
            val time = due.toLocalTime().withSecond(0).withNano(0)
            return when (due.toLocalDate()) {
                today -> Today(time)
                today.plusDays(1) -> Tomorrow(time)
                else -> Date(due.toLocalDate(), time)
            }
        }
    }
}

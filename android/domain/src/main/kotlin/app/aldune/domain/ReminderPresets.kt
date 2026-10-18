package app.aldune.domain

import java.time.ZonedDateTime

/**
 * Los atajos del selector de recordatorio (port de `ReminderPresets` de Core). Reciben `now` con su zona y calculan **en esa zona**,
 * con la hora de pared: el cambio de hora de verano no desplaza las 9:00 ni las 20:00 (mañana a las 9:00 puede estar a 10 o a 12 horas).
 */
object ReminderPresets {
    private const val TONIGHT_HOUR = 20
    private const val TOMORROW_MORNING_HOUR = 9

    /** Una hora de reloj, no de pared: si se cruza un cambio de hora son igualmente 60 minutos. */
    fun inOneHour(now: ZonedDateTime): ZonedDateTime = now.plusHours(1)

    /** Hoy a las 20:00 si aún no han dado; si ya son las 20:00 en punto o más tarde, mañana a las 20:00 (nunca propone una hora que ya pasó). */
    fun tonight(now: ZonedDateTime): ZonedDateTime {
        val eightPm = now.toLocalDate().atTime(TONIGHT_HOUR, 0).atZone(now.zone)
        return if (now.isBefore(eightPm)) eightPm else now.toLocalDate().plusDays(1).atTime(TONIGHT_HOUR, 0).atZone(now.zone)
    }

    /** El día siguiente a las 9:00, sin importar la hora de [now]. */
    fun tomorrowMorning(now: ZonedDateTime): ZonedDateTime =
        now.toLocalDate().plusDays(1).atTime(TOMORROW_MORNING_HOUR, 0).atZone(now.zone)
}

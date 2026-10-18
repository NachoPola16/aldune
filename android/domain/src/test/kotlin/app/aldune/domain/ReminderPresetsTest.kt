package app.aldune.domain

import java.time.Duration
import java.time.ZoneId
import java.time.ZonedDateTime
import kotlin.test.Test
import kotlin.test.assertEquals

class ReminderPresetsTest {
    private val madrid = ZoneId.of("Europe/Madrid")

    private fun at(text: String, zone: ZoneId = madrid): ZonedDateTime = ZonedDateTime.of(java.time.LocalDateTime.parse(text), zone)

    @Test
    fun `dentro de una hora es una hora despues`() {
        assertEquals(at("2026-05-10T15:30:00"), ReminderPresets.inOneHour(at("2026-05-10T14:30:00")))
    }

    @Test
    fun `esta noche antes de las 20 es hoy a las 20`() {
        assertEquals(at("2026-05-10T20:00:00"), ReminderPresets.tonight(at("2026-05-10T09:15:00")))
        assertEquals(at("2026-05-10T20:00:00"), ReminderPresets.tonight(at("2026-05-10T19:59:59")))
    }

    @Test
    fun `esta noche a las 20 en punto o despues es manana a las 20`() {
        assertEquals(at("2026-05-11T20:00:00"), ReminderPresets.tonight(at("2026-05-10T20:00:00")))
        assertEquals(at("2026-05-11T20:00:00"), ReminderPresets.tonight(at("2026-05-10T23:59:59")))
    }

    @Test
    fun `manana por la manana es el dia siguiente a las 9 sea la hora que sea`() {
        for (hour in listOf("00:00:00", "08:59:59", "09:00:00", "23:59:59")) {
            assertEquals(at("2026-05-11T09:00:00"), ReminderPresets.tomorrowMorning(at("2026-05-10T$hour")), hour)
        }
    }

    @Test
    fun `trabaja en la zona de now y no en UTC`() {
        // En Tokio ya es 11 de mayo (00:30), aunque en UTC sean las 15:30 del día 10.
        val tokyo = at("2026-05-11T00:30:00", ZoneId.of("Asia/Tokyo"))
        assertEquals(at("2026-05-11T20:00:00", ZoneId.of("Asia/Tokyo")), ReminderPresets.tonight(tokyo))
        assertEquals(at("2026-05-12T09:00:00", ZoneId.of("Asia/Tokyo")), ReminderPresets.tomorrowMorning(tokyo))
    }

    @Test
    fun `el cambio de hora de primavera no desplaza las 9 de la manana`() {
        // 2026-03-29 en Madrid: a las 02:00 los relojes saltan a las 03:00 (ese día dura 23 h).
        val now = at("2026-03-28T22:00:00")
        val morning = ReminderPresets.tomorrowMorning(now)
        assertEquals(at("2026-03-29T09:00:00"), morning)
        assertEquals(9, morning.hour)
        assertEquals(Duration.ofHours(10), Duration.between(now, morning))
    }

    @Test
    fun `el cambio de hora de otono no desplaza las 9 de la manana`() {
        // 2026-10-25: a las 03:00 los relojes vuelven a las 02:00 (ese día dura 25 h).
        val now = at("2026-10-24T22:00:00")
        val morning = ReminderPresets.tomorrowMorning(now)
        assertEquals(at("2026-10-25T09:00:00"), morning)
        assertEquals(Duration.ofHours(12), Duration.between(now, morning))
    }

    @Test
    fun `esta noche el dia del cambio de hora sigue siendo las 20`() {
        assertEquals(at("2026-03-29T20:00:00"), ReminderPresets.tonight(at("2026-03-29T10:00:00")))
        assertEquals(at("2026-10-25T20:00:00"), ReminderPresets.tonight(at("2026-10-25T10:00:00")))
        // Pasadas las 20:00 del día anterior al cambio, «mañana a las 20:00» es la hora de pared del día del cambio.
        assertEquals(at("2026-03-29T20:00:00"), ReminderPresets.tonight(at("2026-03-28T21:00:00")))
    }

    @Test
    fun `dentro de una hora son 60 minutos reales aunque cruce el cambio de hora`() {
        val now = at("2026-03-29T01:30:00")
        assertEquals(Duration.ofHours(1), Duration.between(now, ReminderPresets.inOneHour(now)))
    }
}

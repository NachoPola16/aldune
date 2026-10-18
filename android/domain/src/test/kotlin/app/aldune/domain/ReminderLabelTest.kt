package app.aldune.domain

import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.ZoneId
import kotlin.test.Test
import kotlin.test.assertEquals

class ReminderLabelTest {
    private val madrid = ZoneId.of("Europe/Madrid")

    private fun at(text: String, zone: ZoneId = madrid): Instant = LocalDateTime.parse(text).atZone(zone).toInstant()

    private fun label(due: String, now: String, zone: ZoneId = madrid) = ReminderLabel.of(at(due, zone), at(now, zone), zone)

    @Test
    fun `hoy`() {
        assertEquals(ReminderLabel.Today(LocalTime.of(20, 0)), label("2026-05-10T20:00:00", "2026-05-10T09:00:00"))
    }

    @Test
    fun `manana`() {
        assertEquals(ReminderLabel.Tomorrow(LocalTime.of(9, 0)), label("2026-05-11T09:00:00", "2026-05-10T09:00:00"))
    }

    @Test
    fun `mas adelante lleva la fecha`() {
        assertEquals(
            ReminderLabel.Date(LocalDate.of(2026, 5, 12), LocalTime.of(18, 30)),
            label("2026-05-12T18:30:00", "2026-05-10T09:00:00"),
        )
    }

    @Test
    fun `vencido es el que ya paso o vence ahora`() {
        assertEquals(ReminderLabel.Overdue, label("2026-05-10T08:59:59", "2026-05-10T09:00:00"))
        assertEquals(ReminderLabel.Overdue, label("2026-05-10T09:00:00", "2026-05-10T09:00:00"))
    }

    @Test
    fun `en la medianoche un minuto decide hoy o manana`() {
        assertEquals(ReminderLabel.Today(LocalTime.of(23, 59)), label("2026-05-10T23:59:00", "2026-05-10T00:00:00"))
        assertEquals(ReminderLabel.Tomorrow(LocalTime.of(0, 0)), label("2026-05-11T00:00:00", "2026-05-10T23:59:00"))
    }

    @Test
    fun `la hora que se muestra es la local sin segundos de mas`() {
        assertEquals(ReminderLabel.Today(LocalTime.of(20, 0)), label("2026-05-10T20:00:30", "2026-05-10T09:00:00"))
    }

    @Test
    fun `el mismo instante se etiqueta distinto segun la zona`() {
        val due = Instant.parse("2026-05-10T23:00:00Z")
        val now = Instant.parse("2026-05-10T12:00:00Z")
        // En Madrid (UTC+2) son las 01:00 del 11; en Los Ángeles (UTC-7), las 16:00 del 10.
        assertEquals(ReminderLabel.Tomorrow(LocalTime.of(1, 0)), ReminderLabel.of(due, now, madrid))
        assertEquals(ReminderLabel.Today(LocalTime.of(16, 0)), ReminderLabel.of(due, now, ZoneId.of("America/Los_Angeles")))
    }

    @Test
    fun `el dia del cambio de hora mantiene la hora de pared`() {
        assertEquals(ReminderLabel.Tomorrow(LocalTime.of(9, 0)), label("2026-03-29T09:00:00", "2026-03-28T22:00:00"))
    }
}

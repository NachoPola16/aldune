package app.aldune.domain

import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

class ReminderRulesTest {
    private val now = Instant.parse("2026-05-10T12:00:00Z")

    private fun note(id: String, state: NoteState = NoteState.ACTIVE) = Note(
        id = id, text = id, lineEnding = LineEnding.LF, color = "#EBD38B", createdAt = Instant.EPOCH, updatedAt = Instant.EPOCH, state = state,
    )

    private fun reminder(noteId: String, minutesFromNow: Long) = Reminder(noteId, now.plus(Duration.ofMinutes(minutesFromNow)))

    @Test
    fun `toSchedule solo trae los futuros de notas activas`() {
        val notes = listOf(note("a"), note("b", NoteState.ARCHIVED), note("c", NoteState.TRASHED), note("d"))
        val reminders = listOf(reminder("a", 5), reminder("b", 5), reminder("c", 5), reminder("d", -5), reminder("e", 5))
        assertEquals(listOf(reminder("a", 5)), ReminderRules.toSchedule(reminders, notes, now))
    }

    @Test
    fun `toSchedule excluye el que vence justo ahora`() {
        assertEquals(emptyList(), ReminderRules.toSchedule(listOf(reminder("a", 0)), listOf(note("a")), now))
    }

    @Test
    fun `overdue trae los vencidos de notas activas por fecha ascendente, incluido el de justo ahora`() {
        val notes = listOf(note("a"), note("b"), note("c"), note("x", NoteState.TRASHED))
        val reminders = listOf(reminder("c", -1), reminder("a", -30), reminder("b", 0), reminder("x", -10), reminder("z", -10), reminder("a2", 10))
        assertEquals(listOf("a", "c", "b"), ReminderRules.overdue(reminders, notes, now).map { it.noteId })
    }

    @Test
    fun `overdue desempata por id para que el orden sea estable`() {
        val notes = listOf(note("a"), note("b"))
        val reminders = listOf(reminder("b", -5), reminder("a", -5))
        assertEquals(listOf("a", "b"), ReminderRules.overdue(reminders, notes, now).map { it.noteId })
    }

    @Test
    fun `orphans trae los de notas que no existen o ya no estan activas`() {
        val notes = listOf(note("a"), note("b", NoteState.ARCHIVED), note("c", NoteState.TRASHED))
        val reminders = listOf(reminder("a", 5), reminder("b", 5), reminder("c", -5), reminder("d", 5))
        assertEquals(listOf("b", "c", "d"), ReminderRules.orphans(reminders, notes).map { it.noteId })
    }

    @Test
    fun `scheduling, overdue y orphans reparten los recordatorios sin repetir ni perder ninguno`() {
        val notes = listOf(note("a"), note("b"), note("c", NoteState.ARCHIVED))
        val reminders = listOf(reminder("a", 5), reminder("b", -5), reminder("c", 5), reminder("d", -5))
        val all = ReminderRules.toSchedule(reminders, notes, now) + ReminderRules.overdue(reminders, notes, now) + ReminderRules.orphans(reminders, notes)
        assertEquals(reminders.toSet(), all.toSet())
        assertEquals(reminders.size, all.size)
    }

    @Test
    fun `snooze una hora es now mas 60 minutos`() {
        val zoned = ZonedDateTime.of(2026, 5, 10, 14, 0, 0, 0, ZoneId.of("Europe/Madrid"))
        assertEquals(zoned.toInstant().plus(Duration.ofHours(1)), ReminderRules.snooze(SnoozeKind.OneHour, zoned))
    }

    @Test
    fun `snooze manana por la manana es manana a las 9 en la zona de now`() {
        val madrid = ZoneId.of("Europe/Madrid")
        val zoned = ZonedDateTime.of(2026, 5, 10, 23, 0, 0, 0, madrid)
        assertEquals(ZonedDateTime.of(2026, 5, 11, 9, 0, 0, 0, madrid).toInstant(), ReminderRules.snooze(SnoozeKind.TomorrowMorning, zoned))
    }
}

class InMemoryReminderStoreTest {
    @Test
    fun `set sustituye el recordatorio de la nota (uno por nota)`() {
        val store = InMemoryReminderStore()
        store.set(Reminder("a", Instant.parse("2026-05-10T12:00:00Z")))
        store.set(Reminder("a", Instant.parse("2026-05-11T12:00:00Z")))
        assertEquals(listOf(Reminder("a", Instant.parse("2026-05-11T12:00:00Z"))), store.all())
        assertEquals(Instant.parse("2026-05-11T12:00:00Z"), store.get("a")!!.dueAt)
    }

    @Test
    fun `clear quita el recordatorio y no falla si no habia`() {
        val store = InMemoryReminderStore()
        store.set(Reminder("a", Instant.EPOCH))
        store.clear("a")
        store.clear("a")
        assertNull(store.get("a"))
        assertTrue(store.all().isEmpty())
    }
}

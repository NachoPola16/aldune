package app.aldune.data

import app.aldune.domain.Reminder
import app.aldune.domain.ReminderStore
import java.time.Instant

/** `ReminderStore` sobre Room. Sin cifrar: un recordatorio es un id y una hora, y no lleva texto de la nota. */
class RoomReminderStore(database: AldunaDatabase) : ReminderStore {
    private val dao = database.reminders()

    override fun get(noteId: String): Reminder? = dao.get(noteId)?.let(::toReminder)

    override fun set(reminder: Reminder) = dao.put(ReminderEntity(reminder.noteId, reminder.dueAt.toEpochMilli()))

    override fun clear(noteId: String) = dao.delete(noteId)

    override fun all(): List<Reminder> = dao.all().map(::toReminder)

    private fun toReminder(row: ReminderEntity) = Reminder(row.noteId, Instant.ofEpochMilli(row.dueAtEpochMillis))
}

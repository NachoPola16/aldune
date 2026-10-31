package app.aldune.syncclient

import app.aldune.core.SyncEnvelopeCodec
import app.aldune.domain.CompletedTasksPolicy
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

/** El ajuste «tareas hechas» por nota viaja como campo opcional del contenido cifrado; su ausencia es «según mis ajustes». */
class CompletedTasksSyncTest {
    private val key = ByteArray(32) { 3 }

    private fun note(policy: Int) = app.aldune.core.SyncNote(
        id = java.util.UUID.fromString("a1b2c3d4-0010-4000-8000-000000000010"), text = "t", color = "#EBD38B",
        createdAt = java.time.OffsetDateTime.parse("2026-01-01T00:00:00Z"), updatedAt = java.time.OffsetDateTime.parse("2026-01-01T00:00:00Z"),
        state = 0, screenOrigin = "primary", tags = emptyList(), isProtected = false, protectedContent = null, dockPosition = null,
        completedTasks = policy,
    )

    @Test fun `una nota con el ajuste por defecto no escribe el campo, como las de antes`() {
        val json = Json.parseToJsonElement(SyncEnvelopeCodec.noteToJson(note(0))).jsonObject
        assertFalse(json.containsKey("completedTasks"))
    }

    @Test fun `mantener y bajar al final viajan como numero 1 y 2 y se leen igual`() {
        for (policy in listOf(1, 2)) {
            val json = Json.parseToJsonElement(SyncEnvelopeCodec.noteToJson(note(policy))).jsonObject
            assertEquals(policy, json.getValue("completedTasks").jsonPrimitive.content.toInt())
            val read = SyncEnvelopeCodec.decryptNote(SyncEnvelopeCodec.createNote(note(policy), key, "d"), key)
            assertEquals(policy, read.completedTasks)
        }
    }

    @Test fun `un numero desconocido de una version futura se lee como por defecto`() {
        val read = SyncEnvelopeCodec.decryptNote(SyncEnvelopeCodec.createNote(note(9), key, "d"), key)
        assertEquals(0, read.completedTasks)
    }

    @Test fun `la politica manda sobre el ajuste del dispositivo en los dos sentidos y solo la de por defecto oculta`() {
        assertTrue(CompletedTasksPolicy.shouldMoveToEnd(CompletedTasksPolicy.DEFAULT, deviceSetting = true))
        assertFalse(CompletedTasksPolicy.shouldMoveToEnd(CompletedTasksPolicy.DEFAULT, deviceSetting = false))
        assertFalse(CompletedTasksPolicy.shouldMoveToEnd(CompletedTasksPolicy.KEEP_IN_PLACE, deviceSetting = true))
        assertTrue(CompletedTasksPolicy.shouldMoveToEnd(CompletedTasksPolicy.MOVE_TO_END, deviceSetting = false))
        assertTrue(CompletedTasksPolicy.mayHide(CompletedTasksPolicy.DEFAULT, deviceSetting = true))
        assertFalse(CompletedTasksPolicy.mayHide(CompletedTasksPolicy.DEFAULT, deviceSetting = false))
        assertFalse(CompletedTasksPolicy.mayHide(CompletedTasksPolicy.KEEP_IN_PLACE, deviceSetting = true))
        assertFalse(CompletedTasksPolicy.mayHide(CompletedTasksPolicy.MOVE_TO_END, deviceSetting = true))
    }

    @Test fun `la politica cicla por defecto, mantener, al final`() {
        assertEquals(1, CompletedTasksPolicy.next(0))
        assertEquals(2, CompletedTasksPolicy.next(1))
        assertEquals(0, CompletedTasksPolicy.next(2))
        // Un número desconocido cuenta como «por defecto», así que el siguiente es «mantener».
        assertEquals(1, CompletedTasksPolicy.next(42))
    }
}

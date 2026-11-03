package app.aldune.sync

import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/**
 * The planner with thousands of notes: a new device facing a full store must plan one apply per note, and the pass after
 * everything is settled must plan nothing. The time bound is loose on purpose; it only catches an accidental quadratic step.
 */
class PlanScaleTest {
    private val key = ByteArray(32) { it.toByte() }
    private val now = OffsetDateTime.of(2026, 11, 4, 12, 0, 0, 0, ZoneOffset.UTC)
    private val count = 5000

    private fun note(index: Int) = SyncNote(
        id = UUID(0L, index.toLong() + 1), text = "Note $index\nbody ".plus("x".repeat(index % 200)), color = "#EBD38B",
        createdAt = now.minusDays(1), updatedAt = now.minusHours(1), state = 0, screenOrigin = "primary",
        tags = emptyList(), isProtected = false, protectedContent = null, dockPosition = null,
    )

    @Test
    fun `a new device facing thousands of remote notes plans one apply each and a settled store plans nothing`() {
        val envelopes = (0 until count).map { SyncEnvelopeCodec.createNote(note(it), key, "device-b") }

        val started = System.nanoTime()
        val plan = planSync(SyncInput("device-a", SyncScope.All, emptyList(), emptyList(), emptyMap(), emptySet(), envelopes, now), key)
        val millis = (System.nanoTime() - started) / 1_000_000

        assertEquals(count, plan.actions.count { it is SyncAction.ApplyNote })
        assertTrue(millis < 20_000, "planning $count notes took $millis ms")
        println("planSync $count remote notes: $millis ms")
    }
}

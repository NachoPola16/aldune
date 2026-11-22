package app.aldune.sync

import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** La fusión automática en el planificador: dos lados que cambiaron desde la última versión acordada. */
class PlanMergeTest {
    private val key = ByteArray(32) { it.toByte() }
    private val me = "android-pc-a"
    private val other = "device-b"
    private val id = UUID.fromString("00000001-0000-4000-8000-000000000000")
    private val now = OffsetDateTime.of(2026, 11, 22, 12, 0, 0, 0, ZoneOffset.UTC)

    private fun at(minute: Int) = OffsetDateTime.of(2026, 11, 22, 10, minute, 0, 0, ZoneOffset.UTC)

    private fun note(text: String, minute: Int, color: String = "#EBD38B") = SyncNote(
        id = id, text = text, color = color, createdAt = at(0), updatedAt = at(minute), state = 0,
        screenOrigin = "primary", tags = emptyList(), isProtected = false, protectedContent = null, dockPosition = null,
    )

    private fun envelope(n: SyncNote, device: String): SyncEnvelope = SyncEnvelopeCodec.createNote(n, key, device)

    private fun plan(
        local: SyncNote, remote: SyncNote, base: SyncNote?, baseStamp: SyncNote? = base,
    ): SyncPlan = planSync(
        SyncInput(
            deviceId = me, scope = SyncScope.All, localNotes = listOf(LocalNote(local, null)), localTombstones = emptyList(),
            bases = mapOf(id to SyncBase(at(0), other)), excludedIds = emptySet(), remote = listOf(envelope(remote, other)), now = now,
            baseNotes = if (base != null) mapOf(id to (baseStamp ?: base)) else emptyMap(),
        ),
        key,
    )

    @Test fun `dos ediciones que no chocan se fusionan, se suben y se fija la nueva base`() {
        val plan = plan(local = note("a\nb\nx", 20), remote = note("y\na\nb", 10), base = note("a\nb", 0))

        assertEquals(1, plan.merged)
        assertEquals(0, plan.conflictsResolved)
        val merged = plan.actions.filterIsInstance<SyncAction.ApplyMerged>().single().note
        assertEquals("y\na\nb\nx", merged.text)
        val uploaded = plan.actions.filterIsInstance<SyncAction.Upload>().single().envelope
        assertEquals(me, uploaded.deviceId)
        assertTrue(merged.updatedAt.toInstant().isAfter(at(20).toInstant()))
        assertEquals(merged.updatedAt.toInstant(), uploaded.updatedAt.toInstant())
        assertEquals(merged.text, plan.newBaseNotes.getValue(id).text)
        assertNotNull(plan.newBases[id])
    }

    @Test fun `el color de uno y el texto del otro se fusionan`() {
        val plan = plan(local = note("a\nb", 10, color = "#C2D4FF"), remote = note("a\nb\nc", 20), base = note("a\nb", 0))

        assertEquals(1, plan.merged)
        val merged = plan.actions.filterIsInstance<SyncAction.ApplyMerged>().single().note
        assertEquals("a\nb\nc", merged.text)
        assertEquals("#C2D4FF", merged.color)
    }

    @Test fun `sin copia base sigue siendo un conflicto como antes`() {
        val plan = plan(local = note("a\nb\nx", 20), remote = note("y\na\nb", 10), base = null)

        assertEquals(0, plan.merged)
        assertEquals(1, plan.conflictsResolved)
    }

    @Test fun `la misma linea cambiada distinto sigue siendo un conflicto`() {
        val plan = plan(local = note("a\nB1", 20), remote = note("a\nB2", 10), base = note("a\nb", 0))

        assertEquals(0, plan.merged)
        assertEquals(1, plan.conflictsResolved)
    }

    @Test fun `una copia de otra version que la base no se usa`() {
        val stale = note("a\nb", 5) // la base apunta a la versión de las 10:00, la copia es de las 10:05
        val plan = plan(local = note("a\nb\nx", 20), remote = note("y\na\nb", 10), base = note("a\nb", 0), baseStamp = stale)

        assertEquals(0, plan.merged)
        assertEquals(1, plan.conflictsResolved)
    }

    @Test fun `lo acordado se guarda como copia base, tambien si la base ya estaba bien`() {
        val local = note("a\nb", 0)
        val plan = planSync(
            SyncInput(
                me, SyncScope.All, listOf(LocalNote(local, other)), emptyList(),
                mapOf(id to SyncBase(at(0), other)), emptySet(), listOf(envelope(local, other)), now,
            ),
            key,
        )

        assertTrue(plan.newBases.isEmpty())
        assertEquals("a\nb", plan.newBaseNotes.getValue(id).text)
    }
}

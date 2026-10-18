package app.aldune.syncclient

import app.aldune.core.SyncNote
import app.aldune.sync.ConflictVersion
import app.aldune.sync.SyncConflict
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class InMemoryConflictStoreTest {
    private val at = OffsetDateTime.of(2026, 1, 1, 0, 0, 0, 0, ZoneOffset.UTC)

    private fun conflict(id: String) = SyncConflict(
        UUID.fromString(id), at, ConflictVersion(at, DEVICE, false, null), ConflictVersion(at, OTHER_DEVICE, true, null),
    )

    @Test fun `guarda, lee y borra por id de nota`() {
        val store = InMemoryConflictStore()
        store.put(conflict(ID1))
        store.put(conflict(ID2))
        assertEquals(listOf(ID1, ID2), store.all().map { it.noteId.toString() })
        assertEquals(ID2, store.get(ID2)!!.noteId.toString())
        store.delete(ID1)
        assertNull(store.get(ID1))
        assertEquals(1, store.all().size)
    }

    @Test fun `un conflicto nuevo de la misma nota reemplaza al anterior (uno por nota)`() {
        val store = InMemoryConflictStore()
        store.put(conflict(ID1))
        store.put(conflict(ID1).copy(detectedAt = at.plusDays(1)))
        assertEquals(1, store.all().size)
        assertEquals(at.plusDays(1), store.get(ID1)!!.detectedAt)
    }

    @Test fun `borrar uno que no existe no hace nada ni cuenta como escritura`() {
        val store = InMemoryConflictStore()
        store.delete(ID1)
        assertEquals(0, store.writeCount)
    }
}

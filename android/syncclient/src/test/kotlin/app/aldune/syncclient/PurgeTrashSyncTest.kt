package app.aldune.syncclient

import app.aldune.domain.IdFactory
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.sync.InMemorySyncTransport
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNotNull
import kotlin.test.assertNull

/**
 * Purging the bin through the real sync engine with two installations. The tombstone of a purge is dated with the moment the note
 * entered the bin, so a restore + edit made on a device that was offline meanwhile is newer and survives.
 */
class PurgeTrashSyncTest {
    private val remote = InMemorySyncTransport()
    private val clock = TestClock()

    private inner class Phone(val deviceId: String) {
        val notes = InMemoryNoteStore()
        val store = InMemoryLocalSyncStore(notes)
        val service = NoteService(notes, clock, deviceId, IdFactory { UUID.randomUUID().toString() })

        fun sync() = assertIs<SyncOutcome.Success>(runSync(store, remote, TEST_KEY, SyncClientSettings(), deviceId, clock))
    }

    private val a = Phone(DEVICE)
    private val b = Phone(OTHER_DEVICE)

    private fun later() = clock.advanceSeconds(60)

    private fun syncAround() {
        a.sync()
        b.sync()
        a.sync()
    }

    @Test fun `a note restored and edited on another device survives the purge where it was trashed`() {
        val id = a.service.create("original").getOrThrow().id
        later(); syncAround()
        a.service.trash(id)
        later(); syncAround()
        assertEquals(NoteState.TRASHED, b.notes.get(id)!!.state)

        later()
        b.service.restore(id)
        later()
        b.service.edit(id, "edited on B")
        later()
        assertEquals(1, a.service.purgeTrashed(days = 0).getOrThrow())
        later(); syncAround(); syncAround()

        for (phone in listOf(a, b)) {
            val kept = assertNotNull(phone.notes.get(id))
            assertEquals("edited on B", kept.text)
            assertEquals(NoteState.ACTIVE, kept.state)
        }
    }

    @Test fun `the purge still reaches a device that left the note in the bin`() {
        val id = a.service.create("original").getOrThrow().id
        later(); syncAround()
        a.service.trash(id)
        later(); syncAround()

        later()
        assertEquals(1, a.service.purgeTrashed(days = 0).getOrThrow())
        later(); syncAround(); syncAround()

        assertNull(a.notes.get(id))
        assertNull(b.notes.get(id))
    }
}

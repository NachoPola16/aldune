package app.aldune.data

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.Clock
import app.aldune.domain.IdFactory
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteResult
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.domain.Tombstone
import java.time.Instant
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/** Pruebas en el dispositivo: base EN MEMORIA, nunca la real de la app. */
@RunWith(AndroidJUnit4::class)
class RoomNoteStoreTest {
    private lateinit var db: AldunaDatabase
    private val cipher = AesGcmNoteCipher(ByteArray(32) { it.toByte() })
    private lateinit var store: RoomNoteStore

    @Before fun setUp() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, AldunaDatabase::class.java).build()
        store = RoomNoteStore(db, cipher)
    }

    @After fun tearDown() = db.close()

    private fun note(id: String = "n1", text: String = "hola") = Note(
        id = id, text = text, lineEnding = LineEnding.LF, color = "#EBD38B",
        createdAt = Instant.parse("2026-10-17T10:00:00Z"), updatedAt = Instant.parse("2026-10-17T10:30:15.1234567Z"),
        state = NoteState.ACTIVE,
    )

    private fun copyRow(row: NoteEntity, id: String = row.id, state: Int = row.state) = NoteEntity(
        id, row.textCipher, row.nonce, row.tag, row.color, row.createdAt, row.updatedAt, state, row.lineEnding, row.tags,
        row.dockPosition, row.screenOrigin, row.syncAuthorDeviceId,
    )

    @Test fun roundTripKeepsEveryField() {
        val original = note(text = "☐ tarea ñ\n→ 😀 viñeta").copy(
            lineEnding = LineEnding.CRLF, color = "#aabbcc", state = NoteState.ARCHIVED,
            tags = listOf("Trabajo", "con \"comillas\", coma", "ÑANDÚ", "nuevo\nsalto"), dockPosition = 1536.5,
            screenOrigin = "secondary", syncAuthorDeviceId = "device-b",
        )
        store.put(original)
        assertEquals(original, store.get("n1"))
        assertEquals(listOf(original), store.all())
    }

    @Test fun nullablesAndNanosecondsSurvive() {
        val n = note().copy(dockPosition = null, syncAuthorDeviceId = null, updatedAt = Instant.parse("2026-10-17T10:30:15.000000100Z"))
        store.put(n)
        val back = store.get("n1")!!
        assertNull(back.dockPosition)
        assertNull(back.syncAuthorDeviceId)
        assertEquals(n.updatedAt, back.updatedAt)
    }

    @Test fun putReplacesById() {
        store.put(note(text = "uno"))
        store.put(note(text = "dos"))
        assertEquals(1, store.all().size)
        assertEquals("dos", store.get("n1")!!.text)
    }

    @Test fun theTextIsNeverStoredInClear() {
        store.put(note(text = "texto secreto reconocible"))
        db.openHelper.readableDatabase.query("SELECT textCipher, nonce, tag FROM note").use {
            assertTrue(it.moveToFirst())
            assertEquals(12, it.getBlob(1).size)
            assertEquals(16, it.getBlob(2).size)
            val raw = it.getBlob(0) + it.getBlob(1) + it.getBlob(2)
            assertFalse(String(raw, Charsets.ISO_8859_1).contains("secreto"))
        }
        // Y ninguna columna de texto lo contiene tampoco.
        val texts = db.openHelper.readableDatabase.query("SELECT id, color, createdAt, updatedAt, tags, screenOrigin FROM note").use { c ->
            buildString { while (c.moveToNext()) for (i in 0 until c.columnCount) append(c.getString(i)) }
        }
        assertFalse(texts.contains("secreto"))
    }

    @Test fun aRowCopiedToAnotherIdDoesNotDecrypt() {
        store.put(note(id = "n1", text = "mia"))
        db.notes().upsert(copyRow(db.notes().get("n1")!!, id = "n2"))
        try {
            store.get("n2")
            fail("Una fila copiada a otro id no debe descifrar")
        } catch (expected: java.security.GeneralSecurityException) {
        }
        assertEquals("mia", store.get("n1")!!.text)
    }

    @Test fun deleteLeavesALocalTombstoneAndPutDoesNotRemoveIt() {
        store.put(note())
        val at = Instant.parse("2026-10-17T12:00:00.5Z")
        assertTrue(store.delete("n1", at))
        assertNull(store.get("n1"))
        assertEquals(listOf(Tombstone("n1", at, Tombstone.LOCAL_DEVICE)), store.tombstones())
        assertFalse(store.delete("n1", at))
        assertEquals(1, store.tombstones().size)
        store.put(note())
        assertEquals(1, store.tombstones().size)
    }

    @Test fun anUnknownStateCodeIsNotReadAsActive() {
        store.put(note())
        db.notes().upsert(copyRow(db.notes().get("n1")!!, state = 9))
        try {
            store.get("n1")
            fail("Un estado desconocido debe fallar, no leerse como activa")
        } catch (expected: IllegalStateException) {
        }
    }

    @Test fun noteServiceWorksOnTopOfRoom() {
        var now = Instant.parse("2026-10-17T10:00:00Z")
        var counter = 0
        val service = NoteService(store, Clock { now }, "device-a", IdFactory { "id-${counter++}" })

        val created = (service.create("# Lista\n☐ a\n☐ b") as NoteResult.Ok).value
        now = now.plusSeconds(60)
        val edited = (service.edit(created.id, "# Lista\n☒ a\n☐ b") as NoteResult.Ok).value
        assertEquals(now, edited.updatedAt)
        // Abrir y cerrar sin editar no escribe: la fila no cambia.
        val before = db.notes().get(created.id)!!
        service.edit(created.id, "# Lista\n☒ a\n☐ b")
        val after = db.notes().get(created.id)!!
        assertEquals(before.updatedAt, after.updatedAt)
        assertArrayEquals(before.nonce, after.nonce)
        assertEquals(listOf("# Lista\n☒ a\n☐ b"), service.list(NoteState.ACTIVE).map { it.text })
    }
}

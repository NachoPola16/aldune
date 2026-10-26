package app.aldune.data

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.core.NoteState as WireState
import app.aldune.core.SyncNote
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.sync.ConflictVersion
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncConflict
import app.aldune.syncclient.ExpectedVersion
import app.aldune.syncclient.LocalAction
import app.aldune.syncclient.LocalCommit
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/** Dos perfiles sobre la misma base (EN MEMORIA, nunca la real): bases y conflictos aparte, notas compartidas. */
@RunWith(AndroidJUnit4::class)
class RoomMultiProfileTest {
    private lateinit var db: AldunaDatabase
    private lateinit var a: RoomLocalSyncStore
    private lateinit var b: RoomLocalSyncStore
    private lateinit var conflicts: RoomConflictStore

    @Before fun setUp() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, AldunaDatabase::class.java).build()
        val cipher = AesGcmNoteCipher(ByteArray(32) { it.toByte() })
        val notes = RoomNoteStore(db, cipher)
        a = RoomLocalSyncStore(db, notes, cipher, "a")
        b = RoomLocalSyncStore(db, notes, cipher, "b")
        conflicts = RoomConflictStore(db, cipher)
    }

    @After fun tearDown() = db.close()

    private val t0 = OffsetDateTime.parse("2026-10-26T10:00:00Z")
    private val base = SyncBase(t0, "pc01")

    private fun wire(text: String) = SyncNote(
        UUID.fromString(ID), text, "#FFF", t0, t0, WireState.ACTIVE, "", emptyList(), false, null, null,
    )

    private fun conflict(loser: String, detectedAt: OffsetDateTime) = SyncConflict(
        UUID.fromString(ID), detectedAt,
        ConflictVersion(t0.plusSeconds(2), "pc01", false, wire("ganadora")),
        ConflictVersion(t0.plusSeconds(1), "pc02", false, wire(loser)),
    )

    private fun saveConflict(store: RoomLocalSyncStore, loser: String, detectedAt: OffsetDateTime) {
        store.commit(LocalCommit(listOf(LocalAction.SaveConflict(conflict(loser, detectedAt))), emptyMap(), mapOf(ID to ExpectedVersion.Absent)))
    }

    @Test fun basesDeCadaPerfilNoSeMezclan() {
        a.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        assertEquals(setOf(ID), a.snapshot().bases.keys)
        assertTrue(b.snapshot().bases.isEmpty())
    }

    @Test fun olvidarElEstadoDeUnPerfilNoTocaElOtro() {
        a.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        b.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        a.forgetSyncState()
        assertTrue(a.snapshot().bases.isEmpty())
        assertEquals(setOf(ID), b.snapshot().bases.keys)
    }

    @Test fun descartarTodoLimpiaLasBasesDeTodosLosPerfiles() {
        a.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        b.commit(LocalCommit(emptyList(), mapOf(ID to base), emptyMap()))
        a.discardAllLocal()
        assertTrue(a.snapshot().bases.isEmpty() && b.snapshot().bases.isEmpty())
    }

    @Test fun laMismaNotaConConflictoEnDosPerfilesSeResuelveDeUnoEnUno() {
        // Sin pérdida silenciosa: resolver el que se ve deja el del otro perfil, que aparece a continuación.
        saveConflict(a, "antiguo", t0.plusMinutes(1))
        saveConflict(b, "reciente", t0.plusMinutes(2).withOffsetSameInstant(ZoneOffset.UTC))

        assertEquals(setOf(ID), a.conflictedNoteIds())
        assertEquals(setOf(ID), b.conflictedNoteIds())
        assertEquals(1, conflicts.all().size)
        assertEquals("reciente", conflicts.get(ID)!!.losing.note!!.text)

        conflicts.delete(ID)
        assertEquals("antiguo", conflicts.get(ID)!!.losing.note!!.text)
        assertTrue(b.conflictedNoteIds().isEmpty())

        conflicts.delete(ID)
        assertNull(conflicts.get(ID))
    }

    private companion object {
        const val ID = "11111111-1111-1111-1111-111111111111"
    }
}

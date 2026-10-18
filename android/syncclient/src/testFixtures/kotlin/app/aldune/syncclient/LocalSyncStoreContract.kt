package app.aldune.syncclient

import app.aldune.core.NoteState as WireState
import app.aldune.core.SyncNote
import app.aldune.domain.Clock
import app.aldune.domain.IdFactory
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.domain.NoteStore
import app.aldune.domain.Tombstone
import app.aldune.sync.ConflictVersion
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncConflict
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import org.junit.Before
import org.junit.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNull
import kotlin.test.assertTrue

/** Las dos mitades de un almacén local: la de la sync y la de las notas (en Room, la misma base; en memoria, el mismo objeto). */
interface StoreHarness {
    val sync: LocalSyncStore
    val notes: NoteStore
}

/**
 * Batería del contrato [LocalSyncStore]. Cada implementación hereda esta clase y da su [newHarness] (la de memoria en `src/test`;
 * la de Room, en las pruebas instrumentadas de `app`). Un test = un almacén nuevo y vacío.
 */
abstract class LocalSyncStoreContract {
    protected abstract fun newHarness(): StoreHarness

    private val id1 = "11111111-1111-1111-1111-111111111111"
    private val id2 = "22222222-2222-2222-2222-222222222222"
    private val id3 = "33333333-3333-3333-3333-333333333333"
    private val t0 = Instant.parse("2026-03-01T10:00:00Z")

    private class MutableClock(var current: Instant) : Clock {
        override fun now(): Instant = current
    }

    private lateinit var h: StoreHarness
    private lateinit var clock: MutableClock
    private lateinit var service: NoteService

    // Los nombres de los tests van en camelCase y sin espacios: el DEX de minSdk 26 no admite espacios en un nombre de método.
    // JUnit 4 a propósito: es lo que ejecuta AndroidJUnit4 en el dispositivo, y en la JVM lo corre el motor vintage de JUnit 5.
    @Before fun setUp() {
        h = newHarness()
        clock = MutableClock(t0.plus(Duration.ofHours(1)))
        service = NoteService(h.notes, clock, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", IdFactory { UUID.randomUUID().toString() })
    }

    private fun note(id: String, text: String = "hola", author: String? = null, state: NoteState = NoteState.ACTIVE, updatedAt: Instant = t0) =
        Note(id, text, LineEnding.LF, "#FFDDD4", t0, updatedAt, state, syncAuthorDeviceId = author)

    private fun wireNote(id: String) = SyncNote(
        UUID.fromString(id), "x", "#FFF", OffsetDateTime.ofInstant(t0, ZoneOffset.UTC), OffsetDateTime.ofInstant(t0, ZoneOffset.UTC),
        WireState.ACTIVE, "", emptyList(), false, null, null,
    )

    private fun base(deviceId: String = "pc01", at: Instant = t0) = SyncBase(OffsetDateTime.ofInstant(at, ZoneOffset.UTC), deviceId)

    private fun conflict(id: String, loser: String = "perdedora") = SyncConflict(
        UUID.fromString(id), OffsetDateTime.ofInstant(t0, ZoneOffset.UTC),
        ConflictVersion(OffsetDateTime.ofInstant(t0.plusSeconds(2), ZoneOffset.UTC), "pc01", false, wireNote(id)),
        ConflictVersion(OffsetDateTime.ofInstant(t0.plusSeconds(1), ZoneOffset.UTC), "pc02", false, wireNote(id).copy(text = loser)),
    )

    /** El commit que haría `runSync`: las versiones esperadas salen de la foto actual. */
    private fun commitOf(vararg actions: LocalAction, bases: Map<String, SyncBase> = emptyMap(), snapshot: LocalSnapshot = h.sync.snapshot()) =
        LocalCommit(actions.toList(), bases, actions.map { it.noteId }.toSet().associateWith { ExpectedVersion.of(snapshot, it) })

    // --- foto ---

    @Test fun unAlmacenNuevoEstaVacio() {
        val snapshot = h.sync.snapshot()
        assertTrue(snapshot.notes.isEmpty() && snapshot.tombstones.isEmpty() && snapshot.bases.isEmpty())
        assertTrue(h.sync.conflictedNoteIds().isEmpty())
    }

    @Test fun laFotoTraeLasNotasLasLapidasLocalesYLasBases() {
        h.notes.put(note(id1))
        h.notes.put(note(id2))
        h.notes.delete(id2, t0)
        h.sync.commit(LocalCommit(emptyList(), mapOf(id3 to base()), emptyMap()))

        val snapshot = h.sync.snapshot()
        assertEquals(listOf(id1), snapshot.notes.map { it.id })
        assertEquals(listOf(Tombstone(id2, t0, Tombstone.LOCAL_DEVICE)), snapshot.tombstones)
        assertEquals(mapOf(id3 to base()), snapshot.bases)
    }

    // --- acciones locales ---

    @Test fun applynoteInsertaLaNotaConElAutorRemoto() {
        val remote = note(id1, "del pc", author = "pc01")
        val report = h.sync.commit(commitOf(LocalAction.ApplyNote(remote)))
        assertEquals(remote, h.notes.get(id1))
        assertEquals(1, report.appliedNotes)
        assertTrue(report.skippedConcurrent.isEmpty())
    }

    @Test fun applynoteReemplazaLaNotaExistente() {
        h.notes.put(note(id1, "vieja"))
        h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, "nueva", author = "pc01", updatedAt = t0.plusSeconds(9)))))
        assertEquals("nueva", h.notes.get(id1)!!.text)
        assertEquals("pc01", h.notes.get(id1)!!.syncAuthorDeviceId)
    }

    @Test fun applynoteQuitaLaLapidaLocalDeEseId() {
        h.notes.put(note(id1))
        h.notes.delete(id1, t0)
        assertEquals(1, h.notes.tombstones().size)
        h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, author = "pc01"))))
        assertEquals(0, h.notes.tombstones().size)
        assertEquals(id1, h.notes.get(id1)!!.id)
    }

    @Test fun applynoteConservaUnaNotaProtegidaEntera() {
        val json = """{"version":1,"salt":"s","cipherText":"c","nonce":"n","tag":"t"}"""
        val protected = note(id1, "", author = "pc01").copy(isProtected = true, protectedContentJson = json)
        h.sync.commit(commitOf(LocalAction.ApplyNote(protected)))
        assertEquals(protected, h.notes.get(id1))
    }

    @Test fun applytombstoneBorraLaNotaYGuardaLaLapidaConElAutorRemoto() {
        h.notes.put(note(id1))
        h.sync.commit(commitOf(LocalAction.ApplyTombstone(id1, t0.plusSeconds(5), "pc01")))
        assertNull(h.notes.get(id1))
        assertEquals(listOf(Tombstone(id1, t0.plusSeconds(5), "pc01")), h.notes.tombstones())
    }

    @Test fun applytombstoneDeUnaNotaQueNoEstabaTambienDejaLaLapida() {
        val report = h.sync.commit(commitOf(LocalAction.ApplyTombstone(id1, t0, "pc01")))
        assertEquals(listOf(Tombstone(id1, t0, "pc01")), h.notes.tombstones())
        assertEquals(1, report.appliedTombstones)
    }

    @Test fun trashnoteMandaLaNotaALaPapeleraConLaFechaDadaYAutorDeEsteEquipo() {
        h.notes.put(note(id1, author = "pc01"))
        val report = h.sync.commit(commitOf(LocalAction.TrashNote(id1, t0.plusSeconds(30))))
        val trashed = h.notes.get(id1)!!
        assertEquals(NoteState.TRASHED, trashed.state)
        assertEquals(t0.plusSeconds(30), trashed.updatedAt)
        assertNull(trashed.syncAuthorDeviceId)
        assertEquals("hola", trashed.text)
        assertEquals(1, report.trashed)
    }

    @Test fun trashnoteDeUnaNotaQueNoExisteNoHaceNada() {
        val report = h.sync.commit(commitOf(LocalAction.TrashNote(id1, t0)))
        assertNull(h.notes.get(id1))
        assertEquals(0, report.trashed)
    }

    @Test fun saveconflictGuardaLaVersionPerdedoraYUnSegundoConflictoDeLaMismaNotaReemplazaAlPrimero() {
        h.notes.put(note(id1))
        h.sync.commit(commitOf(LocalAction.SaveConflict(conflict(id1, "primera"))))
        assertEquals(setOf(id1), h.sync.conflictedNoteIds())
        h.sync.commit(commitOf(LocalAction.SaveConflict(conflict(id1, "segunda"))))
        assertEquals(setOf(id1), h.sync.conflictedNoteIds())
        assertEquals(1, h.sync.commit(commitOf(LocalAction.SaveConflict(conflict(id1)))).conflictsSaved)
    }

    @Test fun lasAccionesSeAplicanEnElOrdenDelPlan() {
        h.notes.put(note(id1))
        h.sync.commit(commitOf(
            LocalAction.ApplyNote(note(id1, "a", author = "pc01")),
            LocalAction.TrashNote(id1, t0.plusSeconds(1)),
        ))
        assertEquals(NoteState.TRASHED, h.notes.get(id1)!!.state)
        assertEquals("a", h.notes.get(id1)!!.text)
    }

    // --- bases ---

    @Test fun lasBasesSeFijanYUnaBaseNuevaReemplazaALaAnterior() {
        h.sync.commit(LocalCommit(emptyList(), mapOf(id1 to base("pc01")), emptyMap()))
        h.sync.commit(LocalCommit(emptyList(), mapOf(id1 to base("pc02"), id2 to base("pc01")), emptyMap()))
        assertEquals(mapOf(id1 to base("pc02"), id2 to base("pc01")), h.sync.snapshot().bases)
    }

    @Test fun lasBasesDeNotasSinAccionesLocalesSeFijanSiempreAunqueLaNotaHayaCambiado() {
        h.notes.put(note(id1))
        val snapshot = h.sync.snapshot()
        service.edit(id1, "editada mientras tanto")
        val report = h.sync.commit(LocalCommit(emptyList(), mapOf(id1 to base()), emptyMap()))
        assertEquals(mapOf(id1 to base()), h.sync.snapshot().bases)
        assertTrue(report.skippedConcurrent.isEmpty())
        assertEquals(1, report.basesSet)
        assertEquals(1, snapshot.notes.size)
    }

    @Test fun unCommitVacioNoCambiaNada() {
        h.notes.put(note(id1))
        val before = h.sync.snapshot()
        assertEquals(CommitReport(), h.sync.commit(LocalCommit(emptyList(), emptyMap(), emptyMap())))
        assertEquals(before, h.sync.snapshot())
    }

    @Test fun unCommitConUnaAccionSinVersionEsperadaEsUnFalloDePrograma() {
        assertFailsWith<IllegalArgumentException> {
            LocalCommit(listOf(LocalAction.TrashNote(id1, t0)), emptyMap(), emptyMap())
        }
    }

    // --- concurrencia optimista ---

    @Test fun unaEdicionDeTextoEntreLaFotoYElCommitSaltaLaAccionYNoFijaLaBase() {
        h.notes.put(note(id1, "original"))
        val snapshot = h.sync.snapshot()
        service.edit(id1, "lo que el usuario escribio mientras tanto")

        val report = h.sync.commit(commitOf(
            LocalAction.ApplyNote(note(id1, "del pc", author = "pc01", updatedAt = t0.plusSeconds(5))),
            bases = mapOf(id1 to base()), snapshot = snapshot,
        ))
        assertEquals("lo que el usuario escribio mientras tanto", h.notes.get(id1)!!.text)
        assertEquals(setOf(id1), report.skippedConcurrent)
        assertEquals(0, report.appliedNotes)
        assertEquals(0, report.basesSet)
        assertNull(h.sync.snapshot().bases[id1])
    }

    @Test fun unCambioDeColorODeEstadoTambienCuentaComoEdicionConcurrente() {
        h.notes.put(note(id1))
        h.notes.put(note(id2))
        val snapshot = h.sync.snapshot()
        service.setColor(id1, "#000000")
        service.archive(id2)

        val report = h.sync.commit(commitOf(
            LocalAction.ApplyNote(note(id1, "a", author = "pc01")), LocalAction.TrashNote(id2, t0), snapshot = snapshot,
        ))
        assertEquals(setOf(id1, id2), report.skippedConcurrent)
        assertEquals("#000000", h.notes.get(id1)!!.color)
        assertEquals(NoteState.ARCHIVED, h.notes.get(id2)!!.state)
    }

    @Test fun soloSeSaltaLaNotaTocadaYLasDemasDelMismoCommitSeAplican() {
        h.notes.put(note(id1))
        h.notes.put(note(id2))
        val snapshot = h.sync.snapshot()
        service.edit(id1, "mientras tanto")

        val report = h.sync.commit(commitOf(
            LocalAction.ApplyNote(note(id1, "del pc", author = "pc01")),
            LocalAction.ApplyNote(note(id2, "del pc 2", author = "pc01")),
            LocalAction.ApplyNote(note(id3, "nueva", author = "pc01")),
            bases = mapOf(id1 to base(), id2 to base(), id3 to base()), snapshot = snapshot,
        ))
        assertEquals(setOf(id1), report.skippedConcurrent)
        assertEquals(2, report.appliedNotes)
        assertEquals("del pc 2", h.notes.get(id2)!!.text)
        assertEquals("nueva", h.notes.get(id3)!!.text)
        assertEquals(setOf(id2, id3), h.sync.snapshot().bases.keys)
    }

    @Test fun unConflictoDeUnaNotaEditadaMientrasTantoTampocoSeGuarda() {
        h.notes.put(note(id1))
        val snapshot = h.sync.snapshot()
        service.edit(id1, "mientras tanto")
        val report = h.sync.commit(commitOf(LocalAction.SaveConflict(conflict(id1)), snapshot = snapshot))
        assertTrue(h.sync.conflictedNoteIds().isEmpty())
        assertEquals(setOf(id1), report.skippedConcurrent)
    }

    @Test fun borrarLaNotaParaSiempreEntreLaFotoYElCommitSaltaSuAccion() {
        h.notes.put(note(id1, state = NoteState.TRASHED))
        val snapshot = h.sync.snapshot()
        service.deleteForever(id1)
        val report = h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, author = "pc01")), snapshot = snapshot))
        assertNull(h.notes.get(id1))
        assertEquals(setOf(id1), report.skippedConcurrent)
    }

    @Test fun unaNotaCreadaEntreLaFotoYElCommitConElMismoIdSaltaLaAccion() {
        val snapshot = h.sync.snapshot()
        h.notes.put(note(id1, "creada mientras tanto"))
        val report = h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, "del pc", author = "pc01")), snapshot = snapshot))
        assertEquals("creada mientras tanto", h.notes.get(id1)!!.text)
        assertEquals(setOf(id1), report.skippedConcurrent)
    }

    @Test fun unaNotaSinCambiosDesdeLaFotoNoSeSaltaAunqueTengaAutorRemoto() {
        h.notes.put(note(id1, author = "pc01"))
        val report = h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, "nueva", author = "pc02", updatedAt = t0.plusSeconds(3)))))
        assertTrue(report.skippedConcurrent.isEmpty())
        assertEquals("pc02", h.notes.get(id1)!!.syncAuthorDeviceId)
    }

    @Test fun unaNotaCuyaFotoTeniaLapidaYAhoraLaTieneOtraSeAplicaPeroSiSeQuitoSeSalta() {
        h.notes.put(note(id1))
        h.notes.delete(id1, t0)
        val snapshot = h.sync.snapshot()
        // Sigue habiendo lápida: la acción entra.
        assertTrue(h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, author = "pc01")), snapshot = snapshot)).skippedConcurrent.isEmpty())
        // Ahora la nota existe (ya no hay lápida): con la foto vieja se salta.
        val stale = h.sync.commit(commitOf(LocalAction.ApplyNote(note(id1, "otra", author = "pc02")), snapshot = snapshot))
        assertEquals(setOf(id1), stale.skippedConcurrent)
        assertEquals("hola", h.notes.get(id1)!!.text)
    }

    // --- primera conexión y desconexión ---

    @Test fun descartarLoLocalBorraTodoSinCrearLapidas() {
        h.notes.put(note(id1))
        h.notes.put(note(id2))
        h.notes.delete(id2, t0)
        h.sync.commit(commitOf(LocalAction.SaveConflict(conflict(id1)), bases = mapOf(id1 to base())))

        h.sync.discardAllLocal()

        val snapshot = h.sync.snapshot()
        assertTrue(snapshot.notes.isEmpty())
        assertTrue(snapshot.tombstones.isEmpty())
        assertTrue(snapshot.bases.isEmpty())
        assertTrue(h.sync.conflictedNoteIds().isEmpty())
    }

    @Test fun olvidarElEstadoDeSyncBorraBasesYConflictosYDejaNotasYLapidas() {
        h.notes.put(note(id1))
        h.notes.put(note(id2))
        h.notes.delete(id2, t0)
        h.sync.commit(commitOf(LocalAction.SaveConflict(conflict(id1)), bases = mapOf(id1 to base())))

        h.sync.forgetSyncState()

        val snapshot = h.sync.snapshot()
        assertEquals(listOf(id1), snapshot.notes.map { it.id })
        assertEquals(listOf(id2), snapshot.tombstones.map { it.noteId })
        assertTrue(snapshot.bases.isEmpty())
        assertTrue(h.sync.conflictedNoteIds().isEmpty())
    }
}

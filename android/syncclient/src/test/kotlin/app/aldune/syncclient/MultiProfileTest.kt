package app.aldune.syncclient

import app.aldune.core.SyncEnvelope
import app.aldune.domain.IdFactory
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.NoteService
import app.aldune.sync.InMemorySyncTransport
import app.aldune.sync.SyncScope
import app.aldune.sync.SyncTransport
import java.io.IOException
import java.net.UnknownHostException
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class MultiProfileTest {
    private val clock = TestClock(T0.plusSeconds(3600))
    private val notes = InMemoryNoteStore()
    private val service = NoteService(notes, clock, DEVICE, IdFactory { UUID.randomUUID().toString() })

    // Dos «perfiles»: dos vistas de sync (bases y conflictos propios) sobre las MISMAS notas, cada una con su almacén remoto.
    private val personal = InMemoryLocalSyncStore(notes)
    private val family = InMemoryLocalSyncStore(notes)
    private val personalRemote = InMemorySyncTransport()
    private val familyRemote = InMemorySyncTransport()

    private fun pass(store: InMemoryLocalSyncStore, remote: SyncTransport, scope: SyncScope = SyncScope.All) =
        runSync(store, remote, TEST_KEY, SyncClientSettings(scope), DEVICE, clock)

    // --- vaultFileName ---

    @Test fun `el perfil heredado conserva el fichero de secretos de siempre`() {
        assertEquals("syncsecrets.bin", vaultFileName(LEGACY_PROFILE_ID))
    }

    @Test fun `un perfil nuevo tiene su propio fichero`() {
        assertEquals("syncsecrets-0b1c-22.bin", vaultFileName("0b1c-22"))
    }

    @Test fun `un id con ruta o raro no llega a ser nombre de fichero`() {
        assertFailsWith<IllegalArgumentException> { vaultFileName("../x") }
        assertFailsWith<IllegalArgumentException> { vaultFileName("") }
    }

    // --- dos perfiles sobre las mismas notas ---

    @Test fun `una nota compartida sube a los dos almacenes y las bases no se mezclan`() {
        val shared = service.create("lista de la compra").getOrThrow()
        service.create("solo mia").getOrThrow()

        assertEquals(2, assertIs<SyncOutcome.Success>(pass(personal, personalRemote)).uploaded)
        val onlyShared = SyncScope.SelectedNotes(setOf(UUID.fromString(shared.id)))
        assertEquals(1, assertIs<SyncOutcome.Success>(pass(family, familyRemote, onlyShared)).uploaded)

        assertEquals(2, personal.bases.size)
        assertEquals(setOf(shared.id), family.bases.keys)
        assertEquals(1, familyRemote.store.size)
        assertEquals(2, personalRemote.store.size)
    }

    @Test fun `si un perfil falla el otro sincroniza igual`() {
        service.create("hola").getOrThrow()
        val broken = object : SyncTransport {
            override fun readAll(): List<SyncEnvelope> = throw UnknownHostException("sin red")
            override fun write(envelope: SyncEnvelope) = throw IOException("no")
            override fun delete(noteId: UUID) = throw IOException("no")
        }
        val outcomes = listOf(pass(personal, broken), pass(family, familyRemote))

        assertIs<SyncOutcome.Failure>(combineOutcomes(outcomes))
        assertTrue(personal.bases.isEmpty())
        assertEquals(1, family.bases.size)
    }

    @Test fun `borrar una nota compartida lleva el borrado firmado a los dos almacenes`() {
        val shared = service.create("lista").getOrThrow()
        val scope = SyncScope.SelectedNotes(setOf(UUID.fromString(shared.id)))
        pass(personal, personalRemote); pass(family, familyRemote, scope)

        clock.advanceSeconds(60)
        service.trash(shared.id).getOrThrow(); service.deleteForever(shared.id).getOrThrow()
        pass(personal, personalRemote); pass(family, familyRemote, scope)

        assertTrue(personalRemote.store.getValue(UUID.fromString(shared.id)).tombstone)
        assertTrue(familyRemote.store.getValue(UUID.fromString(shared.id)).tombstone)
    }

    @Test fun `desconectar un perfil deja intactos el otro y las notas`() {
        service.create("hola").getOrThrow()
        pass(personal, personalRemote); pass(family, familyRemote)

        family.forgetSyncState()

        assertTrue(family.bases.isEmpty())
        assertEquals(1, personal.bases.size)
        assertEquals(1, notes.all().size)
    }

    // --- primera conexión de un perfil adicional ---

    @Test fun `un perfil adicional nunca pregunta ni descarta aunque haya notas`() {
        service.create("personal").getOrThrow()
        val decision = firstConnectDecision(family.snapshot(), isFirstProfile = false)

        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = false), decision)
        completeFirstConnect(family, decision)
        assertEquals(1, notes.all().size)
    }

    @Test fun `un perfil adicional no descarta lapidas aunque no haya notas`() {
        val gone = service.create("x").getOrThrow()
        service.trash(gone.id).getOrThrow(); service.deleteForever(gone.id).getOrThrow()

        val decision = firstConnectDecision(family.snapshot(), isFirstProfile = false)

        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = false), decision)
        completeFirstConnect(family, decision)
        assertEquals(1, notes.tombstones().size)
    }

    @Test fun `el primer perfil sigue preguntando como siempre`() {
        service.create("personal").getOrThrow()
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(personal.snapshot()))
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(personal.snapshot(), isFirstProfile = true))
    }

    // --- combineOutcomes ---

    @Test fun `un solo resultado se queda igual`() {
        val one = SyncOutcome.Success(uploaded = 2, received = 1, conflicts = 3)
        assertEquals(one, combineOutcomes(listOf(one)))
    }

    @Test fun `varios exitos suman`() {
        val sum = combineOutcomes(listOf(SyncOutcome.Success(uploaded = 1, received = 2), SyncOutcome.Success(uploaded = 3, trashed = 1, skippedConcurrent = 1)))
        assertEquals(SyncOutcome.Success(uploaded = 4, received = 2, trashed = 1, skippedConcurrent = 1), sum)
    }

    @Test fun `sin perfiles es un exito vacio`() {
        assertEquals(SyncOutcome.Success(), combineOutcomes(emptyList()))
    }

    @Test fun `un fallo gana a los exitos y es el primero`() {
        val first = SyncFailure(SyncFailureKind.OFFLINE)
        val second = SyncFailure(SyncFailureKind.UNAUTHORIZED)
        val combined = combineOutcomes(listOf(SyncOutcome.Success(uploaded = 1), SyncOutcome.Failure(first), SyncOutcome.Failure(second)))
        assertEquals(SyncOutcome.Failure(first), combined)
    }

    // --- aggregateStatus ---

    private val t1 = Instant.parse("2026-03-01T10:00:00Z")
    private val t2 = Instant.parse("2026-03-01T11:00:00Z")

    @Test fun `sin perfiles el estado esta vacio`() {
        assertEquals(SyncStatus(), aggregateStatus(emptyList()))
    }

    @Test fun `la ultima sync buena es la del perfil mas atrasado`() {
        val status = aggregateStatus(listOf(SyncStatus(lastSuccessAt = t2, lastAttemptAt = t2), SyncStatus(lastSuccessAt = t1, lastAttemptAt = t1)))
        assertEquals(t1, status.lastSuccessAt)
        assertEquals(t2, status.lastAttemptAt)
    }

    @Test fun `si un perfil nunca sincronizo el conjunto tampoco`() {
        assertNull(aggregateStatus(listOf(SyncStatus(lastSuccessAt = t2), SyncStatus())).lastSuccessAt)
    }

    @Test fun `ejecutando y fallo se propagan`() {
        val failure = SyncFailure(SyncFailureKind.SERVER_ERROR)
        val status = aggregateStatus(listOf(SyncStatus(lastSuccessAt = t1), SyncStatus(lastSuccessAt = t1, lastFailure = failure, running = true)))
        assertTrue(status.running)
        assertEquals(failure, status.lastFailure)
    }
}

package app.aldune.sync

import app.aldune.core.ProtectedNoteContent
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/**
 * Casos que no salen de los vectores porque el almacén de carpeta guarda un fichero por id (varias
 * versiones, un borrado sin firma junto a uno firmado) o porque Core los monta con otra pieza (E21).
 */
class EngineTests {
    private val key = ByteArray(32) { it.toByte() }
    private val otherKey = ByteArray(32) { (0xa0 + it).toByte() }
    private val me = "android-pc-a"
    private val id = UUID.fromString("00000001-0000-4000-8000-000000000000")
    private val now = OffsetDateTime.of(2026, 10, 17, 12, 0, 0, 0, ZoneOffset.UTC)

    private fun at(minute: Int) = OffsetDateTime.of(2024, 5, 1, 10, minute, 0, 0, ZoneOffset.UTC)

    private fun note(text: String, minute: Int, protectedContent: ProtectedNoteContent? = null, noteId: UUID = id) = SyncNote(
        id = noteId, text = text, color = "#EBD38B", createdAt = at(0), updatedAt = at(minute), state = 0,
        screenOrigin = "primary", tags = emptyList(), isProtected = protectedContent != null,
        protectedContent = protectedContent, dockPosition = null,
    )

    private fun envelope(n: SyncNote, device: String, k: ByteArray = key) = SyncEnvelopeCodec.createNote(n, k, device)
    private fun tombstone(minute: Int, device: String, k: ByteArray) = SyncEnvelopeCodec.createTombstone(id, at(minute), k, device)

    private fun input(
        local: List<LocalNote> = emptyList(),
        remote: List<SyncEnvelope> = emptyList(),
        excluded: Set<UUID> = emptySet(),
        bases: Map<UUID, SyncBase> = emptyMap(),
        tombstones: List<LocalTombstone> = emptyList(),
        at: OffsetDateTime = now,
    ) = SyncInput(me, SyncScope.All, local, tombstones, bases, excluded, remote, at)

    @Test
    fun `varias versiones del mismo id en el almacen, gana la mayor`() {
        val plan = planSync(input(remote = listOf(
            envelope(note("v1", 10), "device-b"),
            envelope(note("v3", 30), "device-b"),
            envelope(note("v2", 20), "device-b"),
        )), key)
        val apply = plan.actions.single() as SyncAction.ApplyNote
        assertEquals("v3", apply.note.text)
    }

    @Test
    fun `varias versiones con la misma fecha desempata el deviceId mayor`() {
        val plan = planSync(input(remote = listOf(
            envelope(note("de-z", 10), "device-z"),
            envelope(note("de-a", 10), "device-a"),
        )), key)
        assertEquals("de-z", (plan.actions.single() as SyncAction.ApplyNote).note.text)
        assertEquals("device-z", (plan.actions.single() as SyncAction.ApplyNote).authorDeviceId)
    }

    @Test
    fun `con alcance por etiqueta, un borrado firmado no alcanza a una nota fuera del alcance`() {
        // La nota local no lleva la etiqueta: quien tenga el código del perfil no puede borrarla aunque el borrado sea auténtico.
        val local = listOf(LocalNote(note("sin etiqueta", 20), null))
        val deletion = tombstone(30, "device-b", key)
        val scoped = SyncInput(me, SyncScope.Tag("work"), local, emptyList(), emptyMap(), emptySet(), listOf(deletion), now)
        val plan = planSync(scoped, key)
        assertTrue(plan.actions.none { it is SyncAction.ApplyTombstone }, "no debe borrar: ${plan.actions}")
    }

    @Test
    fun `E14 un borrado sin firma junto a uno firmado del mismo id se ignora`() {
        val signedNote = envelope(note("firmada", 10), "device-b")
        val fake = tombstone(20, "device-evil", otherKey)
        // Sin la firmada, el sin firma mandaría la nota local a la papelera; con ella, no toca nada.
        val local = listOf(LocalNote(note("firmada", 10), "device-b"))
        val plan = planSync(input(local = local, remote = listOf(fake, signedNote)), key)
        assertTrue(plan.actions.isEmpty(), "no debe haber acciones: ${plan.actions}")
        assertEquals(0, plan.downloaded)
    }

    @Test
    fun `E21 una nota excluida ni sube ni la toca lo que llega de fuera`() {
        val local = listOf(LocalNote(note("solo de aqui", 10), null))
        val plan = planSync(
            input(
                local = local,
                excluded = setOf(id),
                remote = listOf(envelope(note("copia vieja", 20), "device-b"), tombstone(30, "device-evil", otherKey)),
            ),
            key,
        )
        assertTrue(plan.actions.isEmpty(), "no debe haber acciones: ${plan.actions}")
        assertTrue(plan.newBases.isEmpty())

        val signedDelete = planSync(input(local = local, excluded = setOf(id), remote = listOf(tombstone(30, "device-b", key))), key)
        assertTrue(signedDelete.actions.isEmpty(), "ni un borrado firmado la borra: ${signedDelete.actions}")
    }

    @Test
    fun `una protegida desbloqueada sube siempre con text vacio`() {
        val content = ProtectedNoteContent(salt = "c2FsdA==", cipherText = "Y2lwaGVy", nonce = "bm9uY2U=", tag = "dGFn")
        val unlocked = LocalNote(note("texto en claro en memoria", 10, content), null)
        val plan = planSync(input(local = listOf(unlocked)), key)
        val upload = (plan.actions.single() as SyncAction.Upload).envelope
        val sent = SyncEnvelopeCodec.decryptNote(upload, key)
        assertEquals("", sent.text)
        assertEquals(content, sent.protectedContent)
    }

    @Test
    fun `un borrado recibido sube con su autor y uno hecho aqui con el deviceId propio`() {
        val received = planSync(input(tombstones = listOf(LocalTombstone(id, at(10), "device-b"))), key)
        assertEquals("device-b", (received.actions.single() as SyncAction.Upload).envelope.deviceId)
        val own = planSync(input(tombstones = listOf(LocalTombstone(id, at(10), "local"))), key)
        assertEquals(me, (own.actions.single() as SyncAction.Upload).envelope.deviceId)
        val blank = planSync(input(tombstones = listOf(LocalTombstone(id, at(10), ""))), key)
        assertEquals(me, (blank.actions.single() as SyncAction.Upload).envelope.deviceId)
    }

    @Test
    fun `un borrado sin firma con fecha futura se quita del almacen aunque no haya nota local`() {
        val future = SyncEnvelopeCodec.createTombstone(id, now.plusDays(2), otherKey, "device-evil")
        val plan = planSync(input(remote = listOf(future, envelope(note("otra", 10, noteId = UUID(0, 9)), "device-b"))), key)
        assertTrue(plan.actions.contains(SyncAction.DeleteRemote(id)), "debe borrar el objeto falso: ${plan.actions}")
    }

    @Test
    fun `un objeto con base64 roto cuenta como ilegible y no tumba la pasada`() {
        val broken = envelope(note("rota", 10), "device-b").copy(cipherText = "!!!no es base64!!!")
        val good = envelope(note("buena", 20, noteId = UUID(0, 2)), "device-b")
        val plan = planSync(input(remote = listOf(broken, good)), key)
        assertNull(plan.error)
        assertEquals("buena", (plan.actions.single() as SyncAction.ApplyNote).note.text)

        // Y si es lo único que hay, es «nada descifra».
        assertEquals(NOTHING_READABLE_ERROR, planSync(input(remote = listOf(broken)), key).error)
    }

    @Test
    fun `un almacen vacio no es error`() {
        val plan = planSync(input(local = listOf(LocalNote(note("sola", 10), null))), key)
        assertNull(plan.error)
        assertEquals(1, plan.uploaded)
    }

    @Test
    fun `un reloj con mas precision que NET no estropea la nota mandada a la papelera`() {
        val precise = now.withNano(123_456_789)
        val local = listOf(LocalNote(note("x", 10), "device-b"))
        val plan = planSync(input(local = local, remote = listOf(tombstone(20, "device-evil", otherKey)), at = precise), key)
        val upload = plan.actions.filterIsInstance<SyncAction.Upload>().single().envelope
        // Si la fecha de la cabecera y la del contenido difirieran, esto lanzaría SyncFormatException.
        assertEquals("x", SyncEnvelopeCodec.decryptNote(upload, key).text)
        assertEquals(123_456_700, upload.updatedAt.nano)
    }

    @Test
    fun `un plan con error no escribe nada`() {
        val local = InMemoryLocalStore(listOf(LocalNote(note("a", 10), null)))
        val transport = InMemorySyncTransport(listOf(envelope(note("ajena", 10), "device-b", otherKey)))
        val plan = planSync(input(local = local.notes.values.toList(), remote = transport.readAll()), key)
        val result = execute(plan, local, transport)
        assertEquals(NOTHING_READABLE_ERROR, result.error)
        assertTrue(transport.written.isEmpty())
        assertTrue(transport.deleted.isEmpty())
        assertEquals(0, local.setBasesCalls)
        assertTrue(local.conflicts.isEmpty())
    }

    @Test
    fun `una segunda pasada tras la primera no hace nada`() {
        // Idempotencia: lo que una pasada deja es lo que la siguiente ve ya acordado.
        val local = InMemoryLocalStore(listOf(LocalNote(note("local", 20), null), LocalNote(note("otra", 20, noteId = UUID(0, 3)), null)))
        val transport = InMemorySyncTransport(listOf(envelope(note("remota", 10, noteId = UUID(0, 4)), "device-b")))
        fun pass() = execute(planSync(
            input(local = local.notes.values.toList(), remote = transport.readAll(), bases = local.bases, tombstones = local.tombstones.values.toList()),
            key,
        ), local, transport)

        val first = pass()
        assertEquals(2, first.uploaded)
        assertEquals(1, first.downloaded)
        val second = pass()
        assertEquals(SyncResult(0, 0, 0, 0), second)
    }
}

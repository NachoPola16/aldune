package app.aldune.syncclient

import app.aldune.core.NoteState as WireState
import app.aldune.core.ProtectedNoteContent
import app.aldune.core.SyncNote
import app.aldune.domain.LineEnding
import app.aldune.domain.NoteState
import app.aldune.domain.Tombstone
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncScope
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertNotEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

class SyncMappingTest {
    private fun snapshot(
        notes: List<app.aldune.domain.Note> = emptyList(),
        tombstones: List<Tombstone> = emptyList(),
        bases: Map<String, SyncBase> = emptyMap(),
    ) = LocalSnapshot(notes, tombstones, bases)

    private fun build(snapshot: LocalSnapshot, scope: SyncScope = SyncScope.All, now: Instant = T0) =
        buildSyncInput(snapshot, SyncClientSettings(scope), DEVICE, now)

    // --- deviceId ---

    @Test fun `un deviceId generado son 32 hex en minusculas y no se repite`() {
        val ids = (1..50).map { DeviceIds.generate() }
        assertTrue(ids.all { DeviceIds.isValid(it) && it.length == 32 })
        assertEquals(50, ids.toSet().size)
    }

    @Test fun `solo vale un deviceId de 32 hex en minusculas`() {
        assertTrue(DeviceIds.isValid(DEVICE))
        for (bad in listOf("", "ABCDEF0123456789ABCDEF0123456789", "0123456789abcdef-0123456789abcdef", DEVICE + "0", DEVICE.dropLast(1), "g".repeat(32), UUID.randomUUID().toString())) {
            assertFalse(DeviceIds.isValid(bad), bad)
        }
    }

    @Test fun `buildSyncInput rechaza un deviceId mal formado`() {
        assertFailsWith<IllegalArgumentException> { buildSyncInput(snapshot(), SyncClientSettings(), "pc", T0) }
    }

    // --- Note -> SyncInput ---

    @Test fun `los campos viajan tal cual, sin normalizar color ni etiquetas`() {
        val n = note(color = "#aBcDeF", tags = listOf("Trabajo", "trabajo", " x "), position = 2.5, origin = "\\\\.\\DISPLAY2", state = NoteState.ARCHIVED)
        val local = build(snapshot(listOf(n))).localNotes.single().note
        assertEquals(UUID.fromString(ID1), local.id)
        assertEquals("#aBcDeF", local.color)
        assertEquals(listOf("Trabajo", "trabajo", " x "), local.tags)
        assertEquals(2.5, local.dockPosition)
        assertEquals("\\\\.\\DISPLAY2", local.screenOrigin)
        assertEquals(WireState.ARCHIVED, local.state)
        assertFalse(local.isProtected)
        assertNull(local.protectedContent)
    }

    @Test fun `el estado viaja con su codigo`() {
        for (state in NoteState.entries) {
            assertEquals(state.code, build(snapshot(listOf(note(state = state)))).localNotes.single().note.state)
        }
    }

    @Test fun `una nota con CRLF sube con CRLF y una de Android con LF`() {
        val crlf = build(snapshot(listOf(note(text = "a\nb", ending = LineEnding.CRLF)))).localNotes.single().note
        val lf = build(snapshot(listOf(note(text = "a\nb", ending = LineEnding.LF)))).localNotes.single().note
        assertEquals("a\r\nb", crlf.text)
        assertEquals("a\nb", lf.text)
    }

    @Test fun `el autor de la version pasa tal cual y null es este equipo`() {
        val input = build(snapshot(listOf(note(ID1, author = "pc01"), note(ID2, author = null))))
        assertEquals(listOf("pc01", null), input.localNotes.map { it.authorDeviceId })
    }

    @Test fun `las fechas van en UTC y truncadas a 100 ns`() {
        val instant = Instant.parse("2026-03-01T10:00:00.123456789Z")
        val n = note(createdAt = instant, updatedAt = instant)
        val input = build(snapshot(listOf(n)), now = instant)
        val expected = OffsetDateTime.of(2026, 3, 1, 10, 0, 0, 123_456_700, ZoneOffset.UTC)
        assertEquals(expected, input.localNotes.single().note.createdAt)
        assertEquals(expected, input.localNotes.single().note.updatedAt)
        assertEquals(expected, input.now)
        assertEquals(ZoneOffset.UTC, input.now.offset)
    }

    @Test fun `una fecha con menos de 100 ns de resto no cambia`() {
        val instant = Instant.parse("2026-03-01T10:00:00.1234567Z")
        assertEquals(instant, build(snapshot(listOf(note(updatedAt = instant)))).localNotes.single().note.updatedAt.toInstant())
    }

    @Test fun `un id que no es UUID es un fallo de programa`() {
        assertFailsWith<IllegalArgumentException> { build(snapshot(listOf(note(id = "n1")))) }
        assertFailsWith<IllegalArgumentException> { build(snapshot(tombstones = listOf(Tombstone("n1", T0, "local")))) }
        assertFailsWith<IllegalArgumentException> { build(snapshot(bases = mapOf("n1" to SyncBase(T0.toWire(), "pc")))) }
    }

    @Test fun `un UUID en mayusculas o sin guiones tampoco vale`() {
        assertFailsWith<IllegalArgumentException> { build(snapshot(listOf(note(id = ID1.uppercase().replace("1", "A"))))) }
        assertFailsWith<IllegalArgumentException> { build(snapshot(listOf(note(id = ID1.replace("-", ""))))) }
    }

    @Test fun `las lapidas conservan el marcador local y el autor remoto`() {
        val input = build(snapshot(tombstones = listOf(Tombstone(ID1, T0, Tombstone.LOCAL_DEVICE), Tombstone(ID2, T0, "pc01"))))
        assertEquals(listOf("local", "pc01"), input.localTombstones.map { it.deviceId })
        assertEquals(UUID.fromString(ID1), input.localTombstones[0].noteId)
        assertEquals(T0.toWire(), input.localTombstones[0].deletedAt)
    }

    @Test fun `las bases se pasan por UUID y no se excluye ninguna nota`() {
        val base = SyncBase(T0.toWire(), "pc01")
        val input = build(snapshot(bases = mapOf(ID1 to base)))
        assertEquals(mapOf(UUID.fromString(ID1) to base), input.bases)
        assertTrue(input.excludedIds.isEmpty())
        assertTrue(input.remote.isEmpty())
        assertEquals(DEVICE, input.deviceId)
    }

    @Test fun `el alcance es el del perfil`() {
        val chosen = SyncScope.SelectedNotes(setOf(UUID.fromString(ID1)))
        assertEquals(chosen, build(snapshot(), chosen).scope)
        assertEquals(SyncScope.Tag("x"), build(snapshot(), SyncScope.Tag("x")).scope)
        assertEquals(SyncScope.All, build(snapshot()).scope)
    }

    @Test fun `el alcance de un codigo de perfil se traduce`() {
        assertEquals(SyncScope.All, SyncClientSettings.scopeOf(0, emptyList(), null))
        assertEquals(SyncScope.SelectedNotes(setOf(UUID.fromString(ID1))), SyncClientSettings.scopeOf(1, listOf(UUID.fromString(ID1)), null))
        assertEquals(SyncScope.Tag("viaje"), SyncClientSettings.scopeOf(2, emptyList(), "viaje"))
        assertFailsWith<IllegalArgumentException> { SyncClientSettings.scopeOf(9, emptyList(), null) }
    }

    // --- protegidas ---

    @Test fun `una protegida sube con texto vacio aunque el Note traiga texto`() {
        val n = note(text = "esto no debe viajar en claro", isProtected = true, protectedJson = PROTECTED_JSON)
        val wire = build(snapshot(listOf(n))).localNotes.single().note
        assertEquals("", wire.text)
        assertTrue(wire.isProtected)
        assertEquals(ProtectedNoteContent(1, "c2FsdHNhbHRzYWx0c2FsdA==", "Y2lwaGVy", "bm9uY2Vub25jZW5v", "dGFndGFndGFndGFndGFn"), wire.protectedContent)
    }

    @Test fun `una protegida sin contenido o con contenido ilegible es un fallo de programa`() {
        assertFailsWith<IllegalStateException> { build(snapshot(listOf(note(isProtected = true, protectedJson = null)))) }
        assertFailsWith<IllegalStateException> { build(snapshot(listOf(note(isProtected = true, protectedJson = "no es json")))) }
        assertFailsWith<IllegalStateException> { build(snapshot(listOf(note(isProtected = true, protectedJson = """{"version":1,"salt":"x"}""")))) }
    }

    @Test fun `el contenido protegido se lee sin distinguir mayusculas en las claves`() {
        val pascal = """{"Version":1,"Salt":"s","CipherText":"c","Nonce":"n","Tag":"t"}"""
        val wire = build(snapshot(listOf(note(isProtected = true, protectedJson = pascal)))).localNotes.single().note
        assertEquals(ProtectedNoteContent(1, "s", "c", "n", "t"), wire.protectedContent)
    }

    @Test fun `una nota normal ignora un contenido protegido suelto`() {
        val wire = build(snapshot(listOf(note(isProtected = false, protectedJson = PROTECTED_JSON)))).localNotes.single().note
        assertFalse(wire.isProtected)
        assertNull(wire.protectedContent)
    }

    // --- SyncNote -> Note ---

    private fun wireNote(
        text: String = "hola",
        state: Int = WireState.ACTIVE,
        isProtected: Boolean = false,
        content: ProtectedNoteContent? = null,
    ) = SyncNote(
        id = UUID.fromString(ID1), text = text, color = "#AaBbCc", createdAt = T0.toWire(), updatedAt = T0.plusSeconds(5).toWire(),
        state = state, screenOrigin = "pantalla", tags = listOf("A", "a"), isProtected = isProtected, protectedContent = content, dockPosition = 1.5,
    )

    @Test fun `una nota recibida se convierte con todos sus campos y su autor`() {
        val n = toDomainNote(wireNote(), "pc01")!!
        assertEquals(note(ID1, "hola", LineEnding.LF, "#AaBbCc", T0, T0.plusSeconds(5), NoteState.ACTIVE, listOf("A", "a"), 1.5, "pantalla", "pc01"), n)
    }

    @Test fun `el CRLF se detecta y el texto de dominio va en LF`() {
        val n = toDomainNote(wireNote(text = "a\r\nb"), "pc01")!!
        assertEquals("a\nb", n.text)
        assertEquals(LineEnding.CRLF, n.lineEnding)
        assertEquals("a\r\nb", n.wireText)
    }

    @Test fun `un estado desconocido no se convierte`() {
        assertNull(toDomainNote(wireNote(state = 3), "pc01"))
        assertNull(toDomainNote(wireNote(state = -1), "pc01"))
        assertEquals(NoteState.TRASHED, toDomainNote(wireNote(state = WireState.TRASH), "pc01")!!.state)
    }

    @Test fun `una protegida recibida guarda su contenido en el mismo JSON que escribe core y su texto vacio`() {
        val content = ProtectedNoteContent(1, "c2FsdHNhbHRzYWx0c2FsdA==", "Y2lwaGVy", "bm9uY2Vub25jZW5v", "dGFndGFndGFndGFndGFn")
        val n = toDomainNote(wireNote(text = "residuo", isProtected = true, content = content), "pc01")!!
        assertTrue(n.isProtected)
        assertEquals("", n.text)
        assertEquals(PROTECTED_JSON, n.protectedContentJson)
    }

    @Test fun `una protegida sin contenido no se convierte`() {
        assertNull(toDomainNote(wireNote(isProtected = true, content = null), "pc01"))
    }

    @Test fun `ida y vuelta devuelve la misma nota y la misma protegida`() {
        val plain = note(ID1, "a\nb", LineEnding.CRLF, "#aBc", T0, T0.plusSeconds(1), NoteState.TRASHED, listOf("x", "X"), 3.0, "o", "pc01")
        val prot = note(ID2, "", LineEnding.LF, "#FFF", T0, T0, NoteState.ACTIVE, emptyList(), null, "", "pc02", true, PROTECTED_JSON)
        for (original in listOf(plain, prot)) {
            val wire = build(snapshot(listOf(original))).localNotes.single()
            assertEquals(original, toDomainNote(wire.note, wire.authorDeviceId!!))
        }
    }

    @Test fun `el JSON protegido que no esta en orden de core se normaliza al recibirlo`() {
        val pascal = """{"Version":1,"Salt":"c2FsdHNhbHRzYWx0c2FsdA==","CipherText":"Y2lwaGVy","Nonce":"bm9uY2Vub25jZW5v","Tag":"dGFndGFndGFndGFndGFn"}"""
        val wire = build(snapshot(listOf(note(isProtected = true, protectedJson = pascal)))).localNotes.single().note
        assertEquals(PROTECTED_JSON, toDomainNote(wire, "pc01")!!.protectedContentJson)
        assertNotEquals(pascal, PROTECTED_JSON)
    }
}

package app.aldune.core

import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory
import java.security.GeneralSecurityException
import java.time.OffsetDateTime
import java.util.UUID

class SyncEnvelopeReadTest {
    private val key = Vectors.keyBytes

    private fun envelopeJson(
        format: String = "4",
        noteId: String = "\"0d3c5b8e-1f2a-4b6c-9d7e-8a9b0c1d2e3f\"",
        tombstone: String = "false",
        updatedAt: String = "\"2026-10-17T10:30:15.5+00:00\"",
        deviceId: String = "\"device-a\"",
        content: String = "\"cipherText\":\"AAAA\",\"nonce\":\"AAAA\",\"tag\":\"AAAA\"",
    ) = """{"format":$format,"noteId":$noteId,"tombstone":$tombstone,"updatedAt":$updatedAt,"deviceId":$deviceId,$content}"""

    @TestFactory
    fun `lee y descifra cada nota de los vectores`() = Vectors.objects("envelopes").map { v ->
        DynamicTest.dynamicTest(v.str("name")) {
            val envelope = SyncEnvelopeCodec.deserialize(v.str("envelopeJson"))
            val note = SyncEnvelopeCodec.decryptNote(envelope, key)
            val expected = v.getValue("expected").jsonObject

            assertEquals(UUID.fromString(expected.str("id")), note.id)
            assertEquals(envelope.noteId, note.id)
            assertTrue(envelope.updatedAt.isEqual(note.updatedAt))
            assertTrue(OffsetDateTime.parse(expected.str("updatedAt")).isEqual(note.updatedAt))
            assertTrue(OffsetDateTime.parse(expected.str("createdAt")).isEqual(note.createdAt))
            assertEquals(expected.str("text"), note.text)
            assertEquals(expected.str("color"), note.color)
            assertEquals(expected.getValue("state").jsonPrimitive.content.toInt(), note.state)
            assertEquals(expected.str("screenOrigin"), note.screenOrigin)
            assertEquals(expected.getValue("tags").jsonArray.map { it.jsonPrimitive.content }, note.tags)
            assertEquals(expected.getValue("isProtected").jsonPrimitive.content.toBoolean(), note.isProtected)
            // Un fichero de vectores anterior al nombre visible no trae la clave: es «sin nombre».
            assertEquals(expected["protectedTitle"]?.jsonPrimitive?.contentOrNull, note.protectedTitle)
            // Un fichero anterior a «fijada arriba» no trae la clave: es «no fijada».
            assertEquals(expected["pinnedToTop"]?.jsonPrimitive?.content?.toBoolean() ?: false, note.pinnedToTop)
            val dock = expected.getValue("dockPosition").jsonPrimitive
            if (dock.content == "null") assertNull(note.dockPosition) else assertEquals(dock.content.toDouble(), note.dockPosition)
        }
    }

    @Test
    fun `la nota protegida trae su contenido protegido y las demas no`() {
        val byName = Vectors.objects("envelopes").associateBy { it.str("name") }
        val protectedNote = SyncEnvelopeCodec.decryptNote(
            SyncEnvelopeCodec.deserialize(byName.getValue("protegida").str("envelopeJson")), key)
        assertEquals(1, protectedNote.protectedContent?.version)
        val plain = SyncEnvelopeCodec.decryptNote(
            SyncEnvelopeCodec.deserialize(byName.getValue("nota simple").str("envelopeJson")), key)
        assertNull(plain.protectedContent)
    }

    @Test
    fun `el vector con nombre visible lo trae y los demas no`() {
        val byName = Vectors.objects("envelopes").associateBy { it.str("name") }
        val named = SyncEnvelopeCodec.decryptNote(
            SyncEnvelopeCodec.deserialize(byName.getValue("protegida con nombre visible").str("envelopeJson")), key)
        assertEquals("Banco ☐", named.protectedTitle)
        val plain = SyncEnvelopeCodec.decryptNote(
            SyncEnvelopeCodec.deserialize(byName.getValue("protegida").str("envelopeJson")), key)
        assertNull(plain.protectedTitle)
    }

    @Test
    fun `solo el vector fijada arriba trae el pin y los demas se leen como no fijados`() {
        val byName = Vectors.objects("envelopes").associateBy { it.str("name") }
        val pinned = SyncEnvelopeCodec.decryptNote(SyncEnvelopeCodec.deserialize(byName.getValue("fijada arriba").str("envelopeJson")), key)
        assertEquals(true, pinned.pinnedToTop)
        // Lo que escribe una versión que no conoce el campo (cualquiera de los demás vectores) no lo trae: no fijada.
        for (name in listOf("nota simple", "protegida", "unicode, casillas y etiquetas")) {
            val note = SyncEnvelopeCodec.decryptNote(SyncEnvelopeCodec.deserialize(byName.getValue(name).str("envelopeJson")), key)
            assertEquals(false, note.pinnedToTop, name)
        }
    }

    @Test
    fun `lee la cabecera con claves en cualquier capitalizacion`() {
        val json = envelopeJson().replace("noteId", "NoteId").replace("deviceId", "DEVICEID")
        val envelope = SyncEnvelopeCodec.deserialize(json)
        assertEquals("device-a", envelope.deviceId)
    }

    @Test
    fun `la cabecera conserva el instante aunque cambie el desfase`() {
        val envelope = SyncEnvelopeCodec.deserialize(envelopeJson(updatedAt = "\"2026-10-17T12:30:15.5+02:00\""))
        assertTrue(OffsetDateTime.parse("2026-10-17T10:30:15.5Z").isEqual(envelope.updatedAt))
    }

    @Test
    fun `rechaza un formato mayor que 4 con mensaje claro`() {
        val e = assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.deserialize(envelopeJson(format = "5")) }
        assertEquals("This sync data requires a newer Aldune version (format 5).", e.message)
    }

    @Test
    fun `rechaza un formato menor que 1`() {
        val e = assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.deserialize(envelopeJson(format = "0")) }
        assertEquals("This sync data is too old for this Aldune version (format 0).", e.message)
    }

    @Test
    fun `acepta los formatos 1 a 4`() {
        for (f in 1..4) SyncEnvelopeCodec.deserialize(envelopeJson(format = f.toString()))
    }

    @Test
    fun `si falta format se asume el vigente como hace Core`() {
        val json = envelopeJson().replace("\"format\":4,", "")
        assertEquals(4, SyncEnvelopeCodec.deserialize(json).format)
    }

    @Test
    fun `rechaza sobres incompletos`() {
        val incomplete = listOf(
            envelopeJson(noteId = "\"00000000-0000-0000-0000-000000000000\""),
            envelopeJson(deviceId = "\"  \""),
            envelopeJson(updatedAt = "\"0001-01-01T00:00:00+00:00\""),
            envelopeJson().replace("\"noteId\":\"0d3c5b8e-1f2a-4b6c-9d7e-8a9b0c1d2e3f\",", ""),
            envelopeJson().replace("\"updatedAt\":\"2026-10-17T10:30:15.5+00:00\",", ""),
        )
        for (json in incomplete) {
            val e = assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.deserialize(json) }
            assertEquals("The sync object is incomplete.", e.message)
        }
    }

    @Test
    fun `una nota sin contenido cifrado es invalida pero un borrado no lo necesita`() {
        val e = assertThrows(SyncFormatException::class.java) {
            SyncEnvelopeCodec.deserialize(envelopeJson(content = "\"cipherText\":\"AAAA\""))
        }
        assertEquals("A note sync object is missing encrypted content.", e.message)
        val tomb = SyncEnvelopeCodec.deserialize(envelopeJson(tombstone = "true", content = "\"x\":1"))
        assertTrue(tomb.tombstone)
        assertNull(tomb.cipherText)
    }

    @Test
    fun `json que no es un objeto es un error de formato`() {
        assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.deserialize("[1,2]") }
        assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.deserialize("no es json") }
        assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.deserialize("null") }
    }

    @Test
    fun `no descifra un borrado como si fuera una nota`() {
        val tomb = SyncEnvelopeCodec.deserialize(Vectors.objects("tombstones").first().str("envelopeJson"))
        assertThrows(SyncFormatException::class.java) { SyncEnvelopeCodec.decryptNote(tomb, key) }
    }

    @Test
    fun `una clave equivocada no descifra`() {
        val envelope = SyncEnvelopeCodec.deserialize(Vectors.objects("envelopes").first().str("envelopeJson"))
        val wrong = key.copyOf().also { it[0] = 0x7f }
        assertThrows(GeneralSecurityException::class.java) { SyncEnvelopeCodec.decryptNote(envelope, wrong) }
    }

    @Test
    fun `una cabecera que no coincide con el contenido se rechaza`() {
        val original = Vectors.objects("envelopes").first().str("envelopeJson")
        val otherId = original.replace("0d3c5b8e-1f2a-4b6c-9d7e-8a9b0c1d2e3f", "0d3c5b8e-1f2a-4b6c-9d7e-8a9b0c1d2e40")
        val e1 = assertThrows(SyncFormatException::class.java) {
            SyncEnvelopeCodec.decryptNote(SyncEnvelopeCodec.deserialize(otherId), key)
        }
        assertEquals("The sync note metadata does not match its encrypted content.", e1.message)
        val otherDate = original.replace("2026-10-17T10:30:15.5+00:00", "2026-10-17T10:30:16.5+00:00")
        assertThrows(SyncFormatException::class.java) {
            SyncEnvelopeCodec.decryptNote(SyncEnvelopeCodec.deserialize(otherDate), key)
        }
        assertFalse(otherId == original)
    }
}

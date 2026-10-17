package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.jupiter.api.Assertions.assertArrayEquals
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory
import java.time.OffsetDateTime
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

class SyncEnvelopeWriteTest {
    private val key = Vectors.keyBytes

    private fun vectorNotes(): List<Triple<String, SyncEnvelope, SyncNote>> = Vectors.objects("envelopes").map { v ->
        val envelope = SyncEnvelopeCodec.deserialize(v.str("envelopeJson"))
        Triple(v.str("name"), envelope, SyncEnvelopeCodec.decryptNote(envelope, key))
    }

    @TestFactory
    fun `ida y vuelta de cada nota de los vectores`() = vectorNotes().map { (name, original, note) ->
        DynamicTest.dynamicTest(name) {
            val written = SyncEnvelopeCodec.createNote(note, key, "device-k")
            val reread = SyncEnvelopeCodec.deserialize(SyncEnvelopeCodec.serialize(written))
            assertEquals("device-k", reread.deviceId)
            assertEquals(original.noteId, reread.noteId)
            assertTrue(original.updatedAt.isEqual(reread.updatedAt))
            // Se escribe con el mismo desfase que traía, así que la nota vuelve idéntica.
            assertEquals(note, SyncEnvelopeCodec.decryptNote(reread, key))
        }
    }

    @TestFactory
    fun `con el nonce del vector se reproduce exactamente el cifrado de C#`() = Vectors.objects("envelopes").map { v ->
        DynamicTest.dynamicTest(v.str("name")) {
            val header = Json.parseToJsonElement(v.str("envelopeJson")).jsonObject
            val payloadJson = v.str("payloadJson")
            val nonce = Base64.getDecoder().decode(header.str("nonce"))
            // Lo que se cifra es el texto base64 del JSON; AES-GCM es determinista con nonce fijo.
            val encrypted = ContentCipher.encrypt(
                key,
                Base64.getEncoder().encodeToString(payloadJson.toByteArray(Charsets.UTF_8)),
                nonce,
            )
            assertEquals(header.str("cipherText"), Base64.getEncoder().encodeToString(encrypted.cipherText))
            assertEquals(header.str("tag"), Base64.getEncoder().encodeToString(encrypted.tag))
        }
    }

    @TestFactory
    fun `el JSON de la nota que escribe Kotlin equivale al que escribe Core`() = vectorNotes().zip(Vectors.objects("envelopes")).map { (triple, v) ->
        DynamicTest.dynamicTest(triple.first) {
            val ours = Json.parseToJsonElement(SyncEnvelopeCodec.noteToJson(triple.third))
            val theirs = Json.parseToJsonElement(v.str("payloadJson"))
            assertEquals(theirs, ours)
            // Mismo orden de claves que Core (no es obligatorio, pero evita diferencias gratuitas).
            assertEquals(theirs.jsonObject.keys.toList(), ours.jsonObject.keys.toList())
        }
    }

    @Test
    fun `la cabecera cumple el formato y los campos binarios son base64 estandar con relleno`() {
        val (_, _, note) = vectorNotes().first()
        val written = SyncEnvelopeCodec.createNote(note, key, "device-k")
        val header = Json.parseToJsonElement(SyncEnvelopeCodec.serialize(written)).jsonObject

        assertEquals(listOf("format", "noteId", "tombstone", "updatedAt", "deviceId", "cipherText", "nonce", "tag"), header.keys.toList())
        assertEquals("4", header.getValue("format").jsonPrimitive.content)
        assertEquals("false", header.getValue("tombstone").jsonPrimitive.content)
        assertEquals(note.id.toString(), header.str("noteId"))
        // .NET recorta los ceros finales de la fracción y usa +00:00, no Z.
        assertEquals("2026-10-17T10:30:15.5+00:00", header.str("updatedAt"))
        assertEquals(12, Base64.getDecoder().decode(header.str("nonce")).size)
        assertEquals(16, Base64.getDecoder().decode(header.str("tag")).size)
        for (f in listOf("cipherText", "nonce", "tag")) {
            val text = header.str(f)
            assertTrue(!text.contains('-') && !text.contains('_'), "$f debe usar el alfabeto estándar")
            assertEquals(0, text.length % 4, "$f debe llevar relleno")
        }
    }

    @Test
    fun `se cifra sin datos asociados (AAD) y el texto base64 del json`() {
        val (_, _, note) = vectorNotes()[1]
        val written = SyncEnvelopeCodec.createNote(note, key, "device-k")
        // Descifrado "a mano", sin pasar por nuestro código: si hubiera AAD, esto fallaría.
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"),
            GCMParameterSpec(128, Base64.getDecoder().decode(written.nonce)))
        val plain = cipher.doFinal(Base64.getDecoder().decode(written.cipherText) + Base64.getDecoder().decode(written.tag))
        val json = String(Base64.getDecoder().decode(String(plain, Charsets.UTF_8)), Charsets.UTF_8)
        assertEquals(note.id.toString(), Json.parseToJsonElement(json).jsonObject.str("id"))
    }

    @Test
    fun `cada cifrado usa un nonce nuevo`() {
        val (_, _, note) = vectorNotes().first()
        val nonces = (1..50).map { SyncEnvelopeCodec.createNote(note, key, "d").nonce }.toSet()
        assertEquals(50, nonces.size)
        val a = SyncEnvelopeCodec.createNote(note, key, "d")
        val b = SyncEnvelopeCodec.createNote(note, key, "d")
        assertNotEquals(a.cipherText, b.cipherText)
    }

    @Test
    fun `una nota protegida sin contenido protegido no se escribe`() {
        val (_, _, note) = vectorNotes().first()
        org.junit.jupiter.api.Assertions.assertThrows(IllegalArgumentException::class.java) {
            SyncEnvelopeCodec.createNote(note.copy(isProtected = true, protectedContent = null), key, "d")
        }
    }

    @Test
    fun `las fechas con otro desfase se escriben con su desfase y fraccion recortada`() {
        val (_, _, note) = vectorNotes().first()
        val moved = note.copy(updatedAt = OffsetDateTime.parse("2026-10-17T12:30:15.1230000+02:00"))
        val written = SyncEnvelopeCodec.createNote(moved, key, "d")
        assertEquals("2026-10-17T12:30:15.123+02:00", SyncEnvelopeCodec.serialize(written).let {
            Json.parseToJsonElement(it).jsonObject.str("updatedAt")
        })
    }

    @Test
    fun `campos opcionales ausentes se escriben como null`() {
        val (_, _, note) = vectorNotes().first()
        val json = Json.parseToJsonElement(SyncEnvelopeCodec.noteToJson(note)).jsonObject
        assertEquals(JsonNull, json["protectedContent"])
        assertEquals(JsonNull, json["dockPosition"])
        assertNull((json["dockPosition"] as? JsonPrimitive)?.takeIf { !it.isString && it !is JsonNull })
    }
}

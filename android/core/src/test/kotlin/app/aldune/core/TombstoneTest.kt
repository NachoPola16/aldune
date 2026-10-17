package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory
import java.time.OffsetDateTime
import java.util.Base64
import java.util.UUID

class TombstoneTest {
    private val key = Vectors.keyBytes

    @TestFactory
    fun `la autenticidad de cada borrado de los vectores`() = Vectors.objects("tombstones").map { v ->
        DynamicTest.dynamicTest(v.str("name")) {
            val envelope = SyncEnvelopeCodec.deserialize(v.str("envelopeJson"))
            assertTrue(envelope.tombstone)
            val expected = v.getValue("authentic").toString().toBoolean()
            assertEquals(expected, SyncEnvelopeCodec.isAuthenticTombstone(envelope, key))
        }
    }

    @Test
    fun `el borrado con fecha falsificada no es autentico aunque descifre con la clave`() {
        val forged = Vectors.objects("tombstones").first { it.str("name") == "borrado con fecha falsificada" }
        val envelope = SyncEnvelopeCodec.deserialize(forged.str("envelopeJson"))
        // La cabecera dice 2026-11-16, el contenido firmado dice otra fecha: no coincide.
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(envelope, key))
    }

    @Test
    fun `con el nonce del vector se reproduce el cifrado del borrado firmado`() {
        val v = Vectors.objects("tombstones").first { it.str("name") == "borrado firmado" }
        val header = Json.parseToJsonElement(v.str("envelopeJson")).jsonObject
        val payload = """{"id":"${header.str("noteId")}","deletedAt":"${header.str("updatedAt")}"}"""
        val encrypted = ContentCipher.encrypt(
            key, SyncEnvelopeCodec.base64Text(payload), Base64.getDecoder().decode(header.str("nonce")))
        assertEquals(header.str("cipherText"), Base64.getEncoder().encodeToString(encrypted.cipherText))
        assertEquals(header.str("tag"), Base64.getEncoder().encodeToString(encrypted.tag))
    }

    @Test
    fun `un borrado creado por Kotlin es autentico y su cabecera cuadra`() {
        val id = UUID.fromString("3b9f1c2e-0a44-4c1b-8f3d-5d7e9a1b2c3d")
        val at = OffsetDateTime.parse("2026-10-17T11:00:00+00:00")
        val envelope = SyncEnvelopeCodec.createTombstone(id, at, key, "device-k")
        assertTrue(envelope.tombstone)
        assertEquals(id, envelope.noteId)
        assertTrue(at.isEqual(envelope.updatedAt))
        assertTrue(SyncEnvelopeCodec.isAuthenticTombstone(envelope, key))
        // Pasa por el JSON como lo haría al subir y bajar del almacén.
        val reread = SyncEnvelopeCodec.deserialize(SyncEnvelopeCodec.serialize(envelope))
        assertTrue(SyncEnvelopeCodec.isAuthenticTombstone(reread, key))
    }

    @Test
    fun `no es autentico con otra clave, otra fecha, otro id o sin contenido`() {
        val id = UUID.fromString("3b9f1c2e-0a44-4c1b-8f3d-5d7e9a1b2c3d")
        val at = OffsetDateTime.parse("2026-10-17T11:00:00+00:00")
        val good = SyncEnvelopeCodec.createTombstone(id, at, key, "device-k")

        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good, key.copyOf().also { it[5] = 0x55 }))
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good.copy(updatedAt = at.plusDays(30)), key))
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good.copy(noteId = UUID.randomUUID()), key))
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good.copy(cipherText = null), key))
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good.copy(tag = "AAAA"), key))
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good.copy(nonce = "no es base64"), key))
        // Un sobre de nota no es un borrado, por bien firmado que esté.
        assertFalse(SyncEnvelopeCodec.isAuthenticTombstone(good.copy(tombstone = false), key))
    }

    @Test
    fun `la fecha firmada se compara como instante, no por desfase`() {
        val id = UUID.randomUUID()
        val at = OffsetDateTime.parse("2026-10-17T11:00:00+00:00")
        val envelope = SyncEnvelopeCodec.createTombstone(id, at, key, "device-k")
        val sameInstant = envelope.copy(updatedAt = at.withOffsetSameInstant(java.time.ZoneOffset.ofHours(2)))
        assertTrue(SyncEnvelopeCodec.isAuthenticTombstone(sameInstant, key))
    }
}

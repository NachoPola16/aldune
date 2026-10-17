package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.io.File
import java.time.OffsetDateTime
import java.util.UUID

/**
 * Escribe tests/vectors/kotlin-output.json: sobres, borrados, notas protegidas y códigos de perfil
 * producidos por Kotlin con la clave de los vectores, para que Aldune.Core los lea desde C#.
 * Los nonces y las sales son aleatorios, así que el archivo cambia en cada ejecución (a propósito:
 * lo que se prueba es que el formato lo acepta Core, no unos bytes concretos). Después de escribirlo,
 * este mismo test lo vuelve a leer con el código Kotlin como comprobación de cordura.
 */
class KotlinOutputTest {
    private val key = Vectors.keyBytes
    private val deviceId = "device-kotlin"
    private val pretty = Json { prettyPrint = true }

    private fun note(
        id: String,
        text: String,
        state: Int = NoteState.ACTIVE,
        updatedAt: String = "2026-10-17T10:30:15.5+00:00",
        createdAt: String = "2026-10-01T08:00:00.1234567+00:00",
        tags: List<String> = emptyList(),
        protectedContent: ProtectedNoteContent? = null,
        dockPosition: Double? = null,
    ) = SyncNote(
        id = UUID.fromString(id), text = text, color = "#EBD38B",
        createdAt = OffsetDateTime.parse(createdAt), updatedAt = OffsetDateTime.parse(updatedAt),
        state = state, screenOrigin = "primary", tags = tags,
        isProtected = protectedContent != null, protectedContent = protectedContent, dockPosition = dockPosition,
    )

    private fun str(v: String?): JsonElement = if (v == null) JsonNull else JsonPrimitive(v)

    private fun expected(n: SyncNote) = JsonObject(linkedMapOf(
        "id" to JsonPrimitive(n.id.toString()),
        "text" to JsonPrimitive(n.text),
        "color" to JsonPrimitive(n.color),
        "createdAt" to JsonPrimitive(JsonSupport.formatDate(n.createdAt)),
        "updatedAt" to JsonPrimitive(JsonSupport.formatDate(n.updatedAt)),
        "state" to JsonPrimitive(n.state),
        "screenOrigin" to JsonPrimitive(n.screenOrigin),
        "tags" to JsonArray(n.tags.map(::JsonPrimitive)),
        "isProtected" to JsonPrimitive(n.isProtected),
        "dockPosition" to (n.dockPosition?.let(::JsonPrimitive) ?: JsonNull),
    ))

    private fun envelopeEntry(name: String, n: SyncNote, extra: Map<String, JsonElement> = emptyMap()): JsonObject {
        val envelope = SyncEnvelopeCodec.createNote(n, key, deviceId)
        return JsonObject(linkedMapOf<String, JsonElement>(
            "name" to JsonPrimitive(name),
            "deviceId" to JsonPrimitive(deviceId),
            "envelopeJson" to JsonPrimitive(SyncEnvelopeCodec.serialize(envelope)),
            "payloadJson" to JsonPrimitive(SyncEnvelopeCodec.noteToJson(n)),
            "expected" to expected(n),
        ) + extra)
    }

    private fun tombstoneEntry(name: String, envelope: SyncEnvelope, authentic: Boolean) = JsonObject(linkedMapOf(
        "name" to JsonPrimitive(name),
        "envelopeJson" to JsonPrimitive(SyncEnvelopeCodec.serialize(envelope)),
        "authentic" to JsonPrimitive(authentic),
    ))

    @Test
    fun `escribe tests-vectors-kotlin-output json con lo producido por Kotlin`() {
        val protectedPassword = "contraseña-kotlin-ñ"
        val protectedPlaintext = "secreto de Kotlin: línea 1\nlínea 2 ☐ 🛒"
        val protectedContent = ProtectedNotes.protect(protectedPlaintext, protectedPassword)

        val envelopes = listOf(
            envelopeEntry("kotlin: nota simple", note("a1b2c3d4-0001-4000-8000-000000000001", "Título\ncuerpo")),
            envelopeEntry("kotlin: unicode, casillas y etiquetas", note(
                "a1b2c3d4-0002-4000-8000-000000000002",
                "Compra 🛒\n☐ pan\n☒ leche\n→ viñeta con acentos: áéíóú ñ ü ß",
                state = NoteState.ARCHIVED, tags = listOf("casa", "urgente"), dockPosition = 2.5)),
            envelopeEntry("kotlin: papelera con posición negativa", note(
                "a1b2c3d4-0003-4000-8000-000000000003", "", state = NoteState.TRASH, dockPosition = -3.25)),
            envelopeEntry("kotlin: desfase +02:00 y 7 decimales", note(
                "a1b2c3d4-0004-4000-8000-000000000004", "hora local",
                updatedAt = "2026-10-17T12:30:15.1234567+02:00", createdAt = "2026-10-01T10:00:00+02:00")),
            envelopeEntry("kotlin: protegida", note(
                "a1b2c3d4-0005-4000-8000-000000000005", "", protectedContent = protectedContent),
                extra = mapOf("protectedPassword" to JsonPrimitive(protectedPassword),
                    "protectedPlaintext" to JsonPrimitive(protectedPlaintext))),
        )

        val deletedAt = OffsetDateTime.parse("2026-10-17T11:00:00+00:00")
        val noteId = UUID.fromString("a1b2c3d4-0006-4000-8000-000000000006")
        val signed = SyncEnvelopeCodec.createTombstone(noteId, deletedAt, key, deviceId)
        val forged = signed.copy(updatedAt = deletedAt.plusDays(30))
        val wrongKey = SyncEnvelopeCodec.createTombstone(noteId, deletedAt, ByteArray(32) { 0x42 }, deviceId)
        val tombstones = listOf(
            tombstoneEntry("kotlin: borrado firmado", signed, true),
            tombstoneEntry("kotlin: borrado con fecha falsificada", forged, false),
            tombstoneEntry("kotlin: borrado firmado con otra clave", wrongKey, false),
        )

        val protectedNotes = listOf("contraseña-kotlin-ñ" to "línea única", protectedPassword to protectedPlaintext).map { (pw, text) ->
            val c = ProtectedNotes.protect(text, pw)
            JsonObject(linkedMapOf(
                "password" to JsonPrimitive(pw),
                "plaintext" to JsonPrimitive(text),
                "pbkdf2Iterations" to JsonPrimitive(ProtectedNotes.PBKDF2_ITERATIONS),
                "content" to SyncEnvelopeCodec.protectedContentToJson(c),
            ))
        }

        val chosen = UUID.fromString("a1b2c3d4-0007-4000-8000-000000000007")
        val codeSpecs = listOf(
            SyncShareCodeData(key, ProfileCodes.SCOPE_ALL, emptyList(), "Casa Kotlin", ProfileCodes.TRANSPORT_SERVER, "https://sync.example.com/", null),
            SyncShareCodeData(key, ProfileCodes.SCOPE_CHOSEN, listOf(chosen), null, ProfileCodes.TRANSPORT_WEBDAV, "https://cloud.example.com/remote.php/dav/files/ana/aldune/", null),
            SyncShareCodeData(key, ProfileCodes.SCOPE_TAG, emptyList(), "Trabajo ñ", null, null, "trabajo"),
        )
        val shareCodes = codeSpecs.map { d ->
            JsonObject(linkedMapOf(
                "code" to JsonPrimitive(ProfileCodes.encode(d.key, d.scope, d.noteIds, d.profileName, d.transport, d.serverUrl, d.tag)),
                "expected" to JsonObject(linkedMapOf(
                    "scope" to JsonPrimitive(d.scope),
                    "noteIds" to JsonArray(d.noteIds.map { JsonPrimitive(it.toString()) }),
                    "profileName" to str(d.profileName),
                    "transport" to (d.transport?.let(::JsonPrimitive) ?: JsonNull),
                    "serverUrl" to str(d.serverUrl),
                    "tag" to str(d.tag),
                )),
            ))
        }

        val root = JsonObject(linkedMapOf(
            "format" to JsonPrimitive(SyncCompatibility.CURRENT_FORMAT),
            "notes" to JsonPrimitive("Producido por android/core (Kotlin) con la clave de sync-format4.json. Nonces y sales aleatorios: cambia en cada ejecución. Lo escribe KotlinOutputTest."),
            "generator" to JsonPrimitive("android/core (Kotlin)"),
            "syncKeyHex" to Vectors.root.getValue("syncKeyHex"),
            "syncKeyEncoded" to JsonPrimitive(SyncKeyFormat.encode(key)),
            "envelopes" to JsonArray(envelopes),
            "tombstones" to JsonArray(tombstones),
            "protectedNotes" to JsonArray(protectedNotes),
            "shareCodes" to JsonArray(shareCodes),
        ))

        val file = File(Vectors.dir, "kotlin-output.json")
        file.writeText(pretty.encodeToString(JsonElement.serializer(), root) + "\n", Charsets.UTF_8)

        verifyFile(file)
    }

    /** Relee lo escrito como lo leería otro cliente: nada de reutilizar objetos en memoria. */
    private fun verifyFile(file: File) {
        val written = Json.parseToJsonElement(file.readText(Charsets.UTF_8)).jsonObject
        assertEquals(4, written.getValue("format").jsonPrimitive.content.toInt())
        assertEquals(Vectors.keyEncoded, written.str("syncKeyEncoded"))

        for (v in written.getValue("envelopes").jsonArray.map { it.jsonObject }) {
            val envelope = SyncEnvelopeCodec.deserialize(v.str("envelopeJson"))
            val note = SyncEnvelopeCodec.decryptNote(envelope, key)
            assertEquals(v.getValue("expected").jsonObject.str("id"), note.id.toString())
            assertEquals(v.getValue("expected").jsonObject.str("text"), note.text)
            val password = v["protectedPassword"]?.jsonPrimitive?.content
            if (password != null)
                assertEquals(v.str("protectedPlaintext"), ProtectedNotes.tryUnprotect(note.protectedContent!!, password))
        }
        for (t in written.getValue("tombstones").jsonArray.map { it.jsonObject }) {
            val authentic = t.getValue("authentic").jsonPrimitive.content.toBoolean()
            assertEquals(authentic, SyncEnvelopeCodec.isAuthenticTombstone(SyncEnvelopeCodec.deserialize(t.str("envelopeJson")), key), t.str("name"))
        }
        for (p in written.getValue("protectedNotes").jsonArray.map { it.jsonObject }) {
            val content = SyncEnvelopeCodec.readProtectedContent(p.getValue("content").jsonObject)
            assertEquals(p.str("plaintext"), ProtectedNotes.tryUnprotect(content, p.str("password")))
        }
        val codes = written.getValue("shareCodes").jsonArray.map { it.jsonObject }
        assertTrue(codes.isNotEmpty())
        for (c in codes) {
            val data = ProfileCodes.decode(c.str("code"))
            assertEquals(c.getValue("expected").jsonObject.getValue("scope").jsonPrimitive.content.toInt(), data.scope)
        }
    }
}

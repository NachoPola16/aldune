package app.aldune.core

import app.aldune.core.JsonSupport.boolean
import app.aldune.core.JsonSupport.double
import app.aldune.core.JsonSupport.field
import app.aldune.core.JsonSupport.int
import app.aldune.core.JsonSupport.parseDate
import app.aldune.core.JsonSupport.parseObject
import app.aldune.core.JsonSupport.string
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.time.Instant
import java.time.OffsetDateTime
import java.util.Base64
import java.util.UUID

/**
 * Lectura del sobre de sync (formato 4) con la misma validación que SyncEnvelopeCodec de Aldune.Core.
 * Los errores de formato son [SyncFormatException]; una clave equivocada o un dato manipulado lanza
 * GeneralSecurityException (AEADBadTagException), como CryptographicException en Core.
 */
object SyncEnvelopeCodec {
    private val NIL_UUID = UUID(0, 0)

    // DateTimeOffset == default de .NET: 0001-01-01T00:00:00 UTC.
    private val DEFAULT_INSTANT: Instant = OffsetDateTime.parse("0001-01-01T00:00:00Z").toInstant()

    fun deserialize(json: String): SyncEnvelope {
        val o = parseObject(json, "sync object")
        val envelope = SyncEnvelope(
            // Core inicializa Format al vigente: si falta en el JSON, vale 4.
            format = o.int("format") ?: SyncCompatibility.CURRENT_FORMAT,
            noteId = o.string("noteId")?.let(::parseUuid) ?: NIL_UUID,
            tombstone = o.boolean("tombstone") ?: false,
            updatedAt = o.string("updatedAt")?.let(::parseDate) ?: OffsetDateTime.ofInstant(DEFAULT_INSTANT, java.time.ZoneOffset.UTC),
            deviceId = o.string("deviceId") ?: "",
            cipherText = o.string("cipherText"),
            nonce = o.string("nonce"),
            tag = o.string("tag"),
        )
        validate(envelope)
        return envelope
    }

    fun deserialize(bytes: ByteArray): SyncEnvelope = deserialize(String(bytes, Charsets.UTF_8))

    /**
     * Cifra una nota en un sobre nuevo, con nonce aleatorio. Lo que se cifra es el texto base64 del
     * JSON de la nota (no el JSON), igual que `SyncEnvelopeCodec.CreateNote` de Core.
     */
    fun createNote(note: SyncNote, key: ByteArray, deviceId: String): SyncEnvelope {
        require(!note.isProtected || note.protectedContent != null) {
            "A protected note needs its protected content."
        }
        require(deviceId.isNotBlank()) { "The device id cannot be blank." }
        val encrypted = ContentCipher.encrypt(key, base64Text(noteToJson(note)))
        return SyncEnvelope(
            noteId = note.id,
            tombstone = false,
            updatedAt = note.updatedAt,
            deviceId = deviceId,
            cipherText = Base64.getEncoder().encodeToString(encrypted.cipherText),
            nonce = Base64.getEncoder().encodeToString(encrypted.nonce),
            tag = Base64.getEncoder().encodeToString(encrypted.tag),
        )
    }

    /** JSON del sobre (sin sangría, claves en camelCase, como lo escribe Core). */
    fun serialize(envelope: SyncEnvelope): String = JsonObject(linkedMapOf(
        "format" to JsonPrimitive(envelope.format),
        "noteId" to JsonPrimitive(envelope.noteId.toString()),
        "tombstone" to JsonPrimitive(envelope.tombstone),
        "updatedAt" to JsonPrimitive(JsonSupport.formatDate(envelope.updatedAt)),
        "deviceId" to JsonPrimitive(envelope.deviceId),
        "cipherText" to (envelope.cipherText?.let(::JsonPrimitive) ?: JsonNull),
        "nonce" to (envelope.nonce?.let(::JsonPrimitive) ?: JsonNull),
        "tag" to (envelope.tag?.let(::JsonPrimitive) ?: JsonNull),
    )).toString()

    /** El JSON de la nota que se cifra: mismas claves y orden que `SyncNotePayload` de Core. */
    fun noteToJson(note: SyncNote): String = JsonObject(linkedMapOf(
        "id" to JsonPrimitive(note.id.toString()),
        "text" to JsonPrimitive(note.text),
        "color" to JsonPrimitive(note.color),
        "createdAt" to JsonPrimitive(JsonSupport.formatDate(note.createdAt)),
        "updatedAt" to JsonPrimitive(JsonSupport.formatDate(note.updatedAt)),
        "state" to JsonPrimitive(note.state),
        "screenOrigin" to JsonPrimitive(note.screenOrigin),
        "tags" to JsonArray(note.tags.map(::JsonPrimitive)),
        "isProtected" to JsonPrimitive(note.isProtected),
        "protectedContent" to (note.protectedContent?.let(::protectedContentToJson) ?: JsonNull),
        "dockPosition" to (note.dockPosition?.let(::JsonPrimitive) ?: JsonNull),
    )).toString()

    internal fun base64Text(json: String): String = Base64.getEncoder().encodeToString(json.toByteArray(Charsets.UTF_8))

    internal fun protectedContentToJson(c: ProtectedNoteContent) = JsonObject(linkedMapOf(
        "version" to JsonPrimitive(c.version),
        "salt" to JsonPrimitive(c.salt),
        "cipherText" to JsonPrimitive(c.cipherText),
        "nonce" to JsonPrimitive(c.nonce),
        "tag" to JsonPrimitive(c.tag),
    ))

    /** Descifra una nota y comprueba que `id` y `updatedAt` del contenido coinciden con la cabecera. */
    fun decryptNote(envelope: SyncEnvelope, key: ByteArray): SyncNote {
        validate(envelope)
        val cipherText = envelope.cipherText
        val nonce = envelope.nonce
        val tag = envelope.tag
        if (envelope.tombstone || cipherText == null || nonce == null || tag == null)
            throw SyncFormatException("The sync object is not a note.")

        val plainBase64 = ContentCipher.decrypt(key, decodeEncrypted(cipherText, nonce, tag))
        val payload = parseObject(decodeBase64Text(plainBase64), "encrypted sync note")

        val note = readNote(payload)
        if (note.id != envelope.noteId || !note.updatedAt.toInstant().equals(envelope.updatedAt.toInstant()))
            throw SyncFormatException("The sync note metadata does not match its encrypted content.")
        if (note.isProtected && note.protectedContent == null)
            throw SyncFormatException("The protected sync note is missing its encrypted content.")
        return note
    }

    internal fun decodeEncrypted(cipherText: String, nonce: String, tag: String) = EncryptedContent(
        Base64.getDecoder().decode(cipherText),
        Base64.getDecoder().decode(nonce),
        Base64.getDecoder().decode(tag),
    )

    /** Lo cifrado es el TEXTO base64 del JSON, no el JSON: aquí se deshace esa capa. */
    internal fun decodeBase64Text(plainBase64: String): String =
        String(Base64.getDecoder().decode(plainBase64), Charsets.UTF_8)

    internal fun parseUuid(value: String): UUID = try {
        UUID.fromString(value.trim())
    } catch (e: IllegalArgumentException) {
        throw SyncFormatException("\"$value\" is not a valid note id.", e)
    }

    private fun validate(envelope: SyncEnvelope) {
        if (!SyncCompatibility.isSupported(envelope.format))
            throw SyncFormatException(SyncCompatibility.unsupportedFormatMessage(envelope.format))

        if (envelope.noteId == NIL_UUID || envelope.deviceId.isBlank() ||
            envelope.updatedAt.toInstant() == DEFAULT_INSTANT)
            throw SyncFormatException("The sync object is incomplete.")

        if (!envelope.tombstone && (envelope.cipherText == null || envelope.nonce == null || envelope.tag == null))
            throw SyncFormatException("A note sync object is missing encrypted content.")
    }

    private fun readNote(o: JsonObject): SyncNote {
        val default = OffsetDateTime.ofInstant(DEFAULT_INSTANT, java.time.ZoneOffset.UTC)
        val tags = (o.field("tags") as? JsonArray)?.mapNotNull { (it as? JsonPrimitive)?.takeIf { p -> p.isString }?.content }
        return SyncNote(
            id = o.string("id")?.let(::parseUuid) ?: NIL_UUID,
            text = o.string("text") ?: "",
            color = o.string("color") ?: "",
            createdAt = o.string("createdAt")?.let(::parseDate) ?: default,
            updatedAt = o.string("updatedAt")?.let(::parseDate) ?: default,
            state = o.int("state") ?: NoteState.ACTIVE,
            screenOrigin = o.string("screenOrigin") ?: "",
            tags = tags ?: emptyList(),
            isProtected = o.boolean("isProtected") ?: false,
            protectedContent = (o.field("protectedContent") as? JsonObject)?.let(::readProtectedContent),
            dockPosition = o.double("dockPosition"),
        )
    }

    internal fun readProtectedContent(o: JsonObject) = ProtectedNoteContent(
        version = o.int("version") ?: ProtectedNoteContent.CURRENT_VERSION,
        salt = o.string("salt") ?: "",
        cipherText = o.string("cipherText") ?: "",
        nonce = o.string("nonce") ?: "",
        tag = o.string("tag") ?: "",
    )
}

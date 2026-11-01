package app.aldune.syncclient

import app.aldune.core.ContentCipher
import app.aldune.core.EncryptedContent
import java.security.MessageDigest
import java.time.Instant
import java.time.OffsetDateTime
import java.util.Base64
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put

/**
 * The look as it travels between devices (docs/specs/2026-11-01-sync-look-design.md), in the names and shape the configuration file of Windows uses
 * (`ConfigFields`, section Appearance, without custom themes). [noteColorAsStripe] is the inverse of Android's `fillNoteColor`; [newNoteTone] is
 * 0 light, 1 dark; [colorAssignment] is `colorRule`. A device keeps its own copy and compares hashes: nothing here needs the other platform's code.
 */
data class LookValues(
    val appearance: Int,
    val aspectColors: Map<String, Map<String, String>>?,
    val squareCorners: Boolean?,
    val noteColorAsStripe: Boolean,
    val syncSignal: Boolean?,
    val uniformNoteColor: String?,
    val noteTheme: String?,
    val newNoteTone: Int,
    val colorAssignment: Int,
    val fixedNoteColor: String?,
)

enum class LookAction { NONE, PUSH, PULL }

class LookDocument(val stamp: Instant, val device: String, val look: JsonObject)

object LookSync {
    const val DOCUMENT_VERSION = 1
    private const val MAX_APPEARANCE = 17
    private val COLOR = Regex("^#[0-9A-Fa-f]{6}$")

    /** Same fields, same order as Windows' `SyncLook.Capture`. */
    fun capture(values: LookValues): JsonObject = buildJsonObject {
        put("appearance", values.appearance)
        put("aspectColors", values.aspectColors?.takeIf { it.isNotEmpty() }?.let { colors ->
            buildJsonObject { for ((aspect, slots) in colors) put(aspect, buildJsonObject { for ((slot, value) in slots) put(slot, value) }) }
        } ?: JsonNull)
        put("squareCorners", values.squareCorners?.let(::JsonPrimitive) ?: JsonNull)
        put("noteColorAsStripe", values.noteColorAsStripe)
        put("syncSignal", values.syncSignal?.let(::JsonPrimitive) ?: JsonNull)
        put("uniformNoteColor", values.uniformNoteColor?.let(::JsonPrimitive) ?: JsonNull)
        put("noteTheme", values.noteTheme?.let(::JsonPrimitive) ?: JsonNull)
        put("newNoteTone", values.newNoteTone)
        put("colorAssignment", values.colorAssignment)
        put("fixedNoteColor", values.fixedNoteColor?.let(::JsonPrimitive) ?: JsonNull)
    }

    fun hash(values: LookValues): String =
        MessageDigest.getInstance("SHA-256").digest(capture(values).toString().toByteArray(Charsets.UTF_8)).joinToString("") { "%02X".format(it) }

    /**
     * Applies [look] on top of [current]. A field that is missing or does not validate keeps the current value, unknown fields are ignored, and the
     * custom themes of Windows have no field here, so a later version can add some.
     */
    fun apply(current: LookValues, look: JsonObject): LookValues {
        fun element(name: String): JsonElement? = look[name]
        fun int(name: String, range: IntRange, fallback: Int): Int =
            (element(name) as? JsonPrimitive)?.takeIf { !it.isString }?.intOrNull?.takeIf { it in range } ?: fallback
        fun bool(name: String, fallback: Boolean?): Boolean? = when (val e = element(name)) {
            null -> fallback
            JsonNull -> null
            is JsonPrimitive -> if (e.isString) fallback else e.booleanOrNull ?: fallback
            else -> fallback
        }
        fun color(name: String, fallback: String?): String? = when (val e = element(name)) {
            null -> fallback
            JsonNull -> null
            is JsonPrimitive -> e.contentOrNull?.takeIf { e.isString && COLOR.matches(it) }?.uppercase() ?: fallback
            else -> fallback
        }

        val aspectColors = when (val e = element("aspectColors")) {
            null -> current.aspectColors
            JsonNull -> null
            is JsonObject -> readAspectColors(e) ?: current.aspectColors
            else -> current.aspectColors
        }
        val theme = when (val e = element("noteTheme")) {
            null -> current.noteTheme
            JsonNull -> null
            is JsonPrimitive -> if (e.isString) e.content else current.noteTheme
            else -> current.noteTheme
        }
        return LookValues(
            appearance = int("appearance", 0..MAX_APPEARANCE, current.appearance),
            aspectColors = aspectColors,
            squareCorners = bool("squareCorners", current.squareCorners),
            noteColorAsStripe = bool("noteColorAsStripe", current.noteColorAsStripe) ?: current.noteColorAsStripe,
            syncSignal = bool("syncSignal", current.syncSignal),
            uniformNoteColor = color("uniformNoteColor", current.uniformNoteColor),
            noteTheme = theme,
            newNoteTone = int("newNoteTone", 0..1, current.newNoteTone),
            colorAssignment = int("colorAssignment", 0..3, current.colorAssignment),
            fixedNoteColor = color("fixedNoteColor", current.fixedNoteColor),
        )
    }

    // Only well-formed «#RRGGBB» values survive; an object with entries but none valid is rubbish, not a request to empty (same rule as Windows).
    private fun readAspectColors(obj: JsonObject): Map<String, Map<String, String>>? {
        val result = LinkedHashMap<String, Map<String, String>>()
        for ((aspect, value) in obj) {
            val slots = value as? JsonObject ?: continue
            val colors = LinkedHashMap<String, String>()
            for ((slot, c) in slots) {
                val text = (c as? JsonPrimitive)?.takeIf { it.isString }?.content ?: continue
                if (COLOR.matches(text)) colors[slot] = text.uppercase()
            }
            if (colors.isNotEmpty()) result[aspect] = colors
        }
        return if (result.isEmpty() && obj.isNotEmpty()) null else result
    }

    /** The rule of the spec, identical to `SyncLook.Decide`: a local change is pushed; otherwise a newer remote is pulled; otherwise nothing. */
    fun decide(localHash: String, lastHash: String?, lastStamp: Instant?, remoteStamp: Instant?): LookAction {
        if (lastHash == null || localHash != lastHash) {
            if (lastHash == null && remoteStamp != null) return LookAction.PULL
            return LookAction.PUSH
        }
        return if (remoteStamp != null && (lastStamp == null || remoteStamp > lastStamp)) LookAction.PULL else LookAction.NONE
    }

    /** Seals the document like Windows' `SyncLook.Seal`: JSON `{"C","N","T"}` (base64 of ciphertext, nonce, tag) around the AES-GCM text. */
    fun seal(key: ByteArray, stamp: Instant, device: String, look: JsonObject): ByteArray {
        val plain = buildJsonObject {
            put("v", DOCUMENT_VERSION)
            put("stamp", stamp.toString())
            put("device", device)
            put("look", look)
        }.toString()
        val encrypted = ContentCipher.encrypt(key, plain)
        val b64 = Base64.getEncoder()
        return buildJsonObject {
            put("C", b64.encodeToString(encrypted.cipherText))
            put("N", b64.encodeToString(encrypted.nonce))
            put("T", b64.encodeToString(encrypted.tag))
        }.toString().toByteArray(Charsets.UTF_8)
    }

    /** `null` for anything that is not a look document of ours, does not open with [key] or comes from a later version. */
    fun open(key: ByteArray, blob: ByteArray): LookDocument? = try {
        val sealed = Json.parseToJsonElement(String(blob, Charsets.UTF_8)).jsonObject
        val b64 = Base64.getDecoder()
        val plain = ContentCipher.decrypt(
            key,
            EncryptedContent(b64.decode(sealed.getValue("C").jsonPrimitive.content), b64.decode(sealed.getValue("N").jsonPrimitive.content), b64.decode(sealed.getValue("T").jsonPrimitive.content)),
        )
        val root = Json.parseToJsonElement(plain).jsonObject
        if ((root.getValue("v").jsonPrimitive.intOrNull ?: Int.MAX_VALUE) > DOCUMENT_VERSION) null
        else LookDocument(
            OffsetDateTime.parse(root.getValue("stamp").jsonPrimitive.content).toInstant(),
            root["device"]?.jsonPrimitive?.contentOrNull.orEmpty(),
            root.getValue("look").jsonObject,
        )
    } catch (e: Exception) {
        null
    }
}

package app.aldune.sync

import app.aldune.core.ProtectedNoteContent
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncFormatException
import app.aldune.core.SyncNote
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import java.io.File
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/** Marcador de `sync-scenarios.json` para «una fecha puesta por el reloj». */
const val NOW_MARKER = "<now>"

/** Un escenario de `tests/vectors/sync-scenarios.json` ya convertido a los tipos del motor. */
class Scenario(val name: String, private val json: JsonObject, private val root: JsonObject) {
    val key: ByteArray = hex(root.str("syncKeyHex"))
    val expected: JsonObject = json.obj("expected")

    /** Notas del almacén que el transporte real saltaría (formato desconocido), tal cual venían. */
    val unreadable = mutableListOf<String>()
    val remote: List<SyncEnvelope> = json.arr("remote").mapNotNull { item ->
        val text = item.jsonObject.str("envelopeJson")
        try {
            SyncEnvelopeCodec.deserialize(text)
        } catch (e: SyncFormatException) {
            unreadable += text
            null
        }
    }

    private val settings = json.obj("settings")
    private val localJson = json.obj("local")

    val scope: SyncScope = when (settings.getValue("scope").jsonPrimitive().intOrNull) {
        0 -> SyncScope.All
        1 -> SyncScope.SelectedNotes(settings.arr("noteIds").map { UUID.fromString(it.jsonPrimitive().content) }.toSet())
        2 -> SyncScope.Tag(settings["tag"]?.jsonPrimitive()?.contentOrNull)
        else -> error("alcance desconocido en $name")
    }

    val localNotes: List<LocalNote> = localJson.arr("notes").map {
        LocalNote(readNote(it.jsonObject), it.jsonObject["authorDeviceId"]?.jsonPrimitive()?.contentOrNull)
    }
    val localTombstones: List<LocalTombstone> = localJson.arr("tombstones").map {
        val o = it.jsonObject
        LocalTombstone(uuid(o.str("noteId")), date(o.str("deletedAt")), o.str("deviceId"))
    }
    val bases: Map<UUID, SyncBase> = localJson.arr("bases").associate {
        val o = it.jsonObject
        uuid(o.str("noteId")) to SyncBase(date(o.str("updatedAt")), o.str("deviceId"))
    }

    fun input(now: OffsetDateTime) = SyncInput(
        deviceId = settings.str("deviceId"),
        scope = scope,
        localNotes = localNotes,
        localTombstones = localTombstones,
        bases = bases,
        excludedIds = emptySet(),
        remote = remote,
        now = now,
    )

    companion object {
        private val dir = File(System.getProperty("aldune.vectors.dir") ?: error("falta aldune.vectors.dir"))

        val all: List<Scenario> by lazy {
            val root = Json.parseToJsonElement(File(dir, "sync-scenarios.json").readText(Charsets.UTF_8)).jsonObject
            root.arr("scenarios").map { Scenario(it.jsonObject.str("name"), it.jsonObject, root) }
        }

        /** Los escenarios cuyo nombre empieza por alguno de los códigos (`"E06"`, `"E12"`…). */
        fun named(vararg codes: String): List<Scenario> = all.filter { s -> codes.any { s.name.startsWith(it) } }
    }
}

// ---- lectura del JSON de los vectores ----

private fun JsonElement.jsonPrimitive(): JsonPrimitive = this as JsonPrimitive

fun JsonObject.str(name: String): String = (getValue(name) as JsonPrimitive).content
fun JsonObject.obj(name: String): JsonObject = getValue(name).jsonObject
fun JsonObject.arr(name: String): JsonArray = getValue(name).jsonArray

fun hex(text: String) = ByteArray(text.length / 2) { text.substring(it * 2, it * 2 + 2).toInt(16).toByte() }
fun uuid(text: String): UUID = UUID.fromString(text)
fun date(text: String): OffsetDateTime = OffsetDateTime.parse(text)

fun readNote(o: JsonObject): SyncNote = SyncNote(
    id = uuid(o.str("id")),
    text = o.str("text"),
    color = o.str("color"),
    createdAt = date(o.str("createdAt")),
    updatedAt = date(o.str("updatedAt")),
    state = (o.getValue("state") as JsonPrimitive).intOrNull!!,
    screenOrigin = o.str("screenOrigin"),
    tags = o.arr("tags").map { (it as JsonPrimitive).content },
    isProtected = (o.getValue("isProtected") as JsonPrimitive).booleanOrNull!!,
    protectedContent = (o["protectedContent"] as? JsonObject)?.let {
        ProtectedNoteContent(
            version = (it.getValue("version") as JsonPrimitive).intOrNull!!,
            salt = it.str("salt"), cipherText = it.str("cipherText"), nonce = it.str("nonce"), tag = it.str("tag"),
        )
    },
    dockPosition = (o["dockPosition"] as? JsonPrimitive)?.takeIf { it !is JsonNull }?.doubleOrNull,
)

/** Fecha esperada: un instante, o `<now>` = la que puso el reloj de la pasada. */
fun sameDate(expected: String, actual: OffsetDateTime, now: OffsetDateTime): Boolean =
    if (expected == NOW_MARKER) actual.toInstant() == now.toInstant() else date(expected).toInstant() == actual.toInstant()

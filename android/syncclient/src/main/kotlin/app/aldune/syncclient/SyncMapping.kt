package app.aldune.syncclient

import app.aldune.core.ProfileCodes
import app.aldune.core.ProtectedNoteContent
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteState
import app.aldune.sync.LocalNote
import app.aldune.sync.LocalTombstone
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncInput
import app.aldune.sync.SyncScope
import app.aldune.text.ProtectedTitleRules
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/** Lo que el perfil guardado dice sobre qué se sincroniza. Hoy solo el alcance; los secretos no pasan por aquí. */
data class SyncClientSettings(val scope: SyncScope = SyncScope.All) {
    companion object {
        /** Alcance de un código de perfil (`ProfileCodes.SCOPE_*`). Un número desconocido no se adivina: ya lo rechaza `ProfileCodes.decode`. */
        fun scopeOf(scope: Int, noteIds: Collection<UUID>, tag: String?): SyncScope = when (scope) {
            ProfileCodes.SCOPE_ALL -> SyncScope.All
            ProfileCodes.SCOPE_CHOSEN -> SyncScope.SelectedNotes(noteIds.toSet())
            ProfileCodes.SCOPE_TAG -> SyncScope.Tag(tag)
            else -> throw IllegalArgumentException("Alcance de sync desconocido: $scope")
        }
    }
}

/**
 * La foto local → lo que necesita `planSync`. [remote] se rellena después de leer el almacén (`SyncInput.copy`); aquí es opcional
 * porque la foto se toma antes de la red.
 *
 * Todo lo que no cuadra con el formato (ids que no son UUID, `deviceId` mal formado, JSON protegido ilegible) lo escribió esta misma
 * app, así que es un fallo de programa y lanza: saltarse la nota la dejaría sin subir y sin que nadie lo note.
 */
fun buildSyncInput(
    snapshot: LocalSnapshot,
    settings: SyncClientSettings,
    deviceId: String,
    now: Instant,
    remote: List<SyncEnvelope> = emptyList(),
): SyncInput {
    require(DeviceIds.isValid(deviceId)) { "El deviceId debe ser 32 hex en minúsculas" }
    return SyncInput(
        deviceId = deviceId,
        scope = settings.scope,
        localNotes = snapshot.notes.map { LocalNote(it.toSyncNote(), it.syncAuthorDeviceId) },
        // El marcador «local» se conserva: el motor lo sustituye por el deviceId al firmar.
        localTombstones = snapshot.tombstones.map { LocalTombstone(noteUuid(it.noteId), it.deletedAt.toWire(), it.deviceId) },
        bases = snapshot.bases.mapKeys { noteUuid(it.key) },
        // Android no tiene notas vinculadas a archivos.
        excludedIds = emptySet(),
        remote = remote,
        now = now.toWire(),
    )
}

/**
 * Una nota recibida → la nota del dominio. `null` si no se puede representar sin inventar nada: un estado que esta versión no
 * conoce (p. ej. de un formato futuro; al volver a subirla se perdería) o una protegida sin su contenido. Quien aplica la
 * salta, no fija su base y la cuenta como «omitida».
 */
fun toDomainNote(note: SyncNote, authorDeviceId: String): Note? {
    val state = NoteState.fromCode(note.state) ?: return null
    if (note.isProtected && note.protectedContent == null) return null

    // Una protegida no lleva texto aquí: lo que haya en `text` no es suyo (el motor tampoco lo sube).
    val wireText = if (note.isProtected) "" else note.text
    return Note(
        id = note.id.toString(),
        text = LineEnding.normalize(wireText),
        lineEnding = LineEnding.detect(wireText),
        color = note.color,
        createdAt = note.createdAt.toInstant(),
        updatedAt = note.updatedAt.toInstant(),
        state = state,
        tags = note.tags,
        dockPosition = note.dockPosition,
        screenOrigin = note.screenOrigin,
        syncAuthorDeviceId = authorDeviceId,
        isProtected = note.isProtected,
        protectedContentJson = note.protectedContent?.let(::protectedContentToJson),
        // Se normaliza al entrar, como en Windows: un nombre con saltos de línea o enorme no llega a la lista.
        protectedTitle = ProtectedTitleRules.visible(note.isProtected, note.protectedTitle),
        pinnedToTop = note.pinnedToTop,
        completedTasks = note.completedTasks,
    )
}

private fun Note.toSyncNote(): SyncNote {
    val content = if (isProtected) {
        parseProtectedContent(protectedContentJson ?: error("La nota protegida $id no tiene contenido protegido"), id)
    } else {
        null
    }
    return SyncNote(
        id = noteUuid(id),
        // Una protegida sube con texto vacío aunque el Note traiga otro: si no, su texto viajaría en claro.
        text = if (isProtected) "" else wireText,
        color = color,
        createdAt = createdAt.toWire(),
        updatedAt = updatedAt.toWire(),
        state = state.code,
        screenOrigin = screenOrigin,
        // Tal cual, sin normalizar: la sync compara las etiquetas distinguiendo mayúsculas y cambiarlas crearía conflictos falsos.
        tags = tags,
        isProtected = isProtected,
        protectedContent = content,
        dockPosition = dockPosition,
        protectedTitle = ProtectedTitleRules.visible(isProtected, protectedTitle),
        pinnedToTop = pinnedToTop,
        completedTasks = completedTasks,
    )
}

/** Core escribe los ids en minúsculas con guiones; otra forma se reescribiría al subir y la nota local y la remota dejarían de ser la misma. */
private fun noteUuid(id: String): UUID {
    val uuid = try {
        UUID.fromString(id)
    } catch (e: IllegalArgumentException) {
        throw IllegalArgumentException("El id de nota \"$id\" no es un UUID", e)
    }
    require(uuid.toString() == id) { "El id de nota \"$id\" no está en la forma canónica (minúsculas con guiones)" }
    return uuid
}

/**
 * UTC y con la precisión de .NET (100 ns): con más decimales la cabecera y el contenido cifrado del sobre dirían fechas distintas
 * (ver `toTicks` en SyncPlanner.kt), y dos pasadas seguidas de la misma nota darían sobres con fecha distinta.
 */
internal fun Instant.toWire(): OffsetDateTime =
    OffsetDateTime.ofInstant(this, ZoneOffset.UTC).let { it.withNano(it.nano / 100 * 100) }

// --- contenido protegido: opaco para domain, aquí se pasa del JSON de Note al de core y al revés ---

/** Mismas claves y orden que core: se pide a `SyncEnvelopeCodec` que escriba una nota y se saca de ahí el objeto, para no copiar su formato. */
internal fun protectedContentToJson(content: ProtectedNoteContent): String {
    val epoch = OffsetDateTime.ofInstant(Instant.EPOCH, ZoneOffset.UTC)
    val carrier = SyncNote(
        id = UUID(0, 1), text = "", color = "", createdAt = epoch, updatedAt = epoch, state = 0, screenOrigin = "",
        tags = emptyList(), isProtected = true, protectedContent = content, dockPosition = null, protectedTitle = null,
    )
    val document = Json.parseToJsonElement(SyncEnvelopeCodec.noteToJson(carrier)) as JsonObject
    return document.getValue("protectedContent").toString()
}

/** Lee el JSON guardado; las claves no distinguen mayúsculas, como en core. Si no se entiende, es un fallo de programa (lo escribió esta app). */
internal fun parseProtectedContent(json: String, noteId: String): ProtectedNoteContent {
    val obj = try {
        Json.parseToJsonElement(json) as? JsonObject
    } catch (e: Exception) {
        null
    } ?: throw IllegalStateException("El contenido protegido de la nota $noteId no es un objeto JSON")

    fun text(name: String): String {
        val value = obj.entries.firstOrNull { it.key.equals(name, ignoreCase = true) }?.value as? JsonPrimitive
        return value?.takeIf { it.isString }?.content
            ?: throw IllegalStateException("Al contenido protegido de la nota $noteId le falta \"$name\"")
    }
    val version = obj.entries.firstOrNull { it.key.equals("version", ignoreCase = true) }?.value as? JsonPrimitive
    return ProtectedNoteContent(
        version = version?.content?.toIntOrNull() ?: ProtectedNoteContent.CURRENT_VERSION,
        salt = text("salt"), cipherText = text("cipherText"), nonce = text("nonce"), tag = text("tag"),
    )
}

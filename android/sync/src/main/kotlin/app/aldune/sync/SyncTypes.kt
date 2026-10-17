package app.aldune.sync

import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncNote
import java.time.OffsetDateTime
import java.util.UUID

/** Alcance de la sincronización (`SyncScopeKind` de Core: 0 todas, 1 elegidas, 2 etiqueta). */
sealed interface SyncScope {
    data object All : SyncScope

    data class SelectedNotes(val noteIds: Set<UUID>) : SyncScope

    /** Una etiqueta nula o en blanco cuenta como «todas», igual que en `SyncScopeFilter`. */
    data class Tag(val tag: String?) : SyncScope
}

/** Una nota de este equipo con el autor de su versión: null = la editó este equipo (sube con su `deviceId`). */
data class LocalNote(val note: SyncNote, val authorDeviceId: String? = null)

/**
 * Borrado hecho aquí o recibido. Los hechos aquí se guardan con el marcador `"local"` (o vacío) y
 * suben firmados con el `deviceId` propio; los recibidos conservan el de su autor.
 */
data class LocalTombstone(val noteId: UUID, val deletedAt: OffsetDateTime, val deviceId: String)

/** Última versión en la que los dos lados estuvieron de acuerdo (`SyncBaseVersion` de Core). */
data class SyncBase(val updatedAt: OffsetDateTime, val deviceId: String) {
    /** Fecha por instante (el desfase no cuenta) y `deviceId` ordinal, como `SyncBaseVersion.Matches`. */
    fun matches(envelope: SyncEnvelope): Boolean =
        envelope.updatedAt.toInstant() == updatedAt.toInstant() && envelope.deviceId == deviceId

    companion object {
        fun of(envelope: SyncEnvelope) = SyncBase(envelope.updatedAt, envelope.deviceId)
    }
}

/**
 * Todo lo que el motor necesita para decidir, sin tocar nada. [remote] son los objetos que el
 * transporte pudo leer (el formato desconocido ya se descartó al deserializar). [now] es el reloj
 * leído una sola vez por quien llama: entra como dato porque el plan contiene la nota mandada a la
 * papelera ya con su fecha nueva, y así `planSync` sigue siendo una función pura.
 */
data class SyncInput(
    val deviceId: String,
    val scope: SyncScope,
    val localNotes: List<LocalNote>,
    val localTombstones: List<LocalTombstone>,
    val bases: Map<UUID, SyncBase>,
    /** Notas vinculadas a un archivo sin «sincronizar esta nota»: ni suben ni las toca nada que llegue de fuera. */
    val excludedIds: Set<UUID>,
    val remote: List<SyncEnvelope>,
    val now: OffsetDateTime,
)

/** Una de las dos versiones de un conflicto. De la ganadora solo importan fecha y dispositivo: la nota viva es ella. */
data class ConflictVersion(
    val updatedAt: OffsetDateTime,
    val deviceId: String,
    val tombstone: Boolean,
    val note: SyncNote?,
)

data class SyncConflict(
    val noteId: UUID,
    val detectedAt: OffsetDateTime,
    val winner: ConflictVersion,
    val losing: ConflictVersion,
)

/** Lo que hay que hacer, en el orden en que `SynchronizeCore` lo haría. */
sealed interface SyncAction {
    /** Sube una nota o un borrado ya cifrado y firmado (`envelope.tombstone`). */
    data class Upload(val envelope: SyncEnvelope) : SyncAction

    /** Aplica aquí la versión remota; [authorDeviceId] es quien la escribió, para no resubirla después. */
    data class ApplyNote(val note: SyncNote, val authorDeviceId: String) : SyncAction

    data class ApplyTombstone(val noteId: UUID, val deletedAt: OffsetDateTime, val deviceId: String) : SyncAction

    /** Borrado remoto sin firma: la nota va a la papelera con fecha [at] y se vuelve a publicar. Nunca se borra. */
    data class TrashNote(val noteId: UUID, val at: OffsetDateTime) : SyncAction

    /** Quita el objeto del almacén (borrado sin firma con fecha futura: es falso seguro). */
    data class DeleteRemote(val noteId: UUID) : SyncAction

    data class SaveConflict(val conflict: SyncConflict) : SyncAction
}

/**
 * Resultado de decidir. Si [error] no es null no hay acciones ni bases: la pasada no escribe nada.
 * Los contadores salen de las acciones y son los de `SyncResult` de Core.
 */
data class SyncPlan(
    val actions: List<SyncAction> = emptyList(),
    /** Solo las bases que cambian; las demás siguen como estaban. */
    val newBases: Map<UUID, SyncBase> = emptyMap(),
    val error: String? = null,
) {
    val uploaded: Int get() = actions.count { it is SyncAction.Upload }
    val downloaded: Int get() = actions.count { it is SyncAction.ApplyNote || it is SyncAction.ApplyTombstone }
    val tombstonesApplied: Int get() = actions.count { it is SyncAction.ApplyTombstone }
    val conflictsResolved: Int get() = actions.count { it is SyncAction.SaveConflict }
}

/** Equivale a `SyncResult` de Core. */
data class SyncResult(
    val uploaded: Int,
    val downloaded: Int,
    val conflictsResolved: Int,
    val tombstonesApplied: Int,
    val error: String? = null,
) {
    val succeeded: Boolean get() = error == null
}

/** El lado local: en la app lo implementa Room (una transacción); aquí, un doble en memoria. */
interface LocalStore {
    fun applyNote(note: SyncNote, authorDeviceId: String)

    fun applyTombstone(noteId: UUID, deletedAt: OffsetDateTime, deviceId: String)

    /** Estado «papelera», `updatedAt` = [at] y autor «este equipo» (como `SetState`). */
    fun trashNote(noteId: UUID, at: OffsetDateTime)

    fun saveConflict(conflict: SyncConflict)

    /** Se llama una sola vez, al final de la pasada, solo con las bases que cambian. */
    fun setBases(bases: Map<UUID, SyncBase>)
}

/** El almacén remoto, sea carpeta, servidor o WebDAV. */
interface SyncTransport {
    /** Los objetos legibles; los de formato desconocido los trata el transporte, no el motor. */
    fun readAll(): List<SyncEnvelope>

    fun write(envelope: SyncEnvelope)

    fun delete(noteId: UUID)
}

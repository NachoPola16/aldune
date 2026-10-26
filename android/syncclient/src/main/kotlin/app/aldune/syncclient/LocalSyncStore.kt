package app.aldune.syncclient

import app.aldune.domain.Note
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncConflict
import java.time.Instant

/**
 * Lo que la sync necesita del almacén local, en transacciones. Lo implementa Room (en `app`) y [InMemoryLocalSyncStore] (aquí);
 * las dos pasan [LocalSyncStoreContract]. Todo síncrono: quien llama decide el hilo.
 */
interface LocalSyncStore {
    /** Notas, lápidas y bases leídas en una sola transacción. */
    fun snapshot(): LocalSnapshot

    /**
     * Aplica todo lo local de una pasada en UNA transacción: o entra todo o no entra nada. Nunca hace red.
     * Devuelve qué se aplicó y qué se saltó por una edición hecha mientras la sync esperaba al almacén (ver [LocalCommit]).
     */
    fun commit(commit: LocalCommit): CommitReport

    /** Ids de las notas con un conflicto guardado. */
    fun conflictedNoteIds(): Set<String>

    /** Primera conexión → «descartar»: borra notas, lápidas, bases y conflictos SIN crear lápidas (si se crearan, subirían borrados firmados). */
    fun discardAllLocal()

    /**
     * Primera conexión sin notas pero con lápidas: borra las lápidas y las bases y conflictos, y **deja las notas**. Si se usara
     * [discardAllLocal], una nota escrita mientras se guardaba el perfil (entre la foto y este paso) se perdería.
     */
    fun discardTombstones()

    /** Desconectar: borra bases y conflictos (son de ese perfil); las notas y las lápidas se quedan. */
    fun forgetSyncState()
}

/**
 * La versión de una nota local tal como la vio la foto. El `commit` la compara con la actual: si ya no coincide, alguien
 * (el editor, un cambio de color) tocó la nota mientras la sync hablaba con el servidor.
 */
sealed interface ExpectedVersion {
    /** No había ni nota ni lápida. */
    data object Absent : ExpectedVersion

    data class Existing(val updatedAt: Instant, val authorDeviceId: String?) : ExpectedVersion

    /** No había nota pero sí una lápida. */
    data object Tombstoned : ExpectedVersion

    companion object {
        fun of(snapshot: LocalSnapshot, noteId: String): ExpectedVersion {
            val note = snapshot.notes.firstOrNull { it.id == noteId }
            return when {
                note != null -> Existing(note.updatedAt, note.syncAuthorDeviceId)
                snapshot.tombstones.any { it.noteId == noteId } -> Tombstoned
                else -> Absent
            }
        }
    }
}

/** Una acción local del plan, ya en tipos del dominio (la conversión desde el formato la hace `runSync`, no cada almacén). */
sealed interface LocalAction {
    val noteId: String

    /** Inserta o reemplaza con el autor remoto en `syncAuthorDeviceId` y quita la lápida local de ese id si la hay (`ApplySyncNote` de Core). */
    data class ApplyNote(val note: Note) : LocalAction {
        override val noteId: String get() = note.id
    }

    /** Borra la nota y guarda la lápida con el [deviceId] del autor remoto, para no volver a firmarla como propia. */
    data class ApplyTombstone(override val noteId: String, val deletedAt: Instant, val deviceId: String) : LocalAction

    /** Papelera con fecha [at] y autor «este equipo», como `SetState`. Si la nota no existe no hace nada. */
    data class TrashNote(override val noteId: String, val at: Instant) : LocalAction

    /** Guarda la versión perdedora entera; si ya había un conflicto para esa nota, lo reemplaza. */
    data class SaveConflict(val conflict: SyncConflict) : LocalAction {
        override val noteId: String get() = conflict.noteId.toString()
    }
}

/**
 * Lo que `runSync` entrega al almacén en la fase 2: las acciones locales del plan, las bases nuevas y, por cada nota con acciones,
 * la versión que se vio en la foto.
 *
 * **Concurrencia optimista.** Si la versión actual de una nota con acciones no coincide con [expected], se saltan *todas* sus acciones
 * y *no se fija su base*. Con la base vieja, la pasada siguiente ve los dos lados cambiados y guarda un conflicto real si lo hay, en
 * vez de pisar la edición en silencio. Las bases de notas sin acciones locales se fijan siempre: lo que dicen es verdad en el almacén.
 */
data class LocalCommit(
    val actions: List<LocalAction>,
    val newBases: Map<String, SyncBase>,
    val expected: Map<String, ExpectedVersion>,
) {
    init {
        val missing = actions.map { it.noteId }.toSet() - expected.keys
        require(missing.isEmpty()) { "Faltan las versiones esperadas de: $missing" }
    }
}

/** Qué hizo el `commit`. [skippedConcurrent] son las notas cuyas acciones y base se saltaron por una edición concurrente. */
data class CommitReport(
    val appliedNotes: Int = 0,
    val appliedTombstones: Int = 0,
    val trashed: Int = 0,
    val conflictsSaved: Int = 0,
    val basesSet: Int = 0,
    val skippedConcurrent: Set<String> = emptySet(),
)

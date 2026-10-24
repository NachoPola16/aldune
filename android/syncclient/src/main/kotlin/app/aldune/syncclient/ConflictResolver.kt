package app.aldune.syncclient

import app.aldune.domain.Clock
import app.aldune.domain.NoteError
import app.aldune.domain.NoteResult
import app.aldune.domain.NoteStore
import app.aldune.domain.ReminderRules
import app.aldune.domain.ReminderStore
import app.aldune.sync.SyncConflict

/** Qué clase de conflicto es, que decide qué se le enseña al usuario y qué significan sus dos botones. */
enum class ConflictKind {
    /** La nota viva existe y la perdedora también es una nota: se ve la otra versión y se elige. */
    EditVsEdit,

    /** La nota viva existe y la perdedora es un borrado hecho en otro equipo: franja «Se borró en otro equipo». */
    EditVsDelete,

    /** No hay nota viva: ganó un borrado. Va a la lista aparte de conflictos. */
    Orphan,
}

/**
 * Quita la lápida local de una nota que se recupera. `NoteStore.put` no la toca a propósito (resucitar una nota es cosa de la sync, que
 * lo hace con `ApplyNote`), y mientras la lápida siga ahí el planificador la prefiere a la nota con el mismo id: la nota «recuperada»
 * no volvería a subirse nunca. En la app lo cierra Room; en los tests, `InMemoryNoteStore.removeTombstone`.
 */
fun interface TombstoneEraser {
    fun erase(noteId: String)
}

/**
 * Resolver un conflicto de sync: quedarse con lo que hay o con la otra versión (port de `DismissConflict` y `RestoreConflict`).
 * Sin interfaz y sin red: trabaja sobre el mismo [NoteStore] que `NoteService` y descarta el conflicto de [conflicts].
 *
 * `useOther` es lo que hace Windows: la otra versión se aplica como versión **nueva** (`updatedAt` = ahora, autor «este equipo»), para
 * que la próxima sync publique la decisión. Una perdedora protegida se resuelve sin abrirla: se copia su contenido cifrado tal cual.
 *
 * @param deviceId el de este equipo. Hoy no se usa: el borrado deja la lápida con el marcador `local` y la sync la firma con él. Se recibe
 * ya (como en `NoteService`) para no cambiar la firma si algún día la lápida se firma aquí.
 */
class ConflictResolver(
    private val notes: NoteStore,
    private val conflicts: ConflictStore,
    private val reminders: ReminderStore,
    private val clock: Clock,
    @Suppress("unused") private val deviceId: String,
    private val tombstones: TombstoneEraser,
    private val lock: Any = Any(),
) {

    fun kind(conflict: SyncConflict): ConflictKind = when {
        notes.get(conflict.noteId.toString()) == null -> ConflictKind.Orphan
        conflict.losing.tombstone -> ConflictKind.EditVsDelete
        else -> ConflictKind.EditVsEdit
    }

    /** Los conflictos sin nota viva, los más recientes primero: la lista «N conflictos». */
    fun orphans(): List<SyncConflict> = synchronized(lock) {
        conflicts.all().filter { kind(it) == ConflictKind.Orphan }
            .sortedWith(compareByDescending<SyncConflict> { it.detectedAt.toInstant() }.thenBy { it.noteId })
    }

    /** Se queda con la versión de aquí: descarta el conflicto y no toca la nota. [NoteError.NotFound] si ya no existe. */
    fun keepCurrent(noteId: String): NoteResult<Unit> = synchronized(lock) {
        if (conflicts.get(noteId) == null) return NoteResult.Err(NoteError.NotFound(noteId))
        conflicts.delete(noteId)
        NoteResult.Ok(Unit)
    }

    /**
     * Se queda con la otra versión. Si la perdedora es una nota, la aplica; si es un borrado, borra la nota viva (con lápida local).
     * Después descarta el conflicto y limpia el recordatorio de la nota si ésta ya no está activa o ya no existe.
     *
     * Si el conflicto ya no existe ([NoteError.NotFound]) o la perdedora no se puede representar ([NoteError.InvalidArgument]: un estado
     * desconocido, una protegida sin contenido) no se escribe nada y el conflicto se queda donde estaba.
     */
    fun useOther(noteId: String): NoteResult<Unit> = synchronized(lock) {
        val conflict = conflicts.get(noteId) ?: return NoteResult.Err(NoteError.NotFound(noteId))
        val losing = conflict.losing

        if (losing.tombstone) {
            notes.delete(noteId, clock.now())
        } else {
            val other = losing.note
                ?: return NoteResult.Err(NoteError.InvalidArgument("La versión perdedora de $noteId no trae nota ni borrado"))
            val restored = toDomainNote(other, losing.deviceId)
                ?: return NoteResult.Err(NoteError.InvalidArgument("La versión perdedora de $noteId no se puede representar"))
            notes.put(restored.copy(updatedAt = clock.now(), syncAuthorDeviceId = null))
            tombstones.erase(noteId)
        }
        conflicts.delete(noteId)

        // Sólo el recordatorio de esta nota: las demás se limpian en su momento (arranque y tras cada sync).
        ReminderRules.orphans(listOfNotNull(reminders.get(noteId)), notes.all()).forEach { reminders.clear(it.noteId) }
        NoteResult.Ok(Unit)
    }
}

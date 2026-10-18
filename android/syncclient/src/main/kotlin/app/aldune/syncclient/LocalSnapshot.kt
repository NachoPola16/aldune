package app.aldune.syncclient

import app.aldune.domain.Note
import app.aldune.domain.Tombstone
import app.aldune.sync.SyncBase

/**
 * Lo local leído en **una** transacción: las notas, las lápidas y las bases tienen que ser coherentes entre sí
 * (una base de una nota que ya no está o una lápida de una nota que acaba de volver darían un plan equivocado).
 * Las bases van por id de nota (el `String` de [Note.id]).
 */
data class LocalSnapshot(
    val notes: List<Note>,
    val tombstones: List<Tombstone>,
    val bases: Map<String, SyncBase>,
)

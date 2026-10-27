package app.aldune.sync

import app.aldune.core.NoteState
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncFormatException
import app.aldune.core.SyncNote
import app.aldune.core.SyncVersion
import java.security.GeneralSecurityException
import java.time.OffsetDateTime
import java.time.temporal.ChronoUnit
import java.util.UUID

/** Mensaje exacto de Core cuando ninguna nota del almacén descifra (casi seguro, acceso revocado). */
const val NOTHING_READABLE_ERROR =
    "This device's sync code can't read the notes in the store. If access was revoked, import the new code."

/**
 * Decide qué hace una pasada de sincronización. Es `SyncService.SynchronizeCore` (Aldune.Core) sin sus
 * efectos: no lee ni escribe nada, solo devuelve las acciones. Replica Core **incluidas sus rarezas**
 * (las fijan los vectores de `tests/vectors/sync-scenarios.json`); si un caso parece un fallo, se
 * arregla primero en Core y se regeneran los vectores, no aquí.
 *
 * Lo único no determinista es el nonce de cada sobre que se cifra para subir.
 */
fun planSync(input: SyncInput, key: ByteArray): SyncPlan {
    val now = input.now.toTicks()
    val classified = classifyRemote(input.remote, key)
    if (classified.noReadableNotes) {
        // Ninguna nota del almacén descifra con este código: casi seguro que se revocó el acceso. Se para antes
        // de escribir nada, porque subir las notas de aquí con la clave vieja dejaría sin sync al resto.
        return SyncPlan(error = NOTHING_READABLE_ERROR)
    }

    var remote: Map<UUID, RemoteItem> = classified.readable
        .groupBy { it.envelope.noteId }
        .mapValues { (_, versions) -> versions.reduce { best, next -> if (compareVersions(next.envelope, best.envelope) > 0) next else best } }

    val scope = input.scope
    val scopedIds = (scope as? SyncScope.SelectedNotes)?.noteIds
    val tag = (scope as? SyncScope.Tag)?.tag?.takeIf { it.isNotBlank() }
    if (tag != null) {
        remote = remote.filterValues { item ->
            item.envelope.tombstone || item.note!!.tags.any { it.equals(tag, ignoreCase = true) }
        }
    }
    if (scopedIds != null) remote = remote.filterKeys { it in scopedIds }

    // Una nota vinculada sin sync es solo de este equipo: lo que llegue de fuera con su id no puede pisarla
    // ni borrarla.
    if (input.excludedIds.isNotEmpty()) remote = remote.filterKeys { it !in input.excludedIds }

    val actions = mutableListOf<SyncAction>()
    val allNotes = input.localNotes
    val future = now.plusDays(1)
    for (item in classified.unsignedTombstones) {
        val id = item.envelope.noteId
        if (id in input.excludedIds) continue
        if (id in remote || (scopedIds != null && id !in scopedIds)) continue

        if (item.envelope.updatedAt.toInstant().isAfter(future.toInstant())) {
            actions += SyncAction.DeleteRemote(id)
            continue
        }

        // Ya no se manda a la papelera (igual que en Core): quien tiene el token pero no la clave podía vaciar las notas
        // con borrados sin firma. Si la nota existe aquí se quita el objeto, que con fecha más nueva impediría republicarla.
        if (allNotes.any { it.note.id == id }) actions += SyncAction.DeleteRemote(id)
    }

    val localNotes = allNotes.filter { includes(it.note, scope, input.excludedIds) }.associateBy { it.note.id }

    // Con alcance por etiqueta, una nota que perdió aquí la etiqueta sale del lado local, pero su versión anterior
    // (aún etiquetada) sigue en el almacén y sí entra en el remoto. Sin esto se tomaba por una nota que falta y se
    // aplicaba encima, deshaciendo lo editado aquí después.
    val leftTheTag: Map<UUID, OffsetDateTime> = if (scope is SyncScope.Tag) {
        allNotes.filter { it.note.id !in input.excludedIds && it.note.id !in localNotes }
            .associate { it.note.id to it.note.updatedAt }
    } else {
        emptyMap()
    }

    // Los borrados locales se filtran por ids del alcance pero no por etiqueta (asimetría actual de Core, E26).
    val localTombstones = input.localTombstones
        .filter { scopedIds == null || it.noteId in scopedIds }
        .associateBy { it.noteId }

    val local = createLocalEnvelopes(localNotes.values, localTombstones.values, key, input.deviceId)

    val settled = linkedMapOf<UUID, SyncBase>()
    fun settle(id: UUID, agreed: SyncEnvelope) {
        val known = input.bases[id]
        if (known == null || !known.matches(agreed)) settled[id] = SyncBase.of(agreed)
    }

    fun applyRemote(item: RemoteItem) {
        val envelope = item.envelope
        actions += if (envelope.tombstone) {
            SyncAction.ApplyTombstone(envelope.noteId, envelope.updatedAt, envelope.deviceId)
        } else {
            SyncAction.ApplyNote(item.note!!, envelope.deviceId)
        }
    }

    for (id in (local.keys + remote.keys).toCollection(linkedSetOf())) {
        val localEnvelope = local[id]
        val remoteItem = remote[id]

        if (localEnvelope == null) {
            val remoteEnvelope = remoteItem!!.envelope
            // Lo remoto solo gana si es más reciente: así sigue llegando que otro equipo la etiquete después.
            val editedHere = leftTheTag[id]
            if (editedHere != null && editedHere.toInstant().isAfter(remoteEnvelope.updatedAt.toInstant())) continue
            // Un borrado remoto no alcanza a una nota que aquí está fuera del alcance (igual que en Core).
            if (editedHere != null && remoteEnvelope.tombstone) continue
            applyRemote(remoteItem)
            settle(id, remoteEnvelope)
            continue
        }

        if (remoteItem == null) {
            actions += SyncAction.Upload(localEnvelope)
            settle(id, localEnvelope)
            continue
        }

        val remoteEnvelope = remoteItem.envelope
        val comparison = compareVersions(localEnvelope, remoteEnvelope)
        if (comparison != 0 && isRealConflict(localEnvelope, remoteEnvelope, input.bases[id])) {
            val localVersion = ConflictVersion(
                localEnvelope.updatedAt, localEnvelope.deviceId, localEnvelope.tombstone,
                if (localEnvelope.tombstone) null else localNotes[id]?.note?.forSync(),
            )
            val remoteVersion = ConflictVersion(
                remoteEnvelope.updatedAt, remoteEnvelope.deviceId, remoteEnvelope.tombstone, remoteItem.note,
            )
            // Los dos lados cambiaron, pero al mismo contenido: no hay nada que elegir. Se sigue el camino normal
            // (gana la más reciente) sin dejar un falso conflicto.
            if (!sameContent(localVersion.note, remoteVersion.note)) {
                val winner = if (comparison > 0) localVersion else remoteVersion
                val losing = if (comparison > 0) remoteVersion else localVersion
                actions += SyncAction.SaveConflict(SyncConflict(id, now, winner, losing))
            }
        }
        if (comparison > 0) {
            actions += SyncAction.Upload(localEnvelope)
        } else if (comparison < 0) {
            applyRemote(remoteItem)
        }
        settle(id, if (comparison >= 0) localEnvelope else remoteEnvelope)
    }

    return SyncPlan(actions, settled)
}

private class RemoteItem(val envelope: SyncEnvelope, val note: SyncNote?)

private class RemoteClassification(
    val readable: List<RemoteItem>,
    val unsignedTombstones: List<RemoteItem>,
    val noReadableNotes: Boolean,
)

/**
 * Separa lo que hay en el almacén antes de agrupar por nota, para que un fichero añadido no pueda tapar al
 * auténtico de la misma nota (`ClassifyRemote` de Core): notas que descifran y borrados firmados se procesan;
 * los borrados sin firma van aparte; una nota que no descifra se salta, salvo que no descifre ninguna.
 */
private fun classifyRemote(objects: List<SyncEnvelope>, key: ByteArray): RemoteClassification {
    val readable = mutableListOf<RemoteItem>()
    val unsigned = mutableListOf<RemoteItem>()
    var notes = 0
    var unreadableNotes = 0

    for (envelope in objects) {
        if (envelope.tombstone) {
            if (SyncEnvelopeCodec.isAuthenticTombstone(envelope, key)) readable += RemoteItem(envelope, null)
            else unsigned += RemoteItem(envelope, null)
            continue
        }

        notes++
        try {
            readable += RemoteItem(envelope, SyncEnvelopeCodec.decryptNote(envelope, key))
        } catch (e: GeneralSecurityException) {
            unreadableNotes++
        } catch (e: SyncFormatException) {
            unreadableNotes++
        } catch (e: IllegalArgumentException) {
            // base64 roto o tamaños de nonce/etiqueta incorrectos: el equivalente a FormatException de Core
            unreadableNotes++
        }
    }

    return RemoteClassification(readable, unsigned, notes > 0 && unreadableNotes == notes)
}

private fun compareVersions(left: SyncEnvelope, right: SyncEnvelope): Int =
    SyncVersion.compare(SyncVersion.of(left), SyncVersion.of(right))

/** `SyncScopeFilter.Includes` de Core. */
private fun includes(note: SyncNote, scope: SyncScope, excludedIds: Set<UUID>): Boolean {
    if (note.id in excludedIds) return false
    return when (scope) {
        SyncScope.All -> true
        is SyncScope.SelectedNotes -> note.id in scope.noteIds
        is SyncScope.Tag -> scope.tag.isNullOrBlank() || note.tags.any { it.equals(scope.tag, ignoreCase = true) }
    }
}

/**
 * Presenta cada versión local con su autor real: lo recibido de otro dispositivo conserva el id de quien lo
 * escribió, y solo lo editado aquí lleva el de este equipo. Si se firmara todo con el id local, una versión
 * recibida nunca sería igual a la del almacén y se resubiría o reaplicaría en cada sincronización.
 */
private fun createLocalEnvelopes(
    notes: Collection<LocalNote>,
    tombstones: Collection<LocalTombstone>,
    key: ByteArray,
    deviceId: String,
): Map<UUID, SyncEnvelope> {
    val local = linkedMapOf<UUID, SyncEnvelope>()
    for (item in notes) {
        val author = item.authorDeviceId?.takeIf { it.isNotBlank() } ?: deviceId
        local[item.note.id] = SyncEnvelopeCodec.createNote(item.note.forSync(), key, author)
    }
    // Los borrados hechos aquí llevan el marcador «local»; los recibidos, su autor. Si una nota y un borrado
    // comparten id, manda el borrado, como en Core.
    for (tombstone in tombstones) {
        val author = if (tombstone.deviceId == "local" || tombstone.deviceId.isEmpty()) deviceId else tombstone.deviceId
        local[tombstone.noteId] = SyncEnvelopeCodec.createTombstone(tombstone.noteId, tombstone.deletedAt, key, author)
    }
    return local
}

/** Una nota protegida sube con `text: ""` siempre, aunque esté desbloqueada en memoria: si no, su texto viajaría en claro. */
private fun SyncNote.forSync(): SyncNote = if (isProtected && text.isNotEmpty()) copy(text = "") else this

/**
 * Solo es conflicto si los dos lados cambiaron desde la última versión común: si uno sigue en la base, la otra
 * es una actualización normal. Sin base se guarda el conflicto por prudencia, salvo con la misma fecha (la misma
 * versión firmada por dispositivos distintos).
 */
private fun isRealConflict(local: SyncEnvelope, remote: SyncEnvelope, common: SyncBase?): Boolean {
    if (local.updatedAt.toInstant() == remote.updatedAt.toInstant()) return false
    if (common == null) return true
    return !common.matches(local) && !onlyMigrated(local, common) &&
        !common.matches(remote) && !onlyMigrated(remote, common)
}

// La migración de temas de la 1.5 recolorea con la fecha de la nota + 1 ms: una versión justo 1 ms posterior a la
// acordada es la acordada recoloreada sin más, y si el otro lado la editó no hay nada que elegir.
private fun onlyMigrated(version: SyncEnvelope, common: SyncBase): Boolean =
    version.updatedAt.toInstant() == common.updatedAt.toInstant().plusMillis(1)

/** `NoteEquivalence.SameContent` de Core: lo que el usuario ve y edita, no la fecha ni quién firmó. */
private fun sameContent(a: SyncNote?, b: SyncNote?): Boolean {
    if (a == null || b == null) return false
    return a.text == b.text &&
        a.color.equals(b.color, ignoreCase = true) &&
        a.state == b.state &&
        a.dockPosition == b.dockPosition &&
        a.isProtected == b.isProtected &&
        a.tags.sorted() == b.tags.sorted() &&
        a.protectedContent == b.protectedContent
}

/**
 * La fecha que escribe el sobre tiene la precisión de .NET (100 ns). Una fecha con más decimales llegaría al
 * almacén distinta en la cabecera y en el contenido cifrado, y la nota se rechazaría al leerla.
 */
internal fun OffsetDateTime.toTicks(): OffsetDateTime = truncatedTo(ChronoUnit.NANOS).let { it.withNano(it.nano / 100 * 100) }

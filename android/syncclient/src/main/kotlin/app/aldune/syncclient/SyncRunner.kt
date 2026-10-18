package app.aldune.syncclient

import app.aldune.domain.Clock
import app.aldune.sync.SyncAction
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncTransport
import app.aldune.sync.planSync

/** Lo que dejó una pasada. Los contadores de «recibidas», «borrados», «papelera» y «conflictos» son los que **de verdad** se aplicaron. */
sealed interface SyncOutcome {
    data class Success(
        val uploaded: Int = 0,
        val received: Int = 0,
        val tombstonesApplied: Int = 0,
        val trashed: Int = 0,
        val conflicts: Int = 0,
        /** Acciones que no se pudieron representar (p. ej. un estado de nota de un formato futuro): ni se aplican ni se fija su base. */
        val omitted: Int = 0,
        /** Notas editadas mientras la sync esperaba al servidor: no se tocaron; el disparador pide otra pasada pronto (§4.4). */
        val skippedConcurrent: Int = 0,
    ) : SyncOutcome

    data class Failure(val failure: SyncFailure) : SyncOutcome
}

/**
 * Una pasada de sync en dos fases (spec F5 §4.3):
 *
 * ```
 * foto → buildSyncInput → readAll → planSync → FASE 1: transporte (Upload, DeleteRemote, en el orden del plan)
 *                                             → FASE 2: commit local (una transacción, con las bases)
 * ```
 *
 * Las bases nunca se guardan antes de que el almacén remoto tenga lo que dicen: si se escribieran antes y la subida fallara, la pasada
 * siguiente vería la nota local «igual que la base» y aplicaría encima la versión vieja del almacén, perdiendo la edición. Por eso, si
 * la fase 1 falla, **no se escribe nada local** (las subidas ya hechas son idempotentes y la pasada siguiente converge). Y la red nunca
 * ocurre dentro de la transacción local, que bloquearía las escrituras del editor.
 *
 * No usa `execute()` de `android/sync` (intercala y no es transaccional). No es reentrante: quien llama garantiza una sola pasada a la vez.
 *
 * Un fallo del transporte, del plan o del almacén devuelve [SyncOutcome.Failure]. Un dato local que no cuadra con el formato (id que no es
 * UUID…) es un fallo de programa y **lanza**, para que no se quede una nota sin sincronizar en silencio.
 */
fun runSync(
    store: LocalSyncStore,
    transport: SyncTransport,
    key: ByteArray,
    settings: SyncClientSettings,
    deviceId: String,
    clock: Clock,
): SyncOutcome {
    val snapshot = store.snapshot()
    val input = buildSyncInput(snapshot, settings, deviceId, clock.now())

    val remote = try {
        transport.readAll()
    } catch (e: Exception) {
        return SyncOutcome.Failure(classifyFailure(e))
    }

    val plan = planSync(input.copy(remote = remote), key)
    plan.error?.let { return SyncOutcome.Failure(classifyPlanError(it)) }

    // FASE 1: lo que va al almacén remoto, en el orden del plan.
    try {
        for (action in plan.actions) {
            when (action) {
                is SyncAction.Upload -> transport.write(action.envelope)
                is SyncAction.DeleteRemote -> transport.delete(action.noteId)
                else -> Unit
            }
        }
    } catch (e: Exception) {
        return SyncOutcome.Failure(classifyFailure(e))
    }

    // FASE 2: lo local, de una vez.
    val local = ArrayList<LocalAction>()
    val omittedIds = HashSet<String>()
    for (action in plan.actions) {
        when (action) {
            is SyncAction.ApplyNote -> {
                val note = toDomainNote(action.note, action.authorDeviceId)
                if (note != null) local += LocalAction.ApplyNote(note) else omittedIds += action.note.id.toString()
            }
            is SyncAction.ApplyTombstone -> local += LocalAction.ApplyTombstone(action.noteId.toString(), action.deletedAt.toInstant(), action.deviceId)
            is SyncAction.TrashNote -> local += LocalAction.TrashNote(action.noteId.toString(), action.at.toInstant())
            is SyncAction.SaveConflict -> local += LocalAction.SaveConflict(action.conflict)
            is SyncAction.Upload, is SyncAction.DeleteRemote -> Unit
        }
    }
    // Una nota omitida no se aplica entera: ni su conflicto ni su base (con la base vieja, la pasada que sí la entienda la aplicará bien).
    val actions = local.filter { it.noteId !in omittedIds }
    val bases: Map<String, SyncBase> = plan.newBases.mapKeys { it.key.toString() }.filterKeys { it !in omittedIds }

    val report = if (actions.isEmpty() && bases.isEmpty()) {
        CommitReport()
    } else {
        val expected = actions.map { it.noteId }.toSet().associateWith { ExpectedVersion.of(snapshot, it) }
        try {
            store.commit(LocalCommit(actions, bases, expected))
        } catch (e: Exception) {
            return SyncOutcome.Failure(classifyFailure(e))
        }
    }

    return SyncOutcome.Success(
        uploaded = plan.uploaded,
        received = report.appliedNotes,
        tombstonesApplied = report.appliedTombstones,
        trashed = report.trashed,
        conflicts = report.conflictsSaved,
        omitted = omittedIds.size,
        skippedConcurrent = report.skippedConcurrent.size,
    )
}

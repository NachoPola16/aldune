package app.aldune.sync

/**
 * Aplica un plan en el orden en que lo decidió `planSync`. Con un plan de error no escribe nada. Las bases
 * se guardan una sola vez, al final y solo si cambia alguna, como en Core: si la pasada se corta a medias,
 * lo ya aplicado se vuelve a comparar en la siguiente y converge.
 *
 * Los fallos del transporte o del almacén local se propagan: quien llama decide cómo mostrarlos (Core los
 * convierte en el error del `SyncResult`; aquí lo hará el trabajador de la app, que conoce sus excepciones).
 */
fun execute(plan: SyncPlan, local: LocalStore, transport: SyncTransport): SyncResult {
    plan.error?.let { return SyncResult(0, 0, 0, 0, it) }

    for (action in plan.actions) {
        when (action) {
            is SyncAction.Upload -> transport.write(action.envelope)
            is SyncAction.ApplyNote -> local.applyNote(action.note, action.authorDeviceId)
            is SyncAction.ApplyTombstone -> local.applyTombstone(action.noteId, action.deletedAt, action.deviceId)
            is SyncAction.TrashNote -> local.trashNote(action.noteId, action.at)
            is SyncAction.DeleteRemote -> transport.delete(action.noteId)
            is SyncAction.SaveConflict -> local.saveConflict(action.conflict)
        }
    }
    if (plan.newBases.isNotEmpty()) local.setBases(plan.newBases)

    return SyncResult(plan.uploaded, plan.downloaded, plan.conflictsResolved, plan.tombstonesApplied)
}

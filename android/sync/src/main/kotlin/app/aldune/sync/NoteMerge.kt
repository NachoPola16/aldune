package app.aldune.sync

import app.aldune.core.NoteState
import app.aldune.core.SyncNote

data class NoteMergeResult(val merged: SyncNote?, val conflict: Boolean)

/**
 * Fusiona las dos versiones de una nota que cambiaron las dos desde la última acordada ([base]): port de `NoteMerge` de Core.
 * El texto se fusiona por líneas ([LineMerge]); lo que no es contenido (color, fijada, ajuste de tareas hechas, posición) sigue al lado
 * que lo cambió, o al más reciente si lo cambiaron los dos, sin conflicto; las etiquetas se fusionan como conjuntos contra la base.
 * Es simétrica. Hay conflicto con notas protegidas, con las mismas líneas cambiadas distinto, con el estado cambiado distinto en los
 * dos lados y con papelera contra edición de texto. Quien llama pone `updatedAt` al publicar el resultado.
 */
object NoteMerge {
    fun merge(base: SyncNote, local: SyncNote, localDeviceId: String, remote: SyncNote, remoteDeviceId: String): NoteMergeResult {
        if (base.isProtected || local.isProtected || remote.isProtected) return NoteMergeResult(null, true)

        val text = LineMerge.merge(base.text, local.text, remote.text)
        if (text.conflict) return NoteMergeResult(null, true)

        val localTextChanged = local.text != base.text
        val remoteTextChanged = remote.text != base.text
        val localStateChanged = local.state != base.state
        val remoteStateChanged = remote.state != base.state

        val state = when {
            localStateChanged && remoteStateChanged -> {
                if (local.state != remote.state) return NoteMergeResult(null, true)
                local.state
            }
            localStateChanged -> {
                if (local.state == NoteState.TRASH && remoteTextChanged) return NoteMergeResult(null, true)
                local.state
            }
            remoteStateChanged -> {
                if (remote.state == NoteState.TRASH && localTextChanged) return NoteMergeResult(null, true)
                remote.state
            }
            else -> base.state
        }

        val newer = if (compare(local, localDeviceId, remote, remoteDeviceId) > 0) local else remote

        fun <T> scalar(get: (SyncNote) -> T): T {
            val original = get(base)
            val localChanged = get(local) != original
            val remoteChanged = get(remote) != original
            return when {
                localChanged && !remoteChanged -> get(local)
                remoteChanged && !localChanged -> get(remote)
                else -> get(newer)
            }
        }

        val tags = (
            base.tags.filter { it in local.tags && it in remote.tags } +
                local.tags.filter { it !in base.tags } +
                remote.tags.filter { it !in base.tags }
            ).distinct().sorted()

        val merged = SyncNote(
            id = newer.id,
            text = text.text,
            color = scalar { it.color },
            createdAt = newer.createdAt,
            updatedAt = newer.updatedAt,
            state = state,
            screenOrigin = newer.screenOrigin,
            tags = tags,
            isProtected = false,
            protectedContent = null,
            dockPosition = scalar { it.dockPosition },
            pinnedToTop = scalar { it.pinnedToTop },
            completedTasks = scalar { it.completedTasks },
        )
        return NoteMergeResult(merged, false)
    }

    // El mismo orden total que `SyncVersion.compare`: fecha (el instante) y, en empate, id de dispositivo ordinal.
    private fun compare(a: SyncNote, deviceA: String, b: SyncNote, deviceB: String): Int {
        val time = a.updatedAt.toInstant().compareTo(b.updatedAt.toInstant())
        return if (time != 0) time else deviceA.compareTo(deviceB)
    }
}

package app.aldune.export

import android.content.Context
import android.net.Uri
import app.aldune.domain.AutoExportPlanner
import app.aldune.domain.ExportAction
import app.aldune.domain.ExportManifest
import app.aldune.domain.ExportNote

/**
 * Aplica la copia automática sobre una carpeta del sistema: la lógica de qué escribir está en `AutoExportPlanner` (domain, con tests); aquí
 * solo se lee, se escribe y se guarda el manifiesto. Lanza [FolderLostException] si la carpeta ya no está accesible (el permiso se perdió o
 * la borraron) para que quien llama apague la copia y avise una vez, sin reintentar sin fin.
 */
object AutoExportRunner {
    /** Devuelve cuántas acciones se aplicaron. */
    fun run(context: Context, tree: Uri, notes: List<ExportNote>, includeArchived: Boolean, placeholder: String): Int {
        val folder = SafFolder(context, tree)
        if (!folder.rootExists()) throw FolderLostException("la carpeta ya no está accesible")
        try {
            val manifest = ExportManifest.fromJson(folder.readText(ExportManifest.FILE_NAME))
            val plan = AutoExportPlanner.plan(notes, manifest, includeArchived, placeholder, folder::hashOf)
            if (plan.actions.isEmpty() && plan.manifest == manifest) return 0
            try {
                for (action in plan.actions) when (action) {
                    is ExportAction.Write -> folder.write(action.path, action.content)
                    is ExportAction.Move -> folder.move(action.from, action.to)
                }
            } finally {
                // También si una acción falló a medias: sin el manifiesto, lo ya escrito pasaría a ser «ajeno» en la siguiente pasada.
                folder.write(ExportManifest.FILE_NAME, plan.manifest.toJson())
            }
            return plan.actions.size
        } catch (e: SecurityException) {
            throw FolderLostException("el sistema quitó el permiso de la carpeta", e)
        }
    }
}

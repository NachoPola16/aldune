package app.aldune.data

import android.content.Context

/**
 * Cuándo se marcó cada tarea de cada nota (`HiddenTasks`), solo en este dispositivo: no viaja con la sync ni toca la base de datos.
 * Cada nota guarda `clave=milisegundos` separados por comas; las claves son hashes hexadecimales, así que no hay nada que escapar.
 */
class CompletedTasksStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences("completedTasks", Context.MODE_PRIVATE)

    fun load(noteId: String): Map<String, Long> {
        val raw = prefs.getString(noteId, null) ?: return emptyMap()
        val out = LinkedHashMap<String, Long>()
        for (part in raw.split(',')) {
            val i = part.indexOf('=')
            val time = if (i > 0) part.substring(i + 1).toLongOrNull() else null
            if (time != null) out[part.substring(0, i)] = time
        }
        return out
    }

    fun save(noteId: String, completedAt: Map<String, Long>) {
        if (completedAt.isEmpty()) prefs.edit().remove(noteId).apply()
        else prefs.edit().putString(noteId, completedAt.entries.joinToString(",") { "${it.key}=${it.value}" }).apply()
    }
}

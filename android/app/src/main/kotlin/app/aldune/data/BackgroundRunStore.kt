package app.aldune.data

import android.content.Context
import app.aldune.syncclient.BackgroundRun
import app.aldune.syncclient.SyncFailureKind
import java.time.Instant

/**
 * La última pasada del trabajo de segundo plano, para que Ajustes pueda contar si de verdad corre. Va aparte de [AppSettings]: no es una
 * preferencia y cambia sola cada 15 minutos, no debe provocar recomposiciones ni pasar por el mismo flujo que los ajustes.
 */
class BackgroundRunStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences("background_run", Context.MODE_PRIVATE)

    fun load(): BackgroundRun? {
        if (!prefs.contains(AT)) return null
        val failure = prefs.getString(FAILURE, null)?.let { name -> SyncFailureKind.entries.firstOrNull { it.name == name } }
        return BackgroundRun(Instant.ofEpochMilli(prefs.getLong(AT, 0)), failure)
    }

    fun save(run: BackgroundRun) {
        val failure = run.failure
        prefs.edit().putLong(AT, run.at.toEpochMilli()).also { if (failure == null) it.remove(FAILURE) else it.putString(FAILURE, failure.name) }.apply()
    }

    private companion object {
        const val AT = "at"
        const val FAILURE = "failure"
    }
}

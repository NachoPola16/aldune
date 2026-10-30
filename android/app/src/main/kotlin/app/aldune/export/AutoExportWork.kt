package app.aldune.export

import android.content.Context
import android.net.Uri
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.R
import app.aldune.data.AppSettings
import app.aldune.data.Languages
import app.aldune.data.SettingsStore
import app.aldune.domain.ExportNote
import app.aldune.domain.NoteState
import java.io.IOException
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Si la copia automática perdió su carpeta. Vive aparte de los ajustes a propósito: el trabajador de segundo plano lo escribe y el `ViewModel`
 * guarda sus propios ajustes enteros, así que un campo compartido se pisaría. «Perdida» apaga la copia hasta que el usuario elija carpeta otra vez.
 */
object AutoExportStatus {
    private const val FILE = "autoexport"
    private const val LOST = "lost"
    private val _lost = MutableStateFlow(false)
    val lost: StateFlow<Boolean> = _lost.asStateFlow()

    fun init(context: Context) {
        _lost.value = context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE).getBoolean(LOST, false)
    }

    fun setLost(context: Context, value: Boolean) {
        context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putBoolean(LOST, value).apply()
        _lost.value = value
    }
}

/**
 * Cuándo se escribe la copia: un trabajo único unos segundos después del último cambio (cada cambio reinicia la espera, así una ráfaga de
 * ediciones es una sola escritura) y, como red de seguridad, uno periódico al día. Todo en segundo plano; apagado, no queda nada programado.
 */
object AutoExportWork {
    private const val NOW = "aldune-export-now"
    private const val DAILY = "aldune-export-daily"
    private const val DEBOUNCE_SECONDS = 15L

    fun isActive(settings: AppSettings): Boolean = settings.autoExportEnabled && settings.autoExportUri != null && !AutoExportStatus.lost.value

    /** Deja programado lo que toca según los ajustes: el diario si está activa, nada si no. */
    fun apply(context: Context, settings: AppSettings) {
        val work = WorkManager.getInstance(context)
        if (!isActive(settings)) {
            work.cancelUniqueWork(DAILY)
            work.cancelUniqueWork(NOW)
            return
        }
        work.enqueueUniquePeriodicWork(DAILY, ExistingPeriodicWorkPolicy.KEEP, PeriodicWorkRequestBuilder<AutoExportWorker>(1, TimeUnit.DAYS).build())
    }

    /** Hubo un cambio en las notas: escribir tras un momento. [immediate] salta la espera (al activarla o cambiar de carpeta). */
    fun kick(context: Context, immediate: Boolean = false) {
        val request = OneTimeWorkRequestBuilder<AutoExportWorker>().setInitialDelay(if (immediate) 0 else DEBOUNCE_SECONDS, TimeUnit.SECONDS).build()
        WorkManager.getInstance(context).enqueueUniqueWork(NOW, ExistingWorkPolicy.REPLACE, request)
    }
}

class AutoExportWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        AutoExportStatus.init(applicationContext)
        val store = SettingsStore(applicationContext)
        val settings = store.load()
        if (!AutoExportWork.isActive(settings)) return Result.success()

        val ready = (applicationContext as AldunaApp).container.open() as? AppContainer.Opened.Ready ?: return Result.success()
        val includeArchived = settings.autoExportArchived
        val states = if (includeArchived) listOf(NoteState.ACTIVE, NoteState.ARCHIVED) else listOf(NoteState.ACTIVE)
        val notes = states.flatMap { ready.notes.list(it) }.map { ExportNote(it.id, it.text, it.state, it.isProtected) }
        val placeholder = Languages.wrap(applicationContext, settings.language).getString(R.string.new_note_title)

        return try {
            AutoExportRunner.run(applicationContext, Uri.parse(settings.autoExportUri), notes, includeArchived, placeholder)
            Result.success()
        } catch (e: FolderLostException) {
            // Sin carpeta o sin permiso no hay nada que reintentar: se apaga, y la lista y Ajustes lo explican una vez.
            AutoExportStatus.setLost(applicationContext, true)
            AutoExportWork.apply(applicationContext, settings)
            Result.success()
        } catch (e: IOException) {
            // Un fallo de disco pasajero: unos reintentos y se deja; la siguiente edición o el trabajo diario lo vuelven a intentar.
            if (runAttemptCount < 3) Result.retry() else Result.success()
        }
    }
}

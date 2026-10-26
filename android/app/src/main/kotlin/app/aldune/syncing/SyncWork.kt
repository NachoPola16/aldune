package app.aldune.syncing

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.syncclient.SyncEvent
import app.aldune.data.BackgroundRunStore
import app.aldune.data.SettingsStore
import app.aldune.syncclient.BackgroundRun
import app.aldune.syncclient.SyncFailureKind
import java.time.Instant
import kotlin.coroutines.cancellation.CancellationException
import java.util.concurrent.TimeUnit

/**
 * La sync «cuando el sistema quiera» (spec madre §2.6): un trabajo periódico de 15 min como mínimo y con red. No se promete periodicidad:
 * Android lo agrupa y algunos fabricantes lo retrasan. Cada pasada pasa por la política de disparadores como cualquier otro evento.
 */
class SyncWork(private val context: Context) : SyncSchedule {
    override fun ensure() {
        // Apagado en Ajustes: nada de trabajo periódico (seguirá sincronizando al abrir, al editar y a mano).
        if (!SettingsStore(context).load().syncInBackground) return cancel()
        val request = PeriodicWorkRequestBuilder<SyncWorker>(15, TimeUnit.MINUTES)
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .build()
        // KEEP: si ya está programado no se reinicia el reloj en cada arranque.
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(NAME, ExistingPeriodicWorkPolicy.KEEP, request)
    }

    override fun cancel() {
        WorkManager.getInstance(context).cancelUniqueWork(NAME)
    }

    private companion object {
        const val NAME = "aldune-sync"
    }
}

class SyncWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        val container = (applicationContext as AldunaApp).container
        // Clave perdida: no hay nada que sincronizar y la app lo explica al abrirse. Un fallo de red no es un fallo del trabajo (la política es silenciosa).
        val ready = container.open() as? AppContainer.Opened.Ready ?: return Result.success()
        // Se apunta que el sistema sí lanzó la pasada y cómo acabó: Ajustes lo cuenta (si no corre nunca, el usuario lo ve). Se apunta también
        // si la pasada lanza una excepción, para que un fallo raro no se confunda con «no ha corrido».
        val failure = try {
            ready.sync.run(SyncEvent.Periodic)
            ready.sync.status.value.lastFailure?.kind
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            SyncFailureKind.UNKNOWN
        }
        BackgroundRunStore(applicationContext).save(BackgroundRun(Instant.now(), failure))
        return Result.success()
    }
}

package app.aldune

import android.content.Context
import app.aldune.data.AldunaDatabase
import app.aldune.data.AndroidKeystoreKeyWrapper
import app.aldune.data.RoomLocalSyncStore
import app.aldune.data.RoomConflictStore
import app.aldune.data.RoomNoteStore
import app.aldune.data.RoomReminderStore
import app.aldune.data.SyncProfileStore
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.DataKeyManager
import app.aldune.domain.DataKeyState
import app.aldune.domain.LostReason
import app.aldune.domain.NoteService
import app.aldune.domain.RandomIdFactory
import app.aldune.domain.SystemClock
import app.aldune.syncclient.ConflictResolver
import app.aldune.syncclient.ConflictStore
import app.aldune.syncclient.ProtectionService
import app.aldune.syncclient.SecretVault
import app.aldune.syncclient.TombstoneEraser
import app.aldune.reminders.Reminders
import app.aldune.syncing.SyncController
import app.aldune.syncing.SyncWork
import java.io.File
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel

/**
 * Las piezas de la app, montadas a mano (sin framework de inyección: spec de F4 §2). [open] hace E/S y puede tardar (Keystore, base):
 * se llama fuera del hilo de interfaz.
 */
class AppContainer(context: Context) {
    private val appContext = context.applicationContext

    // noBackupFilesDir: además de la exclusión de la copia del sistema (§4.3), Android nunca copia esta carpeta.
    private val keys = DataKeyManager(File(appContext.noBackupFilesDir, KEY_FILE), AndroidKeystoreKeyWrapper())

    sealed class Opened {
        /** [sync] es única por proceso: la interfaz y el trabajador periódico comparten la misma, así nunca hay dos pasadas a la vez. */
        class Ready(
            val database: AldunaDatabase,
            val notes: NoteService,
            val sync: SyncController,
            val protection: ProtectionService,
            val conflictStore: ConflictStore,
            val conflicts: ConflictResolver,
            val reminders: Reminders,
        ) : Opened()

        /** La base existe pero su clave no se puede recuperar. La interfaz lo explica y ofrece [startOver]; nada se ha borrado. */
        data class KeyLost(val reason: LostReason) : Opened()
    }

    // La interfaz y el trabajador de segundo plano abren por el mismo contenedor (el de AldunaApp): una sola base y una sola sync por proceso.
    private var cached: Opened.Ready? = null
    private var scope = newScope()

    private fun newScope() = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    @Synchronized
    fun open(): Opened {
        cached?.let { return it }
        return when (val state = keys.open(hasExistingData = databaseFile().exists())) {
            is DataKeyState.Lost -> Opened.KeyLost(state.reason)
            is DataKeyState.Ready -> ready(state.key)
        }
    }

    /** Solo tras confirmación doble del usuario: borra la base y la clave, y empieza vacía. */
    @Synchronized
    fun startOver(): Opened.Ready {
        scope.cancel()
        cached?.database?.close()
        cached = null
        scope = newScope()
        return ready(keys.startOver(deleteData = { appContext.deleteDatabase(AldunaDatabase.FILE_NAME) }).key)
    }

    private fun ready(key: ByteArray): Opened.Ready {
        try {
            val cipher = AesGcmNoteCipher(key)
            val database = AldunaDatabase.open(appContext)
            val store = RoomNoteStore(database, cipher)
            // Los secretos de la sync tienen su propia clave de envoltura (otro alias): empezar de cero con las notas no los toca.
            val vault = SecretVault(File(appContext.noBackupFilesDir, VAULT_FILE), AndroidKeystoreKeyWrapper(VAULT_ALIAS))
            val sync = SyncController(SyncProfileStore(appContext), vault, RoomLocalSyncStore(database, store, cipher), SystemClock, scope, SyncWork(appContext))
            val reminderStore = RoomReminderStore(database)
            val reminders = Reminders(appContext, store, reminderStore, SystemClock)
            // Una pasada que trae notas archivadas o borradas deja recordatorios sin nota: se reconcilian también sin interfaz.
            sync.afterNotesChanged = reminders::reconcile
            val conflictStore = RoomConflictStore(database, cipher)
            // Recuperar una nota con «Usar la otra» tiene que quitar su lápida local, o la sync seguiría firmando el borrado (informe de F7).
            val eraser = TombstoneEraser { database.notes().deleteTombstone(it) }
            // Un solo cerrojo para los tres servicios que leen-modifican-escriben el mismo almacén (revisión de F7).
            val storeLock = Any()
            return Opened.Ready(
                database,
                NoteService(store, SystemClock, deviceId = "", idFactory = RandomIdFactory, lock = storeLock),
                sync,
                ProtectionService(store, SystemClock, storeLock),
                conflictStore,
                ConflictResolver(store, conflictStore, reminderStore, SystemClock, deviceId = "", tombstones = eraser, lock = storeLock),
                reminders,
            ).also { cached = it }
        } finally {
            // El cifrador ya tiene su copia; la nuestra se borra pase lo que pase (revisión de agy, 17-10-2026).
            key.fill(0)
        }
    }

    private fun databaseFile(): File = appContext.getDatabasePath(AldunaDatabase.FILE_NAME)

    private companion object {
        const val KEY_FILE = "datakey.bin"
        const val VAULT_FILE = "syncsecrets.bin"
        const val VAULT_ALIAS = "aldune.syncsecrets.wrap.v1"
    }
}

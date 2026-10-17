package app.aldune

import android.content.Context
import app.aldune.data.AldunaDatabase
import app.aldune.data.AndroidKeystoreKeyWrapper
import app.aldune.data.RoomNoteStore
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.DataKeyManager
import app.aldune.domain.DataKeyState
import app.aldune.domain.LostReason
import app.aldune.domain.NoteService
import app.aldune.domain.RandomIdFactory
import app.aldune.domain.SystemClock
import java.io.File

/**
 * Las piezas de la app, montadas a mano (sin framework de inyección: spec de F4 §2). [open] hace E/S y puede tardar (Keystore, base):
 * se llama fuera del hilo de interfaz.
 */
class AppContainer(context: Context) {
    private val appContext = context.applicationContext

    // noBackupFilesDir: además de la exclusión de la copia del sistema (§4.3), Android nunca copia esta carpeta.
    private val keys = DataKeyManager(File(appContext.noBackupFilesDir, KEY_FILE), AndroidKeystoreKeyWrapper())

    sealed class Opened {
        class Ready(val database: AldunaDatabase, val notes: NoteService) : Opened()

        /** La base existe pero su clave no se puede recuperar. La interfaz lo explica y ofrece [startOver]; nada se ha borrado. */
        data class KeyLost(val reason: LostReason) : Opened()
    }

    fun open(): Opened = when (val state = keys.open(hasExistingData = databaseFile().exists())) {
        is DataKeyState.Lost -> Opened.KeyLost(state.reason)
        is DataKeyState.Ready -> ready(state.key)
    }

    /** Solo tras confirmación doble del usuario: borra la base y la clave, y empieza vacía. */
    fun startOver(): Opened.Ready = ready(keys.startOver(deleteData = { appContext.deleteDatabase(AldunaDatabase.FILE_NAME) }).key)

    private fun ready(key: ByteArray): Opened.Ready {
        val cipher = AesGcmNoteCipher(key)
        key.fill(0) // el cifrador ya tiene su copia
        val database = AldunaDatabase.open(appContext)
        val store = RoomNoteStore(database, cipher)
        return Opened.Ready(database, NoteService(store, SystemClock, deviceId = "", idFactory = RandomIdFactory))
    }

    private fun databaseFile(): File = appContext.getDatabasePath(AldunaDatabase.FILE_NAME)

    private companion object {
        const val KEY_FILE = "datakey.bin"
    }
}

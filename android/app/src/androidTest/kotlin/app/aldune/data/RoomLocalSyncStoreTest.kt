package app.aldune.data

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.NoteStore
import app.aldune.syncclient.LocalSyncStore
import app.aldune.syncclient.LocalSyncStoreContract
import app.aldune.syncclient.StoreHarness
import org.junit.After
import org.junit.runner.RunWith

/**
 * La batería del contrato `LocalSyncStore` (la misma que pasa el almacén en memoria en la JVM) con Room, en el dispositivo.
 * Base EN MEMORIA: nunca la real de la app.
 */
@RunWith(AndroidJUnit4::class)
class RoomLocalSyncStoreTest : LocalSyncStoreContract() {
    private var db: AldunaDatabase? = null

    override fun newHarness(): StoreHarness {
        db?.close()
        val context = ApplicationProvider.getApplicationContext<Context>()
        val database = Room.inMemoryDatabaseBuilder(context, AldunaDatabase::class.java).build()
        db = database
        val cipher = AesGcmNoteCipher(ByteArray(32) { it.toByte() })
        val noteStore = RoomNoteStore(database, cipher)
        val syncStore = RoomLocalSyncStore(database, noteStore, cipher)
        return object : StoreHarness {
            override val sync: LocalSyncStore = syncStore
            override val notes: NoteStore = noteStore
        }
    }

    @After fun tearDown() {
        db?.close()
    }
}

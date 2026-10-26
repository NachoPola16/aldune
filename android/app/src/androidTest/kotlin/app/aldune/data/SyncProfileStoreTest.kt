package app.aldune.data

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.sync.SyncScope
import app.aldune.syncclient.SyncFailure
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncStatus
import java.time.Instant
import java.util.UUID
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/** Solo toca el fichero `sync` de la app **debug** (datos de prueba, nunca los de la app real). */
@RunWith(AndroidJUnit4::class)
class SyncProfileStoreTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val prefs get() = context.getSharedPreferences("sync", Context.MODE_PRIVATE)

    @Before @After fun clean() {
        prefs.edit().clear().commit()
    }

    private fun profile(id: String, name: String, scope: SyncScope = SyncScope.All) =
        SyncProfile(id, SyncTransportKind.SERVER, "https://$id.example.com/", scope, name, "0123456789abcdef0123456789abcdef")

    @Test fun sinNadaDevuelveListaVacia() {
        assertTrue(SyncProfileStore(context).loadAll().isEmpty())
    }

    @Test fun guardaVariosYLosRecuperaEnOrden() {
        val store = SyncProfileStore(context)
        val noteId = UUID.fromString("11111111-1111-1111-1111-111111111111")
        store.save(profile("a", "Casa"))
        store.save(profile("b", "Familia", SyncScope.SelectedNotes(setOf(noteId))))
        val loaded = SyncProfileStore(context).loadAll()
        assertEquals(listOf("a", "b"), loaded.map { it.id })
        assertEquals(SyncScope.SelectedNotes(setOf(noteId)), loaded[1].scope)
    }

    @Test fun guardarDeNuevoActualizaSinDuplicar() {
        val store = SyncProfileStore(context)
        store.save(profile("a", "Casa"))
        store.save(profile("a", "Casa 2"))
        assertEquals(listOf("Casa 2"), store.loadAll().map { it.profileName })
    }

    @Test fun quitarUnoNoTocaElOtroNiSuEstado() {
        val store = SyncProfileStore(context)
        store.save(profile("a", "Casa")); store.save(profile("b", "Familia"))
        store.saveStatus("a", SyncStatus(lastSuccessAt = Instant.ofEpochMilli(1000)))
        store.saveStatus("b", SyncStatus(lastSuccessAt = Instant.ofEpochMilli(2000), lastFailure = SyncFailure(SyncFailureKind.OFFLINE, "x")))
        store.remove("a")
        assertEquals(listOf("b"), store.loadAll().map { it.id })
        assertEquals(SyncStatus(), store.loadStatus("a"))
        assertEquals(Instant.ofEpochMilli(2000), store.loadStatus("b").lastSuccessAt)
        assertEquals(SyncFailureKind.OFFLINE, store.loadStatus("b").lastFailure!!.kind)
    }

    @Test fun elDeviceIdSobreviveAQuitarTodo() {
        val store = SyncProfileStore(context)
        val id = store.deviceId()
        store.save(profile("a", "Casa")); store.remove("a")
        assertEquals(id, SyncProfileStore(context).deviceId())
    }

    @Test fun migraElPerfilUnicoDeLasVersionesAnteriores() {
        // Así lo dejaba la versión de un solo perfil.
        prefs.edit().putString("transport", "WEBDAV").putString("url", "https://cloud.example.com/dav/").putString("profileName", "Mi nube")
            .putInt("scope", 2).putString("tag", "viaje").putLong("status.ok", 5000L).putLong("status.attempt", 6000L)
            .putString("deviceId", "0123456789abcdef0123456789abcdef").commit()

        val store = SyncProfileStore(context)
        val migrated = store.loadAll().single()

        assertEquals("default", migrated.id)
        assertEquals(SyncTransportKind.WEBDAV, migrated.transport)
        assertEquals(SyncScope.Tag("viaje"), migrated.scope)
        assertEquals("Mi nube", migrated.profileName)
        assertEquals(Instant.ofEpochMilli(5000), store.loadStatus("default").lastSuccessAt)
        assertTrue("las claves antiguas se retiran", !prefs.contains("url") && !prefs.contains("status.ok"))
        // Idempotente: otra carga no cambia nada.
        assertEquals(listOf("default"), SyncProfileStore(context).loadAll().map { it.id })
    }
}

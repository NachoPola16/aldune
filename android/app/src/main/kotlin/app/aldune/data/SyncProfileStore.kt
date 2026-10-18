package app.aldune.data

import android.content.Context
import app.aldune.core.ProfileCodes
import app.aldune.sync.SyncScope
import app.aldune.syncclient.DeviceIds
import app.aldune.syncclient.SyncFailure
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncStatus
import java.time.Instant
import java.util.UUID

enum class SyncTransportKind { SERVER, WEBDAV }

/** El perfil de sync **sin** secretos (esos van en `SecretVault`). Un perfil en la v1 (spec madre §2.6). */
data class SyncProfile(
    val transport: SyncTransportKind,
    val url: String,
    val scope: SyncScope,
    val profileName: String?,
    val deviceId: String,
)

/**
 * Lo no secreto de la sync: el perfil conectado, el `deviceId` de esta instalación y el último estado. En SharedPreferences, como los
 * ajustes. El `deviceId` se genera una vez y **sobrevive a desconectar**: las versiones ya subidas están firmadas con él, y cambiarlo haría
 * que esas notas parecieran de otro equipo.
 */
class SyncProfileStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    fun deviceId(): String {
        val existing = prefs.getString(DEVICE_ID, null)
        if (existing != null && DeviceIds.isValid(existing)) return existing
        val fresh = DeviceIds.generate()
        prefs.edit().putString(DEVICE_ID, fresh).commit()
        return fresh
    }

    fun load(): SyncProfile? {
        val url = prefs.getString(URL, null) ?: return null
        val transport = prefs.getString(TRANSPORT, null)?.let { name -> SyncTransportKind.entries.firstOrNull { it.name == name } } ?: return null
        val scope = when (prefs.getInt(SCOPE, ProfileCodes.SCOPE_ALL)) {
            ProfileCodes.SCOPE_CHOSEN -> SyncScope.SelectedNotes(
                prefs.getString(NOTE_IDS, "").orEmpty().split(',').filter { it.isNotBlank() }.map(UUID::fromString).toSet(),
            )
            ProfileCodes.SCOPE_TAG -> SyncScope.Tag(prefs.getString(TAG, null))
            else -> SyncScope.All
        }
        return SyncProfile(transport, url, scope, prefs.getString(NAME, null), deviceId())
    }

    fun save(profile: SyncProfile) {
        val editor = prefs.edit().putString(TRANSPORT, profile.transport.name).putString(URL, profile.url).putString(NAME, profile.profileName)
        when (val scope = profile.scope) {
            SyncScope.All -> editor.putInt(SCOPE, ProfileCodes.SCOPE_ALL)
            is SyncScope.SelectedNotes -> editor.putInt(SCOPE, ProfileCodes.SCOPE_CHOSEN).putString(NOTE_IDS, scope.noteIds.joinToString(","))
            is SyncScope.Tag -> editor.putInt(SCOPE, ProfileCodes.SCOPE_TAG).putString(TAG, scope.tag)
        }
        editor.commit()
    }

    /** Desconectar: se olvida el perfil y el estado, pero no el `deviceId`. */
    fun clear() {
        prefs.edit().remove(TRANSPORT).remove(URL).remove(NAME).remove(SCOPE).remove(NOTE_IDS).remove(TAG).remove(STATUS_PREFIX + "ok")
            .remove(STATUS_PREFIX + "attempt").remove(STATUS_PREFIX + "kind").remove(STATUS_PREFIX + "detail").remove(STATUS_PREFIX + "http")
            .commit()
    }

    fun loadStatus(): SyncStatus {
        val kind = prefs.getString(STATUS_PREFIX + "kind", null)?.let { name -> SyncFailureKind.entries.firstOrNull { it.name == name } }
        return SyncStatus(
            lastSuccessAt = prefs.getLong(STATUS_PREFIX + "ok", 0L).takeIf { it > 0 }?.let(Instant::ofEpochMilli),
            lastAttemptAt = prefs.getLong(STATUS_PREFIX + "attempt", 0L).takeIf { it > 0 }?.let(Instant::ofEpochMilli),
            lastFailure = kind?.let {
                SyncFailure(it, prefs.getString(STATUS_PREFIX + "detail", null), prefs.getInt(STATUS_PREFIX + "http", -1).takeIf { code -> code >= 0 })
            },
        )
    }

    /** Solo se guarda lo que sobrevive a cerrar la app; `running` no. */
    fun saveStatus(status: SyncStatus) {
        val editor = prefs.edit()
            .putLong(STATUS_PREFIX + "ok", status.lastSuccessAt?.toEpochMilli() ?: 0L)
            .putLong(STATUS_PREFIX + "attempt", status.lastAttemptAt?.toEpochMilli() ?: 0L)
        val failure = status.lastFailure
        if (failure == null) {
            editor.remove(STATUS_PREFIX + "kind").remove(STATUS_PREFIX + "detail").remove(STATUS_PREFIX + "http")
        } else {
            editor.putString(STATUS_PREFIX + "kind", failure.kind.name).putString(STATUS_PREFIX + "detail", failure.detail)
                .putInt(STATUS_PREFIX + "http", failure.httpStatus ?: -1)
        }
        editor.apply()
    }

    private companion object {
        const val FILE = "sync"
        const val DEVICE_ID = "deviceId"
        const val TRANSPORT = "transport"
        const val URL = "url"
        const val NAME = "profileName"
        const val SCOPE = "scope"
        const val NOTE_IDS = "noteIds"
        const val TAG = "tag"
        const val STATUS_PREFIX = "status."
    }
}

package app.aldune.data

import android.content.Context
import app.aldune.core.ProfileCodes
import app.aldune.sync.SyncScope
import app.aldune.syncclient.DeviceIds
import app.aldune.syncclient.LEGACY_PROFILE_ID
import app.aldune.syncclient.SyncFailure
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncStatus
import java.time.Instant
import java.util.UUID
import org.json.JSONArray
import org.json.JSONObject

enum class SyncTransportKind { SERVER, WEBDAV }

/** Un perfil de sync **sin** secretos (esos van en un `SecretVault` por perfil). [id] es un UUID; el perfil heredado es [LEGACY_PROFILE_ID]. */
data class SyncProfile(
    val id: String,
    val transport: SyncTransportKind,
    val url: String,
    val scope: SyncScope,
    val profileName: String?,
    val deviceId: String,
)

/**
 * Lo no secreto de la sync: los perfiles conectados (JSON en SharedPreferences, como los ajustes), el `deviceId` de esta instalación y el
 * último estado de cada perfil. El `deviceId` se genera una vez y **sobrevive a desconectar**: las versiones ya subidas están firmadas con él,
 * y cambiarlo haría que esas notas parecieran de otro equipo.
 *
 * Las versiones de un solo perfil guardaban las claves sueltas (`url`, `transport`…): la primera lectura las convierte en el perfil
 * [LEGACY_PROFILE_ID] y las retira (ver [migrateLegacy]).
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

    @Synchronized
    fun loadAll(): List<SyncProfile> {
        migrateLegacy()
        val raw = prefs.getString(PROFILES, null) ?: return emptyList()
        val device = deviceId()
        val array = JSONArray(raw)
        return (0 until array.length()).mapNotNull { fromJson(array.getJSONObject(it), device) }
    }

    /** Alta o actualización por id; un perfil nuevo va al final. */
    @Synchronized
    fun save(profile: SyncProfile) {
        val all = loadAll().toMutableList()
        val index = all.indexOfFirst { it.id == profile.id }
        if (index >= 0) all[index] = profile else all += profile
        writeAll(all)
    }

    /** Quita el perfil y su estado. No toca el `deviceId`. */
    @Synchronized
    fun remove(profileId: String) {
        writeAll(loadAll().filter { it.id != profileId })
        prefs.edit().apply { for (suffix in STATUS_SUFFIXES) remove(statusKey(profileId, suffix)) }.commit()
    }

    fun loadStatus(profileId: String): SyncStatus {
        val kind = prefs.getString(statusKey(profileId, "kind"), null)?.let { name -> SyncFailureKind.entries.firstOrNull { it.name == name } }
        return SyncStatus(
            lastSuccessAt = prefs.getLong(statusKey(profileId, "ok"), 0L).takeIf { it > 0 }?.let(Instant::ofEpochMilli),
            lastAttemptAt = prefs.getLong(statusKey(profileId, "attempt"), 0L).takeIf { it > 0 }?.let(Instant::ofEpochMilli),
            lastFailure = kind?.let {
                SyncFailure(it, prefs.getString(statusKey(profileId, "detail"), null), prefs.getInt(statusKey(profileId, "http"), -1).takeIf { code -> code >= 0 })
            },
        )
    }

    /** Solo se guarda lo que sobrevive a cerrar la app; `running` no. */
    fun saveStatus(profileId: String, status: SyncStatus) {
        val editor = prefs.edit()
            .putLong(statusKey(profileId, "ok"), status.lastSuccessAt?.toEpochMilli() ?: 0L)
            .putLong(statusKey(profileId, "attempt"), status.lastAttemptAt?.toEpochMilli() ?: 0L)
        val failure = status.lastFailure
        if (failure == null) {
            editor.remove(statusKey(profileId, "kind")).remove(statusKey(profileId, "detail")).remove(statusKey(profileId, "http"))
        } else {
            editor.putString(statusKey(profileId, "kind"), failure.kind.name).putString(statusKey(profileId, "detail"), failure.detail)
                .putInt(statusKey(profileId, "http"), failure.httpStatus ?: -1)
        }
        editor.apply()
    }

    private fun writeAll(profiles: List<SyncProfile>) {
        val array = JSONArray()
        profiles.forEach { array.put(toJson(it)) }
        prefs.edit().putString(PROFILES, array.toString()).commit()
    }

    /** El perfil único de antes pasa a ser el perfil `default`, con su estado. Idempotente: sin claves antiguas no hace nada. */
    private fun migrateLegacy() {
        if (prefs.contains(PROFILES)) return
        val url = prefs.getString(URL, null) ?: return
        val transport = prefs.getString(TRANSPORT, null)?.let { name -> SyncTransportKind.entries.firstOrNull { it.name == name } }
        val editor = prefs.edit()
        if (transport != null) {
            val scope = when (prefs.getInt(SCOPE, ProfileCodes.SCOPE_ALL)) {
                ProfileCodes.SCOPE_CHOSEN -> SyncScope.SelectedNotes(
                    prefs.getString(NOTE_IDS, "").orEmpty().split(',').filter { it.isNotBlank() }.map(UUID::fromString).toSet(),
                )
                ProfileCodes.SCOPE_TAG -> SyncScope.Tag(prefs.getString(TAG, null))
                else -> SyncScope.All
            }
            val array = JSONArray().put(toJson(SyncProfile(LEGACY_PROFILE_ID, transport, url, scope, prefs.getString(NAME, null), "")))
            editor.putString(PROFILES, array.toString())
            for (suffix in STATUS_SUFFIXES) {
                val old = LEGACY_STATUS_PREFIX + suffix
                when (val value = prefs.all[old]) {
                    is Long -> editor.putLong(statusKey(LEGACY_PROFILE_ID, suffix), value)
                    is Int -> editor.putInt(statusKey(LEGACY_PROFILE_ID, suffix), value)
                    is String -> editor.putString(statusKey(LEGACY_PROFILE_ID, suffix), value)
                }
            }
        }
        editor.remove(TRANSPORT).remove(URL).remove(NAME).remove(SCOPE).remove(NOTE_IDS).remove(TAG)
        for (suffix in STATUS_SUFFIXES) editor.remove(LEGACY_STATUS_PREFIX + suffix)
        editor.commit()
    }

    private fun toJson(profile: SyncProfile): JSONObject = JSONObject().put("id", profile.id).put("transport", profile.transport.name)
        .put("url", profile.url).put("name", profile.profileName ?: JSONObject.NULL).apply {
            when (val scope = profile.scope) {
                SyncScope.All -> put("scope", ProfileCodes.SCOPE_ALL)
                is SyncScope.SelectedNotes -> put("scope", ProfileCodes.SCOPE_CHOSEN).put("noteIds", scope.noteIds.joinToString(","))
                is SyncScope.Tag -> put("scope", ProfileCodes.SCOPE_TAG).put("tag", scope.tag ?: JSONObject.NULL)
            }
        }

    private fun fromJson(o: JSONObject, deviceId: String): SyncProfile? {
        val transport = SyncTransportKind.entries.firstOrNull { it.name == o.optString("transport") } ?: return null
        val scope = when (o.optInt("scope", ProfileCodes.SCOPE_ALL)) {
            ProfileCodes.SCOPE_CHOSEN -> SyncScope.SelectedNotes(o.optString("noteIds", "").split(',').filter { it.isNotBlank() }.map(UUID::fromString).toSet())
            ProfileCodes.SCOPE_TAG -> SyncScope.Tag(if (o.isNull("tag")) null else o.optString("tag"))
            else -> SyncScope.All
        }
        return SyncProfile(o.getString("id"), transport, o.getString("url"), scope, if (o.isNull("name")) null else o.optString("name"), deviceId)
    }

    private fun statusKey(profileId: String, suffix: String) = "status.$profileId.$suffix"

    private companion object {
        const val FILE = "sync"
        const val DEVICE_ID = "deviceId"
        const val PROFILES = "profiles"
        const val TRANSPORT = "transport"
        const val URL = "url"
        const val NAME = "profileName"
        const val SCOPE = "scope"
        const val NOTE_IDS = "noteIds"
        const val TAG = "tag"
        const val LEGACY_STATUS_PREFIX = "status."
        val STATUS_SUFFIXES = listOf("ok", "attempt", "kind", "detail", "http")
    }
}

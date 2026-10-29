package app.aldune.data

import app.aldune.core.ProtectedNoteContent
import app.aldune.core.SyncNote
import app.aldune.sync.ConflictVersion
import app.aldune.sync.SyncConflict
import java.time.OffsetDateTime
import java.util.UUID
import org.json.JSONArray
import org.json.JSONObject

/**
 * El JSON que se cifra en `sync_conflict`: las dos versiones del conflicto. Es un formato **local** (no viaja), así que no tiene que
 * parecerse al del sobre; solo tiene que devolver tal cual lo que el motor entrega, fechas con sus 100 ns incluidas.
 */
internal object ConflictPayload {
    fun write(conflict: SyncConflict): String = JSONObject()
        .put("winner", version(conflict.winner))
        .put("losing", version(conflict.losing))
        .toString()

    fun read(noteId: String, detectedAt: String, json: String): SyncConflict {
        val root = JSONObject(json)
        return SyncConflict(
            UUID.fromString(noteId), OffsetDateTime.parse(detectedAt),
            readVersion(root.getJSONObject("winner")), readVersion(root.getJSONObject("losing")),
        )
    }

    private fun version(v: ConflictVersion) = JSONObject()
        .put("updatedAt", v.updatedAt.toString())
        .put("deviceId", v.deviceId)
        .put("tombstone", v.tombstone)
        .put("note", v.note?.let(::note) ?: JSONObject.NULL)

    private fun readVersion(o: JSONObject) = ConflictVersion(
        OffsetDateTime.parse(o.getString("updatedAt")), o.getString("deviceId"), o.getBoolean("tombstone"),
        if (o.isNull("note")) null else readNote(o.getJSONObject("note")),
    )

    private fun note(n: SyncNote) = JSONObject()
        .put("id", n.id.toString())
        .put("text", n.text)
        .put("color", n.color)
        .put("createdAt", n.createdAt.toString())
        .put("updatedAt", n.updatedAt.toString())
        .put("state", n.state)
        .put("screenOrigin", n.screenOrigin)
        .put("tags", JSONArray(n.tags))
        .put("isProtected", n.isProtected)
        .put(
            "protectedContent",
            n.protectedContent?.let {
                JSONObject().put("version", it.version).put("salt", it.salt).put("cipherText", it.cipherText)
                    .put("nonce", it.nonce).put("tag", it.tag)
            } ?: JSONObject.NULL,
        )
        .put("protectedTitle", n.protectedTitle ?: JSONObject.NULL)
        .put("dockPosition", n.dockPosition ?: JSONObject.NULL)

    private fun readNote(o: JSONObject): SyncNote {
        val tags = o.getJSONArray("tags").let { a -> List(a.length()) { a.getString(it) } }
        val protectedContent = if (o.isNull("protectedContent")) null else o.getJSONObject("protectedContent").let {
            ProtectedNoteContent(it.getInt("version"), it.getString("salt"), it.getString("cipherText"), it.getString("nonce"), it.getString("tag"))
        }
        return SyncNote(
            UUID.fromString(o.getString("id")), o.getString("text"), o.getString("color"),
            OffsetDateTime.parse(o.getString("createdAt")), OffsetDateTime.parse(o.getString("updatedAt")),
            o.getInt("state"), o.getString("screenOrigin"), tags, o.getBoolean("isProtected"), protectedContent,
            if (o.isNull("dockPosition")) null else o.getDouble("dockPosition"),
            // Los conflictos guardados antes del nombre visible no traen la clave.
            protectedTitle = if (o.isNull("protectedTitle")) null else o.getString("protectedTitle"),
        )
    }
}

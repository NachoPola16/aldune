package app.aldune.data

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * Una fila de `note`. El texto va cifrado (AES-GCM, ver `AesGcmNoteCipher`) en tres columnas, como en Windows. Las fechas van como texto
 * ISO-8601 (`Instant.toString()`), porque la sync maneja instantes con hasta 100 ns y un entero en milisegundos los redondearía y
 * fabricaría versiones «distintas» sin que nadie edite. Las columnas de nota protegida existen ya (F7) para no migrar enseguida.
 */
@Entity(tableName = "note")
class NoteEntity(
    @PrimaryKey val id: String,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val textCipher: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val nonce: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val tag: ByteArray,
    val color: String,
    val createdAt: String,
    val updatedAt: String,
    /** Código de `NoteState` (0 activa, 1 archivada, 2 papelera): el mismo número que viaja en sync. */
    val state: Int,
    /** 0 = LF, 1 = CRLF. */
    val lineEnding: Int,
    /** Lista JSON de etiquetas, tal como llegaron (se conserva la grafía). */
    val tags: String,
    val dockPosition: Double?,
    val screenOrigin: String,
    val syncAuthorDeviceId: String?,
    val isProtected: Boolean = false,
    val protectedContent: String? = null,
    /** Nombre visible, en claro, de una nota protegida (v4). */
    val protectedTitle: String? = null,
    /** Fijada arriba (v5). */
    @ColumnInfo(defaultValue = "0") val pinnedToTop: Boolean = false,
)

/** Borrado definitivo pendiente de firmar por la sync. */
@Entity(tableName = "tombstone")
class TombstoneEntity(
    @PrimaryKey val noteId: String,
    val deletedAt: String,
    val deviceId: String,
)

/**
 * Última versión que se sabe igual en los dos lados, para distinguir conflicto real de cambio de un solo lado (F5). Una por (perfil, nota):
 * la misma nota puede estar en el alcance de dos perfiles y cada almacén remoto tiene su propia versión acordada.
 */
@Entity(tableName = "sync_base", primaryKeys = ["profileId", "noteId"])
class SyncBaseEntity(
    val profileId: String,
    val noteId: String,
    val updatedAt: String,
    val deviceId: String,
)

/**
 * Conflicto guardado por la sync (F5): la versión que perdió, para poder recuperarla (F7). Las dos versiones van en un JSON cifrado con el
 * mismo cifrador que el texto de las notas (contiene texto de notas), en tres columnas como `note`. Uno por (perfil, nota).
 */
@Entity(tableName = "sync_conflict", primaryKeys = ["profileId", "noteId"])
class SyncConflictEntity(
    val profileId: String,
    val noteId: String,
    val detectedAt: String,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val payloadCipher: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val nonce: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val tag: ByteArray,
)

/** Un recordatorio por nota, local al dispositivo (F7). */
@Entity(tableName = "reminder")
class ReminderEntity(
    @PrimaryKey val noteId: String,
    val dueAtEpochMillis: Long,
)

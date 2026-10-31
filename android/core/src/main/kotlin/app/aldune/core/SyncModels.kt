package app.aldune.core

import java.time.OffsetDateTime
import java.util.UUID

/** Equivale a FormatException en Aldune.Core: el dato no cumple el formato (se rechaza sin aplicar nada). */
class SyncFormatException(message: String, cause: Throwable? = null) : Exception(message, cause)

/** Ventana de formatos que sabe leer esta versión (igual que SyncCompatibility en Core). */
object SyncCompatibility {
    const val CURRENT_FORMAT = 4
    const val MINIMUM_SUPPORTED_FORMAT = 1

    fun isSupported(format: Int): Boolean = format in MINIMUM_SUPPORTED_FORMAT..CURRENT_FORMAT

    fun unsupportedFormatMessage(format: Int): String =
        if (format > CURRENT_FORMAT) "This sync data requires a newer Aldune version (format $format)."
        else "This sync data is too old for this Aldune version (format $format)."
}

/** Cabecera del sobre: lo que un servidor puede ver sin conocer la clave. */
data class SyncEnvelope(
    val format: Int = SyncCompatibility.CURRENT_FORMAT,
    val noteId: UUID,
    val tombstone: Boolean,
    val updatedAt: OffsetDateTime,
    val deviceId: String,
    val cipherText: String?,
    val nonce: String?,
    val tag: String?,
)

/** Contenido cifrado de una nota protegida (todo en base64 estándar). */
data class ProtectedNoteContent(
    val version: Int = CURRENT_VERSION,
    val salt: String,
    val cipherText: String,
    val nonce: String,
    val tag: String,
) {
    companion object {
        const val CURRENT_VERSION = 1
    }
}

/** NoteState de Core: 0 activa, 1 archivada, 2 papelera. */
object NoteState {
    const val ACTIVE = 0
    const val ARCHIVED = 1
    const val TRASH = 2
}

/** Nota tal como viaja dentro del sobre (el JSON que se cifra). */
data class SyncNote(
    val id: UUID,
    val text: String,
    val color: String,
    val createdAt: OffsetDateTime,
    val updatedAt: OffsetDateTime,
    val state: Int,
    val screenOrigin: String,
    val tags: List<String>,
    val isProtected: Boolean,
    val protectedContent: ProtectedNoteContent?,
    val dockPosition: Double?,
    /** Nombre visible, en claro, de una nota protegida (null = «Nota protegida»). Solo se conserva si la nota está protegida. */
    val protectedTitle: String? = null,
    /** Fijada arriba de las listas. Opcional en el contenido cifrado: solo se escribe si es cierto y su ausencia es «no fijada». */
    val pinnedToTop: Boolean = false,
    /** «Tareas hechas» de la nota: 0 según los ajustes del dispositivo, 1 mantener en su sitio, 2 bajar al final. Opcional: solo se escribe si no es 0. */
    val completedTasks: Int = 0,
)

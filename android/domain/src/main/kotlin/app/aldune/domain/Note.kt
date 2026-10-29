package app.aldune.domain

import java.time.Instant

/** Estado de una nota. En sync viaja como número (`code`), igual que el enum de Core. */
enum class NoteState(val code: Int) {
    ACTIVE(0), ARCHIVED(1), TRASHED(2);

    companion object {
        /** `null` para un código que no existe: quien lo lee decide qué hacer, no se adivina un estado. */
        fun fromCode(code: Int): NoteState? = entries.firstOrNull { it.code == code }
    }
}

/**
 * Salto de línea con el que llegó la nota. El texto de [Note] va siempre con `\n`; este valor solo sirve para
 * devolverlo como estaba al sincronizar, de modo que editar en Android no reescriba todas las líneas de una nota de Windows.
 */
enum class LineEnding(val sequence: String) {
    LF("\n"), CRLF("\r\n");

    companion object {
        /** El primer salto de línea decide; sin saltos, LF (lo que lleva una nota creada aquí). */
        fun detect(text: String): LineEnding {
            val lf = text.indexOf('\n')
            return if (lf > 0 && text[lf - 1] == '\r') CRLF else LF
        }

        /** `\r\n` → `\n`. Un `\r` suelto se deja: no es un salto de línea de Aldune. */
        fun normalize(text: String): String = text.replace("\r\n", "\n")
    }
}

/**
 * Una nota. Inmutable; los cambios se hacen con `copy` dentro de [NoteService].
 *
 * @property text siempre con `\n` (ver [LineEnding]).
 * @property color hex tal cual llega, sin normalizar mayúsculas: la sync lo compara sin distinguirlas pero lo devuelve igual.
 * @property screenOrigin Android no lo usa; se conserva y se devuelve intacto para que el PC no pierda en qué pantalla va la nota.
 * @property syncAuthorDeviceId autor de la versión actual si llegó por sync; `null` = la versión es de este equipo.
 * @property isProtected nota protegida con contraseña en otro equipo. Su `text` es `""`: el texto vive cifrado en [protectedContentJson].
 * @property protectedContentJson el JSON de `ProtectedNoteContent` tal como llegó, opaco para este módulo (que no depende de `core`).
 * Se conserva entero en cualquier cambio local para que la nota vuelva a subir idéntica; abrirla y cambiarle el texto es cosa de F7.
 * @property protectedTitle nombre visible, en claro, de una nota protegida (`null` = «Nota protegida»). Es público por diseño: ver `ProtectedTitleRules`.
 */
data class Note(
    val id: String,
    val text: String,
    val lineEnding: LineEnding,
    val color: String,
    val createdAt: Instant,
    val updatedAt: Instant,
    val state: NoteState,
    val tags: List<String> = emptyList(),
    val dockPosition: Double? = null,
    val screenOrigin: String = "",
    val syncAuthorDeviceId: String? = null,
    val isProtected: Boolean = false,
    val protectedContentJson: String? = null,
    val protectedTitle: String? = null,
) {
    init {
        // Es un fallo de programa, no de usuario: quien construye la nota desde un fichero o desde sync debe normalizar antes.
        require(!text.contains("\r\n")) { "El texto de una nota va siempre con LF; usa LineEnding.normalize al leerlo" }
    }

    /** El texto tal como viaja en sync: con el salto de línea original. */
    val wireText: String get() = if (lineEnding == LineEnding.CRLF) text.replace("\n", "\r\n") else text
}

/** Borrado definitivo pendiente de firmar por la sync. */
data class Tombstone(val noteId: String, val deletedAt: Instant, val deviceId: String) {
    companion object {
        /** El marcador de Core para los borrados hechos en este equipo; la sync lo sustituye por el `deviceId` al firmar. */
        const val LOCAL_DEVICE = "local"
    }
}

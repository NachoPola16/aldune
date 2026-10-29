package app.aldune.domain

import app.aldune.text.MarkdownExport
import java.io.OutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

/**
 * La copia local de todas las notas: un `.zip` con un fichero Markdown por nota (activas en `notas/`, archivadas en `archivadas/`; la papelera
 * no cuenta). Sin protegidas: su texto está cifrado con una contraseña que la app no guarda, y volcarlo en claro desharía la protección. En su
 * lugar va un aviso con cuántas se dejaron fuera. Pura (sin Android): quien la usa le da el flujo de salida que le dé el selector de documentos.
 */
object NotesExport {
    data class Result(val exported: Int, val omittedProtected: Int)

    const val MAX_NAME_LENGTH = 60
    const val OMITTED_FILE = "protegidas-omitidas.txt"
    private const val ACTIVE_DIR = "notas/"
    private const val ARCHIVED_DIR = "archivadas/"

    // Lo que Windows no admite en un nombre de fichero (el zip acaba abriéndose en el PC) y los nombres reservados de MS-DOS.
    private const val INVALID = "<>:\"/\\|?*"
    private val RESERVED = setOf("CON", "PRN", "AUX", "NUL") + (1..9).map { "COM$it" } + (1..9).map { "LPT$it" }

    /**
     * [placeholder] titula una nota vacía; [omittedNote] redacta el aviso de protegidas omitidas (recibe cuántas) en el idioma de la app.
     * Cierra el zip pero no [out].
     */
    fun write(out: OutputStream, notes: List<Note>, placeholder: String, omittedNote: (Int) -> String): Result {
        val zip = ZipOutputStream(out, Charsets.UTF_8)
        val used = mutableMapOf<String, MutableSet<String>>() // carpeta -> nombres ya usados, sin distinguir mayúsculas
        var exported = 0
        var omitted = 0
        for (note in notes) {
            if (note.state == NoteState.TRASHED) continue
            if (note.isProtected) {
                omitted++
                continue
            }
            val dir = if (note.state == NoteState.ARCHIVED) ARCHIVED_DIR else ACTIVE_DIR
            val markdown = MarkdownExport.toMarkdown(note.text, placeholder)
            val title = markdown.lineSequence().first().removePrefix("# ")
            val name = fileName(title, placeholder, used.getOrPut(dir) { mutableSetOf() })
            zip.putNextEntry(ZipEntry(dir + name))
            zip.write(markdown.toByteArray(Charsets.UTF_8))
            zip.closeEntry()
            exported++
        }
        if (omitted > 0) {
            zip.putNextEntry(ZipEntry(OMITTED_FILE))
            zip.write((omittedNote(omitted) + "\r\n").toByteArray(Charsets.UTF_8))
            zip.closeEntry()
        }
        zip.finish()
        return Result(exported, omitted)
    }

    /** Un nombre de fichero `.md` seguro a partir del título, distinto de los de [used] (que se actualiza). */
    fun fileName(title: String, fallback: String, used: MutableSet<String>): String {
        var clean = title.map { if (it in INVALID || it.isISOControl()) ' ' else it }.joinToString("")
            .replace(Regex("\\s+"), " ").trim().trim('.', ' ')
        if (clean.length > MAX_NAME_LENGTH) {
            // Sin partir un emoji (par sustituto) por la mitad.
            val cut = if (clean[MAX_NAME_LENGTH].isLowSurrogate()) MAX_NAME_LENGTH - 1 else MAX_NAME_LENGTH
            clean = clean.substring(0, cut).trim().trim('.', ' ')
        }
        if (clean.isEmpty() || clean.uppercase() in RESERVED) clean = fallback
        var candidate = clean
        var n = 2
        while (!used.add(candidate.lowercase())) candidate = "$clean (${n++})"
        return "$candidate.md"
    }
}

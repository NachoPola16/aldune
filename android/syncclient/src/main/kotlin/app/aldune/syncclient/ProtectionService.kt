package app.aldune.syncclient

import app.aldune.core.PasswordProblem
import app.aldune.core.PasswordRules
import app.aldune.core.ProtectedNoteContent
import app.aldune.core.ProtectedNotes
import app.aldune.core.UnlockedProtectedNote
import app.aldune.domain.Clock
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteStore
import java.time.Instant

/** Por qué no se pudo proteger, abrir, guardar o desproteger una nota. Cada caso lleva lo que la interfaz necesita; sin textos. */
sealed class ProtectionError {
    data class NotFound(val id: String) : ProtectionError()

    data class AlreadyProtected(val id: String) : ProtectionError()

    data class NotProtected(val id: String) : ProtectionError()

    /** Al proteger: la contraseña (o su repetición) no cumple [PasswordRules]. */
    data class InvalidPassword(val problem: PasswordProblem) : ProtectionError()

    data object WrongPassword : ProtectionError()

    /** El contenido protegido lo escribió un cliente más nuevo (versión que esta app no entiende): no es una contraseña mala. */
    data class UnsupportedVersion(val id: String) : ProtectionError()

    /** El contenido protegido guardado no se puede leer (dato dañado). La nota se deja como está. */
    data class UnreadableContent(val id: String) : ProtectionError()

    /** La nota cambió por debajo mientras se trabajaba con ella (otra edición, otra versión llegada por sync); no se escribe. */
    data class StaleWrite(val id: String) : ProtectionError()
}

sealed class ProtectionResult<out T> {
    data class Ok<out T>(val value: T) : ProtectionResult<T>()
    data class Err(val error: ProtectionError) : ProtectionResult<Nothing>()
}

/**
 * Proteger, abrir, editar y desproteger notas con contraseña (port de `Protect`/`TryUnlock`/`UpdateProtectedText`/`RemoveProtection` de
 * `NotesRepository`) sobre el mismo [NoteStore] que [app.aldune.domain.NoteService].
 *
 * Toda escritura sigue la regla de Core: `updatedAt = clock.now()` y `syncAuthorDeviceId = null`, y el texto normal de una protegida
 * queda **vacío** (el texto sólo existe cifrado). Lo demás —color, etiquetas, posición, estado, origen de pantalla— se conserva porque
 * se parte siempre de la nota guardada con `copy`.
 *
 * **Derivar la clave tarda** (600.000 iteraciones de PBKDF2, más de un segundo en una tableta lenta), así que no se hace con el cerrojo
 * cogido ni con una copia vieja de la nota: se vuelve a leer la nota después y se escribe sobre lo último guardado. Si lo que se iba a
 * cifrar cambió mientras tanto, [ProtectionError.StaleWrite] y no se escribe nada.
 *
 * No recuerda ninguna contraseña: [unlock] devuelve una sesión ([UnlockedProtectedNote]) con la clave ya derivada y quien la abre la cierra.
 */
class ProtectionService(private val store: NoteStore, private val clock: Clock, private val lock: Any = Any()) {

    /** Última hora de edición aceptada por nota (ver [save]); en memoria, como en `NoteService`. */
    private val lastEdit = HashMap<String, Instant>()

    /**
     * El contenido cifrado de cada nota tal como lo leyó [unlock] o lo escribió [save] por última vez. Si al guardar ya es otro (llegó una
     * edición por sync que **conserva la sal**, como el `Reprotect` de Windows) se rechaza: sólo comparar la sal dejaba pisar esa edición.
     */
    private val known = HashMap<String, String?>()

    /**
     * Cifra el texto de la nota con la contraseña. Se cifra con los saltos de línea originales de la nota (`wireText`): es lo que ve
     * Windows al abrirla.
     */
    fun protect(id: String, password: String, confirmation: String): ProtectionResult<Note> {
        val problem = PasswordRules.check(password, confirmation)
        if (problem != PasswordProblem.None) return err(ProtectionError.InvalidPassword(problem))
        // Pasa las reglas pero core no acepta una contraseña de sólo espacios: para el usuario es como no haber escrito ninguna.
        if (password.isBlank()) return err(ProtectionError.InvalidPassword(PasswordProblem.Empty))

        val note = store.get(id) ?: return err(ProtectionError.NotFound(id))
        if (note.isProtected) return err(ProtectionError.AlreadyProtected(id))
        val content = ProtectedNotes.protect(note.wireText, password)

        return synchronized(lock) {
            val current = store.get(id) ?: return@synchronized err(ProtectionError.NotFound(id))
            if (current.isProtected) return@synchronized err(ProtectionError.AlreadyProtected(id))
            if (current.wireText != note.wireText) return@synchronized err(ProtectionError.StaleWrite(id))
            ok(write(current, protectedContentToJson(content)))
        }
    }

    /**
     * Abre la nota. El texto de la sesión (`text`) viene con los saltos de línea con los que se cifró; quien lo edita lo pasa por
     * `LineEnding.normalize` y [save] los devuelve a su forma original.
     */
    fun unlock(id: String, password: String): ProtectionResult<UnlockedProtectedNote> {
        val note = store.get(id) ?: return err(ProtectionError.NotFound(id))
        if (!note.isProtected) return err(ProtectionError.NotProtected(id))
        val content = readContent(note) ?: return err(ProtectionError.UnreadableContent(id))
        if (content.version != ProtectedNoteContent.CURRENT_VERSION) return err(ProtectionError.UnsupportedVersion(id))
        val unlocked = ProtectedNotes.unlock(content, password) ?: return err(ProtectionError.WrongPassword)
        synchronized(lock) { known[id] = note.protectedContentJson }
        return ok(unlocked)
    }

    /**
     * Guarda el texto que devuelve el editor de una protegida abierta. Reutiliza la clave de la sesión (no deriva otra vez), así que sirve
     * para cada autoguardado. Los saltos de línea se devuelven a los del texto con el que se abrió (una nota de Windows con CRLF sigue
     * siendo CRLF). [editedAt] y [ProtectionError.StaleWrite] igual que en `NoteService.edit`; además, si la nota guardada ya es otra
     * versión (otra sal: se cambió la contraseña en otro equipo y llegó por sync), tampoco se escribe.
     */
    fun save(id: String, unlocked: UnlockedProtectedNote, newText: String, editedAt: Instant? = null): ProtectionResult<Note> =
        synchronized(lock) {
            val note = store.get(id) ?: return err(ProtectionError.NotFound(id))
            if (!note.isProtected) return err(ProtectionError.NotProtected(id))
            val stored = readContent(note) ?: return err(ProtectionError.UnreadableContent(id))

            // Una sesión ya cerrada (se salió de la nota, o la app pasó a segundo plano y llega un autoguardado tardío) lanza
            // IllegalStateException: no hay clave con la que cifrar, así que no se escribe nada y se avisa como una escritura rechazada.
            val content = try {
                val plain = LineEnding.normalize(newText).let {
                    if (LineEnding.detect(unlocked.text) == LineEnding.CRLF) it.replace("\n", "\r\n") else it
                }
                unlocked.reprotect(plain)
            } catch (e: IllegalStateException) {
                return err(ProtectionError.StaleWrite(id))
            }
            if (content.salt != stored.salt) return err(ProtectionError.StaleWrite(id))
            if (known.containsKey(id) && known[id] != note.protectedContentJson) return err(ProtectionError.StaleWrite(id))

            // Después de las comprobaciones que pueden rechazar: una escritura rechazada no debe quedar anotada como «última edición».
            if (editedAt != null) {
                val last = lastEdit[id]
                if (last != null && editedAt < last) return err(ProtectionError.StaleWrite(id))
                lastEdit[id] = editedAt
            }
            val json = protectedContentToJson(content)
            val saved = write(note, json)
            known[id] = saved.protectedContentJson
            ok(saved)
        }

    /** Quita la protección: el texto vuelve a ser el normal y desaparece el contenido cifrado. Con la contraseña equivocada no cambia nada. */
    fun removeProtection(id: String, password: String): ProtectionResult<Note> {
        val note = store.get(id) ?: return err(ProtectionError.NotFound(id))
        if (!note.isProtected) return err(ProtectionError.NotProtected(id))
        val content = readContent(note) ?: return err(ProtectionError.UnreadableContent(id))
        if (content.version != ProtectedNoteContent.CURRENT_VERSION) return err(ProtectionError.UnsupportedVersion(id))
        val plain = ProtectedNotes.tryUnprotect(content, password) ?: return err(ProtectionError.WrongPassword)

        return synchronized(lock) {
            val current = store.get(id) ?: return@synchronized err(ProtectionError.NotFound(id))
            if (!current.isProtected) return@synchronized err(ProtectionError.NotProtected(id))
            if (current.protectedContentJson != note.protectedContentJson) return@synchronized err(ProtectionError.StaleWrite(id))

            val restored = current.copy(
                text = LineEnding.normalize(plain), lineEnding = LineEnding.detect(plain),
                isProtected = false, protectedContentJson = null,
                updatedAt = clock.now(), syncAuthorDeviceId = null,
            )
            store.put(restored)
            lastEdit.remove(id)
            ok(restored)
        }
    }

    // El texto normal de una protegida va siempre vacío: si no, viajaría en claro por la sync.
    private fun write(note: Note, protectedJson: String): Note {
        val written = note.copy(
            text = "", isProtected = true, protectedContentJson = protectedJson,
            updatedAt = clock.now(), syncAuthorDeviceId = null,
        )
        store.put(written)
        return written
    }

    private fun readContent(note: Note) = try {
        parseProtectedContent(note.protectedContentJson ?: error("sin contenido"), note.id)
    } catch (e: IllegalStateException) {
        null
    }

    private fun <T> ok(value: T): ProtectionResult<T> = ProtectionResult.Ok(value)

    private fun err(error: ProtectionError): ProtectionResult.Err = ProtectionResult.Err(error)
}

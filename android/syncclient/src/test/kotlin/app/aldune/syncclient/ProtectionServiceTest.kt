package app.aldune.syncclient

import app.aldune.core.PasswordProblem
import app.aldune.core.ProtectedNotes
import app.aldune.core.UnlockedProtectedNote
import app.aldune.domain.IdFactory
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteError
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.domain.NoteStore
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertNotEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

class ProtectionServiceTest {
    private val password = "contraseña-1234"
    private val clock = TestClock()
    private val store = InMemoryNoteStore()
    private val service = ProtectionService(store, clock)

    private fun put(n: Note): Note = n.also(store::put)

    private fun ok(result: ProtectionResult<Note>): Note = assertIs<ProtectionResult.Ok<Note>>(result).value

    private fun err(result: ProtectionResult<*>): ProtectionError = assertIs<ProtectionResult.Err>(result).error

    /** Una nota protegida como la deja `protect`, con el texto en claro que se pasa. */
    private fun protectedNote(text: String = "secreto", id: String = ID1): Note {
        put(note(id = id, text = text))
        clock.advanceSeconds(10)
        return ok(service.protect(id, password, password))
    }

    private fun ProtectionService.unlockOrThrow(id: String, pw: String): UnlockedProtectedNote =
        assertIs<ProtectionResult.Ok<UnlockedProtectedNote>>(unlock(id, pw)).value

    private fun decrypt(n: Note): String? = ProtectedNotes.tryUnprotect(parseProtectedContent(n.protectedContentJson!!, n.id), password)

    // --- protect ---

    @Test fun `protect cifra el texto, deja el normal vacio y sella la version`() {
        put(note(text = "hola ☐ ñ", author = OTHER_DEVICE))
        clock.advanceSeconds(60)

        val result = ok(service.protect(ID1, password, password))

        assertEquals("", result.text)
        assertTrue(result.isProtected)
        assertEquals(clock.current, result.updatedAt)
        assertNull(result.syncAuthorDeviceId)
        assertEquals("hola ☐ ñ", decrypt(result))
        assertEquals(result, store.get(ID1))
    }

    @Test fun `protect valida la contrasena con PasswordRules y no escribe si falla`() {
        put(note())
        val writes = store.writeCount
        assertEquals(ProtectionError.InvalidPassword(PasswordProblem.Empty), err(service.protect(ID1, "", "")))
        assertEquals(ProtectionError.InvalidPassword(PasswordProblem.TooShort), err(service.protect(ID1, "abc", "abc")))
        assertEquals(ProtectionError.InvalidPassword(PasswordProblem.Mismatch), err(service.protect(ID1, "abcd", "abce")))
        // Cuatro espacios pasan PasswordRules pero core no los acepta como contraseña: se trata como vacía.
        assertEquals(ProtectionError.InvalidPassword(PasswordProblem.Empty), err(service.protect(ID1, "    ", "    ")))
        assertEquals(writes, store.writeCount)
        assertFalse(store.get(ID1)!!.isProtected)
    }

    @Test fun `protect falla si ya esta protegida o no existe, sin escribir`() {
        val already = protectedNote()
        val writes = store.writeCount
        assertEquals(ProtectionError.AlreadyProtected(ID1), err(service.protect(ID1, "otra-clave", "otra-clave")))
        assertEquals(ProtectionError.NotFound(ID2), err(service.protect(ID2, password, password)))
        assertEquals(writes, store.writeCount)
        assertEquals(already, store.get(ID1))
    }

    @Test fun `protect de una nota CRLF cifra el texto con sus saltos originales`() {
        put(note(text = "a\nb", ending = LineEnding.CRLF))
        val result = ok(service.protect(ID1, password, password))
        assertEquals("a\r\nb", decrypt(result))
        assertEquals(LineEnding.CRLF, result.lineEnding)
    }

    // --- unlock ---

    @Test fun `unlock con la contrasena devuelve la sesion con el texto`() {
        protectedNote("mi texto")
        val unlocked = service.unlockOrThrow(ID1, password)
        assertEquals("mi texto", unlocked.text)
        unlocked.close()
    }

    @Test fun `unlock con otra contrasena o vacia es WrongPassword y no escribe`() {
        protectedNote()
        val writes = store.writeCount
        assertEquals(ProtectionError.WrongPassword, err(service.unlock(ID1, "otra-clave")))
        assertEquals(ProtectionError.WrongPassword, err(service.unlock(ID1, "")))
        assertEquals(writes, store.writeCount)
    }

    @Test fun `unlock de una nota que no esta protegida o no existe`() {
        put(note())
        assertEquals(ProtectionError.NotProtected(ID1), err(service.unlock(ID1, password)))
        assertEquals(ProtectionError.NotFound(ID2), err(service.unlock(ID2, password)))
    }

    @Test fun `unlock de un contenido protegido ilegible es un error y no una excepcion`() {
        put(note(isProtected = true, protectedJson = "no es json"))
        assertEquals(ProtectionError.UnreadableContent(ID1), err(service.unlock(ID1, password)))
    }

    @Test fun `unlock lee una protegida llegada de sync (con el contenido de core)`() {
        val content = ProtectedNotes.protect("de Windows", password)
        put(note(isProtected = true, text = "", protectedJson = protectedContentToJson(content), author = OTHER_DEVICE))
        val unlocked = service.unlockOrThrow(ID1, password)
        assertEquals("de Windows", unlocked.text)
    }

    // --- save ---

    @Test fun `save cifra el texto nuevo con la misma sal y deja el normal vacio`() {
        val before = protectedNote("viejo")
        val unlocked = service.unlockOrThrow(ID1, password)
        clock.advanceSeconds(30)

        val saved = ok(service.save(ID1, unlocked, "nuevo ☐ texto"))

        assertEquals("", saved.text)
        assertTrue(saved.isProtected)
        assertEquals("nuevo ☐ texto", decrypt(saved))
        assertEquals(clock.current, saved.updatedAt)
        assertNull(saved.syncAuthorDeviceId)
        assertEquals(
            parseProtectedContent(before.protectedContentJson!!, ID1).salt,
            parseProtectedContent(saved.protectedContentJson!!, ID1).salt,
        )
        assertEquals(saved, store.get(ID1))
    }

    @Test fun `save de una protegida llegada de Windows con CRLF conserva los saltos de linea`() {
        val content = ProtectedNotes.protect("a\r\nb", password)
        put(note(isProtected = true, text = "", protectedJson = protectedContentToJson(content), author = OTHER_DEVICE))
        val unlocked = service.unlockOrThrow(ID1, password)

        val saved = ok(service.save(ID1, unlocked, "a\nb\nc"))

        assertEquals("a\r\nb\r\nc", decrypt(saved))
    }

    @Test fun `save cifra con LF si el texto abierto era LF`() {
        protectedNote("a\nb")
        val unlocked = service.unlockOrThrow(ID1, password)
        assertEquals("a\nb\nc", decrypt(ok(service.save(ID1, unlocked, "a\nb\nc"))))
    }

    @Test fun `save con una escritura mas antigua que otra aceptada es StaleWrite y no escribe`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        ok(service.save(ID1, unlocked, "uno", editedAt = T0.plusSeconds(100)))
        val writes = store.writeCount

        assertEquals(ProtectionError.StaleWrite(ID1), err(service.save(ID1, unlocked, "viejo", editedAt = T0.plusSeconds(99))))
        assertEquals(writes, store.writeCount)
        assertEquals("uno", decrypt(store.get(ID1)!!))
        // Con la misma hora gana la última en llegar, como en NoteService.edit.
        assertEquals("dos", decrypt(ok(service.save(ID1, unlocked, "dos", editedAt = T0.plusSeconds(100)))))
    }

    @Test fun `una escritura rechazada no queda anotada como ultima edicion`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        ok(service.save(ID1, unlocked, "uno", editedAt = T0.plusSeconds(100)))
        err(service.save(ID1, unlocked, "viejo", editedAt = T0.plusSeconds(50)))
        assertEquals("tres", decrypt(ok(service.save(ID1, unlocked, "tres", editedAt = T0.plusSeconds(100)))))
    }

    @Test fun `save sin editedAt no comprueba nada`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        ok(service.save(ID1, unlocked, "uno", editedAt = T0.plusSeconds(100)))
        assertEquals("dos", decrypt(ok(service.save(ID1, unlocked, "dos"))))
    }

    @Test fun `save de una nota que ya no existe o ya no esta protegida falla sin escribir`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        store.delete(ID1, clock.current)
        val writes = store.writeCount
        assertEquals(ProtectionError.NotFound(ID1), err(service.save(ID1, unlocked, "x")))

        put(note(text = "en claro"))
        assertEquals(ProtectionError.NotProtected(ID1), err(service.save(ID1, unlocked, "x")))
        assertEquals(writes + 1, store.writeCount)
        assertEquals("en claro", store.get(ID1)!!.text)
    }

    @Test fun `save con otra sal en la nota guardada (llego otra version por sync) es StaleWrite y no escribe`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        val other = ProtectedNotes.protect("de otro equipo", "otra-clave")
        store.put(store.get(ID1)!!.copy(protectedContentJson = protectedContentToJson(other)))
        val writes = store.writeCount

        assertEquals(ProtectionError.StaleWrite(ID1), err(service.save(ID1, unlocked, "mio")))
        assertEquals(writes, store.writeCount)
    }

    @Test fun `save con la misma sal pero otro texto llegado por sync (Reprotect de Windows) es StaleWrite y no pisa`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        // Windows conserva la sal al reproteger: cambian el nonce y el texto cifrado, no la sal.
        val remote = service.unlockOrThrow(ID1, password).reprotect("editado en Windows")
        store.put(store.get(ID1)!!.copy(protectedContentJson = protectedContentToJson(remote)))
        val writes = store.writeCount

        assertEquals(ProtectionError.StaleWrite(ID1), err(service.save(ID1, unlocked, "mio")))
        assertEquals(writes, store.writeCount)
    }

    @Test fun `save con la sesion ya cerrada no lanza y es StaleWrite y no escribe`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        unlocked.close()
        val writes = store.writeCount

        assertEquals(ProtectionError.StaleWrite(ID1), err(service.save(ID1, unlocked, "tarde")))
        assertEquals(writes, store.writeCount)
    }

    @Test fun `dos autoguardados seguidos de la misma sesion siguen funcionando`() {
        protectedNote()
        val unlocked = service.unlockOrThrow(ID1, password)
        ok(service.save(ID1, unlocked, "uno"))
        ok(service.save(ID1, unlocked, "dos"))
    }

    @Test fun `NoteService edit sigue rechazando una protegida`() {
        protectedNote()
        val notes = NoteService(store, clock, DEVICE, IdFactory { "id" })
        assertEquals(NoteError.Protected(ID1), notes.edit(ID1, "x").errorOrNull())
    }

    // --- removeProtection ---

    @Test fun `removeProtection restaura el texto normal y quita la proteccion`() {
        protectedNote("vuelve ☐")
        clock.advanceSeconds(5)

        val result = ok(service.removeProtection(ID1, password))

        assertEquals("vuelve ☐", result.text)
        assertFalse(result.isProtected)
        assertNull(result.protectedContentJson)
        assertEquals(clock.current, result.updatedAt)
        assertNull(result.syncAuthorDeviceId)
        assertEquals(result, store.get(ID1))
    }

    @Test fun `removeProtection de un texto CRLF lo guarda con LF y recuerda el salto original`() {
        val content = ProtectedNotes.protect("a\r\nb", password)
        put(note(isProtected = true, text = "", protectedJson = protectedContentToJson(content), author = OTHER_DEVICE))
        val result = ok(service.removeProtection(ID1, password))
        assertEquals("a\nb", result.text)
        assertEquals(LineEnding.CRLF, result.lineEnding)
    }

    @Test fun `removeProtection con otra contrasena es WrongPassword y no toca nada`() {
        val before = protectedNote()
        val writes = store.writeCount
        assertEquals(ProtectionError.WrongPassword, err(service.removeProtection(ID1, "otra-clave")))
        assertEquals(ProtectionError.WrongPassword, err(service.removeProtection(ID1, "")))
        assertEquals(writes, store.writeCount)
        assertEquals(before, store.get(ID1))
    }

    @Test fun `removeProtection de una nota que no esta protegida o no existe`() {
        put(note())
        assertEquals(ProtectionError.NotProtected(ID1), err(service.removeProtection(ID1, password)))
        assertEquals(ProtectionError.NotFound(ID2), err(service.removeProtection(ID2, password)))
    }

    @Test fun `proteger, abrir, guardar y quitar la proteccion termina con el texto nuevo en claro`() {
        put(note(text = "uno"))
        ok(service.protect(ID1, password, password))
        val unlocked = service.unlockOrThrow(ID1, password)
        ok(service.save(ID1, unlocked, "dos"))
        assertEquals("dos", ok(service.removeProtection(ID1, password)).text)
    }

    // --- campos que se conservan: un test por campo, en las tres operaciones ---

    /** Aplica protect, save y removeProtection a una nota con [original] y comprueba [field] después de cada una. */
    private fun assertKept(original: Note, field: (Note) -> Any?) {
        put(original)
        val expected = field(original)
        val protectedNote = ok(service.protect(original.id, password, password))
        assertEquals(expected, field(protectedNote), "tras protect")
        val unlocked = service.unlockOrThrow(original.id, password)
        assertEquals(expected, field(ok(service.save(original.id, unlocked, "otro texto"))), "tras save")
        assertEquals(expected, field(ok(service.removeProtection(original.id, password))), "tras removeProtection")
    }

    private val rich = note(
        color = "#112233", tags = listOf("Trabajo", "casa"), position = 2.5, state = NoteState.ARCHIVED, origin = "\\\\.\\DISPLAY2",
        createdAt = T0.minusSeconds(1000),
    )

    @Test fun `se conserva el color`() = assertKept(rich) { it.color }
    @Test fun `se conservan las etiquetas`() = assertKept(rich) { it.tags }
    @Test fun `se conserva la posicion en el mazo`() = assertKept(rich) { it.dockPosition }
    @Test fun `se conserva el estado`() = assertKept(rich) { it.state }
    @Test fun `se conserva el origen de pantalla`() = assertKept(rich) { it.screenOrigin }
    @Test fun `se conservan el id y la fecha de creacion`() = assertKept(rich) { it.id to it.createdAt }

    @Test fun `el estado papelera tambien se conserva`() = assertKept(rich.copy(state = NoteState.TRASHED)) { it.state }

    @Test fun `cada operacion cambia updatedAt aunque la nota ya fuera de otro equipo`() {
        put(note(updatedAt = T0, author = OTHER_DEVICE))
        clock.advanceSeconds(1)
        val a = ok(service.protect(ID1, password, password))
        assertNotEquals(T0, a.updatedAt)
        clock.advanceSeconds(1)
        val b = ok(service.save(ID1, service.unlockOrThrow(ID1, password), "x"))
        assertTrue(b.updatedAt > a.updatedAt)
        clock.advanceSeconds(1)
        assertTrue(ok(service.removeProtection(ID1, password)).updatedAt > b.updatedAt)
    }

    // --- escrituras concurrentes: la derivación de la clave es lenta y no puede pisar un cambio hecho mientras tanto ---

    /** Un almacén que, tras la primera lectura de [id], aplica [change] como lo haría otro hilo (p. ej. NoteService.setColor). */
    private class ChangingStore(private val inner: InMemoryNoteStore, private val id: String, private val change: (Note) -> Note) : NoteStore by inner {
        private var reads = 0
        override fun get(id: String): Note? {
            val result = inner.get(id)
            if (id == this.id && result != null && reads++ == 0) inner.put(change(result))
            return result
        }
    }

    @Test fun `protect no pisa un cambio de color hecho mientras se derivaba la clave`() {
        put(note(color = "#111111"))
        val racing = ProtectionService(ChangingStore(store, ID1) { it.copy(color = "#999999") }, clock)

        val result = ok(racing.protect(ID1, password, password))

        assertEquals("#999999", result.color)
        assertEquals("#999999", store.get(ID1)!!.color)
    }

    @Test fun `protect con el texto cambiado mientras tanto es StaleWrite y no protege un texto viejo`() {
        put(note(text = "uno"))
        val racing = ProtectionService(ChangingStore(store, ID1) { it.copy(text = "dos") }, clock)

        assertEquals(ProtectionError.StaleWrite(ID1), err(racing.protect(ID1, password, password)))
        assertFalse(store.get(ID1)!!.isProtected)
        assertEquals("dos", store.get(ID1)!!.text)
    }

    @Test fun `removeProtection no pisa un cambio de color hecho mientras se derivaba la clave`() {
        protectedNote()
        val racing = ProtectionService(ChangingStore(store, ID1) { it.copy(color = "#999999") }, clock)
        assertEquals("#999999", ok(racing.removeProtection(ID1, password)).color)
    }

    @Test fun `removeProtection con el contenido cambiado mientras tanto es StaleWrite`() {
        protectedNote()
        val other = ProtectedNotes.protect("otra version", "otra-clave")
        val racing = ProtectionService(ChangingStore(store, ID1) { it.copy(protectedContentJson = protectedContentToJson(other)) }, clock)
        assertEquals(ProtectionError.StaleWrite(ID1), err(racing.removeProtection(ID1, password)))
        assertTrue(store.get(ID1)!!.isProtected)
    }
    @Test fun `unlock con una version de contenido desconocida es UnsupportedVersion, no WrongPassword`() {
        val stored = protectedNote()
        val newer = parseProtectedContent(stored.protectedContentJson!!, ID1).copy(version = 99)
        store.put(stored.copy(protectedContentJson = protectedContentToJson(newer)))
        assertEquals(ProtectionError.UnsupportedVersion(ID1), err(service.unlock(ID1, password)))
    }

    @Test fun `removeProtection con una version de contenido desconocida es UnsupportedVersion`() {
        val stored = protectedNote()
        val newer = parseProtectedContent(stored.protectedContentJson!!, ID1).copy(version = 99)
        store.put(stored.copy(protectedContentJson = protectedContentToJson(newer)))
        assertEquals(ProtectionError.UnsupportedVersion(ID1), err(service.removeProtection(ID1, password)))
    }

    @Test fun `servicios que comparten cerrojo se excluyen entre si`() {
        val shared = Any()
        val a = ProtectionService(store, clock, shared)
        val notes = NoteService(store, clock, DEVICE, IdFactory { "id" }, shared)
        put(note())
        val done = java.util.concurrent.CountDownLatch(1)
        synchronized(shared) {
            Thread { notes.setColor(ID1, "#123456"); done.countDown() }.start()
            assertTrue(!done.await(300, java.util.concurrent.TimeUnit.MILLISECONDS), "setColor no debe entrar con el cerrojo ajeno cogido")
        }
        assertTrue(done.await(5, java.util.concurrent.TimeUnit.SECONDS))
        assertEquals("#123456", store.get(ID1)!!.color)
        a.hashCode()
    }
}

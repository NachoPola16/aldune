package app.aldune.domain

import java.time.Duration
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertTrue

/** Historial de versiones local: 20 por nota, 5 minutos entre copias más una al cerrar, restaurar guarda antes el texto actual. */
class NoteVersionHistoryTest {
    private class Rig {
        val f = Fixture()
        val versions = InMemoryVersionStore()
        val service = NoteService(f.store, f.clock, "android-1", SequentialIds(), history = NoteHistory(versions))
        fun create(text: String) = service.create(text, "#FFDDD4").getOrThrow()
        fun texts(id: String) = service.versions(id).map { it.text }
    }

    @Test fun `la primera edicion guarda el texto que habia`() {
        val r = Rig()
        val note = r.create("original")
        r.f.clock.advance(Duration.ofSeconds(5))
        r.service.edit(note.id, "cambiado")
        assertEquals(listOf("original"), r.texts(note.id))
    }

    @Test fun `las ediciones dentro del plazo no suman versiones y pasado el plazo guardan el texto anterior`() {
        val r = Rig()
        val note = r.create("v0")
        r.service.edit(note.id, "v1")
        r.f.clock.advance(Duration.ofMinutes(1)); r.service.edit(note.id, "v2")
        assertEquals(listOf("v0"), r.texts(note.id))
        r.f.clock.advance(Duration.ofMinutes(6)); r.service.edit(note.id, "v3")
        assertEquals(listOf("v2", "v0"), r.texts(note.id))
    }

    @Test fun `solo se guardan las ultimas 20`() {
        val r = Rig()
        val note = r.create("v0")
        for (i in 1..30) { r.f.clock.advance(Duration.ofMinutes(10)); r.service.edit(note.id, "v$i") }
        val texts = r.texts(note.id)
        assertEquals(NoteHistory.MAX_VERSIONS, texts.size)
        assertEquals("v29", texts.first())
        assertEquals("v10", texts.last())
    }

    @Test fun `cerrar guarda el texto actual una sola vez`() {
        val r = Rig()
        val note = r.create("v0")
        r.service.edit(note.id, "v1")
        r.service.recordVersion(note.id)
        r.service.recordVersion(note.id)
        assertEquals(listOf("v1", "v0"), r.texts(note.id))
    }

    @Test fun `restaurar guarda antes el texto actual y se puede deshacer`() {
        val r = Rig()
        val note = r.create("viejo")
        r.service.edit(note.id, "actual")
        val old = r.service.versions(note.id).single()
        assertIs<NoteResult.Ok<Note>>(r.service.restoreVersion(note.id, old.id))
        assertEquals("viejo", r.service.get(note.id)!!.text)
        assertTrue("actual" in r.texts(note.id))
    }

    @Test fun `restaurar una version ajena o inexistente no hace nada`() {
        val r = Rig()
        val a = r.create("a0"); val b = r.create("b0")
        r.service.edit(a.id, "a1")
        val version = r.service.versions(a.id).single()
        assertIs<NoteResult.Err>(r.service.restoreVersion(b.id, version.id))
        assertIs<NoteResult.Err>(r.service.restoreVersion(a.id, 9999))
        assertEquals("b0", r.service.get(b.id)!!.text)
    }

    @Test fun `una nota protegida no guarda historial y proteger lo borra`() {
        val r = Rig()
        val note = r.create("secreto")
        r.service.edit(note.id, "secreto 2")
        assertFalse(r.service.versions(note.id).isEmpty())
        r.service.forgetVersions(note.id)
        assertTrue(r.service.versions(note.id).isEmpty())
        r.f.store.put(r.service.get(note.id)!!.copy(isProtected = true, protectedContentJson = "{}"))
        r.service.recordVersion(note.id)
        assertTrue(r.service.versions(note.id).isEmpty())
    }

    @Test fun `borrar la nota para siempre borra su historial`() {
        val r = Rig()
        val note = r.create("v0")
        r.service.edit(note.id, "v1")
        r.service.trash(note.id)
        r.service.deleteForever(note.id)
        assertTrue(r.service.versions(note.id).isEmpty())
    }

    @Test fun `un reemplazo por sync guarda antes el texto local y no si no cambia el texto ni si alguna es protegida`() {
        val versions = InMemoryVersionStore()
        val history = NoteHistory(versions)
        val at = java.time.Instant.parse("2026-03-01T10:00:00Z")
        history.keepBeforeOverwrite("n1", "local", false, "remoto", false, at)
        assertEquals(listOf("local"), versions.list("n1").map { it.text })
        history.keepBeforeOverwrite("n2", "igual", false, "igual", false, at)
        history.keepBeforeOverwrite("n3", "secreto", true, "remoto", false, at)
        history.keepBeforeOverwrite("n4", "local", false, "secreto", true, at)
        assertTrue(listOf("n2", "n3", "n4").all { versions.list(it).isEmpty() })
    }

    @Test fun `sin historial inyectado el servicio se comporta como siempre`() {
        val f = Fixture()
        val note = f.create("v0")
        f.service.edit(note.id, "v1")
        assertTrue(f.service.versions(note.id).isEmpty())
    }
}

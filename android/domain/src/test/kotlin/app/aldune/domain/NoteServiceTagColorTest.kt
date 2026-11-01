package app.aldune.domain

import java.time.Duration
import kotlin.test.Test
import kotlin.test.assertEquals

class NoteServiceTagColorTest {
    private fun note(f: Fixture, id: String, state: NoteState = NoteState.ACTIVE, tags: List<String> = emptyList()) {
        f.store.put(Note(id, "text $id", LineEnding.LF, "#FFDDD4", f.clock.current, f.clock.current, state, tags = tags, syncAuthorDeviceId = "pc"))
        f.clock.advance(Duration.ofMinutes(1))
    }

    @Test fun `una etiqueta con color se lo da a la nota que la gana`() {
        val f = Fixture()
        f.service.tagColors = mapOf("casa" to "#A8E0B4", "trabajo" to "#9EC9F0")
        note(f, "n1")
        f.service.setTags("n1", listOf("Casa"))
        assertEquals("#A8E0B4", f.stored("n1").color)
        // Con dos nuevas gana la ultima; guardar las mismas etiquetas otra vez no pisa un color puesto a mano.
        f.service.setTags("n1", listOf("Casa", "Trabajo"))
        assertEquals("#9EC9F0", f.stored("n1").color)
        f.service.setColor("n1", "#FFDDD4")
        f.service.setTags("n1", listOf("Casa", "Trabajo"))
        assertEquals("#FFDDD4", f.stored("n1").color)
    }

    @Test fun `una etiqueta sin color no toca el color de la nota`() {
        val f = Fixture()
        f.service.tagColors = mapOf("casa" to "#A8E0B4")
        note(f, "n1")
        f.service.setTags("n1", listOf("ideas"))
        assertEquals("#FFDDD4", f.stored("n1").color)
    }

    @Test fun `aplicar el color de una etiqueta pinta activas y archivadas pero no la papelera`() {
        val f = Fixture()
        note(f, "a", tags = listOf("Casa"))
        note(f, "b", NoteState.ARCHIVED, listOf("casa"))
        note(f, "c", NoteState.TRASHED, listOf("casa"))
        note(f, "d")
        f.service.applyTagColor("CASA", "#A8E0B4")
        assertEquals("#A8E0B4", f.stored("a").color)
        assertEquals("#A8E0B4", f.stored("b").color)
        assertEquals("#FFDDD4", f.stored("c").color)
        assertEquals("#FFDDD4", f.stored("d").color)
    }
}

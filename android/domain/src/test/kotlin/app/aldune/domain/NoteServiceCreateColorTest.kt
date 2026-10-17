package app.aldune.domain

import app.aldune.text.NoteColorAssigner
import app.aldune.text.NoteColorAssignment
import app.aldune.text.NoteThemes
import app.aldune.text.NoteTone
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals

class NoteServiceCreateColorTest {
    private val serene = NoteThemes.builtIn.first { it.id == NoteThemes.SERENE_ID }
    private val light = NewNoteColorSettings(serene, NoteTone.Light, NoteColorAssignment.Rotate)

    private fun putActive(f: Fixture, id: String, color: String, position: Double, state: NoteState = NoteState.ACTIVE) {
        val at = Instant.parse("2026-01-01T00:00:00Z")
        f.store.put(Note(id, id, LineEnding.LF, color, at, at, state, dockPosition = position))
    }

    @Test fun `un color explicito se respeta tal cual, sin normalizar`() {
        val f = Fixture()
        assertEquals("#abcdef", f.service.create("x", color = "#abcdef", settings = light).getOrThrow().color)
    }

    @Test fun `sin color, el primero del tema para un mazo vacio`() {
        val f = Fixture()
        assertEquals(serene.lightColors[0], f.service.create("x", settings = light).getOrThrow().color)
    }

    @Test fun `rotar sigue al ultimo color del mazo activo, en el orden del mazo y no en el de creacion`() {
        val f = Fixture()
        // Creadas en un orden y colocadas en otro: la ultima del mazo es la de posicion mayor.
        putActive(f, "z", serene.lightColors[4], position = 0.0)
        putActive(f, "a", serene.lightColors[1], position = 10.0)
        assertEquals(serene.lightColors[2], f.service.create("x", settings = light).getOrThrow().color)
    }

    @Test fun `archivadas y papelera no cuentan como mazo`() {
        val f = Fixture()
        putActive(f, "a", serene.lightColors[1], 0.0)
        putActive(f, "b", serene.lightColors[3], 1.0, NoteState.ARCHIVED)
        putActive(f, "c", serene.lightColors[4], 2.0, NoteState.TRASHED)
        assertEquals(serene.lightColors[2], f.service.create("x", settings = light).getOrThrow().color)
    }

    @Test fun `con una etiqueta de vista solo cuentan las notas de esa etiqueta`() {
        val f = Fixture()
        val at = Instant.parse("2026-01-01T00:00:00Z")
        f.store.put(Note("a", "a", LineEnding.LF, serene.lightColors[1], at, at, NoteState.ACTIVE, tags = listOf("Casa"), dockPosition = 0.0))
        f.store.put(Note("b", "b", LineEnding.LF, serene.lightColors[4], at, at, NoteState.ACTIVE, tags = listOf("Trabajo"), dockPosition = 1.0))
        assertEquals(serene.lightColors[2], f.service.create("x", settings = light, viewTag = "casa").getOrThrow().color)
    }

    @Test fun `el resultado es el de NoteColorAssigner para cada regla`() {
        for (rule in NoteColorAssignment.entries) for (tone in NoteTone.entries) {
            val f = Fixture()
            putActive(f, "a", serene.darkColors[2], 0.0)
            putActive(f, "b", serene.lightColors[0], 1.0)
            val settings = NewNoteColorSettings(serene, tone, rule, fixedColor = serene.lightColors[3])
            val expected = NoteColorAssigner.assign(serene, tone, rule, serene.lightColors[3], listOf(serene.darkColors[2], serene.lightColors[0]))
            assertEquals(expected, f.service.create("x", settings = settings).getOrThrow().color, "$rule/$tone")
        }
    }

    @Test fun `los ajustes por defecto son los de Core sin ajustes, tema Clasico claro rotando evitando vecinas`() {
        val f = Fixture()
        val expected = NoteColorAssigner.assign(
            NoteThemes.builtIn[0], NoteTone.Light, NoteColorAssignment.RotateAvoidNeighbors, null, emptyList(),
        )
        assertEquals(expected, f.service.create("x").getOrThrow().color)
    }

    @Test fun `no crear nota no gasta color ni id`() {
        val f = Fixture()
        f.service.create("", settings = light)
        assertEquals("n1", f.service.create("x", settings = light).getOrThrow().id)
    }
}

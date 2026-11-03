package app.aldune.domain.importing

import app.aldune.domain.Fixture
import app.aldune.domain.NoteState
import app.aldune.text.NoteColorAssignment
import app.aldune.text.NoteColorDerivation
import app.aldune.text.NoteThemes
import app.aldune.text.NoteTone
import java.time.Duration
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

/** Same cases as `NoteImporterTests` in Aldune.Core. */
class NoteImporterTest {
    private val policy = ImportColorPolicy(NoteThemes.builtIn[0], NoteTone.Light, NoteColorAssignment.RotateAvoidNeighbors, null)

    private fun n(
        text: String, hue: ImportHue? = null, state: NoteState = NoteState.ACTIVE, pinned: Boolean = false,
        created: Instant? = null, updated: Instant? = null, tags: List<String> = emptyList(),
    ) = ImportedNote(text, hue, tags, state, pinned, created, updated)

    private fun preview(vararg notes: ImportedNote) = ImportPreview(notes = notes.toList())

    private fun all(f: Fixture) = f.store.all()

    private fun run(f: Fixture, p: ImportPreview, colors: ImportColorPolicy = policy) =
        NoteImporter.run(NoteImporter.plan(p, all(f)), f.service, colors, f.clock.current)

    @Test fun `run creates notes with original dates tags state and pin`() {
        val f = Fixture()
        val created = Instant.parse("2022-01-02T03:04:05Z")
        val updated = created.plus(Duration.ofDays(3))
        val result = run(f, preview(n("a\nbody", ImportHue.Green, NoteState.ARCHIVED, true, created, updated, listOf("x", "y"))))

        assertEquals(ImportResult(1, 0), result)
        val note = all(f).single()
        assertEquals(NoteState.ARCHIVED, note.state)
        assertEquals("a\nbody", note.text)
        assertEquals(created, note.createdAt)
        assertEquals(updated, note.updatedAt)
        assertTrue(note.pinnedToTop)
        assertEquals(listOf("x", "y"), note.tags.sorted())
        assertEquals(null, note.syncAuthorDeviceId)
    }

    @Test fun `running twice does not duplicate`() {
        val f = Fixture()
        val created = Instant.parse("2022-01-02T03:04:05Z")
        val p = preview(n("same", created = created), n("other", created = created))
        run(f, p)

        val plan2 = NoteImporter.plan(p, all(f))

        assertTrue(plan2.toCreate.isEmpty())
        assertEquals(2, plan2.duplicates)
        assertEquals(2, all(f).size)
    }

    @Test fun `same text with another original date is a different note but without dates the text decides`() {
        val f = Fixture()
        val d1 = Instant.parse("2022-01-01T00:00:00Z")
        run(f, preview(n("t", created = d1)))
        assertEquals(1, NoteImporter.plan(preview(n("t", created = d1.plus(Duration.ofDays(1)))), all(f)).toCreate.size)
        assertTrue(NoteImporter.plan(preview(n("t")), all(f)).toCreate.isEmpty())
    }

    @Test fun `duplicates inside the batch are collapsed and existing notes are never touched`() {
        val f = Fixture()
        val mine = f.create("mine")
        val plan = NoteImporter.plan(preview(n("mine"), n("dup"), n("dup")), all(f))
        assertEquals(listOf("dup"), plan.toCreate.map { it.text })
        assertEquals(2, plan.duplicates)
        NoteImporter.run(plan, f.service, policy, f.clock.current)
        assertEquals(mine, f.stored(mine.id))
        assertEquals(2, all(f).size)
    }

    @Test fun `protected notes never count as already imported`() {
        val f = Fixture()
        f.store.put(f.create("secret").copy(text = "", isProtected = true))
        assertEquals(1, NoteImporter.plan(preview(n("secret")), all(f)).toCreate.size)
    }

    @Test fun `trashed notes get a fresh bin clock`() {
        val f = Fixture()
        val old = Instant.parse("2019-01-01T00:00:00Z")
        run(f, preview(n("bin", state = NoteState.TRASHED, created = old, updated = old)))
        val note = all(f).single()
        assertEquals(old, note.createdAt)
        assertEquals(f.clock.current, note.updatedAt)
    }

    @Test fun `future and missing dates are clamped to now`() {
        val f = Fixture()
        val future = f.clock.current.plus(Duration.ofDays(2000))
        run(f, preview(n("f", created = future, updated = future.plusSeconds(10)), n("m")))
        for (note in all(f)) {
            assertEquals(f.clock.current, note.createdAt)
            assertEquals(f.clock.current, note.updatedAt)
        }
    }

    @Test fun `colours come from the active theme by hue`() {
        val f = Fixture()
        run(f, preview(n("y", ImportHue.Yellow), n("b", ImportHue.Blue), n("p", ImportHue.Pink)))
        val byText = all(f).associate { it.text to it.color }
        val theme = policy.theme
        assertTrue(byText.values.all { it in theme.lightColors + theme.darkColors })
        assertEquals(3, byText.values.distinct().size)
        assertEquals(ImportHue.Yellow, ImportHues.fromHex(byText.getValue("y")))
        assertEquals(ImportHue.Blue, ImportHues.fromHex(byText.getValue("b")))
    }

    @Test fun `dark tone picks dark colours and no hue still gets a theme colour`() {
        val f = Fixture()
        val dark = policy.copy(tone = NoteTone.Dark, theme = NoteThemes.builtIn.first { it.id == NoteThemes.SERENE_ID })
        run(f, preview(n("g", ImportHue.Green), n("none")), dark)
        assertTrue(all(f).all { NoteColorDerivation.isDark(it.color) })
    }

    @Test fun `an achromatic theme falls back to the normal assignment`() {
        val f = Fixture()
        val gray = NoteThemes.builtIn.first { it.id == NoteThemes.GRAY_ID }
        run(f, preview(n("a", ImportHue.Pink)), policy.copy(theme = gray))
        assertTrue(all(f).single().color in gray.lightColors)
    }

    @Test fun `imported notes are ready to sync`() {
        val f = Fixture()
        run(f, preview(n("s")))
        assertEquals(null, all(f).single().syncAuthorDeviceId)
    }

    @Test fun `one write per note`() {
        val f = Fixture()
        run(f, preview(n("a"), n("b"), n("c")))
        assertEquals(3, f.store.writeCount)
    }

    @Test fun `plan counts what the preview would do without writing anything`() {
        val f = Fixture()
        f.create("mine")
        val writes = f.store.writeCount
        val plan = NoteImporter.plan(preview(n("mine"), n("new")), all(f))
        assertEquals(1, plan.toCreate.size)
        assertEquals(writes, f.store.writeCount)
    }
}

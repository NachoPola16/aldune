package app.aldune.domain.importing

import app.aldune.domain.NoteState
import app.aldune.text.TaskLines
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

/** Same cases as `KeepTakeoutSourceTests` in Aldune.Core, with the inputs the system picker gives (files or a .zip). */
class KeepTakeoutSourceTest {
    private fun read(vararg inputs: ImportInput) = KeepTakeoutSource.read(inputs.toList())

    @Test fun `text note becomes title then body with dates labels and colour`() {
        val preview = read(file("a.json", """
            {"color":"YELLOW","isTrashed":false,"isPinned":true,"isArchived":false,"textContent":"line1\nline2","title":"Shopping",
             "userEditedTimestampUsec":1700000100000000,"createdTimestampUsec":1700000000000000,"labels":[{"name":"home"},{"name":"food"}]}"""))

        val n = preview.notes.single()
        assertEquals("Shopping\nline1\nline2", n.text)
        assertEquals(ImportHue.Yellow, n.hue)
        assertEquals(listOf("home", "food"), n.tags)
        assertTrue(n.pinned)
        assertEquals(NoteState.ACTIVE, n.state)
        assertEquals(Instant.ofEpochSecond(1700000000), n.createdAt)
        assertEquals(Instant.ofEpochSecond(1700000100), n.updatedAt)
    }

    @Test fun `checklist becomes task lines`() {
        val n = read(file("a.json", """{"title":"","listContent":[{"text":"milk","isChecked":false},{"text":"eggs","isChecked":true}],"color":"DEFAULT"}""")).notes.single()
        assertEquals("☐ milk\n☒ eggs", n.text)
        assertNull(n.hue)
        assertTrue(n.text.split("\n").all { TaskLines.isTaskLine(it) })
    }

    @Test fun `trash and archive map to states and trash wins`() {
        val states = read(
            file("t.json", """{"title":"t","textContent":"x","isTrashed":true,"isArchived":true}"""),
            file("r.json", """{"title":"r","textContent":"x","isArchived":true}"""),
        ).notes.associate { it.text.substringBefore("\n") to it.state }
        assertEquals(NoteState.TRASHED, states["t"])
        assertEquals(NoteState.ARCHIVED, states["r"])
    }

    @Test fun `empty notes are skipped and attachments counted`() {
        val preview = read(
            file("e.json", """{"title":"","textContent":""}"""),
            file("p.json", """{"title":"photo","textContent":"see","attachments":[{"filePath":"a.jpg","mimetype":"image/jpeg"},{"filePath":"b.3gp","mimetype":"audio/3gpp"}]}"""),
        )
        assertEquals(1, preview.notes.size)
        assertEquals(ImportSkipReason.EMPTY, preview.skipped.single().reason)
        assertEquals(2, preview.attachmentsIgnored)
        assertEquals(1, preview.notesWithAttachments)
    }

    @Test fun `json that is not a note is ignored and nothing found is reported`() {
        val preview = read(file("other.json", """{"hello":"world"}"""), file("broken.json", "{not json"))
        assertTrue(preview.notes.isEmpty())
        assertEquals(ImportFailure.NOTHING_FOUND, preview.failure)
    }

    @Test fun `zip from takeout is read in memory`() {
        val z = zip("takeout.zip", "Takeout/Keep/a.json" to """{"title":"zipped","textContent":"body"}""".toByteArray(), "Takeout/Keep/pic.jpg" to ByteArray(10))
        assertEquals(listOf("zipped\nbody"), read(z).texts)
    }

    @Test fun `a zip with entry names that try to escape is harmless because nothing is extracted`() {
        val z = zip("t.zip", "../../evil.json" to """{"title":"evil","textContent":"x"}""".toByteArray())
        assertEquals(listOf("evil\nx"), read(z).texts)
    }

    @Test fun `html without a json twin is used as a fallback`() {
        val preview = read(
            file("twin.json", """{"title":"twin","textContent":"json"}"""),
            file("twin.html", "<html><head><title>twin</title></head><body><div class=\"title\">twin</div><div class=\"content\">html</div></body></html>"),
            file("solo.html", "<html><head><title>solo</title></head><body><div class=\"title\">solo</div><div class=\"content\">only html</div></body></html>"),
        )
        assertEquals(listOf("solo\nonly html", "twin\njson"), preview.texts.sorted())
    }

    @Test fun `html twin inside a zip is matched with its json`() {
        val z = zip(
            "t.zip",
            "Keep/a.html" to "<div class=\"title\">a</div><div class=\"content\">html</div>".toByteArray(),
            "Keep/a.json" to """{"title":"a","textContent":"json"}""".toByteArray(),
        )
        assertEquals(listOf("a\njson"), read(z).texts)
    }

    @Test fun `a note over the size limit is skipped`() {
        val preview = read(
            file("big.json", "{\"title\":\"big\",\"textContent\":\"" + "x".repeat(ImportLimits.MAX_NOTE_CHARS + 10) + "\"}"),
            file("ok.json", """{"title":"ok","textContent":"y"}"""),
        )
        assertEquals(1, preview.notes.size)
        assertTrue(preview.skipped.any { it.reason == ImportSkipReason.TOO_LARGE })
    }

    @Test fun `more notes than the limit are left out with their reason`() {
        val inputs = (0 until ImportLimits.MAX_NOTES + 3).map { file("n$it.json", """{"title":"n$it","textContent":"t"}""") }
        val preview = read(*inputs.toTypedArray())
        assertEquals(ImportLimits.MAX_NOTES, preview.notes.size)
        assertEquals(3, preview.skipped.count { it.reason == ImportSkipReason.LIMIT_REACHED })
    }

    @Test fun `a corrupt zip is unreadable and a mix of zip and files is read`() {
        assertEquals(ImportFailure.UNREADABLE, read(ImportInput.ofBytes("bad.zip", ByteArray(50) { 7 })).failure)
        val z = zip("t.zip", "Keep/a.json" to """{"title":"a","textContent":"1"}""".toByteArray())
        assertEquals(2, read(z, file("b.json", """{"title":"b","textContent":"2"}""")).notes.size)
    }

    @Test fun `no inputs is not found`() = assertEquals(ImportFailure.NOT_FOUND, read().failure)
}

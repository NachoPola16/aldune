package app.aldune.domain.importing

import app.aldune.domain.NoteState
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNotEquals
import kotlin.test.assertTrue

/** Same cases as `EvernoteSourceTests` in Aldune.Core. */
class EvernoteSourceTest {
    private fun note(title: String, enml: String, extra: String = "") =
        "<note><title>$title</title><content><![CDATA[<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE en-note SYSTEM \"http://xml.evernote.com/pub/enml2.dtd\"><en-note>$enml</en-note>]]></content>$extra</note>"

    private fun export(vararg notes: String) =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE en-export SYSTEM \"http://xml.evernote.com/pub/evernote-export4.dtd\"><en-export export-date=\"20240101T000000Z\" application=\"Evernote\">" +
            notes.joinToString("") + "</en-export>"

    private fun read(xml: String) = EvernoteSource.read(listOf(file("x.enex", xml)))

    @Test fun `note reads title body tags and dates`() {
        val n = read(export(note("Trip", "<div>pack</div><div><en-todo checked=\"true\"/>passport</div><ul><li>a</li></ul>",
            "<created>20240102T030405Z</created><updated>20240103T030405Z</updated><tag>travel</tag><tag>2024</tag>"))).notes.single()
        assertEquals("Trip\npack\n☒ passport\n→ a", n.text)
        assertEquals(listOf("travel", "2024"), n.tags)
        assertEquals(Instant.parse("2024-01-02T03:04:05Z"), n.createdAt)
        assertEquals(Instant.parse("2024-01-03T03:04:05Z"), n.updatedAt)
        assertEquals(NoteState.ACTIVE, n.state)
    }

    @Test fun `resources are counted and not imported`() {
        val preview = read(export(note("Pic", "<div>x</div><en-media hash=\"1\" type=\"image/png\"/>",
            "<resource><data encoding=\"base64\">AAAA</data><mime>image/png</mime></resource><resource><data encoding=\"base64\">BBBB</data></resource>")))
        assertEquals(1, preview.notes.size)
        assertEquals(2, preview.attachmentsIgnored)
        assertEquals(1, preview.notesWithAttachments)
    }

    @Test fun `empty notes are skipped`() {
        val preview = read(export(note("", "<div><br/></div>"), note("Keep", "<div>x</div>")))
        assertEquals(1, preview.notes.size)
        assertEquals(ImportSkipReason.EMPTY, preview.skipped.single().reason)
    }

    @Test fun `a dtd with entities is refused and never resolved`() {
        val xml = "<?xml version=\"1.0\"?><!DOCTYPE en-export [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><en-export><note><title>&x;</title><content><![CDATA[<en-note>&x;</en-note>]]></content></note></en-export>"
        val preview = read(xml)
        assertTrue(preview.notes.isEmpty())
        assertNotEquals(ImportFailure.NONE, preview.failure)
    }

    @Test fun `the standard external dtd reference is ignored without fetching anything`() {
        assertEquals(1, read(export(note("ok", "<div>x</div>"))).notes.size)
    }

    @Test fun `a xml bomb through internal entities is refused`() {
        val xml = "<?xml version=\"1.0\"?><!DOCTYPE en-export [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;\">]><en-export><note><title>&b;</title></note></en-export>"
        assertTrue(read(xml).notes.isEmpty())
    }

    private val bomb = "<!DOCTYPE en-export [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;\">]><en-export><note><title>&b;</title></note></en-export>"

    @Test fun `a dtd hidden behind padding in the prolog is still refused`() {
        val preview = read("<?xml version=\"1.0\"?><!--" + "x".repeat(40_000) + "-->" + bomb)
        assertTrue(preview.notes.isEmpty())
        assertNotEquals(ImportFailure.NONE, preview.failure)
    }

    @Test fun `a fake root inside a huge comment does not hide the dtd`() {
        val preview = read("<!-- <en-export> " + "x".repeat(1_200_000) + " -->" + bomb)
        assertTrue(preview.notes.isEmpty())
        assertNotEquals(ImportFailure.NONE, preview.failure)
    }

    @Test fun `utf-16 without a byte order mark is refused`() {
        val xml = "<?xml version=\"1.0\" encoding=\"UTF-16LE\"?>" + bomb
        val preview = EvernoteSource.read(listOf(ImportInput.ofBytes("x.enex", xml.toByteArray(Charsets.UTF_16LE), null)))
        assertTrue(preview.notes.isEmpty())
        assertNotEquals(ImportFailure.NONE, preview.failure)
    }

    @Test fun `comments and instructions before the export are fine`() {
        val xml = "<?xml version=\"1.0\"?><!-- made by a tool --><?tool v=1?>" + export(note("ok", "<div>x</div>")).removePrefix("<?xml version=\"1.0\" encoding=\"UTF-8\"?>")
        assertEquals(1, read(xml).notes.size)
    }

    @Test fun `not an enex is a wrong format`() {
        assertEquals(ImportFailure.WRONG_FORMAT, read("<html><body>hi</body></html>").failure)
        assertEquals(ImportFailure.WRONG_FORMAT, read("plain text, not xml").failure)
    }

    @Test fun `more notes than the limit are left out with their reason`() {
        val notes = (0 until ImportLimits.MAX_NOTES + 3).map { note("n$it", "<div>t</div>") }
        val preview = read(export(*notes.toTypedArray()))
        assertEquals(ImportLimits.MAX_NOTES, preview.notes.size)
        assertEquals(3, preview.skipped.count { it.reason == ImportSkipReason.LIMIT_REACHED })
    }

    @Test fun `a note over the size limit is skipped`() {
        val preview = read(export(note("big", "<div>" + "x".repeat(ImportLimits.MAX_NOTE_CHARS * 2 + 10) + "</div>"), note("ok", "<div>y</div>")))
        assertEquals(1, preview.notes.size)
        assertTrue(preview.skipped.any { it.reason == ImportSkipReason.TOO_LARGE })
    }

    @Test fun `a truncated file keeps the notes read so far and reports the rest`() {
        val xml = export(note("a", "<div>1</div>"), note("b", "<div>2</div>")).replace("</en-export>", "")
        val preview = read(xml)
        assertEquals(2, preview.notes.size)
        assertTrue(preview.skipped.any { it.reason == ImportSkipReason.UNREADABLE })
    }
}

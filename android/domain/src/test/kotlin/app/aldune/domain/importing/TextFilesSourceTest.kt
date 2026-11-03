package app.aldune.domain.importing

import app.aldune.text.TextDecoding
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

/** Same cases as `TextFolderSourceTests` in Aldune.Core, for files picked one by one (the system picker has no folder import here). */
class TextFilesSourceTest {
    private fun read(vararg inputs: ImportInput) = TextFilesSource.read(inputs.toList())

    @Test fun `file name is the title when there is no heading`() =
        assertEquals("Shopping list\n☐ milk\n☒ eggs", read(file("Shopping list.md", "- [ ] milk\n- [x] eggs")).notes.single().text)

    @Test fun `heading is the title and the name is not added`() =
        assertEquals("Real title\n\nbody", read(file("file.md", "# Real title\n\nbody")).notes.single().text)

    @Test fun `txt and markdown files are read`() =
        assertEquals(listOf("a\nplain", "b\ndeep"), read(file("a.txt", "plain"), file("b.markdown", "deep")).texts.sorted())

    @Test fun `binary content is rejected and not imported as garbage`() {
        val preview = read(
            ImportInput.ofBytes("bin.txt", byteArrayOf(0, 1, 2, 0xFF.toByte(), 0xFE.toByte(), 0)),
            ImportInput.ofBytes("latin.txt", byteArrayOf(0x63, 0x61, 0x66, 0xE9.toByte())),
            file("ok.md", "fine"),
        )
        assertEquals(1, preview.notes.size)
        assertEquals(2, preview.skipped.count { it.reason == ImportSkipReason.NOT_TEXT })
    }

    @Test fun `empty and oversized files are skipped`() {
        val preview = read(file("empty.md", "   \n"), file("big.txt", "x".repeat(TextDecoding.MAX_BYTES + 1)), file("ok.md", "x"))
        assertEquals(1, preview.notes.size)
        assertTrue(preview.skipped.any { it.reason == ImportSkipReason.EMPTY })
        assertTrue(preview.skipped.any { it.reason == ImportSkipReason.TOO_LARGE })
    }

    @Test fun `dates come from the file modification time`() {
        val n = read(file("d.md", "x", modified = 1_700_000_000_000)).notes.single()
        assertEquals(Instant.ofEpochMilli(1_700_000_000_000), n.updatedAt)
        assertEquals(n.updatedAt, n.createdAt)
        assertNull(read(file("e.md", "x")).notes.single().createdAt)
    }

    @Test fun `a file named with a title only keeps its extension out of the title`() =
        assertEquals("notes.v2\nx", read(file("notes.v2.txt", "x")).notes.single().text)

    @Test fun `no inputs is not found`() = assertEquals(ImportFailure.NOT_FOUND, read().failure)
}

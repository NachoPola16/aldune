package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

/** The way back of [MarkdownExportTest]: same cases as `MarkdownImport` in Aldune.Core, with `\n` line breaks as Android notes use. */
class MarkdownImportTest {
    @Test fun `tasks and bullets become the glyphs of Aldune`() {
        assertEquals("☐ milk\n☒ eggs\n→ plain", MarkdownImport.toNoteText("- [ ] milk\n- [x] eggs\n- plain"))
    }

    @Test fun `a first-line heading is the title without its marker`() {
        assertEquals("Title\n\nbody", MarkdownImport.toNoteText("# Title\r\n\r\nbody"))
    }

    @Test fun `a list item that already has a glyph becomes arrow plus box`() {
        assertEquals("→ ☐ x", MarkdownImport.toNoteText("- ☐ x"))
    }

    @Test fun `bold and rules are not list items, and fenced code is left alone`() {
        assertEquals("**bold**\n---", MarkdownImport.toNoteText("**bold**\n---"))
        assertEquals("```\n- [ ] not a task\n```", MarkdownImport.toNoteText("```\n- [ ] not a task\n```"))
    }

    @Test fun `indentation is kept`() {
        assertEquals("  ☐ nested", MarkdownImport.toNoteText("  - [ ] nested"))
    }

    @Test fun `decoding accepts utf8 with and without bom and utf16 with bom`() {
        assertEquals("héllo", TextDecoding.decode("héllo".toByteArray())!!.text)
        assertEquals("héllo", TextDecoding.decode(byteArrayOf(0xEF.toByte(), 0xBB.toByte(), 0xBF.toByte()) + "héllo".toByteArray())!!.text)
        assertEquals("hi", TextDecoding.decode(byteArrayOf(0xFF.toByte(), 0xFE.toByte(), 'h'.code.toByte(), 0, 'i'.code.toByte(), 0))!!.text)
    }

    @Test fun `decoding refuses ansi, binaries and files over the limit instead of guessing`() {
        assertNull(TextDecoding.decode(byteArrayOf(0x63, 0x61, 0x66, 0xE9.toByte())))
        assertNull(TextDecoding.decode(byteArrayOf(0x00, 0x01, 0x02)))
        assertEquals(TextDecoding.Rejection.TOO_LARGE, TextDecoding.rejection(ByteArray(TextDecoding.MAX_BYTES + 1)))
        assertEquals(TextDecoding.Rejection.NOT_TEXT, TextDecoding.rejection(byteArrayOf(0x63, 0xE9.toByte())))
    }
}

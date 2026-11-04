package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** The same cases as `MarkdownLiveTests` in the Windows test project: both scanners must give the same answer for the same text. */
class MarkdownLiveTest {
    // Renders the spans as «text[flags]» so a whole expectation reads as one string.
    private fun show(text: String): String = MarkdownLive.parse(text).joinToString("") { text.substring(it.start, it.end) + "[" + flags(it.style) + "]" }

    private fun flags(style: Int): String = buildString {
        if (style and MarkdownStyle.MARKER != 0) append('m')
        if (style and MarkdownStyle.BOLD != 0) append('b')
        if (style and MarkdownStyle.ITALIC != 0) append('i')
        if (style and MarkdownStyle.STRIKE != 0) append('s')
        if (style and MarkdownStyle.CODE != 0) append('c')
        if (style and MarkdownStyle.LINK != 0) append('l')
        if (style and MarkdownStyle.HEADING != 0) append('h')
    }

    @Test fun `plain text is one unstyled span`() = assertEquals("hello world[]", show("hello world"))

    @Test fun `empty has no spans`() = assertTrue(MarkdownLive.parse("").isEmpty())

    @Test fun `bold dims the markers`() = assertEquals("a []**[m]bold[b]**[m] z[]", show("a **bold** z"))

    @Test fun `italic with star or underscore`() {
        assertEquals("*[m]x[i]*[m]", show("*x*"))
        assertEquals("_[m]x[i]_[m]", show("_x_"))
    }

    @Test fun `three markers are bold italic`() = assertEquals("***[m]x[bi]***[m]", show("***x***"))

    @Test fun `italic inside bold`() = assertEquals("**[m]a [b]*[m]b[bi]*[m] c[b]**[m]", show("**a *b* c**"))

    @Test fun `strike with two tildes`() = assertEquals("~~[m]gone[s]~~[m]", show("~~gone~~"))

    @Test fun `inline code is literal`() = assertEquals("`[m]a*b*c[c]`[m]", show("`a*b*c`"))

    @Test fun `link shows the text and dims the url`() = assertEquals("[[m]Aldune[l]](https://x.y)[m]", show("[Aldune](https://x.y)"))

    @Test fun `heading dims the hashes and bolds the rest`() {
        assertEquals("# [m]Title[bh]", show("# Title"))
        assertEquals("### [m]Sub[bh]", show("### Sub"))
    }

    @Test fun `heading needs a space and at most six hashes`() {
        assertEquals("#tag[]", show("#tag"))
        assertEquals("####### x[]", show("####### x"))
    }

    @Test fun `heading still reads inline styles`() = assertEquals("# [m]a [bh]*[m]b[bih]*[m]", show("# a *b*"))

    @Test fun `unclosed stays plain`() {
        assertEquals("a **b[]", show("a **b"))
        assertEquals("2 * 3 * 4[]", show("2 * 3 * 4"))
    }

    @Test fun `underscore inside a word is not emphasis`() = assertEquals("snake_case_name[]", show("snake_case_name"))

    @Test fun `a delimiter next to a space does not open or close`() = assertEquals("a * b* c[]", show("a * b* c"))

    @Test fun `escape keeps the star literal`() = assertEquals("\\[m]*a*[]", show("\\*a*"))

    @Test fun `escape also works on backtick and tilde`() {
        assertEquals("\\[m]`a`[]", show("\\`a`"))
        assertEquals("\\[m]~~a~~[]", show("\\~~a~~"))
    }

    @Test fun `styles never cross a line`() = assertEquals("*a\nb*[]", show("*a\nb*"))

    @Test fun `fenced code is code across lines`() = assertEquals("```[mc]\n*x*\n[c]```[mc]", show("```\n*x*\n```"))

    @Test fun `task glyphs stay plain`() = assertEquals("☐ []**[m]x[b]**[m]", show("☐ **x**"))

    @Test fun `spans cover the whole text in order`() {
        val text = "# T\n**a** and *b* `c` [d](e)\n```\nf\n```\nplain"
        var at = 0
        for (span in MarkdownLive.parse(text)) {
            assertEquals(at, span.start)
            assertTrue(span.length > 0)
            at += span.length
        }
        assertEquals(text.length, at)
    }

    @Test fun `a huge line does not blow up`() = assertTrue(MarkdownLive.parse("*a ".repeat(20000)).isNotEmpty())

    @Test fun `linkUrlAt finds the url from the text the markers and the url`() {
        val text = "see [Aldune](https://x.y/a) now"
        val open = text.indexOf('[')
        for (index in listOf(open, open + 3, text.indexOf(']'), text.indexOf("https"), text.indexOf(')')))
            assertEquals("https://x.y/a", MarkdownLive.linkUrlAt(text, index))
        assertNull(MarkdownLive.linkUrlAt(text, 0))
        assertNull(MarkdownLive.linkUrlAt(text, text.indexOf(')') + 1))
    }

    @Test fun `linkUrlAt works on any line`() = assertEquals("http://a.b", MarkdownLive.linkUrlAt("first\n[x](http://a.b)\nlast", 8))

    @Test fun `linkUrlAt ignores code escapes and out of range`() {
        assertNull(MarkdownLive.linkUrlAt("`[a](https://x.y)`", 3))
        assertNull(MarkdownLive.linkUrlAt("```\n[a](https://x.y)\n```", 6))
        assertNull(MarkdownLive.linkUrlAt("\\[a](https://x.y)", 2))
        assertNull(MarkdownLive.linkUrlAt("[a](https://x.y)", -1))
        assertNull(MarkdownLive.linkUrlAt("[a](https://x.y)", 99))
        assertNull(MarkdownLive.linkUrlAt(null, 0))
    }

    @Test fun `many unclosed openers are not quadratic`() {
        val line = "*a `b ".repeat(665)
        val text = (1..40).joinToString(10.toChar().toString()) { line }
        val start = System.nanoTime()
        val spans = MarkdownLive.parse(text)
        val ms = (System.nanoTime() - start) / 1_000_000
        assertTrue(spans.isNotEmpty())
        assertTrue(ms < 1500, "took $ms ms")
    }

    @Test fun `the memo does not change the answer`() = assertEquals("*[m]a *b[i]*[m] *c[]", show("*a *b* *c"))

    @Test fun `linkUrlAt ignores notes over the size cap`() =
        assertNull(MarkdownLive.linkUrlAt("[a](https://x.y)" + "x".repeat(MarkdownLive.MAX_NOTE_LENGTH), 2))

    @Test fun `whitespace is the same set as on Windows`() {
        val nbsp = "**a" + 0xA0.toChar() + "**"
        val nel = "**a" + 0x85.toChar() + "**"
        val fs = "**a" + 0x1C.toChar() + "**"
        assertEquals("$nbsp[]", show(nbsp))
        assertEquals("$nel[]", show(nel))
        assertEquals("**[m]a" + 0x1C.toChar() + "[b]**[m]", show(fs))
    }
}

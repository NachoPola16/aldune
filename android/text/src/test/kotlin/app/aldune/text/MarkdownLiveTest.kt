package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
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
}

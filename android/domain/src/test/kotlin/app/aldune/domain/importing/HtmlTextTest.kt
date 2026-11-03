package app.aldune.domain.importing

import app.aldune.text.TaskLines
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

/** Same cases as `HtmlTextTests` in Aldune.Core (line breaks are `\n` here). */
class HtmlTextTest {
    @Test fun `divs become lines and an empty div with br is a blank line`() =
        assertEquals("one\n\ntwo", HtmlText.toNoteText("<div>one</div><div><br/></div><div>two</div>"))

    @Test fun `paragraphs are separated by a blank line`() = assertEquals("a\n\nb", HtmlText.toNoteText("<p>a</p><p>b</p>"))

    @Test fun `unordered list uses the arrow bullet`() = assertEquals("→ x\n→ y", HtmlText.toNoteText("<ul><li>x</li><li>y</li></ul>"))

    @Test fun `ordered list is numbered and nested lists are indented`() =
        assertEquals("1. a\n  → b\n2. c", HtmlText.toNoteText("<ol><li>a<ul><li>b</li></ul></li><li>c</li></ol>"))

    @Test fun `todo becomes task lines with the space behind the glyph`() {
        val text = HtmlText.toNoteText("<div><en-todo checked=\"true\"/>done</div><div><en-todo/> open</div>")
        assertEquals("☒ done\n☐ open", text)
        assertTrue(TaskLines.isTaskLine("☒ done"))
    }

    @Test fun `links keep their address when it differs from the text`() {
        assertEquals("see site (https://example.com/a)", HtmlText.toNoteText("see <a href=\"https://example.com/a\">site</a>"))
        assertEquals("https://example.com", HtmlText.toNoteText("<a href=\"https://example.com\">https://example.com</a>"))
    }

    @Test fun `a javascript link is not kept`() =
        assertEquals("click", HtmlText.toNoteText("<a href=\"javascript:alert(1)\">click</a>"))

    @Test fun `scripts and styles are dropped and entities decoded`() =
        assertEquals("a & b c", HtmlText.toNoteText("<style>x{}</style><script>alert(1)</script>a &amp; b&nbsp;c"))

    @Test fun `numeric and accented entities are decoded`() =
        assertEquals("ñ é € <", HtmlText.toNoteText("&ntilde; &#233; &#x20AC; &lt;"))

    @Test fun `keep bullet spans become tasks without the list arrow`() {
        val html = "<ul class=\"list\"><li class=\"listitem\"><span class=\"bullet\">&#9744;</span><span class=\"text\">milk</span></li>" +
            "<li class=\"listitem checked\"><span class=\"bullet\">&#9746;</span><span class=\"text\">eggs</span></li></ul>"
        assertEquals("☐ milk\n☒ eggs", HtmlText.toNoteText(html))
    }

    @Test fun `whitespace runs collapse and edges are trimmed`() = assertEquals("a b", HtmlText.toNoteText("  <div>\n  a \n   b  </div>\n"))
}

using Aldune.Core.Importing;

namespace Aldune.Core.Tests.Importing;

public sealed class HtmlTextTests
{
    [Fact]
    public void Divs_BecomeLines_AndAnEmptyDivWithBrIsABlankLine()
    {
        Assert.Equal("one\r\n\r\ntwo", HtmlText.ToNoteText("<div>one</div><div><br/></div><div>two</div>"));
    }

    [Fact]
    public void Paragraphs_AreSeparatedByABlankLine()
    {
        Assert.Equal("a\r\n\r\nb", HtmlText.ToNoteText("<p>a</p><p>b</p>"));
    }

    [Fact]
    public void UnorderedList_UsesTheArrowBullet()
    {
        Assert.Equal("→ x\r\n→ y", HtmlText.ToNoteText("<ul><li>x</li><li>y</li></ul>"));
    }

    [Fact]
    public void OrderedList_IsNumbered_AndNestedListsAreIndented()
    {
        var text = HtmlText.ToNoteText("<ol><li>a<ul><li>b</li></ul></li><li>c</li></ol>");
        Assert.Equal("1. a\r\n  → b\r\n2. c", text);
    }

    [Fact]
    public void Todo_BecomesTaskLines_WithTheSpaceBehindTheGlyph()
    {
        var text = HtmlText.ToNoteText("<div><en-todo checked=\"true\"/>done</div><div><en-todo/> open</div>");
        Assert.Equal("☒ done\r\n☐ open", text);
        Assert.True(TaskLines.IsTaskLine("☒ done"));
    }

    [Fact]
    public void Links_KeepTheirAddress_WhenItDiffersFromTheText()
    {
        Assert.Equal("see site (https://example.com/a)", HtmlText.ToNoteText("see <a href=\"https://example.com/a\">site</a>"));
        Assert.Equal("https://example.com", HtmlText.ToNoteText("<a href=\"https://example.com\">https://example.com</a>"));
    }

    [Fact]
    public void ScriptsAndStyles_AreDropped_AndEntitiesDecoded()
    {
        Assert.Equal("a & b c", HtmlText.ToNoteText("<style>x{}</style><script>alert(1)</script>a &amp; b&nbsp;c"));
    }

    [Fact]
    public void KeepBulletSpans_BecomeTasks_WithoutTheListArrow()
    {
        var html = "<ul class=\"list\"><li class=\"listitem\"><span class=\"bullet\">&#9744;</span><span class=\"text\">milk</span></li>"
                 + "<li class=\"listitem checked\"><span class=\"bullet\">&#9746;</span><span class=\"text\">eggs</span></li></ul>";
        Assert.Equal("☐ milk\r\n☒ eggs", HtmlText.ToNoteText(html));
    }

    [Fact]
    public void WhitespaceRuns_Collapse_AndEdgesAreTrimmed()
    {
        Assert.Equal("a b", HtmlText.ToNoteText("  <div>\n  a \n   b  </div>\n"));
    }
}

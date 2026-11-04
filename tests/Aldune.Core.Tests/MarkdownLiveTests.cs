namespace Aldune.Core.Tests;

/// <summary>The live Markdown scanner: which stretches of a note are bold, italic, code, link text or just syntax to dim.</summary>
public class MarkdownLiveTests
{
    // Renders the spans as «text[flags]» so a whole expectation reads as one string.
    private static string Show(string text)
    {
        var spans = MarkdownLive.Parse(text);
        return string.Concat(spans.Select(s => text.Substring(s.Start, s.Length) + "[" + Flags(s.Style) + "]"));
    }

    private static string Flags(MarkdownStyle style)
    {
        var parts = new List<string>();
        if (style.HasFlag(MarkdownStyle.Marker)) parts.Add("m");
        if (style.HasFlag(MarkdownStyle.Bold)) parts.Add("b");
        if (style.HasFlag(MarkdownStyle.Italic)) parts.Add("i");
        if (style.HasFlag(MarkdownStyle.Strike)) parts.Add("s");
        if (style.HasFlag(MarkdownStyle.Code)) parts.Add("c");
        if (style.HasFlag(MarkdownStyle.Link)) parts.Add("l");
        if (style.HasFlag(MarkdownStyle.Heading)) parts.Add("h");
        return string.Concat(parts);
    }

    [Fact]
    public void PlainText_IsOneUnstyledSpan() => Assert.Equal("hello world[]", Show("hello world"));

    [Fact]
    public void Empty_HasNoSpans() => Assert.Empty(MarkdownLive.Parse(""));

    [Fact]
    public void Bold_DimsTheMarkers() => Assert.Equal("a []**[m]bold[b]**[m] z[]", Show("a **bold** z"));

    [Fact]
    public void Italic_WithStarOrUnderscore()
    {
        Assert.Equal("*[m]x[i]*[m]", Show("*x*"));
        Assert.Equal("_[m]x[i]_[m]", Show("_x_"));
    }

    [Fact]
    public void BoldItalic_ThreeMarkers() => Assert.Equal("***[m]x[bi]***[m]", Show("***x***"));

    [Fact]
    public void Nested_ItalicInsideBold() => Assert.Equal("**[m]a [b]*[m]b[bi]*[m] c[b]**[m]", Show("**a *b* c**"));

    [Fact]
    public void Strike_TwoTildes() => Assert.Equal("~~[m]gone[s]~~[m]", Show("~~gone~~"));

    [Fact]
    public void InlineCode_IsLiteral() => Assert.Equal("`[m]a*b*c[c]`[m]", Show("`a*b*c`"));

    [Fact]
    public void Link_ShowsTextAndDimsTheUrl() => Assert.Equal("[[m]Aldune[l]](https://x.y)[m]", Show("[Aldune](https://x.y)"));

    [Fact]
    public void Heading_DimsHashesAndBoldsTheRest()
    {
        Assert.Equal("# [m]Title[bh]", Show("# Title"));
        Assert.Equal("### [m]Sub[bh]", Show("### Sub"));
    }

    [Fact]
    public void Heading_NeedsASpaceAndAtMostSixHashes()
    {
        Assert.Equal("#tag[]", Show("#tag"));
        Assert.Equal("####### x[]", Show("####### x"));
    }

    [Fact]
    public void Heading_StillReadsInlineStyles() => Assert.Equal("# [m]a [bh]*[m]b[bih]*[m]", Show("# a *b*"));

    [Fact]
    public void Unclosed_StaysPlain()
    {
        Assert.Equal("a **b[]", Show("a **b"));
        Assert.Equal("2 * 3 * 4[]", Show("2 * 3 * 4"));
    }

    [Fact]
    public void Underscore_InsideAWordIsNotEmphasis() => Assert.Equal("snake_case_name[]", Show("snake_case_name"));

    [Fact]
    public void Delimiter_NextToSpaceDoesNotOpenOrClose() => Assert.Equal("a * b* c[]", Show("a * b* c"));

    [Fact]
    public void Escape_KeepsTheStarLiteral() => Assert.Equal("\\[m]*a*[]", Show("\\*a*"));

    [Fact]
    public void Styles_NeverCrossALine() => Assert.Equal("*a\nb*[]", Show("*a\nb*"));

    [Fact]
    public void FencedCode_IsCodeAcrossLines()
    {
        Assert.Equal("```[mc]\n*x*\n[c]```[mc]", Show("```\n*x*\n```"));
    }

    [Fact]
    public void Task_GlyphsStayPlain() => Assert.Equal("☐ []**[m]x[b]**[m]", Show("☐ **x**"));

    [Fact]
    public void Spans_CoverTheWholeTextInOrder()
    {
        const string text = "# T\n**a** and *b* `c` [d](e)\n```\nf\n```\nplain";
        var spans = MarkdownLive.Parse(text);
        int at = 0;
        foreach (var span in spans)
        {
            Assert.Equal(at, span.Start);
            Assert.True(span.Length > 0);
            at += span.Length;
        }
        Assert.Equal(text.Length, at);
    }

    [Fact]
    public void HugeLine_DoesNotBlowUp()
    {
        var text = string.Concat(Enumerable.Repeat("*a ", 20000));
        Assert.NotEmpty(MarkdownLive.Parse(text));
    }
}

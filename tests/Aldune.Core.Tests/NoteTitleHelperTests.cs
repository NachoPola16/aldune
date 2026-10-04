using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteTitleHelperTests
{
    [Fact]
    public void GetTitle_SingleLineText_ReturnsThatLine()
    {
        Assert.Equal("Groceries", NoteTitleHelper.GetTitle("Groceries"));
    }

    [Fact]
    public void GetTitle_MultiLineText_ReturnsOnlyFirstLine()
    {
        Assert.Equal("Office", NoteTitleHelper.GetTitle("Office\n- understand all the apis\n- create tickets"));
    }

    [Fact]
    public void GetTitle_EmptyText_ReturnsPlaceholder()
    {
        Assert.Equal(NoteTitleHelper.PlaceholderTitle, NoteTitleHelper.GetTitle(""));
    }

    [Fact]
    public void GetTitle_WhitespaceOnlyFirstLine_ReturnsPlaceholder()
    {
        Assert.Equal(NoteTitleHelper.PlaceholderTitle, NoteTitleHelper.GetTitle("   \nsecond line has content"));
    }

    [Fact]
    public void GetTitle_FirstLineWithTrailingCarriageReturn_TrimsIt()
    {
        // Windows-style line endings (\r\n) — the \r should not leak into the title
        Assert.Equal("Office", NoteTitleHelper.GetTitle("Office\r\nbody"));
    }

    [Fact]
    public void GetTitle_FirstLineWithLeadingOrTrailingSpaces_TrimsThem()
    {
        Assert.Equal("Office", NoteTitleHelper.GetTitle("  Office  \nbody"));
    }

    [Theory]
    [InlineData("# Tema 3", "Tema 3")]
    [InlineData("### Sub  ", "Sub")]
    [InlineData("#hashtag", "#hashtag")]
    [InlineData("####### siete", "####### siete")]
    public void GetTitle_StripsMarkdownHeadingMarks(string firstLine, string expected) =>
        Assert.Equal(expected, NoteTitleHelper.GetTitle(firstLine + "\nresto"));

    [Fact]
    public void GetTitle_HeadingWithoutText_IsThePlaceholder() =>
        Assert.Equal(NoteTitleHelper.PlaceholderTitle, NoteTitleHelper.GetTitle("#   \nresto"));
}

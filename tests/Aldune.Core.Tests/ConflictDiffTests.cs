using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ConflictDiffTests
{
    [Fact]
    public void Summarize_IdenticalTexts_IsNull()
    {
        Assert.Null(ConflictDiff.Summarize("a\nb", "a\nb"));
    }

    [Fact]
    public void Summarize_ReportsFirstDifferingLine_OneBased()
    {
        var diff = ConflictDiff.Summarize("uno\ndos\ntres", "uno\nDOS\ntres");

        Assert.NotNull(diff);
        Assert.Equal(2, diff!.Line);
        Assert.Equal("dos", diff.WinnerLine);
        Assert.Equal("DOS", diff.LosingLine);
        Assert.Equal(1, diff.DifferingLines);
    }

    [Fact]
    public void Summarize_CountsEveryDifferingLine()
    {
        var diff = ConflictDiff.Summarize("a\nb\nc\nd", "a\nX\nc\nY");

        Assert.Equal(2, diff!.Line);
        Assert.Equal(2, diff.DifferingLines);
    }

    [Fact]
    public void Summarize_ExtraLinesCountAsDifferences_WithEmptyCounterpart()
    {
        var diff = ConflictDiff.Summarize("a", "a\nnueva");

        Assert.Equal(2, diff!.Line);
        Assert.Equal("", diff.WinnerLine);
        Assert.Equal("nueva", diff.LosingLine);
    }

    [Fact]
    public void Summarize_IgnoresLineEndingStyle()
    {
        Assert.Null(ConflictDiff.Summarize("a\r\nb", "a\nb"));
    }

    [Fact]
    public void Clip_ShortensLongLinesWithEllipsis()
    {
        Assert.Equal("abc", ConflictDiff.Clip("abc", 10));
        Assert.Equal("abcd…", ConflictDiff.Clip("abcdefghij", 4));
        Assert.Equal("a b", ConflictDiff.Clip("  a b  ", 10));
    }
}

using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ConflictMergeTests
{
    [Fact]
    public void Combine_IdenticalTexts_ChangesNothing()
    {
        Assert.Equal("a\nb", ConflictMerge.Combine("a\nb", "a\nb"));
    }

    [Fact]
    public void Combine_KeepsEveryActiveLine_AndInsertsTheOthersAfterTheirAnchor()
    {
        // "X" solo la tiene la otra versión: entra justo detrás de "a", la línea compartida que la precede.
        Assert.Equal("a\nX\nb\nc", ConflictMerge.Combine("a\nb\nc", "a\nX\nc"));
    }

    [Fact]
    public void Combine_LineAddedAtTheEnd_GoesAtTheEnd()
    {
        Assert.Equal("a\nnueva", ConflictMerge.Combine("a", "a\nnueva"));
    }

    [Fact]
    public void Combine_LineAddedBeforeAnyShared_GoesFirst()
    {
        Assert.Equal("n\na", ConflictMerge.Combine("a", "n\na"));
    }

    [Fact]
    public void Combine_ConsecutiveNewLines_KeepTheirOrder()
    {
        Assert.Equal("a\nx\ny\nb", ConflictMerge.Combine("a\nb", "a\nx\ny\nb"));
    }

    [Fact]
    public void Combine_DoesNotDuplicateLinesTheActiveVersionAlreadyHas()
    {
        Assert.Equal("a\nb\nc", ConflictMerge.Combine("a\nb\nc", "c\nb\na"));
    }

    [Fact]
    public void Combine_IgnoresBlankLinesAndLineEndingStyle()
    {
        Assert.Equal("a\n\nb", ConflictMerge.Combine("a\r\n\r\nb", "a\n\n\nb"));
    }

    [Fact]
    public void Combine_SameLineTwice_IsAddedOnlyOnce()
    {
        Assert.Equal("a\nx", ConflictMerge.Combine("a", "a\nx\nx"));
    }
}

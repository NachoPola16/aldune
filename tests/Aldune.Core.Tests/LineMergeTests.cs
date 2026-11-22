namespace Aldune.Core.Tests;

public sealed class LineMergeTests
{
    [Fact]
    public void DisjointInsertions()
    {
        var result = LineMerge.Merge("a\nb", "a\nb\nx", "y\na\nb");
        Assert.False(result.Conflict);
        Assert.Equal("y\na\nb\nx", result.Text);
    }

    [Fact]
    public void TickDifferentTasks()
    {
        var result = LineMerge.Merge("☐ pan\n☐ leche", "☒ pan\n☐ leche", "☐ pan\n☒ leche");
        Assert.False(result.Conflict);
        Assert.Equal("☒ pan\n☒ leche", result.Text);
    }

    [Fact]
    public void SameChangeOnBothSidesCountsOnce()
    {
        var result = LineMerge.Merge("a\nb", "a\nb\nc", "a\nb\nc");
        Assert.False(result.Conflict);
        Assert.Equal("a\nb\nc", result.Text);
    }

    [Fact]
    public void SameSpotInsertionsKeepBothInOrdinalOrderWhicheverSideIsLocal()
    {
        var ab = LineMerge.Merge("a", "a\nleche", "a\nhuevos");
        var ba = LineMerge.Merge("a", "a\nhuevos", "a\nleche");
        Assert.False(ab.Conflict);
        Assert.Equal("a\nhuevos\nleche", ab.Text);
        Assert.Equal(ab.Text, ba.Text);
    }

    [Fact]
    public void DeleteOneLineEditAnother()
    {
        var result = LineMerge.Merge("a\nb\nc", "a\nc", "a\nb\nC");
        Assert.False(result.Conflict);
        Assert.Equal("a\nC", result.Text);
    }

    [Fact]
    public void SameLineChangedDifferentlyIsAConflict()
    {
        Assert.True(LineMerge.Merge("a\nb", "a\nB1", "a\nB2").Conflict);
    }

    [Fact]
    public void DeleteVsEditOfTheSameLineIsAConflict()
    {
        Assert.True(LineMerge.Merge("a\nb\nc", "a\nc", "a\nB\nc").Conflict);
    }

    [Fact]
    public void CrlfAndLfAreTheSameLines()
    {
        var result = LineMerge.Merge("a\nb", "a\r\nb\r\nc", "a\nb");
        Assert.False(result.Conflict);
        Assert.Equal("a\nb\nc", result.Text);
    }

    [Theory]
    [InlineData("a\n\nb\n", "a\n\nb\nx\n")]
    [InlineData("☐ a\n\n\n☐ b", "☐ a\n\nnuevo\n\n☐ b")]
    public void UnchangedSideReturnsTheOther(string baseText, string changed)
    {
        Assert.Equal(changed, LineMerge.Merge(baseText, baseText, changed).Text);
        Assert.Equal(changed, LineMerge.Merge(baseText, changed, baseText).Text);
    }
}

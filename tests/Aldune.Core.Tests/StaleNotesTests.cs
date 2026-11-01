using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class StaleNotesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ZeroDaysMeansNoFilter()
    {
        Assert.False(StaleNotes.IsStale(Now.AddYears(-5), 0, Now));
        Assert.True(StaleNotes.IsStale(Now.AddYears(-5), 30, Now));
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(31, true)]
    [InlineData(0, false)]
    public void ANoteIsStaleOnceItHasNotBeenEditedForTheWholePeriod(int daysAgo, bool expected)
    {
        Assert.Equal(expected, StaleNotes.IsStale(Now.AddDays(-daysAgo), 30, Now));
    }

    [Fact]
    public void ANoteDatedInTheFutureIsNeverStale()
    {
        // A clock that was ahead on another device: it must not look abandoned.
        Assert.False(StaleNotes.IsStale(Now.AddDays(3), 7, Now));
    }

    [Fact]
    public void ThePresetsAreIncreasingAndStartWithAWeek()
    {
        Assert.Equal(7, StaleNotes.PresetDays[0]);
        Assert.Equal(StaleNotes.PresetDays.OrderBy(days => days), StaleNotes.PresetDays);
    }
}

namespace Aldune.Core.Tests;

/// <summary>A saved edit is pushed to the other devices a few seconds later, not at the next polling interval, and never more often than a pass every 30 s.</summary>
public sealed class LocalEditSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WaitsTheQuietPeriodWhenNothingRanRecently() =>
        Assert.Equal(LocalEditSync.QuietPeriod, LocalEditSync.DelayUntilRun(null, Now));

    [Fact]
    public void WaitsTheQuietPeriodWhenTheLastPassWasLongAgo() =>
        Assert.Equal(LocalEditSync.QuietPeriod, LocalEditSync.DelayUntilRun(Now.AddMinutes(-10), Now));

    [Fact]
    public void WaitsForTheMinimumGapAfterARecentPass() =>
        Assert.Equal(TimeSpan.FromSeconds(25), LocalEditSync.DelayUntilRun(Now.AddSeconds(-5), Now));
}

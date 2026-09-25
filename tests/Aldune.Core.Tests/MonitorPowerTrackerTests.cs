using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class MonitorPowerTrackerTests
{
    [Fact]
    public void AScreenIsOn_UntilItSaysOtherwise()
    {
        var tracker = new MonitorPowerTracker();

        Assert.False(tracker.IsOff("A"));
        Assert.Empty(tracker.OffIds);
    }

    [Fact]
    public void TwoOffReadingsInARow_MarkItOff()
    {
        var tracker = new MonitorPowerTracker();

        Assert.False(tracker.Report("A", MonitorPowerReading.Off));
        Assert.False(tracker.IsOff("A"));
        Assert.True(tracker.Report("A", MonitorPowerReading.Off));
        Assert.True(tracker.IsOff("A"));
        Assert.Equal(new[] { "A" }, tracker.OffIds);
    }

    [Fact]
    public void ItNeedsTwoOnReadingsToComeBack()
    {
        var tracker = new MonitorPowerTracker();
        tracker.Report("A", MonitorPowerReading.Off);
        tracker.Report("A", MonitorPowerReading.Off);

        Assert.False(tracker.Report("A", MonitorPowerReading.On));
        Assert.True(tracker.IsOff("A"));
        Assert.True(tracker.Report("A", MonitorPowerReading.On));
        Assert.False(tracker.IsOff("A"));
    }

    [Fact]
    public void NoReply_ChangesNothing_AndDoesNotBreakAStreak()
    {
        // Al encenderse, el monitor visto alternaba "encendido" con lecturas fallidas.
        var tracker = new MonitorPowerTracker();
        tracker.Report("A", MonitorPowerReading.Off);
        tracker.Report("A", MonitorPowerReading.Off);

        tracker.Report("A", MonitorPowerReading.On);
        tracker.Report("A", MonitorPowerReading.NoReply);
        Assert.True(tracker.IsOff("A"));
        Assert.True(tracker.Report("A", MonitorPowerReading.On));
        Assert.False(tracker.IsOff("A"));
    }

    [Fact]
    public void AnIsolatedOffReading_IsIgnored()
    {
        var tracker = new MonitorPowerTracker();

        tracker.Report("A", MonitorPowerReading.Off);
        tracker.Report("A", MonitorPowerReading.On);
        tracker.Report("A", MonitorPowerReading.Off);

        Assert.False(tracker.IsOff("A"));
    }

    [Theory]
    [InlineData(1u, MonitorPowerReading.On)]
    [InlineData(2u, MonitorPowerReading.Off)] // standby
    [InlineData(3u, MonitorPowerReading.Off)] // suspend
    [InlineData(4u, MonitorPowerReading.Off)] // off (DPM)
    [InlineData(5u, MonitorPowerReading.Off)] // off (botón)
    [InlineData(0u, MonitorPowerReading.NoReply)]
    [InlineData(9u, MonitorPowerReading.NoReply)]
    public void VcpPowerModeValues_AreInterpreted(uint value, MonitorPowerReading expected)
    {
        Assert.Equal(expected, MonitorPowerTracker.FromVcpPowerMode(value));
    }
}

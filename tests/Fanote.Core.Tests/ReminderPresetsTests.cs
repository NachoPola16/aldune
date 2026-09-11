using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class ReminderPresetsTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Fact]
    public void InOneHour_IsAlwaysNowPlusOneHour()
    {
        var now = new DateTimeOffset(2026, 9, 11, 15, 30, 0, Offset);
        Assert.Equal(now.AddHours(1), ReminderPresets.InOneHour(now));
    }

    [Fact]
    public void Tonight_BeforeEightPm_IsTodayAtEight()
    {
        var now = new DateTimeOffset(2026, 9, 11, 14, 0, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 11, 20, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.Tonight(now));
    }

    [Fact]
    public void Tonight_AtExactlyEightPm_IsTomorrowAtEight()
    {
        var now = new DateTimeOffset(2026, 9, 11, 20, 0, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 12, 20, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.Tonight(now));
    }

    [Fact]
    public void Tonight_AfterEightPm_IsTomorrowAtEight()
    {
        var now = new DateTimeOffset(2026, 9, 11, 23, 45, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 12, 20, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.Tonight(now));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 59)]
    [InlineData(23, 0)]
    public void TomorrowMorning_IsAlwaysTheNextDayAtNine_RegardlessOfCurrentTime(int hour, int minute)
    {
        var now = new DateTimeOffset(2026, 9, 11, hour, minute, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 12, 9, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.TomorrowMorning(now));
    }
}

namespace Aldune.Core.Tests;

/// <summary>
/// Where the ink of a task box sits on its line, so the drag grip can share its centre with any note font. The numbers are measurements of
/// the real window (in-process render at 4x, 14 px text, 2026-11-12): baseline of the note font from the top of the line, and the centre
/// of the box drawn by AlduneCheck / AlduneCheckLarge on that line.
/// </summary>
public sealed class TaskBoxMetricsTests
{
    [Theory]
    // font, baseline from the line top, measured centre of the normal box, of the large box
    [InlineData("Segoe UI", 15.11, 10.75, 10.57)]
    [InlineData("Georgia", 12.84, 8.50, 8.39)]
    [InlineData("Atkinson Hyperlegible", 13.30, 9.00, 8.71)]
    [InlineData("Consolas", 12.88, 8.75, 8.38)]
    [InlineData("Verdana", 14.08, 9.75, 9.45)]
    [InlineData("Gabriola", 14.47, 10.25, 9.95)]   // a 23.8 px line for 14 px text: a share of the line height put the grip 3 px low
    [InlineData("Impact", 14.12, 9.75, 9.51)]
    [InlineData("Cambria Math", 12.10, 7.75, 7.62)]
    public void TheCentreOfTheBoxFollowsTheBaselineOfTheNoteFont(string font, double baseline, double normalCentre, double largeCentre)
    {
        _ = font;
        // A quarter of a pixel is the grid of the measurement; text is also snapped to whole pixels, hence the tolerance.
        Assert.InRange(TaskBoxMetrics.CentreFromLineTop(baseline, 14, large: false) - normalCentre, -0.3, 0.3);
        Assert.InRange(TaskBoxMetrics.CentreFromLineTop(baseline, 14, large: true) - largeCentre, -0.3, 0.3);
    }

    [Fact]
    public void TheOffsetGrowsWithTheTextSize()
    {
        double small = 15.11 - TaskBoxMetrics.CentreFromLineTop(15.11, 14, large: false);
        double big = 30.22 - TaskBoxMetrics.CentreFromLineTop(30.22, 28, large: false);

        Assert.Equal(small * 2, big, 6);
    }

    [Theory]
    [InlineData(double.NaN, 14)]
    [InlineData(0, 14)]
    [InlineData(-3, 14)]
    [InlineData(12, 0)]
    [InlineData(12, double.PositiveInfinity)]
    public void MetricsThatCannotBeUsedGiveNoCentre_SoTheCallerKeepsItsOwnFallback(double baseline, double fontSize)
    {
        Assert.True(double.IsNaN(TaskBoxMetrics.CentreFromLineTop(baseline, fontSize, large: false)));
    }
}

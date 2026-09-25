using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ToastStackTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);

    [Fact]
    public void OneToast_SitsInTheBottomRightCorner_WithTheMargin()
    {
        var places = ToastStack.Place(Area, width: 360, heights: [120]);

        Assert.Equal((1920 - 16 - 360.0, 1040 - 16 - 120.0), places[0]);
    }

    [Fact]
    public void TheNewest_GoesAtTheBottom_AndOlderOnesMoveUp()
    {
        // Orden de llegada: el primero es el más antiguo.
        var places = ToastStack.Place(Area, width: 360, heights: [100, 140]);

        Assert.Equal(1040 - 16 - 140.0, places[1]!.Value.Top);
        Assert.Equal(1040 - 16 - 140 - ToastStack.Gap - 100, places[0]!.Value.Top);
    }

    [Fact]
    public void WhenTheyDoNotFit_TheOldestWaitOutOfSight()
    {
        var small = new WorkingArea(0, 0, 800, 300);

        var places = ToastStack.Place(small, width: 360, heights: [120, 120, 120]);

        Assert.Null(places[0]);
        Assert.NotNull(places[1]);
        Assert.NotNull(places[2]);
    }

    [Fact]
    public void RespectsTheOriginOfASecondaryMonitor()
    {
        var right = new WorkingArea(1920, -200, 1280, 1000);

        var places = ToastStack.Place(right, width: 360, heights: [100]);

        Assert.Equal((1920 + 1280 - 16 - 360.0, -200 + 1000 - 16 - 100.0), places[0]);
    }
}

using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class MonitorBoundsTests
{
    private static readonly MonitorInfo Primary = new(
        "\\\\.\\DISPLAY1", new WorkingArea(0, 0, 1920, 1080), DpiScale: 1.0, IsPrimary: true);

    private static readonly MonitorInfo Secondary = new(
        "\\\\.\\DISPLAY2", new WorkingArea(1920, 0, 1080, 1920), DpiScale: 1.0, IsPrimary: false);

    [Fact]
    public void AllowsWindowToBridgeAdjacentMonitors()
    {
        var position = MonitorBounds.ClampIntoMonitorUnion(
            1900, 100, 300, 320, new[] { Primary, Secondary });

        Assert.Equal(1900, position.Left);
        Assert.Equal(100, position.Top);
    }

    [Fact]
    public void PreventsWindowLeavingTheTopOuterEdge()
    {
        var position = MonitorBounds.ClampIntoMonitorUnion(
            1900, -500, 300, 320, new[] { Primary, Secondary });

        Assert.Equal(1900, position.Left);
        Assert.Equal(0, position.Top);
    }

    [Fact]
    public void PreventsWindowLeavingTheBottomOuterEdge()
    {
        var position = MonitorBounds.ClampIntoMonitorUnion(
            500, 1800, 300, 320, new[] { Primary, Secondary });

        Assert.Equal(500, position.Left);
        Assert.Equal(760, position.Top);
    }

    [Fact]
    public void PreventsWindowLeavingTheLeftAndRightOuterEdges()
    {
        var left = MonitorBounds.ClampIntoMonitorUnion(
            -400, 100, 300, 320, new[] { Primary, Secondary });
        var right = MonitorBounds.ClampIntoMonitorUnion(
            2900, 100, 300, 320, new[] { Primary, Secondary });

        Assert.Equal(0, left.Left);
        Assert.Equal(2700, right.Left);
    }

    [Fact]
    public void UsesTheCorrectBottomEdgeWhenAWindowBridgesMonitors()
    {
        var position = MonitorBounds.ClampIntoMonitorUnion(
            1900, 1500, 300, 320, new[] { Primary, Secondary });

        Assert.Equal(1900, position.Left);
        Assert.Equal(760, position.Top);
    }

    [Fact]
    public void AllowsTheLowerPartOfTheTallerMonitor()
    {
        var position = MonitorBounds.ClampIntoMonitorUnion(
            2000, 1800, 300, 320, new[] { Primary, Secondary });

        Assert.Equal(2000, position.Left);
        Assert.Equal(1600, position.Top);
    }
}

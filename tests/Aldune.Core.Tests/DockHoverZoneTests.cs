using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DockHoverZoneTests
{
    private static readonly WorkingArea Area = new(0, 0, 2560, 1440);

    [Fact]
    public void Contains_ForgivesTheSlopAroundEverySurface()
    {
        var surfaces = new[] { new Rect(2352, 300, 208, 600), new Rect(2360, 910, 190, 54) };
        double slop = DockHoverZone.ExpandedSlop;

        Assert.True(DockHoverZone.Contains(surfaces, 2352 - slop + 1, 500, slop));
        Assert.False(DockHoverZone.Contains(surfaces, 2352 - slop - 1, 500, slop));
        Assert.True(DockHoverZone.Contains(surfaces, 2400, 300 - slop + 1, slop));
        Assert.True(DockHoverZone.Contains(surfaces, 2400, 964 + slop - 1, slop));
        Assert.False(DockHoverZone.Contains(surfaces, 2400, 964 + slop + 1, slop));
    }

    [Fact]
    public void Contains_IgnoresEmptySurfaces()
    {
        Assert.False(DockHoverZone.Contains(new[] { new Rect(0, 0, 0, 0) }, 5, 5, 24));
    }

    [Theory]
    [InlineData(EdgePosition.Right, 2559.5, 700, true)]
    [InlineData(EdgePosition.Right, 2557, 700, false)]
    [InlineData(EdgePosition.Left, 0, 700, true)]
    [InlineData(EdgePosition.Left, 2, 700, false)]
    [InlineData(EdgePosition.Top, 1280, 0.4, true)]
    [InlineData(EdgePosition.Bottom, 1280, 1439, true)]
    [InlineData(EdgePosition.Bottom, 1280, 1437, false)]
    public void IsAgainstEdge_OnlyOnTheLastPixel(EdgePosition edge, double x, double y, bool expected)
    {
        Assert.Equal(expected, DockHoverZone.IsAgainstEdge(Area, edge, x, y));
    }

    [Theory]
    [InlineData(EdgePosition.Right, 2561, 720)]
    [InlineData(EdgePosition.Left, -1, 720)]
    [InlineData(EdgePosition.Top, 1280, -1)]
    [InlineData(EdgePosition.Bottom, 1280, 1441)]
    public void PointBeyondEdge_IsJustOutsideTheMiddleOfThatEdge(EdgePosition edge, double x, double y)
    {
        var (px, py) = DockHoverZone.PointBeyondEdge(Area, edge);
        Assert.Equal(x, px);
        Assert.Equal(y, py);
    }

    [Theory]
    [InlineData(EdgePosition.Right, 0, 0, 2557, 1440)]
    [InlineData(EdgePosition.Left, 3, 0, 2557, 1440)]
    [InlineData(EdgePosition.Top, 0, 3, 2560, 1437)]
    [InlineData(EdgePosition.Bottom, 0, 0, 2560, 1437)]
    public void Inset_LeavesTheEdgeFreeForTheTaskbar(EdgePosition edge, double x, double y, double w, double h)
    {
        Assert.Equal(new WorkingArea(x, y, w, h), DockHoverZone.Inset(Area, edge, 3));
    }

    [Fact]
    public void InsetDock_KeepsTheLastRowsOutOfItsWindowAndRestingZone()
    {
        // Con la barra de tareas oculta en el mismo borde, el canto es suyo: ni la ventana del dock
        // ni su zona de reposo pueden cubrir las filas que la hacen aparecer.
        var inset = DockHoverZone.Inset(Area, EdgePosition.Bottom, DockHoverZone.AutoHideTaskbarClearance);
        var window = EdgeGeometry.WindowRect(inset, EdgePosition.Bottom, 8);
        var rest = EdgeGeometry.RestingVisibleRect(inset, EdgePosition.Bottom, 8);
        Assert.True(window.Y + window.Height <= 1440 - DockHoverZone.AutoHideTaskbarClearance);
        Assert.True(rest.Y + rest.Height <= 1440 - DockHoverZone.AutoHideTaskbarClearance);
    }
}

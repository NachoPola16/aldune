using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class EdgeGeometryTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);

    [Fact]
    public void PillRect_Right_IsFlushAgainstRightEdge()
    {
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Right);
        Assert.Equal(Area.X + Area.Width - EdgeGeometry.PillThickness, rect.X);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Width);
    }

    [Fact]
    public void PillRect_Top_IsFlushAgainstTopEdge()
    {
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Top);
        Assert.Equal(Area.Y, rect.Y);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Height);
    }

    [Fact]
    public void ExpandedRect_SameEdge_IsLargerAndFlushLikePill()
    {
        var pill = EdgeGeometry.PillRect(Area, EdgePosition.Left);
        var expanded = EdgeGeometry.ExpandedRect(Area, EdgePosition.Left);
        Assert.True(expanded.Width > pill.Width);
        Assert.Equal(pill.X, expanded.X);
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void PillRect_TopOrBottom_IsHorizontallyCentered(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(Area, edge);
        double expectedCenter = Area.X + Area.Width / 2;
        double actualCenter = rect.X + rect.Width / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Theory]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void PillRect_LeftOrRight_IsVerticallyCentered(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(Area, edge);
        double expectedCenter = Area.Y + Area.Height / 2;
        double actualCenter = rect.Y + rect.Height / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }
}

using System.Collections.Generic;
using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class EdgeGeometryTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);
    private static readonly WorkingArea SecondaryArea = new(-1920, 100, 1600, 900);

    public static IEnumerable<object[]> Areas()
    {
        yield return new object[] { Area };
        yield return new object[] { SecondaryArea };
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Right_IsInsetFromRightEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Right);
        Assert.Equal(area.X + area.Width - EdgeGeometry.PillThickness - EdgeGeometry.PillEdgeMargin, rect.X);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Width);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Left_IsInsetFromLeftEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Left);
        Assert.Equal(area.X + EdgeGeometry.PillEdgeMargin, rect.X);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Width);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Top_IsInsetFromTopEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Top);
        Assert.Equal(area.Y + EdgeGeometry.PillEdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Bottom_IsInsetFromBottomEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Bottom);
        Assert.Equal(area.Y + area.Height - EdgeGeometry.PillThickness - EdgeGeometry.PillEdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_SameEdge_IsLargerAndFlushAgainstTrueEdge(WorkingArea area)
    {
        var pill = EdgeGeometry.PillRect(area, EdgePosition.Left);
        var expanded = EdgeGeometry.ExpandedRect(area, EdgePosition.Left);
        Assert.True(expanded.Width > pill.Width);
        // Unlike the pill (inset by PillEdgeMargin for its rounded corners/shadow), the
        // expanded panel stays flush against the true screen edge.
        Assert.Equal(area.X, expanded.X);
        Assert.True(expanded.X < pill.X);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Top_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Top);
        Assert.Equal(area.X + (area.Width - EdgeGeometry.ExpandedLength) / 2, rect.X);
        Assert.Equal(area.Y, rect.Y);
        Assert.Equal(EdgeGeometry.ExpandedLength, rect.Width);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Bottom_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Bottom);
        Assert.Equal(area.X + (area.Width - EdgeGeometry.ExpandedLength) / 2, rect.X);
        Assert.Equal(area.Y + area.Height - EdgeGeometry.ExpandedThickness, rect.Y);
        Assert.Equal(EdgeGeometry.ExpandedLength, rect.Width);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Left_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Left);
        Assert.Equal(area.X, rect.X);
        Assert.Equal(area.Y + (area.Height - EdgeGeometry.ExpandedLength) / 2, rect.Y);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Width);
        Assert.Equal(EdgeGeometry.ExpandedLength, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Right_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Right);
        Assert.Equal(area.X + area.Width - EdgeGeometry.ExpandedThickness, rect.X);
        Assert.Equal(area.Y + (area.Height - EdgeGeometry.ExpandedLength) / 2, rect.Y);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Width);
        Assert.Equal(EdgeGeometry.ExpandedLength, rect.Height);
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

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void PillRect_TopOrBottom_IsHorizontallyCentered_SecondaryArea(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(SecondaryArea, edge);
        double expectedCenter = SecondaryArea.X + SecondaryArea.Width / 2;
        double actualCenter = rect.X + rect.Width / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Theory]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void PillRect_LeftOrRight_IsVerticallyCentered_SecondaryArea(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(SecondaryArea, edge);
        double expectedCenter = SecondaryArea.Y + SecondaryArea.Height / 2;
        double actualCenter = rect.Y + rect.Height / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }
}

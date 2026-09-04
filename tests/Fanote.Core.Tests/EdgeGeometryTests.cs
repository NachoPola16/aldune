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
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Right, noteCount: 3);
        Assert.Equal(area.X + area.Width - EdgeGeometry.PillThickness - EdgeGeometry.PillEdgeMargin, rect.X);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Width);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Left_IsInsetFromLeftEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Left, noteCount: 3);
        Assert.Equal(area.X + EdgeGeometry.PillEdgeMargin, rect.X);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Width);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Top_IsInsetFromTopEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Top, noteCount: 3);
        Assert.Equal(area.Y + EdgeGeometry.PillEdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void PillRect_Bottom_IsInsetFromBottomEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.PillRect(area, EdgePosition.Bottom, noteCount: 3);
        Assert.Equal(area.Y + area.Height - EdgeGeometry.PillThickness - EdgeGeometry.PillEdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.PillThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_SameEdge_IsLargerAndFlushAgainstTrueEdge(WorkingArea area)
    {
        var pill = EdgeGeometry.PillRect(area, EdgePosition.Left, noteCount: 3);
        var expanded = EdgeGeometry.ExpandedRect(area, EdgePosition.Left, noteCount: 3);
        Assert.True(expanded.Width > pill.Width);
        // Unlike the pill (inset by PillEdgeMargin for its rounded corners/shadow), the
        // expanded panel stays flush against the true screen edge.
        Assert.Equal(area.X, expanded.X);
        Assert.True(expanded.X < pill.X);
    }

    [Theory]
    [InlineData(EdgePosition.Right)]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void ExpandedRect_SharesTheStartOfThePillsLengthAxis_SoItGrowsInOneDirectionOnly(EdgePosition edge)
    {
        // Centering the expanded panel independently (by its own, much longer length) made its
        // start slide in the opposite direction from its end as it opened — the two edges moved
        // apart from a new shared center instead of the panel simply extending past the pill.
        // Real user feedback (2026-09-04): that dual-direction motion read as "doesn't make
        // sense". Anchoring both rects to the pill's own centering fixes one end in place, so
        // opening only ever extends the other end.
        var pill = EdgeGeometry.PillRect(Area, edge, noteCount: 5);
        var expanded = EdgeGeometry.ExpandedRect(Area, edge, noteCount: 5);

        if (edge is EdgePosition.Top or EdgePosition.Bottom)
        {
            Assert.Equal(pill.X, expanded.X, precision: 3);
        }
        else
        {
            Assert.Equal(pill.Y, expanded.Y, precision: 3);
        }
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Top_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var pill = EdgeGeometry.PillRect(area, EdgePosition.Top, noteCount: 20);
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Top, noteCount: 20);
        double expectedLength = EdgeGeometry.ExpandedMaxLength + EdgeGeometry.ExpandedFooterLength;
        // Same start as the pill (see ExpandedRect_SharesTheStartOfThePillsLengthAxis...), not
        // centered on its own (much longer) length.
        Assert.Equal(pill.X, rect.X);
        Assert.Equal(area.Y, rect.Y);
        Assert.Equal(expectedLength, rect.Width);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Bottom_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var pill = EdgeGeometry.PillRect(area, EdgePosition.Bottom, noteCount: 20);
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Bottom, noteCount: 20);
        double expectedLength = EdgeGeometry.ExpandedMaxLength + EdgeGeometry.ExpandedFooterLength;
        Assert.Equal(pill.X, rect.X);
        Assert.Equal(area.Y + area.Height - EdgeGeometry.ExpandedThickness, rect.Y);
        Assert.Equal(expectedLength, rect.Width);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Left_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var pill = EdgeGeometry.PillRect(area, EdgePosition.Left, noteCount: 20);
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Left, noteCount: 20);
        double expectedLength = EdgeGeometry.ExpandedMaxLength + EdgeGeometry.ExpandedFooterLength;
        Assert.Equal(area.X, rect.X);
        Assert.Equal(pill.Y, rect.Y);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Width);
        Assert.Equal(expectedLength, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void ExpandedRect_Right_HasExpectedAbsoluteGeometry(WorkingArea area)
    {
        var pill = EdgeGeometry.PillRect(area, EdgePosition.Right, noteCount: 20);
        var rect = EdgeGeometry.ExpandedRect(area, EdgePosition.Right, noteCount: 20);
        double expectedLength = EdgeGeometry.ExpandedMaxLength + EdgeGeometry.ExpandedFooterLength;
        Assert.Equal(area.X + area.Width - EdgeGeometry.ExpandedThickness, rect.X);
        Assert.Equal(pill.Y, rect.Y);
        Assert.Equal(EdgeGeometry.ExpandedThickness, rect.Width);
        Assert.Equal(expectedLength, rect.Height);
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void PillRect_TopOrBottom_IsHorizontallyCentered(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(Area, edge, noteCount: 3);
        double expectedCenter = Area.X + Area.Width / 2;
        double actualCenter = rect.X + rect.Width / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Theory]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void PillRect_LeftOrRight_IsVerticallyCentered(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(Area, edge, noteCount: 3);
        double expectedCenter = Area.Y + Area.Height / 2;
        double actualCenter = rect.Y + rect.Height / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void PillRect_TopOrBottom_IsHorizontallyCentered_SecondaryArea(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(SecondaryArea, edge, noteCount: 3);
        double expectedCenter = SecondaryArea.X + SecondaryArea.Width / 2;
        double actualCenter = rect.X + rect.Width / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Theory]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void PillRect_LeftOrRight_IsVerticallyCentered_SecondaryArea(EdgePosition edge)
    {
        var rect = EdgeGeometry.PillRect(SecondaryArea, edge, noteCount: 3);
        double expectedCenter = SecondaryArea.Y + SecondaryArea.Height / 2;
        double actualCenter = rect.Y + rect.Height / 2;
        Assert.Equal(expectedCenter, actualCenter, precision: 3);
    }

    [Fact]
    public void PillRect_WithZeroNotes_UsesMinLength()
    {
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Right, noteCount: 0);
        Assert.Equal(EdgeGeometry.PillMinLength, rect.Height);
    }

    [Fact]
    public void PillRect_WithFewNotes_GrowsProportionally()
    {
        int noteCount = 4;
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Right, noteCount);
        double expectedLength = noteCount * EdgeGeometry.PillPerNoteLength;
        Assert.True(expectedLength > EdgeGeometry.PillMinLength && expectedLength < EdgeGeometry.PillMaxLength,
            "This test assumes 4 notes falls strictly between min and max — adjust the constants or this count if that changes.");
        Assert.Equal(expectedLength, rect.Height, precision: 3);
    }

    [Fact]
    public void PillRect_WithManyNotes_CapsAtMaxLength()
    {
        var rect = EdgeGeometry.PillRect(Area, EdgePosition.Right, noteCount: 1000);
        Assert.Equal(EdgeGeometry.PillMaxLength, rect.Height);
    }

    [Fact]
    public void ExpandedRect_WithZeroNotes_UsesMinLength()
    {
        var rect = EdgeGeometry.ExpandedRect(Area, EdgePosition.Right, noteCount: 0);
        Assert.Equal(EdgeGeometry.ExpandedMinLength + EdgeGeometry.ExpandedFooterLength, rect.Height);
    }

    [Fact]
    public void ExpandedRect_WithFewNotes_GrowsProportionally()
    {
        // 3, not 4 — with ExpandedPerNoteLength raised to 88 (to give the rotated vertical tab
        // label room), 4 notes (352) would already exceed ExpandedMaxLength (320); 3 (264) is
        // the largest count that still falls strictly between the min and max bounds.
        int noteCount = 3;
        var rect = EdgeGeometry.ExpandedRect(Area, EdgePosition.Right, noteCount);
        double tabsLength = noteCount * EdgeGeometry.ExpandedPerNoteLength;
        Assert.True(tabsLength > EdgeGeometry.ExpandedMinLength && tabsLength < EdgeGeometry.ExpandedMaxLength,
            "This test assumes 3 notes falls strictly between min and max — adjust the constants or this count if that changes.");
        Assert.Equal(tabsLength + EdgeGeometry.ExpandedFooterLength, rect.Height, precision: 3);
    }

    [Fact]
    public void ExpandedRect_WithManyNotes_CapsAtMaxLength()
    {
        var rect = EdgeGeometry.ExpandedRect(Area, EdgePosition.Right, noteCount: 1000);
        Assert.Equal(EdgeGeometry.ExpandedMaxLength + EdgeGeometry.ExpandedFooterLength, rect.Height);
    }
}

using System.Collections.Generic;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteCascadeTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);
    private static readonly WorkingArea Secondary = new(-1920, 100, 1600, 900);
    private const double W = 320;
    private const double H = 360;

    public static IEnumerable<object[]> AreasAndEdges()
    {
        foreach (var area in new[] { Area, Secondary })
            foreach (var edge in new[] { EdgePosition.Top, EdgePosition.Bottom, EdgePosition.Left, EdgePosition.Right })
                yield return new object[] { area, edge };
    }

    [Theory]
    [MemberData(nameof(AreasAndEdges))]
    public void FirstNote_NeverOverlapsTheDeployedDock(WorkingArea area, EdgePosition edge)
    {
        var dock = EdgeGeometry.WindowRect(area, edge, noteCount: 8);
        var (left, top) = NoteCascade.Position(area, edge, 8, W, H, level: 0);

        bool overlaps = left < dock.X + dock.Width && left + W > dock.X
            && top < dock.Y + dock.Height && top + H > dock.Y;
        Assert.False(overlaps);
    }

    [Theory]
    [MemberData(nameof(AreasAndEdges))]
    public void LaterNotes_MoveAwayFromTheDock_NeverTowardIt(WorkingArea area, EdgePosition edge)
    {
        var dock = EdgeGeometry.WindowRect(area, edge, noteCount: 8);
        for (int level = 1; level <= NoteCascade.MaxLevels; level++)
        {
            var (left, top) = NoteCascade.Position(area, edge, 8, W, H, level);
            bool overlaps = left < dock.X + dock.Width && left + W > dock.X
                && top < dock.Y + dock.Height && top + H > dock.Y;
            Assert.False(overlaps);
        }
    }

    [Fact]
    public void Right_FirstNoteSitsLeftOfTheDockRect()
    {
        var dock = EdgeGeometry.WindowRect(Area, EdgePosition.Right, 5);
        var (left, _) = NoteCascade.Position(Area, EdgePosition.Right, 5, W, H, 0);
        Assert.Equal(dock.X - NoteCascade.DockGap - W, left);
    }

    [Fact]
    public void Left_FirstNoteSitsRightOfTheDockRect()
    {
        var dock = EdgeGeometry.WindowRect(Area, EdgePosition.Left, 5);
        var (left, _) = NoteCascade.Position(Area, EdgePosition.Left, 5, W, H, 0);
        Assert.Equal(dock.X + dock.Width + NoteCascade.DockGap, left);
    }

    [Fact]
    public void Level_IsCappedAtMaxLevels()
    {
        var capped = NoteCascade.Position(Area, EdgePosition.Left, 5, W, H, NoteCascade.MaxLevels);
        var beyond = NoteCascade.Position(Area, EdgePosition.Left, 5, W, H, NoteCascade.MaxLevels + 9);
        Assert.Equal(capped, beyond);
    }
}

using System.Text.Json;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DockAlignmentTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);
    private static readonly WorkingArea Offset = new(-1920, 100, 1600, 900);

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void Center_IsTheDefaultAndKeepsTheOldPlacement(EdgePosition edge)
    {
        Assert.Equal(EdgeGeometry.WindowRect(Area, edge, 4),
            EdgeGeometry.WindowRect(Area, edge, 4, DockAlignment.Center));
    }

    [Theory]
    [InlineData(EdgePosition.Left)]
    [InlineData(EdgePosition.Right)]
    public void SideEdges_Start_StickToTheTop_End_ToTheBottom(EdgePosition edge)
    {
        foreach (var area in new[] { Area, Offset })
        {
            var start = EdgeGeometry.WindowRect(area, edge, 4, DockAlignment.Start);
            var end = EdgeGeometry.WindowRect(area, edge, 4, DockAlignment.End);
            var center = EdgeGeometry.WindowRect(area, edge, 4);

            Assert.Equal(area.Y + EdgeGeometry.EdgeMargin, start.Y);
            Assert.Equal(area.Y + area.Height - start.Height - EdgeGeometry.EdgeMargin, end.Y);
            Assert.True(start.Y <= center.Y && center.Y <= end.Y);
            // Lo demás no cambia: mismo borde, mismo tamaño.
            Assert.Equal(center.X, start.X);
            Assert.Equal(center.Width, end.Width);
            Assert.Equal(center.Height, start.Height);
        }
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void HorizontalEdges_Start_StickToTheLeft_End_ToTheRight(EdgePosition edge)
    {
        foreach (var area in new[] { Area, Offset })
        {
            var start = EdgeGeometry.WindowRect(area, edge, 4, DockAlignment.Start);
            var end = EdgeGeometry.WindowRect(area, edge, 4, DockAlignment.End);
            var center = EdgeGeometry.WindowRect(area, edge, 4);

            Assert.Equal(area.X + EdgeGeometry.EdgeMargin, start.X);
            Assert.Equal(area.X + area.Width - start.Width - EdgeGeometry.EdgeMargin, end.X);
            Assert.True(start.X <= center.X && center.X <= end.X);
            Assert.Equal(center.Y, start.Y);
            Assert.Equal(center.Width, end.Width);
        }
    }

    [Theory]
    [InlineData(DockAlignment.Start)]
    [InlineData(DockAlignment.End)]
    public void EveryAlignment_StaysInsideTheWorkingArea(DockAlignment alignment)
    {
        foreach (var edge in new[] { EdgePosition.Top, EdgePosition.Bottom, EdgePosition.Left, EdgePosition.Right })
        {
            foreach (int notes in new[] { 1, 4, 40 })
            {
                var rect = EdgeGeometry.WindowRect(Area, edge, notes, alignment);
                Assert.True(rect.X >= Area.X - 0.01 && rect.Y >= Area.Y - 0.01, $"{edge} {notes}");
                Assert.True(rect.X + rect.Width <= Area.X + Area.Width + 0.01, $"{edge} {notes}");
                Assert.True(rect.Y + rect.Height <= Area.Y + Area.Height + 0.01, $"{edge} {notes}");
            }
        }
    }

    [Fact]
    public void RestingHoverZone_FollowsTheAlignedWindow()
    {
        var center = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, 4);
        var start = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, 4, DockAlignment.Start);
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Right, 4, DockAlignment.Start);

        Assert.True(start.Y < center.Y);
        Assert.True(start.Y >= window.Y - 0.01 && start.Y + start.Height <= window.Y + window.Height + 0.01);
    }

    [Fact]
    public void Cascade_OnHorizontalEdges_FollowsTheAlignedDock()
    {
        var (centerLeft, _) = NoteCascade.Position(Area, EdgePosition.Top, 4, 300, 320, 0);
        var (startLeft, _) = NoteCascade.Position(Area, EdgePosition.Top, 4, 300, 320, 0, DockAlignment.Start);

        Assert.True(startLeft < centerLeft);
    }

    [Fact]
    public void AnOldSettingsFile_WithoutTheField_LoadsAsCenter_AndTheEnumIsWrittenAsANumber()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"DockEdge\":3}")!;
        Assert.Equal(DockAlignment.Center, old.DockAlignment);

        var json = JsonSerializer.Serialize(new AppSettings { DockAlignment = DockAlignment.End });
        Assert.Contains("\"DockAlignment\":2", json);
    }
}

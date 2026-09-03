using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class TabRegionShapeTests
{
    private static readonly Rect FooterRect = new(0, 300, 116, 80);

    [Fact]
    public void BuildRegion_NoTabs_ReturnsOnlyTheFooterPiece()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), FooterRect, cornerRadius: 9);

        var piece = Assert.Single(pieces);
        Assert.Equal(FooterRect, piece.Bounds);
        Assert.Equal(0, piece.CornerRadius);
    }

    [Fact]
    public void BuildRegion_OneTab_ReturnsTabPieceThenFooterPiece()
    {
        var tab = new Rect(0, 0, 32, 80);

        var pieces = TabRegionShape.BuildRegion(new[] { tab }, FooterRect, cornerRadius: 9);

        Assert.Equal(2, pieces.Count);
        Assert.Equal(tab, pieces[0].Bounds);
        Assert.Equal(9, pieces[0].CornerRadius);
        Assert.Equal(FooterRect, pieces[1].Bounds);
        Assert.Equal(0, pieces[1].CornerRadius);
    }

    [Fact]
    public void BuildRegion_MultipleTabs_EachTabGetsTheGivenCornerRadius()
    {
        var tabs = new[]
        {
            new Rect(0, 0, 32, 80),
            new Rect(0, 56, 46, 80),
            new Rect(0, 112, 60, 80),
        };

        var pieces = TabRegionShape.BuildRegion(tabs, FooterRect, cornerRadius: 12);

        Assert.Equal(4, pieces.Count); // 3 pestañas + footer
        for (int i = 0; i < tabs.Length; i++)
        {
            Assert.Equal(tabs[i], pieces[i].Bounds);
            Assert.Equal(12, pieces[i].CornerRadius);
        }
        Assert.Equal(0, pieces[^1].CornerRadius);
    }

    [Fact]
    public void BuildRegion_FooterPiece_AlwaysHasZeroCornerRadius()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), FooterRect, cornerRadius: 999);

        Assert.Equal(0, Assert.Single(pieces).CornerRadius);
    }
}

using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class OklchColorTests
{
    [Theory]
    [InlineData("#EBD38B", 0.871, 0.095, 92)]  // citron, documentado en NoteColorPalette
    [InlineData("#262F47", 0.308, 0.045, 268)] // tinta del tema Sereno
    public void TryFromHex_MatchesTheValuesThePaletteWasDesignedWith(string hex, double l, double c, double h)
    {
        Assert.True(OklchColor.TryFromHex(hex, out var color));
        Assert.Equal(l, color.L, tolerance: 0.005);
        Assert.Equal(c, color.C, tolerance: 0.005);
        // Tolerancia de matiz holgada: con poco croma, redondear a hex mueve el matiz unos grados.
        Assert.InRange(color.H, h - 5, h + 5);
    }

    [Theory]
    [InlineData("#EBD38B")]
    [InlineData("#262F47")]
    [InlineData("#E8E8E8")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void RoundTrip_GivesTheSameHex(string hex)
    {
        Assert.True(OklchColor.TryFromHex(hex, out var color));
        Assert.Equal(hex, color.ToHex());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EBD38B")]
    [InlineData("#EBD38")]
    [InlineData("#GGGGGG")]
    [InlineData("# BD38B")]
    public void TryFromHex_RejectsAnythingThatIsNotRRGGBB(string? hex)
    {
        Assert.False(OklchColor.TryFromHex(hex, out _));
    }

    [Fact]
    public void TryFromHex_AcceptsLowercase()
    {
        Assert.True(OklchColor.TryFromHex("#ebd38b", out var color));
        Assert.Equal("#EBD38B", color.ToHex());
    }

    [Fact]
    public void ToHex_OutOfGamut_ReducesChromaInsteadOfClipping()
    {
        // Un azul muy saturado y muy claro no existe en sRGB: se conserva L y H y baja C.
        var hex = new OklchColor(0.9, 0.3, 265).ToHex();

        Assert.True(OklchColor.TryFromHex(hex, out var back));
        Assert.Equal(0.9, back.L, 2);
        Assert.InRange(back.H, 255, 275);
        Assert.True(back.C < 0.3);
    }

    [Fact]
    public void DistanceTo_IsZeroForTheSameColorAndGrowsWithDifference()
    {
        OklchColor.TryFromHex("#262F47", out var ink);
        OklchColor.TryFromHex("#26323E", out var slate);
        OklchColor.TryFromHex("#EBE6D9", out var bone);

        Assert.Equal(0, ink.DistanceTo(ink), 6);
        Assert.True(ink.DistanceTo(slate) < ink.DistanceTo(bone));
    }
}

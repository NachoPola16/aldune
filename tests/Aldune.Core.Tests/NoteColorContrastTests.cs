using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteColorContrastTests
{
    [Theory]
    [InlineData("#000000")]
    [InlineData("#1E1A14")]
    [InlineData("#000080")]
    [InlineData("#663399")]
    [InlineData("#737373")]
    public void ForegroundFor_DarkColors_UsesWhite(string color)
    {
        Assert.Equal(NoteColorContrast.White, NoteColorContrast.ForegroundFor(color));
        Assert.True(NoteColorContrast.IsReadable(color, NoteColorContrast.White));
    }

    [Theory]
    [InlineData("#EBD38B")]
    [InlineData("#AAE6B1")]
    [InlineData("#83E7F2")]
    [InlineData("#C2D4FF")]
    [InlineData("#F9BEF2")]
    [InlineData("#FFC5AF")]
    [InlineData("#FFFFFF")]
    [InlineData("#F5E3B3")]
    public void ForegroundFor_Pastels_PreservesWarmInk(string color)
    {
        Assert.Equal(NoteColorContrast.Ink, NoteColorContrast.ForegroundFor(color));
        Assert.True(NoteColorContrast.IsReadable(color, NoteColorContrast.Ink));
    }

    [Fact]
    public void ForegroundFor_MiddleGray_UsesBlackToCloseTheAaGap()
    {
        const string color = "#777777";
        Assert.False(NoteColorContrast.IsReadable(color, NoteColorContrast.Ink));
        Assert.False(NoteColorContrast.IsReadable(color, NoteColorContrast.White));
        Assert.Equal(NoteColorContrast.Black, NoteColorContrast.ForegroundFor(color));
        Assert.True(NoteColorContrast.IsReadable(color, NoteColorContrast.Black));
    }

    [Fact]
    public void ForegroundFor_AllGraysAndSampledRgbCube_AlwaysMeetsAa()
    {
        for (int channel = 0; channel <= 255; channel++)
            AssertReadable($"#{channel:X2}{channel:X2}{channel:X2}");

        for (int red = 0; red <= 255; red += 17)
        for (int green = 0; green <= 255; green += 17)
        for (int blue = 0; blue <= 255; blue += 17)
            AssertReadable($"#{red:X2}{green:X2}{blue:X2}");

        static void AssertReadable(string color) =>
            Assert.True(NoteColorContrast.IsReadable(color, NoteColorContrast.ForegroundFor(color)), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#FFFFFFFF")]
    [InlineData("#GG0000")]
    [InlineData("# 12345")]
    [InlineData("#12345 ")]
    public void InvalidColor_IsRejectedAndHasSafeForegroundFallback(string? color)
    {
        Assert.False(NoteColorContrast.TryGetLuminance(color, out _));
        Assert.False(NoteColorContrast.IsReadable(color, NoteColorContrast.White));
        Assert.False(NoteColorContrast.IsReadable(NoteColorContrast.White, color));
        Assert.Equal(NoteColorContrast.Ink, NoteColorContrast.ForegroundFor(color));
    }

    [Theory]
    [InlineData("#000000", 0)]
    [InlineData("#ffffff", 1)]
    [InlineData("#FF0000", 0.2126)]
    [InlineData("#00FF00", 0.7152)]
    [InlineData("#0000FF", 0.0722)]
    [InlineData("#808080", 0.2158605)]
    [InlineData("#0A0A0A", 0.00303527)]
    [InlineData("#0B0B0B", 0.00334654)]
    public void Luminance_UsesLinearizedSrgb(string color, double expected)
    {
        Assert.True(NoteColorContrast.TryGetLuminance(color, out var luminance));
        Assert.InRange(Math.Abs(expected - luminance), 0, 0.000001);
    }

    [Theory]
    [InlineData("#EBD38B", "#614E00")]
    [InlineData("#AAE6B1", "#006123")]
    [InlineData("#FFFFFF", "#1E1A14")]
    [InlineData("#777777", "#000000")]
    [InlineData("#663399", "#FFFFFF")]
    [InlineData("#000000", "#FFFFFF")]
    public void MutedInk_IsSofterThanTheInkButStillReadable(string face, string ink)
    {
        string muted = NoteColorContrast.MutedInkFor(face, ink);
        Assert.True(NoteColorContrast.IsReadable(face, muted), $"{muted} on {face}");
        if (NoteColorContrast.IsReadable(face, ink)) Assert.True(NoteColorContrast.IsReadable(face, ink));
    }

    [Fact]
    public void MutedInk_WithNoRoomToFade_ReturnsTheInk()
    {
        // Ink that barely reaches 4.5:1 cannot be softened without failing it.
        Assert.Equal("#767676", NoteColorContrast.MutedInkFor("#FFFFFF", "#767676"));
    }

    [Fact]
    public void MutedInk_FadesWhenThereIsRoom()
    {
        Assert.NotEqual("#000000", NoteColorContrast.MutedInkFor("#FFFFFF", "#000000"));
    }

    [Theory]
    [InlineData("#EBD38B")]
    [InlineData("#AAE6B1")]
    [InlineData("#83E7F2")]
    [InlineData("#C2D4FF")]
    [InlineData("#F9BEF2")]
    [InlineData("#FFC5AF")]
    [InlineData("#663399")]
    [InlineData("#777777")]
    public void SoftenedFace_KeepsTheLightnessAndHue_LosesChroma_AndItsLabelStaysReadable(string color)
    {
        string soft = NoteColorDerivation.SoftenFace(color, 0.6);

        Assert.True(OklchColor.TryFromHex(color, out var original));
        Assert.True(OklchColor.TryFromHex(soft, out var softened));
        Assert.InRange(softened.L, original.L - 0.02, original.L + 0.02);
        Assert.True(softened.C <= original.C + 0.001, $"{soft} is not less saturated than {color}");
        Assert.True(NoteColorContrast.IsReadable(soft, NoteColorDerivation.LabelFor(soft)), $"label on {soft}");
        Assert.True(NoteColorContrast.IsReadable(soft, NoteColorContrast.MutedInkFor(soft, NoteColorDerivation.LabelFor(soft))));
    }

    [Fact]
    public void SoftenFace_LeavesWhatItCannotParseAlone()
    {
        Assert.Equal("not a colour", NoteColorDerivation.SoftenFace("not a colour", 0.6));
    }
}

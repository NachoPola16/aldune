using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ColorFitTests
{
    [Fact]
    public void Ratio_IsTheWcagContrast()
    {
        Assert.Equal(21, ColorFit.Ratio("#000000", "#FFFFFF"), 1);
        Assert.Equal(1, ColorFit.Ratio("#808080", "#808080"), 3);
        Assert.Equal(1, ColorFit.Ratio("rojo", "#FFFFFF"), 3);
    }

    [Theory]
    [InlineData("#FFFF00")]
    [InlineData("#FFFFFF")]
    [InlineData("#8C8CA8")]
    [InlineData("#0055E5")]
    public void Background_UnderWhiteText_DarkensJustEnough(string bar)
    {
        var fitted = ColorFit.Background(bar, "#FFFFFF", 4.5);

        Assert.True(ColorFit.Ratio(fitted, "#FFFFFF") >= 4.5, fitted);
    }

    [Fact]
    public void Background_AlreadyReadable_IsUnchanged()
    {
        Assert.Equal("#0055E5", ColorFit.Background("#0055e5", "#FFFFFF", 4.5));
    }

    [Fact]
    public void Background_UnderDarkText_Lightens()
    {
        var fitted = ColorFit.Background("#3465A4", "#201B16", 4.5);

        Assert.True(ColorFit.Ratio(fitted, "#201B16") >= 4.5, fitted);
        Assert.True(OklchColor.TryFromHex(fitted, out var after));
        Assert.True(OklchColor.TryFromHex("#3465A4", out var before));
        Assert.True(after.L > before.L);
    }

    [Fact]
    public void Fitting_KeepsTheHue()
    {
        // Lo que el usuario elige es el matiz: la claridad se ajusta, el color se reconoce.
        Assert.True(OklchColor.TryFromHex("#E95420", out var chosen));
        Assert.True(OklchColor.TryFromHex(ColorFit.Background("#E95420", "#FFFFFF", 4.5), out var fitted));
        Assert.True(Math.Abs(chosen.H - fitted.H) < 15, $"{chosen.H} → {fitted.H}");
    }

    [Theory]
    [InlineData("#FFFFFF", "#F1EFE4")]
    [InlineData("#000000", "#171C21")]
    [InlineData("#FFFF00", "#F6F1E8")]
    public void Foreground_OnAFixedBackground_BecomesReadable(string text, string background)
    {
        Assert.True(ColorFit.Ratio(ColorFit.Foreground(text, background, 4.5), background) >= 4.5);
    }

    [Fact]
    public void LighterAndDarker_MoveTheLightness()
    {
        Assert.True(OklchColor.TryFromHex(ColorFit.Lighter("#1C4FA8", 0.1), out var lighter));
        Assert.True(OklchColor.TryFromHex(ColorFit.Darker("#1C4FA8", 0.1), out var darker));
        Assert.True(OklchColor.TryFromHex("#1C4FA8", out var original));
        Assert.True(lighter.L > original.L && darker.L < original.L);
    }
}

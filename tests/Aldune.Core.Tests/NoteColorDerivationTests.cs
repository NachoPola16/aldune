using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteColorDerivationTests
{
    // Los valores que se diseñaron a mano para la paleta de fábrica: no pueden moverse ni un píxel.
    [Theory]
    [InlineData("#EBD38B", "#B7A059", "#614E00")]
    [InlineData("#AAE6B1", "#78B280", "#006123")]
    [InlineData("#83E7F2", "#4CB3BE", "#005B63")]
    [InlineData("#C2D4FF", "#90A1CA", "#3A4C83")]
    [InlineData("#F9BEF2", "#C38CBE", "#743170")]
    [InlineData("#FFC5AF", "#C9937E", "#7D391D")]
    public void ClassicColors_KeepTheirHandTunedRimAndLabel(string face, string rim, string label)
    {
        Assert.Equal(rim, NoteColorDerivation.RimFor(face));
        Assert.Equal(label, NoteColorDerivation.LabelFor(face));
    }

    [Fact]
    public void ClassicColors_AreTheSixInTheirOriginalOrder()
    {
        Assert.Equal(new[] { "#EBD38B", "#AAE6B1", "#83E7F2", "#C2D4FF", "#F9BEF2", "#FFC5AF" },
            NoteColorDerivation.ClassicColors);
    }

    [Fact]
    public void ClassicOverride_IsCaseInsensitive()
    {
        Assert.Equal("#B7A059", NoteColorDerivation.RimFor("#ebd38b"));
    }

    [Theory]
    [InlineData("#2E3034", true)]
    [InlineData("#462527", true)]
    [InlineData("#EBE6D9", false)]
    [InlineData("#EBD38B", false)]
    [InlineData("not a color", false)]
    [InlineData(null, false)]
    public void IsDark_SplitsAtLightnessPointSix(string? color, bool dark)
    {
        Assert.Equal(dark, NoteColorDerivation.IsDark(color));
    }

    [Fact]
    public void DarkFace_GetsALighterRimAndALightLabel()
    {
        OklchColor.TryFromHex("#262F47", out var face);
        OklchColor.TryFromHex(NoteColorDerivation.RimFor("#262F47"), out var rim);
        OklchColor.TryFromHex(NoteColorDerivation.LabelFor("#262F47"), out var label);

        Assert.Equal(face.L + 0.07, rim.L, tolerance: 0.005);
        Assert.Equal(0.86, label.L, tolerance: 0.005);
    }

    [Fact]
    public void LightFace_GetsADarkerRimAndLabel()
    {
        OklchColor.TryFromHex("#EBE6D9", out var face);
        OklchColor.TryFromHex(NoteColorDerivation.RimFor("#EBE6D9"), out var rim);
        OklchColor.TryFromHex(NoteColorDerivation.LabelFor("#EBE6D9"), out var label);

        Assert.Equal(face.L - 0.16, rim.L, tolerance: 0.005);
        Assert.Equal(face.L - 0.44, label.L, tolerance: 0.005);
    }

    // Colores que no son de ningún tema: notas de versiones antiguas y colores personalizados.
    [Theory]
    [InlineData("#F7E6A3")]
    [InlineData("#808080")]
    [InlineData("#FF0000")]
    [InlineData("#0000FF")]
    [InlineData("#101010")]
    [InlineData("#FAFAFA")]
    public void AnyColor_GetsAReadableLabel(string face)
    {
        Assert.True(NoteColorContrast.IsReadable(face, NoteColorDerivation.LabelFor(face)));
    }

    [Fact]
    public void InvalidColor_FallsBackWithoutThrowing()
    {
        Assert.Equal("oops", NoteColorDerivation.RimFor("oops"));
        Assert.Equal(NoteColorContrast.Ink, NoteColorDerivation.LabelFor("oops"));
    }

    // La tira de reposo del dock es del mismo oscuro que el chrome: un guion oscuro sin contorno
    // desaparecía (Grafito, Sereno oscuro). El contorno tiene que separarse claramente del fondo.
    [Theory]
    [InlineData("#2F2D2C")]
    [InlineData("#2B2E33")]
    [InlineData("#262F36")]
    [InlineData("#462527")]
    [InlineData("#1D3538")]
    public void DarkFaces_GetARestOutlineThatStandsOutFromTheDock(string face)
    {
        var outline = NoteColorDerivation.RestOutlineFor(face);
        Assert.NotNull(outline);
        Assert.True(OklchColor.TryFromHex(outline, out var o));
        Assert.True(OklchColor.TryFromHex("#2A261F", out var ground));
        Assert.True(OklchColor.TryFromHex(face, out var f));
        Assert.True(o.L - ground.L >= 0.15, $"{outline} L={o.L:0.00} frente al fondo {ground.L:0.00}");
        // Mismo matiz: sigue siendo el color de esa nota, no un gris cualquiera.
        if (f.C > 0.02) Assert.True(Math.Abs(o.H - f.H) < 2, $"matiz {o.H:0} frente a {f.H:0}");
    }

    [Theory]
    [InlineData("#EBD38B")]
    [InlineData("#EBE6D9")]
    [InlineData("#DEE8F0")]
    public void LightFaces_KeepNoRestOutline(string face)
    {
        Assert.Null(NoteColorDerivation.RestOutlineFor(face));
    }
}

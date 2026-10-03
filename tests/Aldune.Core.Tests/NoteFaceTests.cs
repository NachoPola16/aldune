using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteFaceTests
{
    private static readonly IReadOnlyDictionary<string, string> Dark = AppPalette.For(AppearanceMode.Dark, false);

    public static TheoryData<AppearanceMode> Palettes => new()
    {
        AppearanceMode.Dark, AppearanceMode.Light, AppearanceMode.Pastel, AppearanceMode.Midnight
    };

    [Theory]
    [InlineData("#EBD38B")]
    [InlineData("#472525")]
    [InlineData("#C2D4FF")]
    public void Filled_IsExactlyTodaysLook(string color)
    {
        var face = NoteFace.For(SkinCard.Filled, color, null, 0, Dark);

        Assert.Equal(color, face.Face);
        Assert.Equal(NoteColorContrast.ForegroundFor(color), face.Ink);
        Assert.Equal(NoteColorDerivation.RimFor(color), face.Rim);
        Assert.Equal(NoteColorDerivation.LabelFor(color), face.Label);
        Assert.Equal(NoteColorDerivation.LabelFor(color), face.Snippet);
        Assert.Null(face.Accent);
        Assert.Equal(color, face.Pill);
        Assert.Equal(NoteColorDerivation.RestOutlineFor(color), face.PillRim);
    }

    [Fact]
    public void Filled_WithUniformColor_PaintsIt()
    {
        Assert.Equal("#33363A", NoteFace.For(SkinCard.Filled, "#EBD38B", "#33363A", 0, Dark).Face);
        Assert.Equal("#EBD38B", NoteFace.For(SkinCard.Filled, "#EBD38B", "rojo", 0, Dark).Face);
    }

    [Fact]
    public void Stripe_KeepsTheTerminalBackground_AndPutsTheColorInTheStripe()
    {
        var face = NoteFace.For(SkinCard.Stripe, "#472525", null, 0, Dark);

        Assert.Equal(Dark["Ground"], face.Face);
        Assert.Equal(Dark["Text"], face.Ink);
        // Título en el color del texto, no en el de la nota (decidido con el usuario para bash).
        Assert.Equal(Dark["Text"], face.Label);
        Assert.Equal(Dark["Border"], face.Rim);
        Assert.Equal(NoteColorDerivation.StripeColor("#472525", Dark["Ground"]), face.Accent);
        Assert.Equal(face.Accent, face.PillRim);
    }

    [Fact]
    public void Stripe_WithUniformColor_PaintsEveryStripeTheSame()
    {
        var a = NoteFace.For(SkinCard.Stripe, "#472525", "#83A598", 0, Dark);
        var b = NoteFace.For(SkinCard.Stripe, "#113447", "#83A598", 3, Dark);

        Assert.Equal(a.Accent, b.Accent);
    }

    [Theory]
    [InlineData(0, "Channel1")]
    [InlineData(1, "Channel2")]
    [InlineData(3, "Channel4")]
    public void Tinted_UsesTheColorOfItsChannel_NotTheNotes(int channel, string key)
    {
        var face = NoteFace.For(SkinCard.Tinted, "#EBD38B", null, channel, Dark);

        Assert.Equal(Dark[key], face.Accent);
        Assert.Equal(Dark[key], face.Label);
        Assert.Equal(Dark["Text"], face.Ink);
    }

    [Fact]
    public void Tinted_WithUniformColor_ReplacesTheChannelColors()
    {
        // Solo la onda y el número de canal siguen distinguiendo las tarjetas (spec, sección 5).
        Assert.Equal("#33363A", NoteFace.For(SkinCard.Tinted, "#EBD38B", "#33363A", 0, Dark).Accent);
        Assert.Equal("#33363A", NoteFace.For(SkinCard.Tinted, "#C2D4FF", "#33363A", 2, Dark).Accent);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Tinted_TitleInTheChannelColor_IsReadable(AppearanceMode mode)
    {
        var palette = AppPalette.For(mode, false);
        for (int channel = 0; channel < 4; channel++)
        {
            var face = NoteFace.For(SkinCard.Tinted, "#EBD38B", null, channel, palette);
            Assert.Equal(palette[NoteChannels.PaletteKey(channel)], face.Label);
            Assert.True(NoteColorContrast.IsReadable(face.Face, face.Label), $"{mode} CH{channel + 1}: {face.Label} sobre {face.Face}");
            Assert.True(NoteColorContrast.IsReadable(face.Face, face.Ink), $"{mode} CH{channel + 1}: tinta");
        }
    }

    [Fact]
    public void Tinted_WithAnUnreadableUniformColor_FallsBackToTheTextForTheTitle()
    {
        var face = NoteFace.For(SkinCard.Tinted, "#EBD38B", "#33363A", 0, Dark);

        Assert.Equal(Dark["Text"], face.Label);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    [InlineData(9, 1)]
    public void Channels_CycleEveryFourPositions(int position, int channel)
    {
        Assert.Equal(channel, NoteChannels.Of(position));
    }

    [Fact]
    public void Channels_HaveANumber_AndFourDifferentWaves()
    {
        Assert.Equal("CH1", NoteChannels.Number(0));
        Assert.Equal("CH4", NoteChannels.Number(3));
        Assert.Equal(4, Enumerable.Range(0, 4).Select(NoteChannels.WavePath).Distinct().Count());
        Assert.Equal(NoteChannels.WavePath(0), NoteChannels.WavePath(4));
    }

    [Fact]
    public void ColorMix_GoesFromOneColorToTheOther()
    {
        Assert.Equal("#000000", ColorMix.Toward("#000000", "#FFFFFF", 0));
        Assert.Equal("#FFFFFF", ColorMix.Toward("#000000", "#FFFFFF", 1));
        Assert.Equal("#808080", ColorMix.Toward("#000000", "#FFFFFF", 0.5));
        Assert.Equal("#000000", ColorMix.Toward("#000000", "rojo", 0.5));
    }
}

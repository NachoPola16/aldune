using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteThemesTests
{
    [Fact]
    public void BuiltIn_AreTheFactoryThemes_InThatOrder()
    {
        // Grafito se retiró en la 1.5: su papel (monocromo) lo cubre "mismo color en todas las notas".
        Assert.Equal(new[] { "classic", "serene", "pastel", "autumn", "ocean", "xp" }, NoteThemes.BuiltIn.Select(t => t.Id));
        Assert.All(NoteThemes.BuiltIn, theme => Assert.True(theme.IsBuiltIn));
    }

    // La regla de los temas calculados (spec de temas): la misma claridad OKLCH en cada grupo, para
    // que ninguna nota destaque sobre las demás por ser más clara u oscura; solo cambia el matiz.
    [Theory]
    [InlineData("serene")]
    [InlineData("pastel")]
    [InlineData("autumn")]
    [InlineData("ocean")]
    [InlineData("xp")]
    public void CalculatedThemes_KeepOneLightnessPerGroup(string id)
    {
        var theme = NoteThemes.Resolve(id, null);
        Assert.Equal(id, theme.Id);
        foreach (var group in new[] { theme.DarkColors, theme.LightColors }.Where(g => g.Count > 0))
        {
            var lightness = group.Select(Lightness).ToList();
            Assert.True(lightness.Max() - lightness.Min() <= 0.02, $"{id}: {string.Join(", ", group)}");
        }
    }

    // Que se distingan entre sí: dos notas seguidas no pueden parecer del mismo color. Todos los de
    // serie, no solo los nuevos: Sereno pasaba sin cubrir con colores a 0.008 de distancia.
    [Theory]
    [InlineData("classic")]
    [InlineData("serene")]
    [InlineData("pastel")]
    [InlineData("autumn")]
    [InlineData("ocean")]
    [InlineData("xp")]
    public void BuiltInThemes_ColorsAreDistinguishable(string id)
    {
        var theme = NoteThemes.Resolve(id, null);
        Assert.Equal(id, theme.Id);
        foreach (var group in new[] { theme.DarkColors, theme.LightColors }.Where(g => g.Count > 0))
        {
            Assert.True(group.Count >= 4, id);
            var colors = group.Select(Oklch).ToList();
            for (int i = 0; i < colors.Count; i++)
                for (int j = i + 1; j < colors.Count; j++)
                    Assert.True(colors[i].DistanceTo(colors[j]) >= 0.03, $"{id}: {group[i]} y {group[j]} se parecen demasiado");
        }
    }

    // La tinta que la app elige para cada color se tiene que leer bien (AA, 4.5:1).
    [Theory]
    [InlineData("classic")]
    [InlineData("serene")]
    [InlineData("pastel")]
    [InlineData("autumn")]
    [InlineData("ocean")]
    [InlineData("xp")]
    public void BuiltInThemes_InkIsReadable(string id)
    {
        var theme = NoteThemes.Resolve(id, null);
        foreach (var color in theme.DarkColors.Concat(theme.LightColors))
            Assert.True(NoteColorContrast.IsReadable(color, NoteColorContrast.ForegroundFor(color)), $"{id}: {color}");
    }

    [Fact]
    public void Graphite_IsRetired_AndResolvesToClassic()
    {
        Assert.Equal(NoteThemes.ClassicId, NoteThemes.Resolve(NoteThemes.GraphiteId, null).Id);
    }

    [Fact]
    public void SereneRecolorMap_SendsEachOldColorToTheNewOneOfItsSlotAndTone()
    {
        var serene = NoteThemes.Resolve(NoteThemes.SereneId, null);
        for (int i = 0; i < NoteThemes.LegacySereneDarkColors.Count; i++)
            Assert.Equal(serene.DarkColors[i], NoteThemes.SereneRecolorMap[NoteThemes.LegacySereneDarkColors[i]]);
        for (int i = 0; i < NoteThemes.LegacySereneLightColors.Count; i++)
            Assert.Equal(serene.LightColors[i], NoteThemes.SereneRecolorMap[NoteThemes.LegacySereneLightColors[i]]);
        Assert.Equal(14, NoteThemes.SereneRecolorMap.Count);
    }

    private static OklchColor Oklch(string hex)
    {
        Assert.True(OklchColor.TryFromHex(hex, out var color), hex);
        return color;
    }

    private static double Lightness(string hex) => Oklch(hex).L;

    [Fact]
    public void Classic_IsTheSixFactoryColors_LightOnly()
    {
        var classic = NoteThemes.Resolve(null, null);

        Assert.Equal("classic", classic.Id);
        Assert.Equal(NoteColorDerivation.ClassicColors, classic.LightColors);
        Assert.Empty(classic.DarkColors);
    }

    [Fact]
    public void BuiltIn_DarkColorsAreDarkAndLightColorsAreLight()
    {
        foreach (var theme in NoteThemes.BuiltIn)
        {
            Assert.All(theme.DarkColors, color => Assert.True(NoteColorDerivation.IsDark(color), $"{theme.Id} {color}"));
            Assert.All(theme.LightColors, color => Assert.False(NoteColorDerivation.IsDark(color), $"{theme.Id} {color}"));
        }
    }

    [Fact]
    public void BuiltIn_EveryColorHasAReadableDerivedLabel()
    {
        foreach (var color in NoteThemes.BuiltIn.SelectMany(t => t.DarkColors.Concat(t.LightColors)))
        {
            Assert.True(NoteColorContrast.IsReadable(color, NoteColorDerivation.LabelFor(color)), color);
        }
    }

    [Fact]
    public void Resolve_UnknownId_FallsBackToClassic()
    {
        Assert.Equal("classic", NoteThemes.Resolve("gone", null).Id);
    }

    [Fact]
    public void Resolve_FindsACustomTheme()
    {
        var custom = new NoteTheme { Id = "mine", Name = "Mío", LightColors = ["#EEEEEE"] };

        Assert.Same(custom, NoteThemes.Resolve("mine", [custom]));
    }

    [Fact]
    public void Sanitize_DropsInvalidColorsAndEmptyThemes()
    {
        var themes = new List<NoteTheme>
        {
            new() { Id = "a", Name = "A", DarkColors = ["#262F47", "nope"], LightColors = ["#eeeeee"] },
            new() { Id = "b", Name = "B", DarkColors = ["bad"], LightColors = [] },
        };

        var clean = NoteThemes.Sanitize(themes);

        var a = Assert.Single(clean);
        Assert.Equal(new[] { "#262F47" }, a.DarkColors);
        Assert.Equal(new[] { "#EEEEEE" }, a.LightColors);
    }

    [Fact]
    public void Sanitize_DropsThemesThatClaimABuiltInIdOrRepeatAnId()
    {
        var themes = new List<NoteTheme>
        {
            new() { Id = "classic", Name = "Impostor", LightColors = ["#EEEEEE"] },
            new() { Id = "x", Name = "X1", LightColors = ["#EEEEEE"] },
            new() { Id = "x", Name = "X2", LightColors = ["#DDDDDD"] },
        };

        var clean = NoteThemes.Sanitize(themes);

        Assert.Equal(new[] { "X1" }, clean.Select(t => t.Name));
        Assert.All(clean, t => Assert.False(t.IsBuiltIn));
    }

    [Fact]
    public void Sanitize_Null_GivesAnEmptyList()
    {
        Assert.Empty(NoteThemes.Sanitize(null));
    }

    [Fact]
    public void Duplicate_CopiesColorsWithANewIdAndIsNotBuiltIn()
    {
        var serene = NoteThemes.Resolve("serene", null);

        var copy = NoteThemes.Duplicate(serene, "Mi sereno");

        Assert.NotEqual(serene.Id, copy.Id);
        Assert.Equal("Mi sereno", copy.Name);
        Assert.False(copy.IsBuiltIn);
        Assert.Equal(serene.DarkColors, copy.DarkColors);
        Assert.NotSame(serene.DarkColors, copy.DarkColors);
    }
}

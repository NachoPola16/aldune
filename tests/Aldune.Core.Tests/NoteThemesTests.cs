using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteThemesTests
{
    [Fact]
    public void BuiltIn_AreClassicSereneAndGraphite_InThatOrder()
    {
        Assert.Equal(new[] { "classic", "serene", "graphite" }, NoteThemes.BuiltIn.Select(t => t.Id));
        Assert.All(NoteThemes.BuiltIn, theme => Assert.True(theme.IsBuiltIn));
    }

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

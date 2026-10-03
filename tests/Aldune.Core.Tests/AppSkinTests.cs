using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AppSkinTests
{
    [Fact]
    public void Default_IsTheLookOfToday()
    {
        var skin = AppSkin.Default;

        Assert.Equal("Segoe UI Variable Text, Segoe UI", skin.ChromeFont);
        Assert.Equal("", skin.NoteFont);
        Assert.Equal("Ink Free, Segoe UI Variable Text", skin.NoteTitleFont);
        Assert.Equal(17, skin.NoteTitleFontSize);
        Assert.Equal(SkinBorder.Flat, skin.Border);
        Assert.Equal(SkinTitleBar.Plain, skin.TitleBar);
        Assert.Equal(SkinCard.Filled, skin.Card);
        Assert.Equal(SkinTitleAdornment.None, skin.Adornment);
        Assert.False(skin.PromptLine);
        Assert.False(skin.NoteGrid);
    }

    // Los aspectos de hoy no cambian con este bloque: todos usan la piel de siempre.
    [Theory]
    [InlineData(AppearanceMode.Dark)]
    [InlineData(AppearanceMode.Light)]
    [InlineData(AppearanceMode.System)]
    [InlineData(AppearanceMode.Pastel)]
    [InlineData(AppearanceMode.Midnight)]
    public void ExistingAspects_UseTheDefaultSkin(AppearanceMode mode)
    {
        Assert.Same(AppSkin.Default, AppSkin.For(mode));
    }

    [Fact]
    public void Skins_CompareByValue()
    {
        // ThemeManager y SkinState solo avisan si la piel cambia de verdad.
        Assert.Equal(AppSkin.Default, AppSkin.Default with { });
        Assert.NotEqual(AppSkin.Default, AppSkin.Default with { Card = SkinCard.Stripe });
    }
}

using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AppSkinTests
{
    [Fact]
    public void Default_IsTheLookOfToday()
    {
        var skin = AppSkin.Default;

        Assert.Equal("", skin.ChromeFont);
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

    [Fact]
    public void RetroSkins_FollowTheSpec()
    {
        var xpLight = AppSkin.For(AppearanceMode.XpLight);
        Assert.StartsWith("Tahoma", xpLight.ChromeFont);
        Assert.Equal(SkinTitleBar.GradientVertical, xpLight.TitleBar);
        Assert.True(xpLight.Gloss);
        Assert.False(xpLight.SquareCorners);

        var xpDark = AppSkin.For(AppearanceMode.XpDark);
        Assert.Equal(SkinBorder.Bevel, xpDark.Border);
        Assert.Equal(SkinTitleBar.GradientHorizontal, xpDark.TitleBar);
        Assert.True(xpDark.SquareCorners);

        foreach (var mode in new[] { AppearanceMode.TelecomLight, AppearanceMode.TelecomDark })
        {
            var telecom = AppSkin.For(mode);
            Assert.StartsWith("Cascadia Mono", telecom.ChromeFont);
            Assert.Equal(SkinCard.Tinted, telecom.Card);
            Assert.Equal(SkinTitleAdornment.Channel, telecom.Adornment);
        }
        Assert.True(AppSkin.For(AppearanceMode.TelecomDark).NoteGrid);
        Assert.False(AppSkin.For(AppearanceMode.TelecomLight).NoteGrid);

        var bash = AppSkin.For(AppearanceMode.Bash);
        Assert.Equal(SkinCard.Stripe, bash.Card);
        Assert.Equal(SkinTitleAdornment.Folder, bash.Adornment);
        Assert.True(bash.PromptLine);

        var phosphor = AppSkin.For(AppearanceMode.Phosphor);
        Assert.Equal(SkinCard.Mono, phosphor.Card);
        Assert.Equal(SkinTitleAdornment.Uppercase, phosphor.Adornment);

        // Rectas en bash, telecomunicaciones, fósforo y XP + 95; señal encendida donde la llevan las maquetas.
        foreach (var mode in new[] { AppearanceMode.TelecomLight, AppearanceMode.TelecomDark, AppearanceMode.Bash, AppearanceMode.Phosphor })
        {
            Assert.True(AppSkin.For(mode).SquareCorners, mode.ToString());
            Assert.True(AppSkin.For(mode).SyncSignal, mode.ToString());
        }
        Assert.False(AppSkin.For(AppearanceMode.XpLight).SyncSignal);
    }

    [Fact]
    public void Default_HasNoGloss_RoundCorners_AndNoSignal()
    {
        Assert.False(AppSkin.Default.Gloss);
        Assert.False(AppSkin.Default.SquareCorners);
        Assert.False(AppSkin.Default.SyncSignal);
    }
}

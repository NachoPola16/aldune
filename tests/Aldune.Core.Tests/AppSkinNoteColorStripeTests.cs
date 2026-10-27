namespace Aldune.Core.Tests;

/// <summary>
/// «Color de la nota solo como franja»: en los aspectos de siempre (nota entera de su color) pasa a la tarjeta con
/// franja de la piel de terminal. Los aspectos que ya deciden cómo llevan el color (terminal, osciloscopio, fósforo)
/// no cambian: la opción no tiene sentido en ellos.
/// </summary>
public sealed class AppSkinNoteColorStripeTests
{
    [Theory]
    [InlineData(AppearanceMode.Dark)]
    [InlineData(AppearanceMode.Light)]
    [InlineData(AppearanceMode.Pastel)]
    [InlineData(AppearanceMode.XpLight)]
    public void FilledSkins_BecomeStripeWhenAsked(AppearanceMode mode)
    {
        Assert.Equal(SkinCard.Filled, AppSkin.For(mode).Card);
        Assert.Equal(SkinCard.Stripe, AppSkin.For(mode, colorAsStripe: true).Card);
    }

    [Theory]
    [InlineData(AppearanceMode.Bash, SkinCard.Stripe)]
    [InlineData(AppearanceMode.TelecomLight, SkinCard.Tinted)]
    [InlineData(AppearanceMode.Phosphor, SkinCard.Mono)]
    public void SkinsThatChooseTheirOwnCard_AreNotChanged(AppearanceMode mode, SkinCard expected)
    {
        Assert.Equal(expected, AppSkin.For(mode, colorAsStripe: true).Card);
    }

    [Fact]
    public void WithoutTheOption_NothingChanges()
    {
        foreach (var mode in Enum.GetValues<AppearanceMode>())
            Assert.Equal(AppSkin.For(mode), AppSkin.For(mode, colorAsStripe: false));
    }

    [Fact]
    public void TheSettingDefaultsToOff_AndIsInTheConfigExport()
    {
        Assert.False(new AppSettings().NoteColorAsStripe);
        Assert.NotNull(ConfigFields.Find("noteColorAsStripe"));
    }
}

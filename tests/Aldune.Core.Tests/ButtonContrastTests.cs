namespace Aldune.Core.Tests;

/// <summary>Botones: el texto desactivado y el del botón de acento (también al pasar el ratón) se tienen que poder leer en todos los aspectos.</summary>
public sealed class ButtonContrastTests
{
    public static IEnumerable<object[]> Modes() =>
        Enum.GetValues<AppearanceMode>().Where(m => m != AppearanceMode.System).Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(Modes))]
    public void DisabledText_IsReadableOnTheButtonAndOnTheWindow(AppearanceMode mode)
    {
        var p = AppPalette.WithReadableDisabledText(AppPalette.For(mode, windowsUsesLight: false));

        Assert.True(ColorFit.Ratio(p["TextDisabled"], p["Surface"]) >= AppPalette.DisabledTextContrast, $"{mode}: Surface");
        Assert.True(ColorFit.Ratio(p["TextDisabled"], p["Ground"]) >= AppPalette.DisabledTextContrast, $"{mode}: Ground");
        // Sigue pareciendo «apagado»: más tenue que el texto normal.
        Assert.True(ColorFit.Ratio(p["TextDisabled"], p["Surface"]) < ColorFit.Ratio(p["Text"], p["Surface"]), $"{mode}: tenue");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void TheAccentButtonIsReadableAtRestAndOnHover(AppearanceMode mode)
    {
        var p = AppPalette.For(mode, windowsUsesLight: false);

        Assert.True(ColorFit.Ratio(p["OnAccent"], p["Accent"]) >= 4.5, $"{mode}: reposo");
        Assert.True(ColorFit.Ratio(p["OnAccent"], p["AccentHover"]) >= 4.5, $"{mode}: hover");
    }
}

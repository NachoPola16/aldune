using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>
/// Con un tema de alto contraste de Windows la app usa los colores del sistema (fondo, texto, resaltado...). Estos son los
/// de los temas que trae Windows 11 y el clásico blanco sobre negro; el listón es el mismo de las demás paletas, más alto.
/// </summary>
public class HighContrastPaletteTests
{
    // window, windowText, highlight, highlightText, grayText, hotlight
    public static TheoryData<string, string, string, string, string, string> SystemThemes => new()
    {
        { "#000000", "#FFFFFF", "#1AEBFF", "#000000", "#3FF23F", "#FFFF00" },   // Night sky
        { "#202020", "#FFFFFF", "#8080FF", "#000000", "#A6A6A6", "#FFFF00" },   // Aquatic
        { "#2D2D2D", "#FFFFFF", "#FFC800", "#000000", "#FFFFFF", "#FFC800" },   // Dusk
        { "#FFFFFF", "#000000", "#37006E", "#FFFFFF", "#600000", "#00009F" },   // Alto contraste blanco
        { "#FFFAEF", "#000000", "#1C0099", "#FFFFFF", "#005600", "#1C0099" },   // Desert
    };

    private static IReadOnlyDictionary<string, string> Build(string w, string t, string h, string ht, string g, string hot) =>
        AppPalette.HighContrast(w, t, h, ht, g, hot);

    [Theory]
    [MemberData(nameof(SystemThemes))]
    public void ItDefinesExactlyTheSameTokensAsTheOtherPalettes(string w, string t, string h, string ht, string g, string hot) =>
        Assert.Equal(AppPalette.For(light: false).Keys.Order(), Build(w, t, h, ht, g, hot).Keys.Order());

    [Theory]
    [MemberData(nameof(SystemThemes))]
    public void EveryToken_IsAValidHexColor(string w, string t, string h, string ht, string g, string hot)
    {
        foreach (var (_, value) in Build(w, t, h, ht, g, hot))
            Assert.Matches("^#([0-9A-F]{6}|[0-9A-F]{8})$", value.ToUpperInvariant());
    }

    [Theory]
    [MemberData(nameof(SystemThemes))]
    public void TextIsReadableAtTheHighContrastLevel(string w, string t, string h, string ht, string g, string hot)
    {
        var p = Build(w, t, h, ht, g, hot);
        foreach (var text in new[] { "Text", "TextStrong", "TextSoft", "TextWarm", "MutedText", "Hint", "DangerText", "ErrorText" })
            foreach (var ground in new[] { "Ground", "Surface", "Raised", "Popup", "Hover", "DangerBg" })
                Assert.True(Ratio(p[text], p[ground]) >= 7.0, $"{text} sobre {ground}: {Ratio(p[text], p[ground]):0.00}");
    }

    [Theory]
    [MemberData(nameof(SystemThemes))]
    public void ButtonsAreReadableAndDelimited(string w, string t, string h, string ht, string g, string hot)
    {
        var p = Build(w, t, h, ht, g, hot);
        Assert.True(Ratio(p["OnAccent"], p["Accent"]) >= 7.0, "OnAccent sobre Accent");
        Assert.True(Ratio(p["OnDanger"], p["DangerStrong"]) >= 7.0, "OnDanger sobre DangerStrong");
        Assert.True(Ratio(p["TextStrong"], p["DockPrimary"]) >= 7.0, "botón + del dock");
        // Sin relleno que distinga un control del fondo, lo delimita el borde: tiene que verse.
        Assert.True(Ratio(p["Border"], p["Ground"]) >= 7.0, "Border sobre Ground");
        Assert.True(Ratio(p["BorderSubtle"], p["Ground"]) >= 7.0, "BorderSubtle sobre Ground");
    }

    [Theory]
    [MemberData(nameof(SystemThemes))]
    public void TheFocusRingIsVisibleOnEveryBackground(string w, string t, string h, string ht, string g, string hot)
    {
        var p = Build(w, t, h, ht, g, hot);
        var ring = AppPalette.FocusRing(p);
        foreach (var ground in new[] { "Ground", "Surface", "Raised", "Popup", "Hover", "Pressed" })
            Assert.True(Ratio(ring, p[ground]) >= AppPalette.FocusRingContrast, $"anillo sobre {ground}");
    }

    [Fact]
    public void ADarkWindowGivesTheDarkBaseAndALightOneTheLightBase()
    {
        Assert.False(AppPalette.IsLightHighContrast("#000000"));
        Assert.True(AppPalette.IsLightHighContrast("#FFFFFF"));
    }

    private static double Ratio(string a, string b) => ColorFit.Ratio(Opaque(a), Opaque(b));

    private static string Opaque(string hex) => hex.Length == 9 ? "#" + hex.Substring(3) : hex;
}

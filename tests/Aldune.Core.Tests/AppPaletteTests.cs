namespace Aldune.Core.Tests;

public class AppPaletteTests
{
    // Los aspectos de siempre y cada retro con sus colores de fábrica y con colores extremos en todos
    // los huecos (spec: "tests con colores extremos"). Un caso = "Modo" o "Modo/#COLOR".
    public static TheoryData<string> Palettes
    {
        get
        {
            var data = new TheoryData<string> { "Dark", "Light", "Pastel", "Midnight" };
            foreach (var aspect in AspectCatalog.Retro)
            {
                data.Add(aspect.Mode.ToString());
                foreach (var extreme in new[] { "#FFFFFF", "#000000", "#FFFF00" })
                    data.Add($"{aspect.Mode}/{extreme}");
            }
            return data;
        }
    }

    private static IReadOnlyDictionary<string, string> Palette(string palette)
    {
        var parts = palette.Split('/');
        var mode = Enum.Parse<AppearanceMode>(parts[0]);
        Dictionary<string, string>? colors = null;
        if (parts.Length == 2)
            colors = AspectCatalog.For(mode)!.Slots.ToDictionary(slot => slot.Id, _ => parts[1]);
        return AppPalette.For(mode, windowsUsesLight: false, colors);
    }

    private static IReadOnlyDictionary<string, string> Palette(AppearanceMode mode) => AppPalette.For(mode, windowsUsesLight: false);

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryPalette_DefinesExactlyTheSameTokensAsDark(string mode)
    {
        Assert.Equal(Palette(AppearanceMode.Dark).Keys.Order(), Palette(mode).Keys.Order());
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryToken_IsAValidHexColor(string mode)
    {
        foreach (var (_, value) in Palette(mode))
            Assert.Matches("^#([0-9A-F]{6}|[0-9A-F]{8})$", value.ToUpperInvariant());
    }

    // El mismo listón para todas, sacado de lo que ya cumplía el oscuro: ninguna puede leerse peor
    // que la que la gente ya usa.
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_IsReadableOnEveryChromeBackground(string mode)
    {
        var p = Palette(mode);
        foreach (var text in new[] { "Text", "TextStrong", "TextSoft", "TextWarm" })
            foreach (var ground in new[] { "Ground", "Surface", "Raised", "Popup" })
                AssertContrast(p, text, ground, 4.5);

        AssertContrast(p, "MutedText", "Ground", 4.5);
        AssertContrast(p, "MutedText", "Surface", 4.5);
        AssertContrast(p, "Hint", "Ground", 3.5);
        AssertContrast(p, "DangerText", "Ground", 4.5);
        AssertContrast(p, "ErrorText", "Ground", 4.5);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Buttons_AreReadable(string mode)
    {
        var p = Palette(mode);
        AssertContrast(p, "OnAccent", "Accent", 4.5);
        AssertContrast(p, "OnDanger", "DangerStrong", 4.5);
        AssertContrast(p, "Text", "DangerBg", 4.5);
        AssertContrast(p, "Text", "Hover", 4.5);
    }

    // Pastel y Medianoche tienen que notarse: fondo teñido hacia su matiz, no un gris con otro nombre.
    [Theory]
    [InlineData(AppearanceMode.Pastel, 300)]
    [InlineData(AppearanceMode.Midnight, 255)]
    public void TintedPalettes_AreVisiblyTinted(AppearanceMode mode, double hue)
    {
        Assert.True(OklchColor.TryFromHex(Palette(mode)["Ground"], out var ground));
        Assert.True(ground.C >= 0.012, $"croma {ground.C:0.000}");
        Assert.True(Math.Abs(ground.H - hue) <= 20, $"matiz {ground.H:0}");
    }

    [Theory]
    [InlineData(AppearanceMode.Dark, false, false)]
    [InlineData(AppearanceMode.Dark, true, false)]
    [InlineData(AppearanceMode.Light, false, true)]
    [InlineData(AppearanceMode.Light, true, true)]
    [InlineData(AppearanceMode.System, false, false)]
    [InlineData(AppearanceMode.System, true, true)]
    [InlineData(AppearanceMode.Pastel, false, true)]
    [InlineData(AppearanceMode.Midnight, true, false)]
    [InlineData(AppearanceMode.XpLight, false, true)]
    [InlineData(AppearanceMode.TelecomLight, false, true)]
    [InlineData(AppearanceMode.XpDark, true, false)]
    [InlineData(AppearanceMode.TelecomDark, true, false)]
    [InlineData(AppearanceMode.Bash, true, false)]
    [InlineData(AppearanceMode.Phosphor, true, false)]
    public void IsLight_FollowsTheChoiceOrWindows(AppearanceMode mode, bool windowsUsesLight, bool expectedLight)
    {
        Assert.Equal(expectedLight, AppPalette.IsLight(mode, windowsUsesLight));
    }

    [Fact]
    public void System_UsesTheLightOrDarkPalette()
    {
        Assert.Equal(Palette(AppearanceMode.Light), AppPalette.For(AppearanceMode.System, windowsUsesLight: true));
        Assert.Equal(Palette(AppearanceMode.Dark), AppPalette.For(AppearanceMode.System, windowsUsesLight: false));
    }

    // Las claves de la piel no cambian nada en los aspectos de hoy: la barra es el fondo y su texto
    // el de los títulos de siempre.
    [Theory]
    [InlineData(AppearanceMode.Dark)]
    [InlineData(AppearanceMode.Light)]
    public void SkinTokens_AreNeutralInTheExistingPalettes(AppearanceMode mode)
    {
        var p = Palette(mode);
        Assert.Equal(p["Ground"], p["TitleBar"]);
        Assert.Equal(p["Ground"], p["TitleBarEnd"]);
        Assert.Equal(p["TextStrong"], p["OnTitleBar"]);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void TitleBarAndPrompt_AreReadable(string mode)
    {
        var p = Palette(mode);
        AssertContrast(p, "OnTitleBar", "TitleBar", 4.5);
        AssertContrast(p, "OnTitleBar", "TitleBarEnd", 4.5);
        AssertContrast(p, "PromptUser", "Ground", 4.5);
        AssertContrast(p, "PromptPath", "Ground", 4.5);
    }

    // CH1 amarillo, CH2 cian, CH3 magenta y CH4 azul son los de los osciloscopios: teñirlos de lavanda
    // o de azul noche les quitaría el sentido. El prompt, igual: es el color de una terminal.
    [Theory]
    [InlineData(AppearanceMode.Pastel, AppearanceMode.Light)]
    [InlineData(AppearanceMode.Midnight, AppearanceMode.Dark)]
    public void ChannelAndPromptColors_AreNotTinted(AppearanceMode tinted, AppearanceMode source)
    {
        foreach (var key in new[] { "Channel1", "Channel2", "Channel3", "Channel4", "PromptUser", "PromptPath" })
            Assert.Equal(Palette(source)[key], Palette(tinted)[key]);
    }

    [Fact]
    public void ExistingAspects_IgnoreAspectColors()
    {
        var colors = new Dictionary<string, string> { ["accent"] = "#FF0000" };
        foreach (var mode in new[] { AppearanceMode.Dark, AppearanceMode.Light, AppearanceMode.Pastel, AppearanceMode.Midnight })
            Assert.Equal(AppPalette.For(mode, false), AppPalette.For(mode, false, colors));
    }

    [Fact]
    public void ChosenColors_ShowUp_WithTheirHue()
    {
        var xp = AppPalette.For(AppearanceMode.XpLight, false, new Dictionary<string, string> { ["titleBar"] = "#A8452A" });
        Assert.True(OklchColor.TryFromHex("#A8452A", out var chosen));
        Assert.True(OklchColor.TryFromHex(xp["TitleBarEnd"], out var bar));
        Assert.True(Math.Abs(chosen.H - bar.H) < 15, $"{chosen.H} → {bar.H}");

        var bash = AppPalette.For(AppearanceMode.Bash, false, new Dictionary<string, string> { ["user"] = "#8AE234" });
        Assert.True(OklchColor.TryFromHex(bash["PromptUser"], out var user));
        Assert.True(OklchColor.TryFromHex("#8AE234", out var green));
        Assert.True(Math.Abs(green.H - user.H) < 15);
    }

    // Un fósforo blanco o una barra plateada no tienen matiz: la paleta no puede salir de un color inventado.
    [Theory]
    [InlineData(AppearanceMode.Phosphor, "phosphor", "#E8E8E8")]
    [InlineData(AppearanceMode.Bash, "background", "#1E1E1E")]
    public void GreyChoices_GiveAGreyPalette(AppearanceMode mode, string slot, string grey)
    {
        var palette = AppPalette.For(mode, false, new Dictionary<string, string> { [slot] = grey });
        foreach (var key in new[] { "Ground", "Surface", "Raised", "Text" })
        {
            Assert.True(OklchColor.TryFromHex(palette[key], out var color));
            Assert.True(color.C < 0.02, $"{key} {palette[key]} C {color.C}");
        }
    }

    [Theory]
    [InlineData(AppearanceMode.Bash, "#282828")]
    [InlineData(AppearanceMode.TelecomDark, "#171C21")]
    public void TheChosenBackground_IsTheGround(AppearanceMode mode, string ground)
    {
        Assert.Equal(ground, AppPalette.For(mode, false)["Ground"]);
    }


    private static void AssertContrast(IReadOnlyDictionary<string, string> palette, string foreground, string background, double minimum)
    {
        Assert.True(NoteColorContrast.TryGetLuminance(palette[foreground], out var fg));
        Assert.True(NoteColorContrast.TryGetLuminance(palette[background], out var bg));
        var ratio = (Math.Max(fg, bg) + 0.05) / (Math.Min(fg, bg) + 0.05);
        Assert.True(ratio >= minimum, $"{foreground} sobre {background}: {ratio:0.00} < {minimum}");
    }
}

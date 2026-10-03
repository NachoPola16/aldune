# Bloque 2: piel (AppSkin) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** La maquinaria de "piel" que necesitan los aspectos retro (tipografía, relieve, barra en
degradado, estilos de tarjeta, adornos de título, línea de prompt, retícula), sin cambiar cómo se ve
ningún aspecto existente; más los tres pendientes que dejó el bloque 1.

**Architecture:** La piel es un dato puro en Core (`AppSkin`, un `record` con enums), igual que la
paleta. Los colores que dependen de la piel (cara de la nota, franja, canal, cápsula) se calculan en
Core (`NoteFace`) para que nota y dock no puedan discrepar y se puedan probar. La capa WPF solo
traduce: `ThemeManager.ApplySkin` pone fuentes y degradado como recursos dinámicos, y un objeto
observable (`SkinState.Current`) alimenta los `DataTrigger` de las plantillas (franja, onda, relieve,
botones cuadrados). Todos los aspectos de hoy usan `AppSkin.Default`, que reproduce lo que ya se ve.

**Tech Stack:** C# / .NET 10, WPF, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-03-aspectos-retro-design.md` (secciones 1, 3 "mismo color" y
5; la 2 solo como referencia de qué usará cada aspecto). Maquetas: lienzo
<https://claude.ai/artifact/N2AdurN792fDCFb3WeiCSX> (referencia visual; los números de este plan ya
las recogen).

Los seis aspectos y la página de Ajustes → Aspecto ampliada son el bloque 3; exportar/importar, el 4.

## Desviaciones de la spec (decididas al planificar)

- **Barra de título**: la spec dice `Plain | Gradient`, pero su tabla pide degradado vertical (XP
  claro) y horizontal (XP + 95). El enum es `Plain | GradientVertical | GradientHorizontal`.
- **Claves de paleta**: además de `TitleBar`, `TitleBarEnd`, `OnTitleBar`, `BevelLight` y `BevelDark`
  (las de la spec), hacen falta `Channel1…Channel4` (colores de canal de `Tinted`, que la spec fija pero
  no dice dónde viven) y `PromptUser`/`PromptPath` (los huecos "usuario" y "ruta" de bash). Ni los
  canales ni el prompt se tiñen en Pastel/Medianoche: tienen significado propio, como los de peligro.
- **Fuentes**: tres recursos (`AldunePrimaryFont`, `AlduneNoteFont`, `AlduneNoteTitleFont`) más el
  tamaño del título (`AlduneNoteTitleFontSize`): el título manuscrito de hoy (Ink Free, 17 px) no
  sirve de medida para una monoespaciada.
- **Barra de título en Ajustes y gestor**: esas ventanas no tienen barra; su cabecera es contenido. En
  este bloque la cabecera recibe el fondo `AlduneTitleBarFill` (transparente en `Plain`) y el título
  `AlduneOnTitleBarBrush`. Ajustar alturas y botones de esas cabeceras a una barra "de verdad" es del
  bloque 3, con las capturas de cada aspecto delante. AppDialog, CustomColorWindow, PasswordPrompt,
  SyncConflicts, ThemeEditor y Toast quedan igual (bloque 3).
- **Fuera de este bloque**: el estilo de tarjeta de Fósforo (monocromo; ¿`Stripe` con todas las franjas
  del color del fósforo o un estilo propio?) y el brillo de los botones de XP claro: los decide el plan
  del bloque 3.

## Global Constraints

- Textos de interfaz siempre con `Strings.T(en, es, de, fr, pt)` en `src/Aldune/Resources/Strings.cs`, los cinco idiomas.
- Comentarios en español, explicando el porqué, con la densidad del código de alrededor.
- Lógica nueva en Core con test primero (TDD). La UI se verifica con sondas (`docs/WPF_PROBES.md`).
- `.cs` y `.xaml` con BOM UTF-8; documentación sin BOM.
- Un `settings.json` antiguo tiene que cargar sin migración. Este bloque no añade campos a `AppSettings`.
- Formato de sync **4**: no se sube.
- Colores de chrome con `{DynamicResource Aldune<Clave>Brush}`; colores de nota por temas y por `NoteFace`, nunca hex sueltos nuevos en XAML.
- Una clave de paleta nueva va en Dark y en Light (Pastel y Medianoche se derivan solas); los tests fijan su contraste.
- **Ningún aspecto existente cambia de aspecto** (Oscuro, Claro, Como Windows, Pastel, Medianoche): `AppSkin.Default` y los valores neutros lo garantizan, y la sonda final lo compara con capturas de antes del bloque.
- Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`: sondas con base de datos temporal.
- Las sondas de teclado y el smoke test mueven ratón/teclado: avisar al usuario y esperar su "ok" antes de lanzarlos.
- Commits sin la línea `Co-Authored-By`.
- Comandos: `dotnet build Aldune.slnx -c Debug`, `dotnet test Aldune.slnx --no-build` (todos verdes; solo los 4 avisos CA1416 conocidos).

## Review Focus

1. **Los aspectos de hoy no cambian ni un píxel** con la piel por defecto (fuentes, cara de la nota,
   pestañas, cápsulas, cabeceras de Ajustes y gestor, pie del dock) → tests de `AppSkin.For` y de
   valores neutros (Tasks 1 y 2) y comparación de capturas antes/después en la sonda (Task 9).
2. **Cambiar de piel con notas y dock abiertos** repinta fuentes, caras, franjas, relieve y botones
   sin reiniciar y sin valores viejos en los convertidores → estado de la sonda (Task 9).
3. **"Mismo color" con `Stripe` y `Tinted`**: todas las franjas iguales, y en `Tinted` el color único
   sustituye a los de canal → tests de `NoteFace` (Task 3).
4. **Dock a la izquierda, arriba y abajo** con `Stripe`, `Tinted` y `Bevel`: la franja en el lado
   interior, la onda sin pisar el aviso de recordatorio, el relieve con la luz arriba-izquierda →
   capturas de la sonda (Task 9).
5. **Títulos y usuarios raros**: título vacío, nota protegida, título larguísimo, acentos y emoji,
   usuario de Windows con espacios → los adornos y el prompt no fallan ni se desordenan → tests de
   `NoteLabels` (Task 4).

---

### Task 1: `AppSkin` en Core

**Files:**
- Create: `src/Aldune.Core/AppSkin.cs`
- Test: `tests/Aldune.Core.Tests/AppSkinTests.cs`

**Interfaces:**
- Produces: `enum SkinBorder { Flat = 0, Bevel = 1 }`, `enum SkinTitleBar { Plain = 0, GradientVertical = 1, GradientHorizontal = 2 }`, `enum SkinCard { Filled = 0, Stripe = 1, Tinted = 2 }`, `enum SkinTitleAdornment { None = 0, Channel = 1, Folder = 2, Uppercase = 3 }`; `sealed record AppSkin(string ChromeFont, string NoteFont, string NoteTitleFont, double NoteTitleFontSize, SkinBorder Border, SkinTitleBar TitleBar, SkinCard Card, SkinTitleAdornment Adornment, bool PromptLine, bool NoteGrid)`; `AppSkin.Default`; `AppSkin.For(AppearanceMode) : AppSkin`.

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/AppSkinTests.cs` (con BOM):

```csharp
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
        Assert.Equal("Segoe UI Variable Text, Segoe UI", skin.NoteFont);
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
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~AppSkinTests"`. Expected: no compila (`AppSkin` no existe).

- [ ] **Step 3: Implementar**

`src/Aldune.Core/AppSkin.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Bordes del chrome: planos (hoy) o con el relieve de Windows 95 (claro arriba-izquierda,
/// oscuro abajo-derecha).</summary>
public enum SkinBorder
{
    Flat = 0,
    Bevel = 1,
}

/// <summary>Barra de título de las notas (y fondo de la cabecera de Ajustes y del gestor): sin barra
/// propia, como hoy, o en degradado vertical (XP) u horizontal (Windows 98).</summary>
public enum SkinTitleBar
{
    Plain = 0,
    GradientVertical = 1,
    GradientHorizontal = 2,
}

/// <summary>Cómo lleva cada nota su color en el dock y en su ventana. Ver <see cref="NoteFace"/>.</summary>
public enum SkinCard
{
    /// <summary>La nota entera de su color (hoy).</summary>
    Filled = 0,
    /// <summary>Fondo del chrome y el color solo en una franja de 3 px (terminal).</summary>
    Stripe = 1,
    /// <summary>Tinte suave del color de su canal, por puesto en el dock (osciloscopio).</summary>
    Tinted = 2,
}

/// <summary>Adorno de los títulos en el dock. Solo se pinta: el texto de la nota no cambia.</summary>
public enum SkinTitleAdornment
{
    /// <summary>Mayúsculas espaciadas (hoy).</summary>
    None = 0,
    /// <summary>Prefijo de canal: <c>CH1 HOY</c>.</summary>
    Channel = 1,
    /// <summary>Sufijo de carpeta: <c>hoy/</c>.</summary>
    Folder = 2,
    /// <summary>Mayúsculas sin espaciar.</summary>
    Uppercase = 3,
}

/// <summary>
/// La forma de un aspecto, frente a su paleta (<see cref="AppPalette"/>): tipografía, bordes, barra de
/// título, cómo lleva cada nota su color y los adornos. Datos puros; la capa WPF los convierte en
/// recursos y en el estado que leen las plantillas. No se guarda en settings.json: sale del aspecto.
/// </summary>
public sealed record AppSkin(
    string ChromeFont,
    string NoteFont,
    string NoteTitleFont,
    double NoteTitleFontSize,
    SkinBorder Border,
    SkinTitleBar TitleBar,
    SkinCard Card,
    SkinTitleAdornment Adornment,
    bool PromptLine,
    bool NoteGrid)
{
    /// <summary>Lo de siempre: Segoe en el chrome y el cuerpo, título manuscrito, todo plano.</summary>
    public static AppSkin Default { get; } = new(
        ChromeFont: "Segoe UI Variable Text, Segoe UI",
        NoteFont: "Segoe UI Variable Text, Segoe UI",
        NoteTitleFont: "Ink Free, Segoe UI Variable Text",
        NoteTitleFontSize: 17,
        Border: SkinBorder.Flat,
        TitleBar: SkinTitleBar.Plain,
        Card: SkinCard.Filled,
        Adornment: SkinTitleAdornment.None,
        PromptLine: false,
        NoteGrid: false);

    /// <summary>La piel de cada aspecto. Los de hoy usan la de siempre; los retro llegan en el bloque 3.</summary>
    public static AppSkin For(AppearanceMode mode) => Default;
}
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/AppSkin.cs tests/Aldune.Core.Tests/AppSkinTests.cs
git commit -m "Piel de los aspectos (AppSkin) en Core: tipografia, bordes, barra, tarjetas y adornos; los aspectos de hoy usan la de siempre"
```

---

### Task 2: Claves nuevas de paleta

**Files:**
- Modify: `src/Aldune.Core/AppPalette.cs`
- Test: `tests/Aldune.Core.Tests/AppPaletteTests.cs`

**Interfaces:**
- Produces: claves `TitleBar`, `TitleBarEnd`, `OnTitleBar`, `BevelLight`, `BevelDark`, `Channel1`…`Channel4`, `PromptUser`, `PromptPath` en todas las paletas (y por tanto pinceles `Aldune<Clave>Brush`).

- [ ] **Step 1: Tests** (añadir a `AppPaletteTests`, antes de `AssertContrast`)

```csharp
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
    public void TitleBarAndPrompt_AreReadable(AppearanceMode mode)
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
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~AppPaletteTests"`. Expected: FAIL con `KeyNotFoundException` (`TitleBar`).

- [ ] **Step 3: Implementar**

En `AppPalette.cs`, al final del diccionario `Dark` (después de `DockThumbBorder`):

```csharp
        // Piel (bloque 2). Barra = fondo y sin relieve visible: el oscuro de siempre no cambia. Los
        // aspectos retro dan su propio valor a cada una. CH3 y CH4, más claros que los de las
        // maquetas: su título va sobre su propio tinte y con los de la maqueta no llegaba a 4.5:1.
        ["TitleBar"] = "#2A261F",
        ["TitleBarEnd"] = "#2A261F",
        ["OnTitleBar"] = "#F5F0E6",
        ["BevelLight"] = "#5F584E",
        ["BevelDark"] = "#15120E",
        ["Channel1"] = "#F2D338",
        ["Channel2"] = "#3FD0E0",
        ["Channel3"] = "#EA80DD",
        ["Channel4"] = "#7FA8FF",
        ["PromptUser"] = "#A9C99A",
        ["PromptPath"] = "#9FB8D0",
```

Al final del diccionario `Light`:

```csharp
        // Piel (bloque 2), como en Dark. Canales oscurecidos para el papel claro: el título de una
        // pestaña con tinte va en su color sobre ese tinte y tiene que llegar a 4.5:1.
        ["TitleBar"] = "#F6F1E8",
        ["TitleBarEnd"] = "#F6F1E8",
        ["OnTitleBar"] = "#1E1A14",
        ["BevelLight"] = "#FFFFFF",
        ["BevelDark"] = "#978D80",
        ["Channel1"] = "#6E5600",
        ["Channel2"] = "#00636C",
        ["Channel3"] = "#9C1F6E",
        ["Channel4"] = "#1F57B5",
        ["PromptUser"] = "#44703A",
        ["PromptPath"] = "#2F5F86",
```

En el conjunto `Semantic`, añadir las seis claves y ampliar su comentario:

```csharp
    /// <summary>Colores con significado propio (peligro, error, aviso, éxito; los canales del
    /// osciloscopio y los del prompt): no se tiñen, un "borrar" tiene que seguir pareciendo peligroso
    /// y CH1 tiene que seguir siendo amarillo en cualquier paleta.</summary>
    private static readonly HashSet<string> Semantic =
    [
        "DangerBg", "DangerStrong", "DangerStrongHover", "OnDanger", "DangerText", "DangerHover",
        "OnDangerBg", "ErrorText", "ErrorBorder", "Warning", "Success", "Notice", "Attention",
        "Channel1", "Channel2", "Channel3", "Channel4", "PromptUser", "PromptPath",
    ];
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS (incluidos los que ya existían: mismo conjunto de claves en las cuatro paletas, hex válidos). Si algún contraste no llega, bajar (en Light) o subir (en Dark) la claridad OKLCH de ese color conservando el matiz hasta pasar, y anotar el valor final en el comentario.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/AppPalette.cs tests/Aldune.Core.Tests/AppPaletteTests.cs
git commit -m "Paleta: claves de la piel (barra de titulo, relieve, canales, prompt) con valores neutros en los aspectos de hoy"
```

---

### Task 3: Cara de la nota por estilo de tarjeta (`NoteFace`)

**Files:**
- Create: `src/Aldune.Core/ColorMix.cs`, `src/Aldune.Core/NoteChannels.cs`, `src/Aldune.Core/NoteFace.cs`
- Modify: `src/Aldune.Core/NoteColorDerivation.cs` (nuevo `StripeColor`), `src/Aldune.Core/NoteDisplayColor.cs` (nuevo `IsActive`)
- Test: `tests/Aldune.Core.Tests/NoteFaceTests.cs`, `tests/Aldune.Core.Tests/NoteColorDerivationTests.cs`

**Interfaces:**
- Consumes: `AppPalette` claves `Ground`, `Text`, `Border`, `Channel1…4` (Task 2); `SkinCard` (Task 1).
- Produces: `ColorMix.Toward(string from, string to, double amount) : string`; `NoteChannels.Of(int position) : int` (0–3), `NoteChannels.Number(int channel) : string` (`"CH1"`…), `NoteChannels.PaletteKey(int channel) : string` (`"Channel1"`…), `NoteChannels.WavePath(int channel) : string`; `NoteColorDerivation.StripeColor(string color, string ground) : string`; `NoteDisplayColor.IsActive(string? uniform) : bool`; `sealed record NoteFaceColors(string Face, string Ink, string Rim, string Label, string Snippet, string? Accent, string Pill, string? PillRim)`; `NoteFace.For(SkinCard card, string noteColor, string? uniformColor, int channel, IReadOnlyDictionary<string, string> palette) : NoteFaceColors`.

- [ ] **Step 1: Tests de `StripeColor`** (añadir a `NoteColorDerivationTests`)

```csharp
    public static TheoryData<string> DarkGrounds => new()
    {
        AppPalette.For(AppearanceMode.Dark, false)["Ground"],
        AppPalette.For(AppearanceMode.Midnight, false)["Ground"],
        "#282828", // Gruvbox
        "#1D2021",
    };

    // La franja de bash tiene que verse sobre la terminal aunque el color de la nota sea apagado
    // (Sereno, un gris): 3:1, el mínimo para un elemento gráfico.
    [Theory]
    [MemberData(nameof(DarkGrounds))]
    public void StripeColor_StandsOutOnDarkGrounds(string ground)
    {
        foreach (var theme in NoteThemes.BuiltIn)
            foreach (var color in theme.DarkColors.Concat(theme.LightColors))
            {
                var stripe = NoteColorDerivation.StripeColor(color, ground);
                Assert.True(Ratio(stripe, ground) >= 3, $"{theme.Id} {color} → {stripe} sobre {ground}");
            }
    }

    [Fact]
    public void StripeColor_OnALightGround_IsDarkEnough()
    {
        var ground = AppPalette.For(AppearanceMode.Light, false)["Ground"];
        foreach (var color in NoteThemes.Resolve(NoteThemes.ClassicId, null).LightColors)
            Assert.True(Ratio(NoteColorDerivation.StripeColor(color, ground), ground) >= 3, color);
    }

    [Fact]
    public void StripeColor_KeepsTheHue_AndAGreyStaysGrey()
    {
        Assert.True(OklchColor.TryFromHex("#472525", out var face));
        Assert.True(OklchColor.TryFromHex(NoteColorDerivation.StripeColor("#472525", "#282828"), out var stripe));
        Assert.True(Math.Abs(face.H - stripe.H) < 20, $"{face.H} → {stripe.H}");

        // "Mismo color" en gris: una franja gris, no de un matiz inventado.
        Assert.True(OklchColor.TryFromHex(NoteColorDerivation.StripeColor("#33363A", "#282828"), out var grey));
        Assert.True(grey.C < 0.03, $"C {grey.C}");
    }

    private static double Ratio(string a, string b)
    {
        Assert.True(NoteColorContrast.TryGetLuminance(a, out var la));
        Assert.True(NoteColorContrast.TryGetLuminance(b, out var lb));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
```

(Si `NoteColorDerivationTests` no tiene `using Xunit;` o no está en `Aldune.Core.Tests`, seguir la forma del fichero.)

- [ ] **Step 2: Tests de `NoteFace`, `ColorMix` y `NoteChannels`**

`tests/Aldune.Core.Tests/NoteFaceTests.cs` (con BOM):

```csharp
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
```

- [ ] **Step 3: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~NoteFaceTests|FullyQualifiedName~StripeColor"`. Expected: no compila.

- [ ] **Step 4: Implementar**

`src/Aldune.Core/ColorMix.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Mezcla lineal en sRGB entre dos colores #RRGGBB, la de las maquetas: los tintes de
/// <see cref="NoteFace"/> se afinaron así a ojo y en OKLCH no salen iguales.</summary>
public static class ColorMix
{
    /// <summary><paramref name="amount"/> 0 = <paramref name="from"/>, 1 = <paramref name="to"/>. Con un
    /// color inválido devuelve <paramref name="from"/> tal cual.</summary>
    public static string Toward(string from, string to, double amount)
    {
        if (!TryParse(from, out var a) || !TryParse(to, out var b)) return from;
        amount = Math.Clamp(amount, 0, 1);
        int Channel(int x, int y) => (int)Math.Round(x + (y - x) * amount, MidpointRounding.AwayFromZero);
        return $"#{Channel(a.R, b.R):X2}{Channel(a.G, b.G):X2}{Channel(a.B, b.B):X2}";
    }

    private static bool TryParse(string? hex, out (int R, int G, int B) rgb)
    {
        rgb = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        if (!int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out int value)) return false;
        rgb = ((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        return true;
    }
}
```

`src/Aldune.Core/NoteChannels.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>
/// Canales del osciloscopio (piel <see cref="SkinCard.Tinted"/>): cada nota es un canal según su puesto
/// en el mazo, cíclico de 4 en 4 (CH1 amarillo, CH2 cian, CH3 magenta, CH4 azul), con su forma de onda.
/// </summary>
public static class NoteChannels
{
    public const int Count = 4;

    // Senoidal, cuadrada, triangular y diente de sierra, en una caja de 54×18 (las de las maquetas).
    private static readonly string[] Waves =
    [
        "M0 9 C4.5 0 9 0 13.5 9 S22.5 18 27 9 S36 0 40.5 9 S49.5 18 54 9",
        "M0 15 H7 V3 H20 V15 H33 V3 H46 V15 H54",
        "M0 15 L9 3 L18 15 L27 3 L36 15 L45 3 L54 15",
        "M0 15 L13 3 V15 L26 3 V15 L39 3 V15 L52 3 V15",
    ];

    public static int Of(int position) => ((position % Count) + Count) % Count;

    public static string Number(int channel) => $"CH{Of(channel) + 1}";

    public static string PaletteKey(int channel) => $"Channel{Of(channel) + 1}";

    public static string WavePath(int channel) => Waves[Of(channel)];
}
```

En `NoteDisplayColor.cs`, sacar la validación a un método público y usarlo en `Resolve`:

```csharp
    /// <summary>¿Hay un color único válido? Uno inválido (settings.json editado a mano) cuenta como apagado.</summary>
    public static bool IsActive(string? uniform) =>
        uniform is not null && uniform.Length == 7 && OklchColor.TryFromHex(uniform, out _);

    public static string Resolve(string color, string? uniform) =>
        IsActive(uniform) ? uniform!.ToUpperInvariant() : color;
```

En `NoteColorDerivation.cs`, antes de `FindClassic`:

```csharp
    /// <summary>
    /// Franja de la piel <see cref="SkinCard.Stripe"/> (bash): la nota es del fondo de la terminal y su
    /// color va solo en 3 px a un lado, que tienen que verse aunque el color sea apagado. Mismo matiz,
    /// claridad fija (0.72 sobre un fondo oscuro, 0.5 sobre uno claro) y un croma mínimo para que no
    /// parezca gris; un gris de verdad (croma casi 0, el de "mismo color" monocromo) se queda gris.
    /// </summary>
    public static string StripeColor(string color, string ground)
    {
        if (!OklchColor.TryFromHex(color, out var face)) return color;
        double lightness = IsDark(ground) ? 0.72 : 0.5;
        double chroma = face.C < 0.02 ? face.C : Math.Max(face.C, 0.09);
        return new OklchColor(lightness, chroma, face.H).ToHex();
    }
```

`src/Aldune.Core/NoteFace.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Colores con los que se pinta una nota (ventana, pestaña del dock y cápsula de la tira).</summary>
/// <param name="Face">Fondo.</param>
/// <param name="Ink">Texto del cuerpo.</param>
/// <param name="Rim">Borde.</param>
/// <param name="Label">Título en el dock (y en la ventana con <see cref="SkinCard.Tinted"/>).</param>
/// <param name="Snippet">Vista previa en el dock.</param>
/// <param name="Accent">Franja (<see cref="SkinCard.Stripe"/>) o color del canal (<see cref="SkinCard.Tinted"/>); null en <see cref="SkinCard.Filled"/>.</param>
/// <param name="Pill">Relleno de la cápsula de la tira de reposo.</param>
/// <param name="PillRim">Contorno de la cápsula, o null si no lleva.</param>
public sealed record NoteFaceColors(
    string Face, string Ink, string Rim, string Label, string Snippet, string? Accent, string Pill, string? PillRim);

/// <summary>
/// Un único sitio decide cómo se pinta una nota según la piel, para que la ventana y el dock no puedan
/// discrepar. Recibe el color guardado: el color único de "mismo color" se aplica aquí.
/// </summary>
public static class NoteFace
{
    public static NoteFaceColors For(
        SkinCard card, string noteColor, string? uniformColor, int channel, IReadOnlyDictionary<string, string> palette)
    {
        var color = NoteDisplayColor.Resolve(noteColor, uniformColor);
        var ground = palette["Ground"];
        var text = palette["Text"];

        switch (card)
        {
            case SkinCard.Stripe:
            {
                var stripe = NoteColorDerivation.StripeColor(color, ground);
                return new NoteFaceColors(ground, text, palette["Border"], text, text, stripe,
                    ColorMix.Toward(stripe, ground, 0.35), stripe);
            }
            case SkinCard.Tinted:
            {
                // El color es el del canal (por puesto en el dock), no el de la nota: es lo que da
                // sentido a CH1… Con "mismo color", el color único sustituye a todos los canales.
                var accent = NoteDisplayColor.IsActive(uniformColor) ? color : palette[NoteChannels.PaletteKey(channel)];
                var face = ColorMix.Toward(accent, ground, 0.88);
                var label = NoteColorContrast.IsReadable(face, accent) ? accent : text;
                return new NoteFaceColors(face, text, ColorMix.Toward(accent, ground, 0.5), label, text, accent,
                    ColorMix.Toward(accent, ground, 0.55), accent);
            }
            default:
            {
                var label = NoteColorDerivation.LabelFor(color);
                return new NoteFaceColors(color, NoteColorContrast.ForegroundFor(color), NoteColorDerivation.RimFor(color),
                    label, label, null, color, NoteColorDerivation.RestOutlineFor(color));
            }
        }
    }
}
```

- [ ] **Step 5: Tests en verde** — Run el filtro del Step 3 y después `dotnet test tests/Aldune.Core.Tests -c Debug` (los de `NoteDisplayColor` siguen en verde). Si `Tinted_TitleInTheChannelColor_IsReadable` falla para un canal, ajustar la claridad OKLCH de ese `ChannelN` en la paleta de origen (Dark o Light, Task 2) conservando el matiz, no el tinte de 0.88, y anotar el valor.

- [ ] **Step 6: Commit**

```bash
git add src/Aldune.Core tests/Aldune.Core.Tests
git commit -m "NoteFace: como se pinta una nota segun el estilo de tarjeta (llena, franja o tinte de canal), con franja visible y mismo color"
```

---

### Task 4: Títulos adornados y línea de prompt (`NoteLabels`)

**Files:**
- Create: `src/Aldune.Core/NoteLabels.cs`
- Modify: `src/Aldune/Resources/Strings.cs` (el convertidor de la pestaña pasa a usar `NoteLabels.Dock` en la Task 5, que es la que crea lo que necesita)
- Test: `tests/Aldune.Core.Tests/NoteLabelsTests.cs`

**Interfaces:**
- Consumes: `SkinTitleAdornment` (Task 1), `NoteChannels.Number` (Task 3).
- Produces: `NoteLabels.Dock(string title, SkinTitleAdornment adornment, int channel, CultureInfo culture) : string`; `NoteLabels.Folder(string title, CultureInfo culture) : string`; `sealed record PromptParts(string User, string Path, string Command)`; `NoteLabels.Prompt(string userName, string notesFolder, string title, CultureInfo culture) : PromptParts`; `Strings.PromptNotesFolder`.

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/NoteLabelsTests.cs` (con BOM):

```csharp
using System.Globalization;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteLabelsTests
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    [Fact]
    public void None_IsTheSpacedCapitalsOfToday()
    {
        // Lo que hacía NoteTabLabelConverter: mayúsculas, espacios duros y un espacio fino entre letras.
        Assert.Equal("H O Y   Y", NoteLabels.Dock("  hoy y ", SkinTitleAdornment.None, 0, Es));
    }

    [Theory]
    [InlineData(SkinTitleAdornment.Channel, 1, "Tareas universidad", "CH2 TAREAS_UNIVERSIDAD")]
    [InlineData(SkinTitleAdornment.Channel, 5, "Hoy", "CH2 HOY")]
    [InlineData(SkinTitleAdornment.Folder, 0, " Tareas   universidad ", "tareas_universidad/")]
    [InlineData(SkinTitleAdornment.Uppercase, 0, "Gestión", "GESTIÓN")]
    public void Adornments(SkinTitleAdornment adornment, int channel, string title, string expected)
    {
        Assert.Equal(expected, NoteLabels.Dock(title, adornment, channel, Es));
    }

    [Theory]
    [InlineData(SkinTitleAdornment.None)]
    [InlineData(SkinTitleAdornment.Channel)]
    [InlineData(SkinTitleAdornment.Folder)]
    [InlineData(SkinTitleAdornment.Uppercase)]
    public void BlankTitle_StaysBlank(SkinTitleAdornment adornment)
    {
        Assert.Equal("", NoteLabels.Dock("   ", adornment, 0, Es));
    }

    [Fact]
    public void Folder_KeepsAccentsAndEmoji()
    {
        Assert.Equal("compra_🛒_sábado/", NoteLabels.Dock("Compra 🛒 sábado", SkinTitleAdornment.Folder, 0, Es));
    }

    [Fact]
    public void Prompt_IsAShellLine()
    {
        var prompt = NoteLabels.Prompt("Nacho Pola", "notas", "Hoy", Es);

        Assert.Equal("nacho_pola@aldune", prompt.User);
        Assert.Equal("~/notas", prompt.Path);
        Assert.Equal("$ cat hoy", prompt.Command);
    }

    [Fact]
    public void Prompt_WithoutTitleOrUser_StillReadsAsAPrompt()
    {
        var prompt = NoteLabels.Prompt("  ", "notas", "", Es);

        Assert.Equal("user@aldune", prompt.User);
        Assert.Equal("$ ", prompt.Command);
    }
}
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~NoteLabelsTests"`. Expected: no compila.

- [ ] **Step 3: Implementar**

`src/Aldune.Core/NoteLabels.cs` (con BOM):

```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aldune.Core;

/// <summary>Partes de la línea de prompt de la nota (piel bash), cada una con su color.</summary>
public sealed record PromptParts(string User, string Path, string Command);

/// <summary>
/// Títulos tal como se pintan según la piel. Solo presentación: el texto de la nota no cambia
/// (<c>hoy/</c> o <c>CH1 HOY</c> se pintan, no se guardan).
/// </summary>
public static class NoteLabels
{
    // Escapes explícitos y no los caracteres literales: son invisibles en el editor.
    private const char HairSpace = ' ';
    private const char NoBreakSpace = ' ';

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static string Dock(string title, SkinTitleAdornment adornment, int channel, CultureInfo culture)
    {
        title = title.Trim();
        if (title.Length == 0) return string.Empty;

        return adornment switch
        {
            SkinTitleAdornment.Channel => $"{NoteChannels.Number(channel)} {Whitespace.Replace(title.ToUpper(culture), "_")}",
            SkinTitleAdornment.Folder => Folder(title, culture) + "/",
            SkinTitleAdornment.Uppercase => title.ToUpper(culture),
            _ => Spaced(title.ToUpper(culture).Replace(' ', NoBreakSpace)),
        };
    }

    /// <summary>El título como nombre de carpeta de terminal: minúsculas y guiones bajos.</summary>
    public static string Folder(string title, CultureInfo culture) =>
        Whitespace.Replace(title.Trim().ToLower(culture), "_");

    public static PromptParts Prompt(string userName, string notesFolder, string title, CultureInfo culture)
    {
        var user = Whitespace.Replace(userName.Trim().ToLower(culture), "_");
        if (user.Length == 0) user = "user";
        var folder = Folder(title, culture);
        return new PromptParts($"{user}@aldune", $"~/{notesFolder}", folder.Length == 0 ? "$ " : $"$ cat {folder}");
    }

    // Mayúsculas espaciadas de la pestaña de siempre (antes en NoteTabLabelConverter).
    private static string Spaced(string text)
    {
        var builder = new StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0) builder.Append(HairSpace);
            builder.Append(text[i]);
        }
        return builder.ToString();
    }
}
```

(Nota: `Spaced` recorre `char` igual que el convertidor de hoy, así que un emoji de dos `char` queda
partido por un espacio fino, como ya pasaba; no se cambia aquí.)

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Texto de la carpeta del prompt**

En `Strings.cs`, junto a los textos de la señal de sincronización:

```csharp
    /// <summary>Carpeta de la línea de prompt de la piel bash (<c>usuario@aldune:~/notas$</c>).</summary>
    public static string PromptNotesFolder => T("notes", "notas", "notizen", "notes", "notas");
```

- [ ] **Step 6: Commit**

```bash
git add src/Aldune.Core/NoteLabels.cs src/Aldune/Resources/Strings.cs tests/Aldune.Core.Tests/NoteLabelsTests.cs
git commit -m "Titulos adornados de la piel (canal, carpeta, mayusculas) y linea de prompt, en Core"
```

---

### Task 5: Piel en la capa WPF: recursos, estado observable y fuentes

**Files:**
- Create: `src/Aldune/Windowing/SkinState.cs`, `src/Aldune/Windowing/NoteChannelDisplay.cs`
- Modify: `src/Aldune/Windowing/ThemeManager.cs`, `src/Aldune/App.xaml` (estilo implícito de `Window`), `src/Aldune/App.xaml.cs` (las dos llamadas a `ThemeManager.Apply`), `src/Aldune/Windowing/SettingsWindow.xaml(.cs)` (cabecera; y donde se cambia el aspecto), `src/Aldune/Windowing/NotesManagerWindow.xaml` (cabecera), `src/Aldune/Windowing/NoteWindow.xaml` (fuentes del título y del cuerpo), `src/Aldune/Windowing/AppCoordinator.cs` (fija `NoteChannelDisplay`), `src/Aldune/Windowing/NoteTabLabelConverter.cs` (pasa a `NoteLabels.Dock`)

**Interfaces:**
- Consumes: `AppSkin` (Task 1), claves de paleta (Task 2), `NoteChannels.Of` (Task 3), `NoteLabels.Dock` (Task 4).
- Produces: `ThemeManager.ApplySkin(Application, AppSkin)`, `ThemeManager.Skin : AppSkin`, `ThemeManager.Palette : IReadOnlyDictionary<string, string>`; recursos `AldunePrimaryFont`, `AlduneNoteFont`, `AlduneNoteTitleFont` (`FontFamily`), `AlduneNoteTitleFontSize` (`double`), `AlduneTitleBarFill` (`Brush`, transparente en `Plain`); `SkinState.Current` con `Skin`, `Square`, `Card`, `Bevel`, `GradientTitleBar`, `PromptLine` (notifica con `PropertyChanged(string.Empty)`); `NoteChannelDisplay.Set(IEnumerable<Note>)`, `NoteChannelDisplay.Of(Guid) : int`.

- [ ] **Step 1: `SkinState`**

`src/Aldune/Windowing/SkinState.cs` (con BOM):

```csharp
using System.ComponentModel;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// La piel activa, para los DataTrigger de las plantillas
/// (<c>{Binding Card, Source={x:Static local:SkinState.Current}}</c>). Lo que es un recurso (fuentes,
/// degradado) lo pone <see cref="ThemeManager"/>; lo que decide qué piezas se ven (franja, onda,
/// relieve, botones cuadrados) se lee de aquí y cambia en vivo.
/// </summary>
public sealed class SkinState : INotifyPropertyChanged
{
    public static SkinState Current { get; } = new();

    private AppSkin _skin = AppSkin.Default;
    private bool _square;

    public AppSkin Skin
    {
        get => _skin;
        internal set { if (_skin == value) return; _skin = value; Raise(); }
    }

    /// <summary>Esquinas rectas (opción del bloque 1): los botones redondos del dock pasan a cuadrados.</summary>
    public bool Square
    {
        get => _square;
        internal set { if (_square == value) return; _square = value; Raise(); }
    }

    public SkinCard Card => _skin.Card;
    public bool Bevel => _skin.Border == SkinBorder.Bevel;
    public bool GradientTitleBar => _skin.TitleBar != SkinTitleBar.Plain;
    public bool PromptLine => _skin.PromptLine;

    public event PropertyChangedEventHandler? PropertyChanged;

    // string.Empty: cambian a la vez todas las propiedades derivadas.
    private void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
```

- [ ] **Step 2: `NoteChannelDisplay`**

`src/Aldune/Windowing/NoteChannelDisplay.cs` (con BOM):

```csharp
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Canal de cada nota (piel de osciloscopio) para los convertidores de XAML, que no reciben el mazo.
/// Mismo patrón que <see cref="NoteColorDisplay"/>. Sale del orden del mazo completo, sin el filtro de
/// etiqueta: filtrar no puede cambiar de canal una nota, ni hacer que su ventana y su pestaña discrepen.
/// </summary>
internal static class NoteChannelDisplay
{
    private static Dictionary<Guid, int> _positions = [];

    public static void Set(IEnumerable<Note> ordered) =>
        _positions = ordered.Select((note, index) => (note.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index);

    public static int Of(Guid noteId) => _positions.TryGetValue(noteId, out var position) ? NoteChannels.Of(position) : 0;
}
```

En `AppCoordinator`, donde se calcula la lista ordenada de notas activas que se reparte a los docks
(buscar la llamada a `SetNotes`; usar la lista **antes** de aplicar el filtro de etiqueta del dock),
llamar a `NoteChannelDisplay.Set(<esa lista>)` justo antes de repartirla. Si cada dock filtra por su
cuenta, la lista sin filtrar es la que sale de `NoteOrder`/el repositorio en ese mismo método.

- [ ] **Step 3: `ThemeManager.ApplySkin`**

En `ThemeManager.cs` (ojo: la clase tiene un método `Color(string)`, así que el tipo se escribe
`System.Windows.Media.Color`, como ya hace `Apply`):

```csharp
    private static ResourceDictionary? _skinResources;

    public static AppSkin Skin { get; private set; } = AppSkin.Default;

    /// <summary>Paleta activa, para quien calcula colores en Core (NoteFace).</summary>
    public static IReadOnlyDictionary<string, string> Palette => _palette;

    /// <summary>
    /// Pone la piel en los recursos (fuentes, tamaño del título, fondo de la barra) y en
    /// <see cref="SkinState"/>. Quien la cambie con notas abiertas llama después a
    /// AppCoordinator.RefreshNoteAppearance: las caras de las notas y las pestañas se calculan al pintarse.
    /// </summary>
    public static void ApplySkin(Application app, AppSkin skin)
    {
        var dictionary = new ResourceDictionary
        {
            ["AldunePrimaryFont"] = new FontFamily(skin.ChromeFont),
            ["AlduneNoteFont"] = new FontFamily(skin.NoteFont),
            ["AlduneNoteTitleFont"] = new FontFamily(skin.NoteTitleFont),
            ["AlduneNoteTitleFontSize"] = skin.NoteTitleFontSize,
            ["AlduneTitleBarFill"] = TitleBarFill(skin.TitleBar),
        };

        if (_skinResources is not null) app.Resources.MergedDictionaries.Remove(_skinResources);
        app.Resources.MergedDictionaries.Add(dictionary);
        _skinResources = dictionary;
        Skin = skin;
        SkinState.Current.Skin = skin;
        Changed?.Invoke();
    }

    // Plain es transparente, no el color de la barra: así las cabeceras de hoy (nota, Ajustes, gestor)
    // siguen pintando lo que tienen debajo, sea cual sea el fondo de cada ventana.
    private static Brush TitleBarFill(SkinTitleBar style)
    {
        var start = (System.Windows.Media.Color)ColorConverter.ConvertFromString(_palette["TitleBar"]);
        var end = (System.Windows.Media.Color)ColorConverter.ConvertFromString(_palette["TitleBarEnd"]);
        Brush brush = style switch
        {
            SkinTitleBar.GradientVertical => new LinearGradientBrush(start, end, 90),
            SkinTitleBar.GradientHorizontal => new LinearGradientBrush(start, end, 0),
            _ => Brushes.Transparent,
        };
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }
```

En `Apply(Application, AppearanceMode)`, justo después de `_palette = palette;`, rehacer los recursos
de la piel (el degradado sale de la paleta, y así quien solo llama a `Apply` —las sondas— también
tiene fuentes):

```csharp
        // El degradado de la barra sale de la paleta: se rehace con ella. También deja las fuentes
        // puestas a quien solo llama a Apply.
        ApplySkin(app, Skin);
```

En `ApplyShape`, junto a `IsSquare = square;`:

```csharp
        SkinState.Current.Square = square;
```

- [ ] **Step 4: Llamarla al arrancar y al cambiar de aspecto**

`grep -n "ThemeManager.Apply(" src/Aldune` — en cada llamada (arranque en `App.xaml.cs`, el cambio
de tema de Windows en `OnUserPreferenceChanged`, y donde Ajustes cambia el aspecto), justo después:

```csharp
            ThemeManager.ApplySkin(this, AppSkin.For(settings.Appearance));
```

(con la variable de ajustes y la `Application` que haya en cada sitio: `Application.Current`,
`_settings`…). Donde haya notas abiertas (Ajustes y `OnUserPreferenceChanged`), después
`_coordinator?.RefreshNoteAppearance();`. Hoy todos los aspectos dan `AppSkin.Default`, así que esto no
cambia nada visible: deja el cableado listo para el bloque 3.

- [ ] **Step 5: Fuentes como recursos**

`App.xaml`, estilo implícito de `Window` (línea ~19):

```xml
            <Setter Property="FontFamily" Value="{DynamicResource AldunePrimaryFont}" />
```

`NoteWindow.xaml`: en el `TextBlock` del marcador del título y en `TitleBox`, sustituir
`FontFamily="Ink Free, Segoe UI Variable Text" FontSize="17"` (o `FontSize="17"` en línea aparte) por:

```xml
FontFamily="{DynamicResource AlduneNoteTitleFont}" FontSize="{DynamicResource AlduneNoteTitleFontSize}"
```

y añadir `FontFamily="{DynamicResource AlduneNoteFont}"` a `TextBody` y al `TextBlock` de
`BodyPlaceholder`. Conservar los comentarios de Ink Free, añadiendo al de arriba una línea: "La fuente y
el tamaño salen de la piel (AppSkin): estos son los de la piel de siempre."

- [ ] **Step 6: Cabeceras de Ajustes y del gestor**

`SettingsWindow.xaml`: la cabecera `<Grid Margin="24,18,14,0">` pasa a ir dentro de un borde sin
margen que lleva el fondo de la barra (el margen pasa a relleno):

```xml
        <!-- CaptionHeight cubre esta franja, asi que la ventana se arrastra desde aqui. Fondo de la
             barra de la piel: transparente en la de siempre. -->
        <Border x:Name="TitleBarStrip" Background="{DynamicResource AlduneTitleBarFill}" Padding="24,18,14,0">
            <Grid>
                <!-- (contenido de la cabecera, sin cambios) -->
            </Grid>
        </Border>
```

y el `TextBlock` de `Strings.SettingsHeader` pasa de `AlduneTextStrongBrush` a `AlduneOnTitleBarBrush`
(mismo valor en los aspectos de hoy). Lo mismo en `NotesManagerWindow.xaml` con su cabecera
(`<Grid Grid.Row="0" Margin="20,18,14,0">`: el `Grid.Row` pasa al `Border`) y su título
`ManageNotesTitle`. El subtítulo y los botones de esas cabeceras no se tocan (bloque 3).

- [ ] **Step 7: La pestaña usa `NoteLabels.Dock`**

`NoteTabLabelConverter.Convert` pasa a:

```csharp
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var note = value as Note;
        var text = note?.Text ?? value as string ?? string.Empty;
        var title = note is { IsProtected: true }
            ? Strings.ProtectedNote
            : NoteTitleHelper.GetTitle(text);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        // Mayúsculas espaciadas de siempre o el adorno de la piel (NoteLabels, en Core, con tests).
        int channel = note is null ? 0 : NoteChannelDisplay.Of(note.Id);
        return NoteLabels.Dock(title, ThemeManager.Skin.Adornment, channel, culture);
    }
```

y se quitan las constantes `HairSpace`/`NoBreakSpace` y el `using System.Text` si quedan sin uso.

- [ ] **Step 8: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos (ni de XAML).

- [ ] **Step 9: Commit**

```bash
git add -A src
git commit -m "Piel en WPF: ThemeManager.ApplySkin (fuentes, barra de titulo) y SkinState para las plantillas; canal de cada nota; sin cambios visibles"
```

---

### Task 6: El dock según la piel

**Files:**
- Create: `src/Aldune/Windowing/BevelEdge.cs`, `src/Aldune/Windowing/NoteFaceConverter.cs`, `src/Aldune/Windowing/NoteWaveConverter.cs`
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml`, `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (`ApplyLeftEdgeTabShape`, `ApplyTopBottomTabShape`)

**Interfaces:**
- Consumes: `NoteFace.For` (Task 3), `NoteChannels.WavePath` (Task 3), `SkinState`, `ThemeManager.Skin/Palette`, `NoteChannelDisplay`, `NoteColorDisplay.Uniform` (Task 5 y bloque 1).
- Produces: `BevelEdge` (elemento que pinta el relieve: `Sunken`, `OnlyWhenSquare`, `EdgeThickness`), `NoteFaceConverter` (parámetro `Face`|`Ink`|`Rim`|`Label`|`Snippet`|`Accent`|`Pill`|`PillRim`|`PillRimThickness`), `NoteWaveConverter`. La Task 7 reutiliza `BevelEdge`.

- [ ] **Step 1: `BevelEdge`**

`src/Aldune/Windowing/BevelEdge.cs` (con BOM):

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// Relieve de Windows 95 sobre una pieza (tarjeta, cápsula, pie, botón, ventana): claro arriba e
/// izquierda, oscuro abajo y derecha (al revés si está hundida). Solo pinta con la piel de bordes
/// <c>Bevel</c>; con la de siempre no dibuja nada. Va encima de la pieza y no recibe el ratón. Un
/// elemento propio y no dos Border superpuestos: un Border tiene un solo color de borde.
/// </summary>
public sealed class BevelEdge : FrameworkElement
{
    public static readonly DependencyProperty SunkenProperty = DependencyProperty.Register(
        nameof(Sunken), typeof(bool), typeof(BevelEdge), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Para piezas redondas (los botones del pie): el relieve recto solo cuadra cuando son cuadradas.</summary>
    public static readonly DependencyProperty OnlyWhenSquareProperty = DependencyProperty.Register(
        nameof(OnlyWhenSquare), typeof(bool), typeof(BevelEdge), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EdgeThicknessProperty = DependencyProperty.Register(
        nameof(EdgeThickness), typeof(double), typeof(BevelEdge), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool Sunken { get => (bool)GetValue(SunkenProperty); set => SetValue(SunkenProperty, value); }
    public bool OnlyWhenSquare { get => (bool)GetValue(OnlyWhenSquareProperty); set => SetValue(OnlyWhenSquareProperty, value); }
    public double EdgeThickness { get => (double)GetValue(EdgeThicknessProperty); set => SetValue(EdgeThicknessProperty, value); }

    public BevelEdge()
    {
        IsHitTestVisible = false;
        // Suscripción solo mientras está en pantalla: SkinState vive toda la app y retendría cada
        // pestaña que se ha pintado alguna vez.
        Loaded += (_, _) =>
        {
            SkinState.Current.PropertyChanged += OnSkinChanged;
            ThemeManager.Changed += InvalidateVisual;
        };
        Unloaded += (_, _) =>
        {
            SkinState.Current.PropertyChanged -= OnSkinChanged;
            ThemeManager.Changed -= InvalidateVisual;
        };
    }

    private void OnSkinChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var state = SkinState.Current;
        if (!state.Bevel || (OnlyWhenSquare && !state.Square)) return;
        if (TryFindResource("AlduneBevelLightBrush") is not Brush light ||
            TryFindResource("AlduneBevelDarkBrush") is not Brush dark) return;
        if (Sunken) (light, dark) = (dark, light);

        double t = EdgeThickness, w = ActualWidth, h = ActualHeight;
        if (w < 2 * t || h < 2 * t) return;
        dc.DrawRectangle(light, null, new Rect(0, 0, w, t));
        dc.DrawRectangle(light, null, new Rect(0, 0, t, h));
        dc.DrawRectangle(dark, null, new Rect(0, h - t, w, t));
        dc.DrawRectangle(dark, null, new Rect(w - t, 0, t, h));
    }
}
```

- [ ] **Step 2: Convertidores**

`src/Aldune/Windowing/NoteFaceConverter.cs` (con BOM):

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Nota → uno de sus colores según la piel (<see cref="NoteFace"/>). El parámetro elige cuál: Face,
/// Ink, Rim, Label, Snippet, Accent, Pill, PillRim; PillRimThickness da el grosor del contorno de la
/// cápsula (0 sin contorno). Se reevalúa al rehacer el mazo (RefreshAll), que es lo que hace quien
/// cambia la piel o el color único.
/// </summary>
public sealed class NoteFaceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Note note) return Brushes.Transparent;
        var face = NoteFace.For(ThemeManager.Skin.Card, note.Color, NoteColorDisplay.Uniform,
            NoteChannelDisplay.Of(note.Id), ThemeManager.Palette);

        if (parameter as string == "PillRimThickness")
            return face.PillRim is null ? new Thickness(0) : new Thickness(1);

        string? hex = (parameter as string) switch
        {
            "Face" => face.Face,
            "Ink" => face.Ink,
            "Rim" => face.Rim,
            "Label" => face.Label,
            "Snippet" => face.Snippet,
            "Accent" => face.Accent,
            "Pill" => face.Pill,
            "PillRim" => face.PillRim,
            _ => null,
        };
        if (hex is null) return Brushes.Transparent;
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

Antes de fijar `PillRimThickness` en 1, mirar qué grosor devuelve hoy `NoteRestOutlineConverter` con
`ConverterParameter=Thickness` y usar el mismo (la cápsula de la piel de siempre no puede cambiar).

`src/Aldune/Windowing/NoteWaveConverter.cs` (con BOM):

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>Nota → forma de onda de su canal (piel de osciloscopio). Cuatro geometrías congeladas.</summary>
public sealed class NoteWaveConverter : IValueConverter
{
    private static readonly Geometry[] Waves = Enumerable.Range(0, NoteChannels.Count)
        .Select(channel =>
        {
            var geometry = Geometry.Parse(NoteChannels.WavePath(channel));
            geometry.Freeze();
            return geometry;
        })
        .ToArray();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Note note ? Waves[NoteChannelDisplay.Of(note.Id)] : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 3: Pestañas**

En `EdgeDockWindow.xaml`:

- Espacio de nombres de Core en la raíz: `xmlns:core="clr-namespace:Aldune.Core;assembly=Aldune.Core"`.
- En `Window.Resources`: `<local:NoteFaceConverter x:Key="NoteFaceConverter" />` y `<local:NoteWaveConverter x:Key="NoteWaveConverter" />`.
- En el `Button` del `TabsList`: `Background="{Binding Converter={StaticResource NoteFaceConverter}, ConverterParameter=Face}"` (sustituye al de `NoteDisplayColorConverter`).
- En la plantilla `NoteTabButtonStyle`:
  - `CardBorder.BorderBrush` → `{Binding Converter={StaticResource NoteFaceConverter}, ConverterParameter=Rim}`.
  - Título → `Foreground="{Binding Converter={StaticResource NoteFaceConverter}, ConverterParameter=Label}"`; `SnippetText` y `ReminderBadge` → `ConverterParameter=Snippet` y `ConverterParameter=Label`.
  - Después de `SheenBorder`, la franja y la onda:

```xml
                            <!-- Franja de la piel Stripe (bash) o del canal (Tinted), en el lado interior
                                 de la pestaña. Espejada por código con el dock a la izquierda, arriba o
                                 abajo (ApplyLeftEdgeTabShape / ApplyTopBottomTabShape). -->
                            <Border x:Name="StripeBar" Width="3" HorizontalAlignment="Left"
                                    Background="{Binding Converter={StaticResource NoteFaceConverter}, ConverterParameter=Accent}"
                                    Visibility="Collapsed" />
                            <!-- Forma de onda del canal, abajo: arriba está el aviso de recordatorio, y al
                                 apilarse las pestañas solo queda a la vista la franja de arriba, que es la
                                 del título. -->
                            <Path x:Name="Waveform" Width="54" Height="18"
                                  HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,22,6"
                                  Data="{Binding Converter={StaticResource NoteWaveConverter}}"
                                  Stroke="{Binding Converter={StaticResource NoteFaceConverter}, ConverterParameter=Accent}"
                                  StrokeThickness="1.5" Visibility="Collapsed" />
```

  - Antes de `HoverOverlay`: `<local:BevelEdge x:Name="CardBevel" />`.
  - En `ControlTemplate.Triggers`:

```xml
                            <DataTrigger Binding="{Binding Card, Source={x:Static local:SkinState.Current}}" Value="{x:Static core:SkinCard.Stripe}">
                                <Setter TargetName="StripeBar" Property="Visibility" Value="Visible" />
                                <Setter TargetName="SheenBorder" Property="Visibility" Value="Collapsed" />
                            </DataTrigger>
                            <DataTrigger Binding="{Binding Card, Source={x:Static local:SkinState.Current}}" Value="{x:Static core:SkinCard.Tinted}">
                                <Setter TargetName="StripeBar" Property="Visibility" Value="Visible" />
                                <Setter TargetName="Waveform" Property="Visibility" Value="Visible" />
                                <Setter TargetName="SheenBorder" Property="Visibility" Value="Collapsed" />
                            </DataTrigger>
```

- En `EdgeDockWindow.xaml.cs`, en `ApplyLeftEdgeTabShape` (la pestaña pegada al borde izquierdo): la
  franja pasa al lado interior (`StripeBar.HorizontalAlignment = HorizontalAlignment.Right`) y la onda
  al otro (`Waveform.HorizontalAlignment = HorizontalAlignment.Left; Waveform.Margin = new Thickness(22, 0, 0, 6)`).
  En `ApplyTopBottomTabShape`: la franja horizontal en el lado interior (dock arriba → abajo; dock abajo
  → arriba): `StripeBar.Width = double.NaN; StripeBar.Height = 3; StripeBar.HorizontalAlignment = HorizontalAlignment.Stretch; StripeBar.VerticalAlignment = <Bottom|Top>`.
  Los elementos se buscan en la plantilla como ya hacen esos métodos con `CardBorder` (`FindName` /
  `Template.FindName`).

- [ ] **Step 4: Tira de reposo, pie y botones del pie**

- Plantillas de la cápsula (`RestList` y `HorizontalRestDashTemplate`): el `Border` pasa a
  `Background=…ConverterParameter=Pill`, `BorderBrush=…ConverterParameter=PillRim`,
  `BorderThickness=…ConverterParameter=PillRimThickness` (todos con `{Binding Converter={StaticResource NoteFaceConverter}, …}`),
  y se envuelve en un `Grid` con `<local:BevelEdge EdgeThickness="1" />` encima.
- `RestStrip` y `FooterBorder`: su contenido se envuelve en un `Grid` con un `<local:BevelEdge />` encima
  que cubre el borde entero (`Margin` negativo igual al `Padding` del `Border`: `-5,-5,-5,0` en
  `RestStrip` según su `Padding="0,5,0,0"` → usar `Margin="0,-5,0,0"`; `-7` en `FooterBorder`).
- `CircularIconButtonStyle`: con esquinas rectas los botones redondos pasan a cuadrados (spec,
  sección 5). Junto a la `Ellipse` `Bg`, un `Rectangle` con el mismo relleno, trazo y sombra, oculto, y
  el relieve:

```xml
                        <Grid>
                            <Ellipse x:Name="Bg" Fill="{TemplateBinding Background}"
                                     Stroke="#8FA79E90" StrokeThickness="1"
                                     Effect="{StaticResource ButtonShadow}" />
                            <Rectangle x:Name="SquareBg" Fill="{TemplateBinding Background}"
                                       Stroke="#8FA79E90" StrokeThickness="1"
                                       Effect="{StaticResource ButtonShadow}" Visibility="Collapsed" />
                            <local:BevelEdge OnlyWhenSquare="True" />
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Grid>
```

  y en sus disparadores: cada `Setter` de `Bg.Fill` (ratón encima, pulsado) se duplica para `SquareBg`,
  más:

```xml
                            <DataTrigger Binding="{Binding Square, Source={x:Static local:SkinState.Current}}" Value="True">
                                <Setter TargetName="Bg" Property="Visibility" Value="Collapsed" />
                                <Setter TargetName="SquareBg" Property="Visibility" Value="Visible" />
                            </DataTrigger>
```

  (El trazo `#8FA79E90` ya estaba escrito así; no se añade ningún hex nuevo.)

- [ ] **Step 5: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 6: Commit**

```bash
git add -A src
git commit -m "Dock segun la piel: pestanas llenas, con franja o con tinte y onda de canal; relieve; botones cuadrados con esquinas rectas"
```

---

### Task 7: La nota según la piel

**Files:**
- Modify: `src/Aldune/Windowing/NoteWindow.xaml`, `src/Aldune/Windowing/NoteWindow.xaml.cs`

**Interfaces:**
- Consumes: `NoteFace.For`, `NoteLabels.Prompt`, `NoteLabels.Dock`… (Tasks 3–4), `NoteChannels.Number`, `ThemeManager.Skin/Palette`, `NoteChannelDisplay`, `BevelEdge` (Task 6), `Strings.PromptNotesFolder`.
- Produces: nada nuevo para otras tareas.

- [ ] **Step 1: XAML**

- Filas del `Grid` raíz: `40` / `Auto` (nueva, prompt) / `*` / `Auto`. Todo lo que hoy está en
  `Grid.Row="1"` pasa a `2` y lo de `Grid.Row="2"` (`AllDoneBar`, `SyncSignalText`…) a `3`. Antes de
  cambiar, `grep -n "Grid.Row" src/Aldune/Windowing/NoteWindow.xaml` y revisar en el code-behind
  `FitHeightToContent` y cualquier cálculo que dependa de las filas.
- `Header`: `Background="{DynamicResource AlduneTitleBarFill}"` (transparente en la piel de siempre).
- Recurso nuevo junto a `NoteInkBrush`: `<SolidColorBrush x:Key="NoteHeaderInkBrush" Color="#1E1A14" />`
  (mismo valor inicial que `NoteInkBrush`; lo fija `ApplyColor`). `CloseButtonStyle.Foreground`,
  `DragHandle.Foreground` y `TitlePlaceholderStyle.Foreground` pasan a `NoteHeaderInkBrush`.
- El `Grid Margin="32,0,126,0"` del título se envuelve en un `DockPanel` con el mismo margen, y delante
  el prefijo de canal:

```xml
            <DockPanel Margin="32,0,126,0" VerticalAlignment="Center">
                <!-- Prefijo de canal de la piel de osciloscopio (CH1…): solo se pinta, el título no cambia. -->
                <TextBlock x:Name="ChannelPrefix" DockPanel.Dock="Left" Margin="4,0,6,0" VerticalAlignment="Center"
                           FontFamily="{DynamicResource AlduneNoteTitleFont}" FontSize="{DynamicResource AlduneNoteTitleFontSize}"
                           FontWeight="Bold" Visibility="Collapsed" />
                <Grid>
                    <!-- (marcador y TitleBox, sin cambios salvo las fuentes de la Task 5) -->
                </Grid>
            </DockPanel>
```

- Fila 1, la línea de prompt:

```xml
        <!-- Línea de prompt de la piel bash (usuario@aldune:~/notas$ cat hoy). Fila propia y no dentro
             del cuadro de texto: no es texto de la nota y no se puede editar ni seleccionar con él. -->
        <TextBlock x:Name="PromptLine" Grid.Row="1" Margin="20,10,20,0" FontSize="14"
                   FontFamily="{DynamicResource AlduneNoteFont}" TextTrimming="CharacterEllipsis"
                   Visibility="Collapsed">
            <Run x:Name="PromptUser" FontWeight="Bold" /><Run Text=":" /><Run x:Name="PromptPath" FontWeight="Bold" /><Run x:Name="PromptCommand" />
        </TextBlock>
```

- Franja de la cara (piel `Stripe`), al final del `Grid` raíz, y el relieve de la ventana encima de todo:

```xml
        <!-- Franja de la piel Stripe: el color de la nota va solo aquí (ver NoteFace). -->
        <Border x:Name="FaceStripe" Grid.Row="1" Grid.RowSpan="3" Width="3" HorizontalAlignment="Left"
                Visibility="Collapsed" IsHitTestVisible="False" />
        <local:BevelEdge Grid.RowSpan="4" />
```

- [ ] **Step 2: `ApplyColor` con `NoteFace`**

`ApplyColor(string color)` pasa a calcular la cara con la piel (sustituye a la línea
`color = NoteColorDisplay.Resolve(color);` y a las derivaciones sueltas; lo demás —hover, selección,
troquelado— se queda, sobre `face`):

```csharp
    private void ApplyColor(string color)
    {
        var skin = ThemeManager.Skin;
        int channel = NoteChannelDisplay.Of(_note.Id);
        var face = NoteFace.For(skin.Card, color, NoteColorDisplay.Uniform, channel, ThemeManager.Palette);
        var brush = BrushOf(face.Face);
        var rim = BrushOf(face.Rim);
        var ink = BrushOf(face.Ink);

        Background = brush;
        // Con retícula (osciloscopio) el cuadro de texto deja ver la cuadrícula del fondo.
        TextBody.Background = skin.NoteGrid ? Brushes.Transparent : brush;
        BodyHost.Background = skin.NoteGrid ? GridBrush(face.Ink) : null;
        Resources["NoteInkBrush"] = ink;
        Resources["NotePlaceholderBrush"] = ink;
        // (hover y selección como hoy, con face.Face en vez de color y face.Ink en vez de foreground)
        ...
        WindowRim.BorderBrush = rim;
        Perforation.Stroke = rim;

        // Cabecera: en degradado lleva el texto de la barra y pierde el troquelado (la barra ya separa);
        // con tinte de canal, el título va en el color del canal.
        Brush headerInk = skin.TitleBar != SkinTitleBar.Plain
            ? (Brush)FindResource("AlduneOnTitleBarBrush")
            : skin.Card == SkinCard.Tinted ? BrushOf(face.Label) : ink;
        Resources["NoteHeaderInkBrush"] = headerInk;
        TitleBox.Foreground = TitleBox.CaretBrush = headerInk;
        Perforation.Visibility = skin.TitleBar == SkinTitleBar.Plain ? Visibility.Visible : Visibility.Collapsed;

        FaceStripe.Visibility = skin.Card == SkinCard.Stripe ? Visibility.Visible : Visibility.Collapsed;
        if (face.Accent is not null) FaceStripe.Background = BrushOf(face.Accent);

        ChannelPrefix.Visibility = skin.Adornment == SkinTitleAdornment.Channel ? Visibility.Visible : Visibility.Collapsed;
        ChannelPrefix.Text = NoteChannels.Number(channel);
        ChannelPrefix.Foreground = headerInk;

        UpdatePromptLine(face);
    }

    private static Brush BrushOf(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
```

(`TextBody.Foreground`, `CaretBrush`, `SelectionBrush`, `SelectionTextBrush` y `Foreground` siguen como
hoy con `ink`/`brush`; solo el título pasa a `headerInk`. Respetar el comentario de la selección.)

Retícula y prompt:

```csharp
    // Retícula de osciloscopio: líneas de 1 px cada 24, de la tinta muy translúcida, para que se lea
    // como fondo y no compita con el texto.
    private static Brush GridBrush(string inkHex)
    {
        var ink = (System.Windows.Media.Color)ColorConverter.ConvertFromString(inkHex);
        var pen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x26, ink.R, ink.G, ink.B)), 1);
        var lines = new GeometryGroup();
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(24, 0)));
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 24)));
        var brush = new DrawingBrush(new GeometryDrawing(null, pen, lines))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 24, 24),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 24, 24),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    // Con franja (bash) la nota es la terminal, así que usuario y ruta llevan sus colores de la
    // paleta; sobre una nota de color, la tinta de la nota (los de la paleta podrían no leerse).
    private void UpdatePromptLine(NoteFaceColors face)
    {
        var skin = ThemeManager.Skin;
        PromptLine.Visibility = skin.PromptLine ? Visibility.Visible : Visibility.Collapsed;
        if (!skin.PromptLine) return;

        var parts = NoteLabels.Prompt(Environment.UserName, Strings.PromptNotesFolder, TitleBox.Text,
            System.Globalization.CultureInfo.CurrentCulture);
        PromptUser.Text = parts.User;
        PromptPath.Text = parts.Path;
        PromptCommand.Text = parts.Command;
        var ink = BrushOf(face.Ink);
        bool terminal = skin.Card == SkinCard.Stripe;
        PromptUser.Foreground = terminal ? (Brush)FindResource("AldunePromptUserBrush") : ink;
        PromptPath.Foreground = terminal ? (Brush)FindResource("AldunePromptPathBrush") : ink;
        PromptLine.Foreground = ink;
    }
```

El prompt depende del título: en el manejador de `TextChanged` de `TitleBox` (o donde ya se reacciona a
que cambie el título; si no hay, añadir `TitleBox.TextChanged += (_, _) => UpdatePromptLine(CurrentFace());`
en el constructor con un `CurrentFace()` que repita la llamada a `NoteFace.For` de `ApplyColor`), refrescar
`UpdatePromptLine`.

- [ ] **Step 3: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 4: Commit**

```bash
git add -A src
git commit -m "La nota segun la piel: cara con franja o tinte de canal, barra en degradado, prefijo de canal, linea de prompt, reticula y relieve"
```

---

### Task 8: Pendientes del bloque 1

**Files:**
- Modify: `src/Aldune.Core/NotesRepository.cs`, `src/Aldune/Windowing/AppCoordinator.cs` (`SyncSignalFor`), `src/Aldune/Windowing/NoteWindow.xaml.cs` (repintar la señal), `src/Aldune/Windowing/SettingsWindow.xaml(.cs)` (muestras del color único)
- Test: `tests/Aldune.Core.Tests/NotesRepositorySyncLookupTests.cs`

**Interfaces:**
- Produces: `NotesRepository.HasSyncConflict(Guid noteId) : bool`, `NotesRepository.GetSyncBase(Guid noteId) : SyncBaseVersion?`.

- [ ] **Step 1: Tests de las consultas por nota**

`tests/Aldune.Core.Tests/NotesRepositorySyncLookupTests.cs` (con BOM; copiar el arranque de
repositorio temporal de `ThemeMigrationsTests`: `ContentCipher`, `NotesDatabase`, `IDisposable`):

```csharp
using System.Security.Cryptography;
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class NotesRepositorySyncLookupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-sync-lookup-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));
    private readonly NotesRepository _repository;

    public NotesRepositorySyncLookupTests()
    {
        Directory.CreateDirectory(_dir);
        _repository = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "notes.db")), _cipher);
    }

    public void Dispose()
    {
        _cipher.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void GetSyncBase_ReturnsOnlyThatNotesBase()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
        _repository.SetSyncBases([new(a, new SyncBaseVersion(at, "pc"))]);

        Assert.Equal(new SyncBaseVersion(at, "pc"), _repository.GetSyncBase(a));
        Assert.Null(_repository.GetSyncBase(b));
    }

    [Fact]
    public void HasSyncConflict_IsFalseWithoutConflicts()
    {
        Assert.False(_repository.HasSyncConflict(Guid.NewGuid()));
    }
}
```

El caso "con conflicto" ya lo cubre la integración: añadir a `SyncConflictTests`, al final de
`ConcurrentEdits_UseTheNewestVersionAndConverge` (antes de restaurar el conflicto):

```csharp
        Assert.True(_deviceA.Repository.HasSyncConflict(note.Id));
        Assert.False(_deviceB.Repository.HasSyncConflict(note.Id));
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~SyncLookup|FullyQualifiedName~ConcurrentEdits"`. Expected: no compila.

- [ ] **Step 3: Implementar** (en `NotesRepository`, junto a `GetSyncBases` y `GetSyncConflicts`)

```csharp
    /// <summary>La base de una sola nota, sin leer las de todas (la señal de sincronización la pide en
    /// cada autoguardado).</summary>
    public SyncBaseVersion? GetSyncBase(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT UpdatedAt, DeviceId FROM SyncBase WHERE NoteId = $id;";
        command.Parameters.AddWithValue("$id", noteId.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? new SyncBaseVersion(ParseDate(reader["UpdatedAt"]), reader.GetString(1)) : null;
    }

    /// <summary>¿Está la nota en la cola de conflictos? Sin descifrar la cola: GetSyncConflicts descifra
    /// cada versión perdedora.</summary>
    public bool HasSyncConflict(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM SyncConflict WHERE NoteId = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", noteId.ToString());
        return command.ExecuteScalar() is not null;
    }
```

(Comprobar que la columna se llama `NoteId` en `SyncConflict` y cómo se guarda el Guid —`ToString()`
como en `SyncBase`— mirando `SaveSyncConflict`.)

`AppCoordinator.SyncSignalFor`:

```csharp
        bool hasConflict = _repository.HasSyncConflict(noteId);
        return NoteSyncSignal.For(note, _settings, _repository.GetSyncBase(noteId), hasConflict);
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Repintar la señal tras cambiar color o etiquetas**

En `NoteWindow.xaml.cs`, al final de `OnColorSwatchClick` y `OnCustomColorClick` (tras
`_coordinator.RefreshAll()`), y donde la ventana aplica un cambio de etiquetas de su nota (buscar el
uso de `TagAssignmentPanel` o la llamada al repositorio que guarda etiquetas), llamar a
`UpdateSyncSignal();`: el cambio pone la nota en pendiente y la señal tiene que decirlo ya, no en la
próxima sincronización.

- [ ] **Step 6: Elegir el color único entre los del tema**

Spec, sección 3: "El color se elige entre los del tema de notas activo o libre". En
`SettingsWindow.xaml`, debajo del `StackPanel` horizontal de `UniformColorSwatch`/`UniformColorButton`:

```xml
                        <StackPanel x:Name="UniformSwatches" Margin="27,8,0,0" />
```

En `SettingsWindow.xaml.cs`, en `UpdateUniformColorUi()`:

```csharp
        UniformSwatches.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) NoteSwatchPanel.Fill(UniformSwatches, ActiveTheme, _settings.UniformNoteColor, OnUniformSwatchClick);
```

(con el `ActiveTheme` que use ya la ventana —el manejador de la casilla lo usa—) y:

```csharp
    private void OnUniformSwatchClick(object sender, MouseButtonEventArgs e)
    {
        _settings.UniformNoteColor = (string)((Border)sender).Tag;
        ApplyUniformColor();
    }
```

(`using System.Windows.Input;` y `System.Windows.Controls` si faltan.)

- [ ] **Step 7: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde.

- [ ] **Step 8: Commit**

```bash
git add -A src tests
git commit -m "Pendientes del bloque 1: la senal consulta solo su nota, se repinta tras cambiar color o etiquetas, y el color unico se elige entre los del tema"
```

---

### Task 9: Verificación con sonda y cierre

**Files:**
- Modify: `docs/STATUS.md`
- Sonda desechable fuera del repo (técnica en `docs/WPF_PROBES.md`)

- [ ] **Step 1: Línea base** — En un worktree temporal fuera del repo con el commit anterior al bloque 2
  (`git worktree add <scratchpad>/base <commit>`, el padre del commit de la Task 1), compilar y, con la
  misma sonda, guardar capturas en aspecto Oscuro y Claro de: una nota clara y una oscura, el dock
  desplegado a la derecha, Ajustes y el gestor. Quitar el worktree al terminar (`git worktree remove`).

- [ ] **Step 2: Sonda en la versión nueva** (sin teclas ni ratón; base de datos temporal; el dock con
  `KeepDockOpen = true` en un monitor real):
  1. Mismas capturas que la línea base, con la piel por defecto. **Comparación automática píxel a
     píxel** con la línea base: diferencia 0 o, si la hay, recortes de la zona y explicación (Review Focus 1).
  2. `ThemeManager.ApplySkin(app, AppSkin.Default with { Card = SkinCard.Stripe, Adornment = SkinTitleAdornment.Folder, PromptLine = true, ChromeFont = "Cascadia Mono", NoteFont = "Cascadia Mono", NoteTitleFont = "Cascadia Mono", NoteTitleFontSize = 14 })` + `coord.RefreshNoteAppearance()` **con la nota y el dock ya abiertos** (Review Focus 2): franja en nota, pestañas y cápsulas; títulos `hoy/`; línea de prompt.
  3. Igual con `Card = SkinCard.Tinted, Adornment = SkinTitleAdornment.Channel, NoteGrid = true`: tintes por canal, `CH1…CH4` cíclicos, ondas, retícula.
  4. `Border = SkinBorder.Bevel, TitleBar = SkinTitleBar.GradientHorizontal` con `ThemeManager.ApplyShape(app, true)`: relieve en tarjetas, cápsulas, pie, botones cuadrados y ventana de la nota. En Oscuro `TitleBar` y `TitleBarEnd` valen el fondo, así que para ver la barra la sonda añade **después** de `ApplySkin` un diccionario propio a `app.Resources.MergedDictionaries` (el último añadido gana) con `AlduneTitleBarFill` = un degradado de prueba (#1C4FA8 → #6B8FD0) y `AlduneOnTitleBarBrush` = blanco: la cabecera de la nota, Ajustes y el gestor tienen que tomarlos.
  5. Los estados 2–4 con el dock a la izquierda y arriba (Review Focus 4).
  6. `NoteColorDisplay.Uniform = "#33363A"` con `Stripe` y con `Tinted` (Review Focus 3).
  7. Vuelta a `AppSkin.Default` y `ApplyShape(app, false)`: igual que el estado 1.

  Comprobaciones automáticas: en 2, `FaceStripe.Visibility == Visible` y el `Background` de la nota es
  el `Ground` de la paleta; en 3, los `Accent` de las cuatro primeras pestañas son `Channel1…4` y la
  quinta repite `Channel1`; en 7, sin diferencias con el estado 1. Revisar las capturas a ojo contra las
  maquetas (forma, no colores: los colores de los aspectos son del bloque 3).

- [ ] **Step 3: Smoke test** — avisar al usuario (mueve el ratón) y, con su "ok": `dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug`. Expected: verde.

- [ ] **Step 4: STATUS.md** — sección "## 2026-10-0X: bloque 2 de aspectos (piel)": qué es la piel y qué no
  cambia, las desviaciones de la spec de este plan, lo que queda para el bloque 3 (estilo de Fósforo,
  brillo de XP, cabeceras de Ajustes/gestor y diálogos), los pendientes del bloque 1 cerrados y el
  número de tests.

- [ ] **Step 5: Commit**

```bash
git add docs/STATUS.md
git commit -m "Docs: estado tras el bloque 2 de aspectos"
```

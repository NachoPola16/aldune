# Temas de notas — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Temas de color para las notas (Clásico, Sereno, Grafito y temas propios), con tono de las
notas nuevas, regla de asignación de color, aplicar a las notas existentes y pestañas oscuras con
"filo superior".

**Architecture:** Toda la lógica de color vive en `Aldune.Core`, pura y con tests: conversión
OKLCH, color derivado (borde y etiqueta), catálogo de temas y asignador. La app WPF solo lee el tema
activo de `AppSettings` y pinta. El formato de las notas y el de sync no cambian: cada nota sigue
guardando un `#RRGGBB`.

**Tech Stack:** .NET 10, WPF, xUnit, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-09-23-aldune-temas-design.md`

## Global Constraints

- **Nada de commits por tarea.** El usuario pidió juntar todo hasta la 1.0 (memoria
  `aldune-1-0-batching`). Cada tarea termina con build y tests en verde, sin `git commit`.
- Textos de interfaz siempre con `Strings.T("en", "es")` en `src/Aldune/Resources/Strings.cs`.
- Comentarios en español, con la densidad y el tono del código que los rodea.
- Hex siempre `#RRGGBB` en mayúsculas cuando lo genera la app; al comparar, sin distinguir
  mayúsculas.
- El tema de fábrica sigue siendo **Clásico**. Un `settings.json` anterior carga sin migración y la
  app se ve igual que antes hasta que el usuario cambia algo.
- Los 6 colores de Clásico conservan exactamente sus bordes y etiquetas actuales.
- Toda etiqueta derivada tiene contraste ≥ 4,5:1 contra su cara.
- Build: `dotnet build Aldune.slnx -c Debug` sin errores ni avisos nuevos. Tests:
  `dotnet test Aldune.slnx --no-build`.

## Review Focus

1. **Notas con colores de versiones antiguas o personalizados** (no son de ningún tema): tienen que
   seguir teniendo borde y etiqueta legibles. Test en la tarea 2.
2. **`settings.json` editado a mano o de una versión anterior**: tema activo con id inexistente,
   colores no válidos o temas propios vacíos no pueden tumbar la app. Tests en la tarea 3.
3. **Temas con uno o dos colores** con "rotar sin repetir el de al lado": no puede entrar en bucle
   ni devolver null. Test en la tarea 4.
4. **Ventanas de nota abiertas al aplicar un tema** o al cambiar el tema: deben repintarse con el
   color nuevo, y el menú de colores tiene que mostrar los colores del tema actual. Verificación
   en las tareas 7 y 8.
5. **Dock filtrado por etiqueta**: los "vecinos" de una nota nueva son los que se ven en esa vista,
   no los de todas las notas. Cubierto en la tarea 5 (`NotesForCurrentDockView`).

---

## Mapa de ficheros

| Fichero | Responsabilidad |
|---|---|
| `src/Aldune.Core/OklchColor.cs` (nuevo) | Hex ⇄ OKLCH, ajuste al gamut, distancia OKLab. |
| `src/Aldune.Core/NoteColorDerivation.cs` (nuevo) | ¿Es oscura?, borde y etiqueta de cualquier cara. |
| `src/Aldune.Core/NoteTheme.cs` (nuevo) | El modelo de tema. |
| `src/Aldune.Core/NoteThemes.cs` (nuevo) | Temas de serie, resolver el activo, sanear los propios, duplicar. |
| `src/Aldune.Core/NoteColorAssigner.cs` (nuevo) | Tono, regla y color de una nota nueva. |
| `src/Aldune.Core/AppSettings.cs` | Campos nuevos. |
| `src/Aldune.Core/SettingsService.cs` | Sanear temas propios al cargar. |
| `src/Aldune/Windowing/NoteColorPalette.cs` | Pierde la paleta fija; delega en Core. |
| `src/Aldune/Windowing/NoteSwatchPanel.cs` (nuevo) | Pastillas de color de un tema; la usan nota, dock y Ajustes. |
| `src/Aldune/Windowing/AppCoordinator.cs` | Tema activo, color de nota nueva, aplicar a existentes. |
| `src/Aldune/Windowing/EdgeDockWindow.xaml(.cs)` | Asignación, pastillas del menú de pestaña, acabado oscuro. |
| `src/Aldune/Windowing/NoteWindow.xaml(.cs)` | Pastillas del menú ⋯, hover, repintado externo. |
| `src/Aldune/Windowing/SettingsWindow.xaml(.cs)` | Sección "Temas". |
| `src/Aldune/Windowing/ThemeEditorWindow.xaml(.cs)` (nuevo) | Crear y editar temas propios. |
| `src/Aldune/App.xaml` | Estilos de botón y caja de texto de diálogo, compartidos. |
| `src/Aldune/Windowing/CustomColorWindow.xaml` | Usa los estilos compartidos. |
| `src/Aldune/Resources/Strings.cs` | Textos. |

---

### Task 1: Conversión OKLCH

**Files:**
- Create: `src/Aldune.Core/OklchColor.cs`
- Test: `tests/Aldune.Core.Tests/OklchColorTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct OklchColor(double L, double C, double H)`
  - `public static bool TryFromHex(string? hex, out OklchColor color)`
  - `public string ToHex()`: `#RRGGBB` en mayúsculas; si se sale de sRGB, reduce C hasta que cabe.
  - `public double DistanceTo(OklchColor other)`: distancia euclídea en OKLab.

- [ ] **Step 1: Test que falla**

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class OklchColorTests
{
    [Theory]
    [InlineData("#EBD38B", 0.871, 0.095, 92)]  // citron, documentado en NoteColorPalette
    [InlineData("#262F47", 0.308, 0.045, 268)] // tinta del tema Sereno
    public void TryFromHex_MatchesTheValuesThePaletteWasDesignedWith(string hex, double l, double c, double h)
    {
        Assert.True(OklchColor.TryFromHex(hex, out var color));
        Assert.Equal(l, color.L, 2);
        Assert.Equal(c, color.C, 2);
        // Tolerancia de matiz holgada: con poco croma, redondear a hex mueve el matiz unos grados.
        Assert.InRange(color.H, h - 5, h + 5);
    }

    [Theory]
    [InlineData("#EBD38B")]
    [InlineData("#262F47")]
    [InlineData("#E8E8E8")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void RoundTrip_GivesTheSameHex(string hex)
    {
        Assert.True(OklchColor.TryFromHex(hex, out var color));
        Assert.Equal(hex, color.ToHex());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EBD38B")]
    [InlineData("#EBD38")]
    [InlineData("#GGGGGG")]
    [InlineData("# BD38B")]
    public void TryFromHex_RejectsAnythingThatIsNotRRGGBB(string? hex)
    {
        Assert.False(OklchColor.TryFromHex(hex, out _));
    }

    [Fact]
    public void TryFromHex_AcceptsLowercase()
    {
        Assert.True(OklchColor.TryFromHex("#ebd38b", out var color));
        Assert.Equal("#EBD38B", color.ToHex());
    }

    [Fact]
    public void ToHex_OutOfGamut_ReducesChromaInsteadOfClipping()
    {
        // Un azul muy saturado y muy claro no existe en sRGB: se conserva L y H y baja C.
        var hex = new OklchColor(0.9, 0.3, 265).ToHex();

        Assert.True(OklchColor.TryFromHex(hex, out var back));
        Assert.Equal(0.9, back.L, 2);
        Assert.InRange(back.H, 255, 275);
        Assert.True(back.C < 0.3);
    }

    [Fact]
    public void DistanceTo_IsZeroForTheSameColorAndGrowsWithDifference()
    {
        OklchColor.TryFromHex("#262F47", out var ink);
        OklchColor.TryFromHex("#26323E", out var slate);
        OklchColor.TryFromHex("#EBE6D9", out var bone);

        Assert.Equal(0, ink.DistanceTo(ink), 6);
        Assert.True(ink.DistanceTo(slate) < ink.DistanceTo(bone));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/Aldune.Core.Tests --filter OklchColorTests`
Expected: FAIL de compilación, "The type or namespace name 'OklchColor' could not be found".

- [ ] **Step 3: Implementación**

```csharp
using System.Globalization;

namespace Aldune.Core;

/// <summary>
/// Un color en OKLCH: claridad percibida (L, 0-1), croma (C) y matiz en grados (H). Es el espacio
/// con el que se diseñaron las paletas de la app. En OKLCH, dos colores con la misma L pesan lo
/// mismo a la vista, cosa que en RGB o HSL no pasa. Conversión de Björn Ottosson (oklab).
/// </summary>
public readonly record struct OklchColor(double L, double C, double H)
{
    public static bool TryFromHex(string? hex, out OklchColor color)
    {
        color = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        foreach (char character in hex.AsSpan(1))
        {
            if (!Uri.IsHexDigit(character)) return false;
        }

        int rgb = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        double r = ToLinear((rgb >> 16) & 0xFF);
        double g = ToLinear((rgb >> 8) & 0xFF);
        double b = ToLinear(rgb & 0xFF);

        double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        double lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        double a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        double bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

        double hue = Math.Atan2(bb, a) * 180 / Math.PI;
        color = new OklchColor(lightness, Math.Sqrt(a * a + bb * bb), hue < 0 ? hue + 360 : hue);
        return true;
    }

    /// <summary>
    /// A <c>#RRGGBB</c>. Si el color no cabe en sRGB se reduce el croma (búsqueda binaria) y se
    /// conservan claridad y matiz: recortar canal a canal cambiaría el matiz y la claridad, que es
    /// justo lo que las paletas mantienen fijo.
    /// </summary>
    public string ToHex()
    {
        var rgb = ToLinearRgb(L, C, H);
        if (!InGamut(rgb))
        {
            double low = 0, high = C;
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (InGamut(ToLinearRgb(L, mid, H))) low = mid; else high = mid;
            }
            rgb = ToLinearRgb(L, low, H);
        }

        return $"#{ToByte(rgb.R):X2}{ToByte(rgb.G):X2}{ToByte(rgb.B):X2}";
    }

    /// <summary>Distancia euclídea en OKLab: la medida de "cuánto se parecen" dos colores.</summary>
    public double DistanceTo(OklchColor other)
    {
        var (a1, b1) = ToAb(this);
        var (a2, b2) = ToAb(other);
        double dl = L - other.L, da = a1 - a2, db = b1 - b2;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    private static (double A, double B) ToAb(OklchColor color)
    {
        double radians = color.H * Math.PI / 180;
        return (color.C * Math.Cos(radians), color.C * Math.Sin(radians));
    }

    private static (double R, double G, double B) ToLinearRgb(double lightness, double chroma, double hue)
    {
        var (a, b) = ToAb(new OklchColor(lightness, chroma, hue));
        double l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        double m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        double s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
        return (
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    private const double GamutTolerance = 1e-4;

    private static bool InGamut((double R, double G, double B) rgb) =>
        rgb.R >= -GamutTolerance && rgb.R <= 1 + GamutTolerance
        && rgb.G >= -GamutTolerance && rgb.G <= 1 + GamutTolerance
        && rgb.B >= -GamutTolerance && rgb.B <= 1 + GamutTolerance;

    private static double ToLinear(int channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static int ToByte(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        double encoded = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (int)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test tests/Aldune.Core.Tests --filter OklchColorTests`
Expected: PASS (todos).

---

### Task 2: Color derivado (borde y etiqueta)

**Files:**
- Create: `src/Aldune.Core/NoteColorDerivation.cs`
- Modify: `src/Aldune/Windowing/NoteColorPalette.cs` (todo el fichero)
- Test: `tests/Aldune.Core.Tests/NoteColorDerivationTests.cs`

**Interfaces:**
- Consumes: `OklchColor` (tarea 1), `NoteColorContrast.ForegroundFor/IsReadable` (existente).
- Produces:
  - `public static class NoteColorDerivation`
  - `public const double DarkThreshold = 0.6;`
  - `public static bool IsDark(string? color)`: L < 0.6; false si no es un hex válido.
  - `public static string RimFor(string color)`
  - `public static string LabelFor(string color)`
  - `public static IReadOnlyList<string> ClassicColors`: los 6 de siempre, en su orden.

- [ ] **Step 1: Test que falla**

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteColorDerivationTests
{
    // Los valores que se diseñaron a mano para la paleta de fábrica: no pueden moverse ni un píxel.
    [Theory]
    [InlineData("#EBD38B", "#B7A059", "#614E00")]
    [InlineData("#AAE6B1", "#78B280", "#006123")]
    [InlineData("#83E7F2", "#4CB3BE", "#005B63")]
    [InlineData("#C2D4FF", "#90A1CA", "#3A4C83")]
    [InlineData("#F9BEF2", "#C38CBE", "#743170")]
    [InlineData("#FFC5AF", "#C9937E", "#7D391D")]
    public void ClassicColors_KeepTheirHandTunedRimAndLabel(string face, string rim, string label)
    {
        Assert.Equal(rim, NoteColorDerivation.RimFor(face));
        Assert.Equal(label, NoteColorDerivation.LabelFor(face));
    }

    [Fact]
    public void ClassicColors_AreTheSixInTheirOriginalOrder()
    {
        Assert.Equal(new[] { "#EBD38B", "#AAE6B1", "#83E7F2", "#C2D4FF", "#F9BEF2", "#FFC5AF" },
            NoteColorDerivation.ClassicColors);
    }

    [Fact]
    public void ClassicOverride_IsCaseInsensitive()
    {
        Assert.Equal("#B7A059", NoteColorDerivation.RimFor("#ebd38b"));
    }

    [Theory]
    [InlineData("#2E3034", true)]
    [InlineData("#462527", true)]
    [InlineData("#EBE6D9", false)]
    [InlineData("#EBD38B", false)]
    [InlineData("not a color", false)]
    [InlineData(null, false)]
    public void IsDark_SplitsAtLightnessPointSix(string? color, bool dark)
    {
        Assert.Equal(dark, NoteColorDerivation.IsDark(color));
    }

    [Fact]
    public void DarkFace_GetsALighterRimAndALightLabel()
    {
        OklchColor.TryFromHex("#262F47", out var face);
        OklchColor.TryFromHex(NoteColorDerivation.RimFor("#262F47"), out var rim);
        OklchColor.TryFromHex(NoteColorDerivation.LabelFor("#262F47"), out var label);

        Assert.Equal(face.L + 0.07, rim.L, 2);
        Assert.Equal(0.86, label.L, 2);
    }

    [Fact]
    public void LightFace_GetsADarkerRimAndLabel()
    {
        OklchColor.TryFromHex("#EBE6D9", out var face);
        OklchColor.TryFromHex(NoteColorDerivation.RimFor("#EBE6D9"), out var rim);
        OklchColor.TryFromHex(NoteColorDerivation.LabelFor("#EBE6D9"), out var label);

        Assert.Equal(face.L - 0.16, rim.L, 2);
        Assert.Equal(face.L - 0.44, label.L, 2);
    }

    // Colores que no son de ningún tema: notas de versiones antiguas y colores personalizados.
    [Theory]
    [InlineData("#F7E6A3")]
    [InlineData("#808080")]
    [InlineData("#FF0000")]
    [InlineData("#0000FF")]
    [InlineData("#101010")]
    [InlineData("#FAFAFA")]
    public void AnyColor_GetsAReadableLabel(string face)
    {
        Assert.True(NoteColorContrast.IsReadable(face, NoteColorDerivation.LabelFor(face)));
    }

    [Fact]
    public void InvalidColor_FallsBackWithoutThrowing()
    {
        Assert.Equal("oops", NoteColorDerivation.RimFor("oops"));
        Assert.Equal(NoteColorContrast.Ink, NoteColorDerivation.LabelFor("oops"));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/Aldune.Core.Tests --filter NoteColorDerivationTests`
Expected: FAIL de compilación, "NoteColorDerivation could not be found".

- [ ] **Step 3: Implementación**

`src/Aldune.Core/NoteColorDerivation.cs`:

```csharp
namespace Aldune.Core;

/// <summary>
/// El borde y el color de la etiqueta de una nota, derivados de su cara en OKLCH. Con temas,
/// cualquier color tiene que salir bien sin escribir sus acompañantes a mano:
///
/// - Cara clara: borde a L−0.16 y etiqueta a L−0.44, mismo matiz y croma. Es la regla con la que
///   se diseñó la paleta de fábrica.
/// - Cara oscura: borde a L+0.07 (el "filo superior" de la pestaña) y etiqueta a L 0.86, con un
///   poco más de croma para que no se vea gris.
///
/// Los seis colores de fábrica van por una tabla aparte: se afinaron a mano y la fórmula no los
/// reproduce al píxel. Si una etiqueta calculada no llegara a 4,5:1, se usa la tinta adaptativa.
/// </summary>
public static class NoteColorDerivation
{
    public const double DarkThreshold = 0.6;

    private static readonly (string Face, string Rim, string Label)[] Classic =
    {
        ("#EBD38B", "#B7A059", "#614E00"), // citron  oklch(0.87 0.095 92)
        ("#AAE6B1", "#78B280", "#006123"), // sage    oklch(0.87 0.095 148)
        ("#83E7F2", "#4CB3BE", "#005B63"), // sky     oklch(0.87 0.095 205)
        ("#C2D4FF", "#90A1CA", "#3A4C83"), // iris    oklch(0.87 0.063 268)
        ("#F9BEF2", "#C38CBE", "#743170"), // rose    oklch(0.87 0.095 330)
        ("#FFC5AF", "#C9937E", "#7D391D"), // amber   oklch(0.87 0.073 42)
    };

    public static IReadOnlyList<string> ClassicColors { get; } = Classic.Select(entry => entry.Face).ToArray();

    public static bool IsDark(string? color) =>
        OklchColor.TryFromHex(color, out var oklch) && oklch.L < DarkThreshold;

    public static string RimFor(string color)
    {
        if (FindClassic(color) is { } classic) return classic.Rim;
        if (!OklchColor.TryFromHex(color, out var face)) return color;

        return face.L < DarkThreshold
            ? face with { L = Math.Min(face.L + 0.07, 1) }.ToHex()
            : face with { L = Math.Max(face.L - 0.16, 0) }.ToHex();
    }

    public static string LabelFor(string color)
    {
        if (FindClassic(color) is { } classic) return classic.Label;
        if (!OklchColor.TryFromHex(color, out var face)) return NoteColorContrast.ForegroundFor(color);

        var label = face.L < DarkThreshold
            ? new OklchColor(0.86, Math.Min(face.C * 1.6, 0.08), face.H).ToHex()
            : face with { L = Math.Max(face.L - 0.44, 0) }.ToHex();

        return NoteColorContrast.IsReadable(color, label) ? label : NoteColorContrast.ForegroundFor(color);
    }

    private static (string Face, string Rim, string Label)? FindClassic(string? color)
    {
        foreach (var entry in Classic)
        {
            if (string.Equals(entry.Face, color, StringComparison.OrdinalIgnoreCase)) return entry;
        }
        return null;
    }
}
```

`src/Aldune/Windowing/NoteColorPalette.cs` (sustituye el fichero entero):

```csharp
namespace Aldune.Windowing;

/// <summary>
/// Colores del chrome y acceso WPF a los colores derivados de una nota. Las paletas de notas ya no
/// viven aquí: son temas (<see cref="Aldune.Core.NoteThemes"/>) y el borde y la etiqueta se
/// calculan para cualquier color (<see cref="Aldune.Core.NoteColorDerivation"/>).
/// </summary>
internal static class NoteColorPalette
{
    /// <summary>Tinta del texto sobre una nota clara. Negro tintado hacia el cálido, nunca #000.</summary>
    internal const string Ink = Aldune.Core.NoteColorContrast.Ink;

    /// <summary>Fondo del chrome (dock, gestor). Neutro tintado, nunca el #3A3A3A plano.</summary>
    internal const string Ground = "#2A261F";

    /// <summary>Un escalón por encima de <see cref="Ground"/>, para controles sobre él.</summary>
    internal const string GroundRaised = "#3C3730";

    internal static string RimFor(string color) => Aldune.Core.NoteColorDerivation.RimFor(color);

    internal static string LabelFor(string color) => Aldune.Core.NoteColorDerivation.LabelFor(color);

    /// <summary>Adaptive foreground keeps light and dark custom colors readable.</summary>
    internal static string ForegroundFor(string color) => Aldune.Core.NoteColorContrast.ForegroundFor(color);

    internal static bool IsReadableCustom(string color) =>
        Aldune.Core.NoteColorContrast.IsReadable(color, ForegroundFor(color));
}
```

Esto rompe cuatro usos de `NoteColorPalette.Colors`. Se sustituyen **provisionalmente** por
`Aldune.Core.NoteColorDerivation.ClassicColors` (las tareas 5 a 7 los reemplazan por el tema
activo). `ClassicColors` es `IReadOnlyList<string>`: donde se usaba `.Length` pasa a `.Count`.
- `AppCoordinator.cs:439`: `var color = NoteColorDerivation.ClassicColors[existing % NoteColorDerivation.ClassicColors.Count];`
- `EdgeDockWindow.xaml.cs:2379`: igual, con `existingCount`.
- `EdgeDockWindow.xaml.cs:2107` (`foreach (var color in NoteColorPalette.Colors)`) y
  `NoteWindow.xaml.cs:1192`: `foreach (var color in Aldune.Core.NoteColorDerivation.ClassicColors)`.
- `NoteWindow.xaml.cs` (`ApplyColor`): sustituir

```csharp
        bool darkenHover = foreground == NoteColorContrast.White
            || Array.IndexOf(NoteColorPalette.Colors, color) >= 0;
```

por

```csharp
        // Muy clara (L ≥ 0.8) o con tinta blanca: aclarar no se vería, así que el hover oscurece.
        // Cubre la paleta de fábrica (L 0.87) igual que antes y los claros de los temas nuevos.
        bool darkenHover = foreground == NoteColorContrast.White
            || (OklchColor.TryFromHex(color, out var face) && face.L >= 0.8);
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet build Aldune.slnx -c Debug` y luego `dotnet test Aldune.slnx --no-build`
Expected: build sin errores; todos los tests pasan (los 536 de antes más los nuevos).

---

### Task 3: Modelo de tema, temas de serie y ajustes

**Files:**
- Create: `src/Aldune.Core/NoteTheme.cs`, `src/Aldune.Core/NoteThemes.cs`
- Modify: `src/Aldune.Core/AppSettings.cs` (final de la clase), `src/Aldune.Core/SettingsService.cs` (`Load`)
- Test: `tests/Aldune.Core.Tests/NoteThemesTests.cs`, `tests/Aldune.Core.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Consumes: `OklchColor.TryFromHex`, `NoteColorDerivation.ClassicColors`.
- Produces:
  - `public sealed class NoteTheme { string Id; string Name; List<string> DarkColors; List<string> LightColors; bool IsBuiltIn }` (get/set, para System.Text.Json).
  - `public enum NoteTone { Light, Dark, Both }`
  - `public enum NoteColorAssignment { RotateAvoidNeighbors, Rotate, MostDistinct, Fixed }`
  - `NoteThemes.ClassicId/SereneId/GraphiteId` (`"classic"`, `"serene"`, `"graphite"`)
  - `NoteThemes.BuiltIn` → `IReadOnlyList<NoteTheme>` en el orden Clásico, Sereno, Grafito.
  - `NoteThemes.Resolve(string? id, IEnumerable<NoteTheme>? custom)` → `NoteTheme` (nunca null).
  - `NoteThemes.All(IEnumerable<NoteTheme>? custom)` → de serie + propios.
  - `NoteThemes.Sanitize(IEnumerable<NoteTheme>? custom)` → `List<NoteTheme>`.
  - `NoteThemes.Duplicate(NoteTheme source, string name)` → `NoteTheme` con Id GUID nuevo.
  - `AppSettings.ActiveThemeId`, `NewNoteTone`, `ColorAssignment`, `FixedNoteColor`, `CustomThemes`.

- [ ] **Step 1: Test que falla**

`tests/Aldune.Core.Tests/NoteThemesTests.cs`:

```csharp
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
```

Añadir a `tests/Aldune.Core.Tests/SettingsServiceTests.cs`:

```csharp
    [Fact]
    public void Load_SettingsWrittenBeforeThemes_GetTheFactoryThemeDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{\"KeepDockOpen\":true}");

        var settings = new SettingsService(_settingsPath).Load();

        Assert.Null(settings.ActiveThemeId);
        Assert.Equal(NoteTone.Light, settings.NewNoteTone);
        Assert.Equal(NoteColorAssignment.RotateAvoidNeighbors, settings.ColorAssignment);
        Assert.Null(settings.FixedNoteColor);
        Assert.Empty(settings.CustomThemes);
    }

    [Fact]
    public void Load_SanitizesCustomThemes()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath,
            "{\"CustomThemes\":[{\"Id\":\"a\",\"Name\":\"A\",\"DarkColors\":[\"bad\"],\"LightColors\":[]}]}");

        var settings = new SettingsService(_settingsPath).Load();

        Assert.Empty(settings.CustomThemes);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsThemeSettings()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings
        {
            ActiveThemeId = "mine",
            NewNoteTone = NoteTone.Both,
            ColorAssignment = NoteColorAssignment.Fixed,
            FixedNoteColor = "#262F47",
            CustomThemes = [new NoteTheme { Id = "mine", Name = "Mío", DarkColors = ["#262F47"] }],
        });

        var loaded = sut.Load();

        Assert.Equal("mine", loaded.ActiveThemeId);
        Assert.Equal(NoteTone.Both, loaded.NewNoteTone);
        Assert.Equal(NoteColorAssignment.Fixed, loaded.ColorAssignment);
        Assert.Equal("#262F47", loaded.FixedNoteColor);
        Assert.Equal(new[] { "#262F47" }, Assert.Single(loaded.CustomThemes).DarkColors);
    }
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/Aldune.Core.Tests --filter "NoteThemesTests|SettingsServiceTests"`
Expected: FAIL de compilación, "NoteThemes / NoteTone could not be found".

- [ ] **Step 3: Implementación**

`src/Aldune.Core/NoteTheme.cs`:

```csharp
namespace Aldune.Core;

/// <summary>
/// Un conjunto de colores de nota pensados para ir juntos: tonos oscuros (con tinta clara) y tonos
/// claros (con tinta oscura), cada lista en su orden de rotación. Puede faltar una de las dos.
/// Get/set y listas mutables porque los temas propios se guardan tal cual en settings.json.
/// </summary>
public sealed class NoteTheme
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> DarkColors { get; set; } = new();
    public List<string> LightColors { get; set; } = new();

    /// <summary>Los de serie no se editan ni se borran; se duplican.</summary>
    public bool IsBuiltIn { get; set; }
}

/// <summary>De qué tono nacen las notas nuevas.</summary>
public enum NoteTone
{
    Light,
    Dark,

    /// <summary>Alternando: oscura tras una clara y clara tras una oscura.</summary>
    Both,
}

/// <summary>Cómo se elige el color de una nota nueva dentro del tema.</summary>
public enum NoteColorAssignment
{
    /// <summary>En el orden del tema, saltando los colores de las notas de al lado.</summary>
    RotateAvoidNeighbors,
    Rotate,
    MostDistinct,
    Fixed,
}
```

`src/Aldune.Core/NoteThemes.cs`:

```csharp
namespace Aldune.Core;

/// <summary>
/// Los temas de serie y las reglas para los propios. Los de serie se crean en cada acceso (listas
/// nuevas): quien los recibe puede modificarlos sin tocar el original.
///
/// Los colores de Sereno y Grafito se calcularon en OKLCH con la misma claridad dentro de cada grupo
/// (oscuros L≈0.31 y 0.30, claros L≈0.925 y 0.93) y poco croma: se distinguen entre sí sin parecer
/// un arcoíris. Ver docs/superpowers/specs/2026-09-23-aldune-temas-design.md.
/// </summary>
public static class NoteThemes
{
    public const string ClassicId = "classic";
    public const string SereneId = "serene";
    public const string GraphiteId = "graphite";

    public static IReadOnlyList<NoteTheme> BuiltIn =>
    [
        new NoteTheme
        {
            Id = ClassicId, Name = "Clásico", IsBuiltIn = true,
            LightColors = NoteColorDerivation.ClassicColors.ToList(),
        },
        new NoteTheme
        {
            Id = SereneId, Name = "Sereno", IsBuiltIn = true,
            // Grafito, Pizarra, Tinta, Petróleo, Musgo, Tabaco, Burdeos, Ciruela
            DarkColors = ["#2E3034", "#26323E", "#262F47", "#1D3538", "#283426", "#3C2D21", "#462527", "#392A3C"],
            // Hueso, Piedra, Niebla, Salvia, Lino, Polvo
            LightColors = ["#EBE6D9", "#EBE5E0", "#DEE8F0", "#DEEADE", "#F0E4D7", "#F2E2E1"],
        },
        new NoteTheme
        {
            Id = GraphiteId, Name = "Grafito", IsBuiltIn = true,
            // Carbón, Grafito, Humo, Acero, Ónice
            DarkColors = ["#2F2D2C", "#2B2E33", "#332C29", "#262F36", "#2E2E2E"],
            // Papel, Tiza, Arena, Ceniza
            LightColors = ["#EBE7E0", "#E4E8ED", "#EFE6DD", "#E8E8E8"],
        },
    ];

    public static IReadOnlyList<NoteTheme> All(IEnumerable<NoteTheme>? custom) =>
        BuiltIn.Concat(custom ?? Enumerable.Empty<NoteTheme>()).ToList();

    /// <summary>El tema con ese id, o Clásico si no existe (borrado, o settings.json editado a mano).</summary>
    public static NoteTheme Resolve(string? id, IEnumerable<NoteTheme>? custom) =>
        All(custom).FirstOrDefault(theme => theme.Id == id) ?? BuiltIn[0];

    /// <summary>
    /// Limpia los temas propios leídos de disco: quita los colores que no son <c>#RRGGBB</c> y los
    /// normaliza a mayúsculas, y descarta los temas sin ningún color, los que usan el id de uno de
    /// serie y los que repiten un id ya visto. Nunca marca uno como de serie.
    /// </summary>
    public static List<NoteTheme> Sanitize(IEnumerable<NoteTheme>? custom)
    {
        var builtInIds = BuiltIn.Select(theme => theme.Id).ToHashSet();
        var seen = new HashSet<string>();
        var clean = new List<NoteTheme>();

        foreach (var theme in custom ?? Enumerable.Empty<NoteTheme>())
        {
            if (theme is null || string.IsNullOrWhiteSpace(theme.Id)) continue;
            if (builtInIds.Contains(theme.Id) || !seen.Add(theme.Id)) continue;

            var dark = CleanColors(theme.DarkColors);
            var light = CleanColors(theme.LightColors);
            if (dark.Count == 0 && light.Count == 0) continue;

            clean.Add(new NoteTheme
            {
                Id = theme.Id,
                Name = string.IsNullOrWhiteSpace(theme.Name) ? theme.Id : theme.Name.Trim(),
                DarkColors = dark,
                LightColors = light,
            });
        }

        return clean;
    }

    public static NoteTheme Duplicate(NoteTheme source, string name) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        DarkColors = source.DarkColors.ToList(),
        LightColors = source.LightColors.ToList(),
    };

    private static List<string> CleanColors(IEnumerable<string>? colors) =>
        (colors ?? Enumerable.Empty<string>())
            .Where(color => OklchColor.TryFromHex(color, out _))
            .Select(color => color.ToUpperInvariant())
            .ToList();
}
```

Al final de `AppSettings` (antes de la llave de cierre de la clase):

```csharp
    /// <summary>
    /// Tema de color de las notas. Nulo = Clásico, la paleta de siempre: un settings.json anterior
    /// a los temas carga así y nada cambia hasta que el usuario elige otro. Un id que ya no existe
    /// también cae a Clásico (ver <see cref="NoteThemes.Resolve"/>).
    /// </summary>
    public string? ActiveThemeId { get; set; }

    /// <summary>De qué tono nacen las notas nuevas. Por defecto claras, como siempre.</summary>
    public NoteTone NewNoteTone { get; set; } = NoteTone.Light;

    /// <summary>Cómo se elige el color de una nota nueva dentro del tema.</summary>
    public NoteColorAssignment ColorAssignment { get; set; } = NoteColorAssignment.RotateAvoidNeighbors;

    /// <summary>Color de <see cref="NoteColorAssignment.Fixed"/>. Nulo = el primero del tono pedido.</summary>
    public string? FixedNoteColor { get; set; }

    /// <summary>Temas creados por el usuario. Locales a este equipo: no se sincronizan en la 1.0.</summary>
    public List<NoteTheme> CustomThemes { get; set; } = new();
```

En `SettingsService.Load`, justo después de la línea
`var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();`:

```csharp
        // Un settings.json editado a mano no puede dejar temas que rompan el pintado de las notas.
        settings.CustomThemes = NoteThemes.Sanitize(settings.CustomThemes);
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test tests/Aldune.Core.Tests --filter "NoteThemesTests|SettingsServiceTests"`
Expected: PASS.

---

### Task 4: Asignador de color

**Files:**
- Create: `src/Aldune.Core/NoteColorAssigner.cs`
- Test: `tests/Aldune.Core.Tests/NoteColorAssignerTests.cs`

**Interfaces:**
- Consumes: `NoteTheme`, `NoteTone`, `NoteColorAssignment` (tarea 3), `OklchColor`, `NoteColorDerivation.IsDark`.
- Produces:
  `public static string NoteColorAssigner.Assign(NoteTheme theme, NoteTone tone, NoteColorAssignment rule, string? fixedColor, IReadOnlyList<string> dockColors)`.
  `dockColors`: los colores de las notas de la vista del dock, en su orden. La nota nueva aparece
  detrás de la última.

- [ ] **Step 1: Test que falla**

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteColorAssignerTests
{
    private static readonly NoteTheme Theme = new()
    {
        Id = "t", Name = "T",
        DarkColors = ["#262F47", "#26323E", "#462527"],
        LightColors = ["#EBE6D9", "#DEE8F0", "#DEEADE"],
    };

    private static string Assign(NoteColorAssignment rule, NoteTone tone, params string[] dock) =>
        NoteColorAssigner.Assign(Theme, tone, rule, fixedColor: null, dock);

    [Fact]
    public void EmptyDock_GivesTheFirstColorOfTheTone()
    {
        Assert.Equal("#EBE6D9", Assign(NoteColorAssignment.Rotate, NoteTone.Light));
        Assert.Equal("#262F47", Assign(NoteColorAssignment.RotateAvoidNeighbors, NoteTone.Dark));
        Assert.Equal("#262F47", Assign(NoteColorAssignment.MostDistinct, NoteTone.Dark));
    }

    [Fact]
    public void Rotate_FollowsTheLastColorOfThatToneInTheDock()
    {
        Assert.Equal("#DEEADE", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#EBE6D9", "#DEE8F0"));
        Assert.Equal("#EBE6D9", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#DEEADE"));
    }

    [Fact]
    public void Rotate_IgnoresColorsFromOutsideTheTheme()
    {
        Assert.Equal("#DEE8F0", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#EBE6D9", "#123456"));
    }

    [Fact]
    public void Rotate_ComparesHexWithoutCase()
    {
        Assert.Equal("#DEE8F0", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#ebe6d9"));
    }

    [Fact]
    public void AvoidNeighbors_SkipsTheColorsOfTheLastTwoNotes()
    {
        // Rotar daría #DEE8F0 (tras #EBE6D9), pero es la penúltima: salta a #DEEADE.
        Assert.Equal("#DEEADE",
            Assign(NoteColorAssignment.RotateAvoidNeighbors, NoteTone.Light, "#DEE8F0", "#EBE6D9"));
    }

    [Fact]
    public void AvoidNeighbors_AfterDeletingNotes_DoesNotRepeatTheOneNextToIt()
    {
        // Antes: existing % Colors.Length. Con 3 notas y la del medio borrada, la cuenta daba el
        // mismo color que la última. Ahora manda el color de las vecinas, no el número de notas.
        Assert.NotEqual("#DEE8F0",
            Assign(NoteColorAssignment.RotateAvoidNeighbors, NoteTone.Light, "#EBE6D9", "#DEE8F0"));
    }

    [Fact]
    public void AvoidNeighbors_WithTooFewColors_FallsBackToRotateWithoutLooping()
    {
        var tiny = new NoteTheme { Id = "x", Name = "X", LightColors = ["#EBE6D9", "#DEE8F0"] };

        var color = NoteColorAssigner.Assign(tiny, NoteTone.Light, NoteColorAssignment.RotateAvoidNeighbors,
            null, ["#DEE8F0", "#EBE6D9"]);

        Assert.Equal("#DEE8F0", color);
    }

    [Fact]
    public void MostDistinct_PicksTheCandidateFarthestFromEverythingInTheDock()
    {
        // Con Tinta y Pizarra (fríos) en el dock, Burdeos (cálido) es el más distinto.
        Assert.Equal("#462527",
            Assign(NoteColorAssignment.MostDistinct, NoteTone.Dark, "#262F47", "#26323E"));
    }

    [Fact]
    public void Fixed_UsesTheFixedColorOrTheFirstCandidate()
    {
        Assert.Equal("#ABCDEF", NoteColorAssigner.Assign(Theme, NoteTone.Light, NoteColorAssignment.Fixed, "#abcdef", []));
        Assert.Equal("#EBE6D9", NoteColorAssigner.Assign(Theme, NoteTone.Light, NoteColorAssignment.Fixed, null, []));
        Assert.Equal("#EBE6D9", NoteColorAssigner.Assign(Theme, NoteTone.Light, NoteColorAssignment.Fixed, "bad", []));
    }

    [Fact]
    public void Both_AlternatesWithTheLastNote_StartingDark()
    {
        Assert.True(NoteColorDerivation.IsDark(Assign(NoteColorAssignment.Rotate, NoteTone.Both)));
        Assert.False(NoteColorDerivation.IsDark(Assign(NoteColorAssignment.Rotate, NoteTone.Both, "#262F47")));
        Assert.True(NoteColorDerivation.IsDark(Assign(NoteColorAssignment.Rotate, NoteTone.Both, "#EBE6D9")));
    }

    [Fact]
    public void MissingTone_UsesTheOtherList()
    {
        var classic = NoteThemes.Resolve(NoteThemes.ClassicId, null);

        var color = NoteColorAssigner.Assign(classic, NoteTone.Dark, NoteColorAssignment.Rotate, null, []);

        Assert.Equal("#EBD38B", color);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test tests/Aldune.Core.Tests --filter NoteColorAssignerTests`
Expected: FAIL de compilación, "NoteColorAssigner could not be found".

- [ ] **Step 3: Implementación**

```csharp
namespace Aldune.Core;

/// <summary>
/// El color de una nota nueva: primero el tono (qué lista del tema) y después la regla (qué color
/// de esa lista). Mira los colores de las notas que ya hay en la vista del dock, en su orden; la
/// nueva aparece detrás de la última, así que "las vecinas" son las dos últimas.
///
/// Sustituye a <c>existing % Colors.Length</c>, que repetía el color de la vecina en cuanto se
/// borraba una nota: el número de notas no dice nada de qué colores tienen.
/// </summary>
public static class NoteColorAssigner
{
    private const int NeighborCount = 2;

    public static string Assign(NoteTheme theme, NoteTone tone, NoteColorAssignment rule,
        string? fixedColor, IReadOnlyList<string> dockColors)
    {
        var candidates = Candidates(theme, WantsDark(tone, dockColors));

        return rule switch
        {
            NoteColorAssignment.Fixed => OklchColor.TryFromHex(fixedColor, out _)
                ? fixedColor!.ToUpperInvariant()
                : candidates[0],
            NoteColorAssignment.Rotate => candidates[RotateIndex(candidates, dockColors)],
            NoteColorAssignment.MostDistinct => MostDistinct(candidates, dockColors),
            _ => AvoidNeighbors(candidates, dockColors),
        };
    }

    private static bool WantsDark(NoteTone tone, IReadOnlyList<string> dockColors) => tone switch
    {
        NoteTone.Dark => true,
        NoteTone.Light => false,
        // Alternando: oscura si no hay nada o si la última es clara.
        _ => dockColors.Count == 0 || !NoteColorDerivation.IsDark(dockColors[^1]),
    };

    /// <summary>La lista del tono pedido; si el tema no tiene de ese tono, la otra. Nunca vacía:
    /// un tema sin ningún color no pasa <see cref="NoteThemes.Sanitize"/>, y si aun así llegara se
    /// usa Clásico.</summary>
    private static IReadOnlyList<string> Candidates(NoteTheme theme, bool dark)
    {
        var preferred = dark ? theme.DarkColors : theme.LightColors;
        var other = dark ? theme.LightColors : theme.DarkColors;
        if (preferred.Count > 0) return preferred;
        if (other.Count > 0) return other;
        return NoteColorDerivation.ClassicColors;
    }

    /// <summary>El índice siguiente al último color de estos candidatos usado en el dock, o 0.</summary>
    private static int RotateIndex(IReadOnlyList<string> candidates, IReadOnlyList<string> dockColors)
    {
        for (int i = dockColors.Count - 1; i >= 0; i--)
        {
            int index = IndexOf(candidates, dockColors[i]);
            if (index >= 0) return (index + 1) % candidates.Count;
        }
        return 0;
    }

    private static string AvoidNeighbors(IReadOnlyList<string> candidates, IReadOnlyList<string> dockColors)
    {
        int start = RotateIndex(candidates, dockColors);
        var neighbors = dockColors.Skip(Math.Max(0, dockColors.Count - NeighborCount)).ToList();

        for (int step = 0; step < candidates.Count; step++)
        {
            var candidate = candidates[(start + step) % candidates.Count];
            if (IndexOf(neighbors, candidate) < 0) return candidate;
        }

        // Tema con tan pocos colores que todos están al lado: rotar sin más.
        return candidates[start];
    }

    private static string MostDistinct(IReadOnlyList<string> candidates, IReadOnlyList<string> dockColors)
    {
        var existing = dockColors
            .Select(color => OklchColor.TryFromHex(color, out var oklch) ? oklch : (OklchColor?)null)
            .OfType<OklchColor>()
            .ToList();
        if (existing.Count == 0) return candidates[0];

        string best = candidates[0];
        double bestDistance = double.MinValue;
        foreach (var candidate in candidates)
        {
            if (!OklchColor.TryFromHex(candidate, out var oklch)) continue;
            double nearest = existing.Min(other => oklch.DistanceTo(other));
            // Estrictamente mayor: en empate gana el primero en el orden del tema.
            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = candidate;
            }
        }
        return best;
    }

    private static int IndexOf(IReadOnlyList<string> colors, string color)
    {
        for (int i = 0; i < colors.Count; i++)
        {
            if (string.Equals(colors[i], color, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test tests/Aldune.Core.Tests --filter NoteColorAssignerTests`
Expected: PASS.

- [ ] **Step 5: Suite completa**

Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`
Expected: todo en verde.

---

### Task 5: Las notas nuevas usan el tema

**Files:**
- Modify: `src/Aldune/Windowing/AppCoordinator.cs` (`CreateAndOpenNote`, cerca de la línea 433; nuevas propiedades y métodos)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (`CreateNote`, cerca de la línea 2375)

**Interfaces:**
- Consumes: `NoteThemes.Resolve`, `NoteColorAssigner.Assign`.
- Produces (en `AppCoordinator`):
  - `internal NoteTheme ActiveTheme { get; }`
  - `internal string NextNoteColor()`: color para una nota nueva en la vista actual del dock.

- [ ] **Step 1: Coordinador**

En `AppCoordinator`, junto a `NotesForCurrentDockView` (línea ~747):

```csharp
    /// <summary>El tema de color elegido en Ajustes, o Clásico si no hay ajustes o el id no existe.</summary>
    internal NoteTheme ActiveTheme => NoteThemes.Resolve(_settings?.ActiveThemeId, _settings?.CustomThemes);

    /// <summary>
    /// El color de una nota que se va a crear. Mira la vista actual del dock y no todas las notas:
    /// en la vista de una etiqueta, las vecinas de la nota nueva son las de esa etiqueta.
    /// </summary>
    internal string NextNoteColor() => NoteColorAssigner.Assign(
        ActiveTheme,
        _settings?.NewNoteTone ?? NoteTone.Light,
        _settings?.ColorAssignment ?? NoteColorAssignment.RotateAvoidNeighbors,
        _settings?.FixedNoteColor,
        NotesForCurrentDockView().Select(note => note.Color).ToList());
```

En `CreateAndOpenNote`, sustituir

```csharp
        var existing = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorDerivation.ClassicColors[existing % NoteColorDerivation.ClassicColors.Count];
        var note = _repository.Create(string.Empty, color, screenOrigin: "primary");
```

por

```csharp
        var note = _repository.Create(string.Empty, NextNoteColor(), screenOrigin: "primary");
```

- [ ] **Step 2: Dock**

En `EdgeDockWindow.CreateNote`, sustituir

```csharp
        var existingCount = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorDerivation.ClassicColors[existingCount % NoteColorDerivation.ClassicColors.Count];
        var note = _repository.Create(content, color, screenOrigin: "primary");
```

por

```csharp
        var note = _repository.Create(content, _coordinator.NextNoteColor(), screenOrigin: "primary");
```

- [ ] **Step 3: Verificar**

Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`
Expected: todo en verde. Con Clásico y el tono por defecto, las notas nuevas siguen saliendo de
los 6 colores de siempre.

---

### Task 6: Pastillas de color del tema (menú ⋯ y menú de pestaña)

**Files:**
- Create: `src/Aldune/Windowing/NoteSwatchPanel.cs`
- Modify: `src/Aldune/Windowing/NoteWindow.xaml:323`, `src/Aldune/Windowing/NoteWindow.xaml.cs` (`PopulateColorSwatches`, `ApplySwatchSelection`, `OnColorSwatchClick`, `OnMenuClick`)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml:660`, `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (`BuildTabMenuSwatches`)

**Interfaces:**
- Consumes: `AppCoordinator.ActiveTheme` (tarea 5), `NoteColorPalette.LabelFor`.
- Produces:
  - `internal static void NoteSwatchPanel.Fill(Panel host, NoteTheme theme, string? selectedColor, MouseButtonEventHandler onClick)`:
    vacía `host` y añade una fila (`WrapPanel`) de oscuros y otra de claros, las que no estén
    vacías. Cada pastilla es un `Border` con `Tag` = hex.
  - `internal static void NoteSwatchPanel.MarkSelected(Panel host, string? selectedColor)`

- [ ] **Step 1: Crear el panel compartido**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Las pastillas de color de un tema: una fila de oscuros y otra de claros. Las usan el menú "⋯" de
/// la nota, el menú de la pestaña del dock y Ajustes; antes había dos copias casi iguales, cada una
/// con los seis colores fijos.
/// </summary>
internal static class NoteSwatchPanel
{
    private const double SwatchSize = 22;

    internal static void Fill(Panel host, NoteTheme theme, string? selectedColor, MouseButtonEventHandler onClick)
    {
        host.Children.Clear();
        foreach (var row in new[] { theme.DarkColors, theme.LightColors })
        {
            if (row.Count == 0) continue;

            var panel = new WrapPanel();
            foreach (var color in row)
            {
                var swatch = CreateSwatch(color);
                swatch.MouseLeftButtonUp += onClick;
                panel.Children.Add(swatch);
            }
            host.Children.Add(panel);
        }
        MarkSelected(host, selectedColor);
    }

    internal static void MarkSelected(Panel host, string? selectedColor)
    {
        foreach (var swatch in host.Children.OfType<Panel>().SelectMany(row => row.Children.OfType<Border>()))
        {
            bool selected = string.Equals((string)swatch.Tag, selectedColor, StringComparison.OrdinalIgnoreCase);
            swatch.BorderThickness = new Thickness(selected ? 2 : 0);
            if (swatch.Child is UIElement tick) tick.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static Border CreateSwatch(string color)
    {
        var label = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.LabelFor(color))!;
        return new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString(color)!,
            Width = SwatchSize,
            Height = SwatchSize,
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(5),
            // El anillo y el tick en el color de la etiqueta de esa cara: se ven igual sobre una
            // pastilla clara que sobre una oscura, cosa que la tinta fija de antes no hacía.
            BorderBrush = label,
            Cursor = Cursors.Hand,
            Tag = color,
            ToolTip = color,
            Child = new TextBlock
            {
                Text = "✓",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = label,
                IsHitTestVisible = false,
            },
        };
    }
}
```

- [ ] **Step 2: Menú ⋯ de la nota**

En `NoteWindow.xaml:323`, sustituir
`<StackPanel x:Name="ColorSwatches" Orientation="Horizontal" Margin="6,4,6,8" />`
por
`<StackPanel x:Name="ColorSwatches" Margin="6,4,6,8" />` (vertical: una fila por tono).

En `NoteWindow.xaml.cs`:
- Borrar `PopulateColorSwatches` y `ApplySwatchSelection` y añadir:

```csharp
    /// <summary>Se rellena en cada apertura del menú: si el tema cambia con la nota abierta, el menú
    /// enseña ya los colores nuevos.</summary>
    private void PopulateColorSwatches() =>
        NoteSwatchPanel.Fill(ColorSwatches, _coordinator.ActiveTheme, _note.Color, OnColorSwatchClick);
```

- Quitar la llamada `PopulateColorSwatches();` del constructor (junto a `ApplyColor(note.Color);`).
- En `OnMenuClick`, justo antes de `ActionsPopup.IsOpen = true;`, añadir `PopulateColorSwatches();`.
- En `OnColorSwatchClick`, sustituir el bucle `foreach (Border swatch in ColorSwatches.Children) { ApplySwatchSelection(...) }`
  por `NoteSwatchPanel.MarkSelected(ColorSwatches, color);`.

- [ ] **Step 3: Menú de la pestaña del dock**

En `EdgeDockWindow.xaml:660`, quitar `Orientation="Horizontal"` de `TabMenuSwatches`.
Sustituir el cuerpo de `BuildTabMenuSwatches(Note note)` por:

```csharp
        NoteSwatchPanel.Fill(TabMenuSwatches, _coordinator.ActiveTheme, note.Color, OnTabMenuColorClick);
```

y actualizar su comentario: "Las pastillas del tema activo, con la de la nota marcada. Se
reconstruyen en cada apertura porque el menú sirve a la pestaña que se acaba de pulsar."

- [ ] **Step 4: Verificar**

Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`
Expected: todo en verde. Con Clásico se ve una sola fila con los 6 colores de siempre.

---

### Task 7: Acabado "filo superior" de las pestañas oscuras

**Files:**
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (`OnTabLoaded`, línea ~2285; método nuevo)

**Interfaces:**
- Consumes: `NoteColorDerivation.IsDark`.

- [ ] **Step 1: Método nuevo**

Junto a `ApplyTopBottomTabShape`:

```csharp
    /// <summary>
    /// Pestaña de cara oscura: sin el reflejo del lomo ni el borde completo, que están pensados
    /// para caras claras y sobre una oscura parecían plástico o un botón desactivado. En su lugar,
    /// una línea de 1 px arriba con el filo (RimFor, más claro que la cara), como el canto de un
    /// papel grueso. Va después de ApplyLeftEdgeTabShape/ApplyTopBottomTabShape porque pisa el
    /// grosor del borde que fijan ellos. El hover aclara en vez de oscurecer.
    /// </summary>
    private static void ApplyFaceFinish(Button button)
    {
        if (button.DataContext is not Note note || !NoteColorDerivation.IsDark(note.Color)) return;

        if (button.Template.FindName("CardBorder", button) is Border card)
            card.BorderThickness = new Thickness(0, 1, 0, 0);

        if (button.Template.FindName("SheenBorder", button) is Border sheen)
            sheen.Visibility = Visibility.Collapsed;

        if (button.Template.FindName("HoverOverlay", button) is Border hover)
            hover.Background = (Brush)new BrushConverter().ConvertFromString("#F4F1EC")!;
    }
```

- [ ] **Step 2: Llamarlo**

En `OnTabLoaded`, justo después de
`else if (IsTopBottomEdge) ApplyTopBottomTabShape(button);`, añadir `ApplyFaceFinish(button);`.

- [ ] **Step 3: Verificación visual**

Build. Con una sonda desechable en el scratchpad (mismo patrón que las de `PopupToggle` y del
editor: `Probe.csproj` que referencia `Aldune.csproj`, recursos de `App.xaml` sin `x:Shared`),
crear un `EdgeDockWindow` con notas de Sereno oscuras y claras, desplegarlo, capturarlo con
`RenderTargetBitmap` a PNG y revisar la imagen: las oscuras sin reflejo y con filo claro arriba, y
las claras como siempre.
Expected: coincide con "Filo superior" de la maqueta.

---

### Task 8: Sección "Temas" en Ajustes y aplicar a las notas existentes

**Files:**
- Modify: `src/Aldune/Resources/Strings.cs` (bloque nuevo, junto a `LanguageSectionTitle`)
- Modify: `src/Aldune/Windowing/SettingsWindow.xaml` (sección nueva), `src/Aldune/Windowing/SettingsWindow.xaml.cs`
- Modify: `src/Aldune/Windowing/AppCoordinator.cs`, `src/Aldune/Windowing/NoteWindow.xaml.cs`

**Interfaces:**
- Consumes: `NoteThemes.All/Resolve`, `NoteSwatchPanel.Fill`, `AppCoordinator.ActiveTheme`.
- Produces:
  - `internal int AppCoordinator.ApplyThemeToActiveNotes()`: devuelve cuántas notas cambió.
  - `internal void NoteWindow.ApplyExternalColor(string color)`
  - `internal string Strings.ThemeDisplayName(NoteTheme theme)` (y el resto de textos de abajo).
  - `internal void SettingsWindow.RefreshThemeSection()`, para la tarea 9.

- [ ] **Step 1: Textos**

En `Strings.cs`, tras el bloque de idioma:

```csharp
    // --- Temas -----------------------------------------------------------------------------------

    public static string ThemesSectionTitle => T("Note colors", "Colores de las notas");
    public static string ThemesSectionHint => T(
        "The theme sets the colors for new notes and the quick colors in each note's menu.",
        "El tema decide el color de las notas nuevas y los colores rápidos del menú de cada nota.");
    public static string ThemeNew => T("New", "Nuevo");
    public static string ThemeDuplicate => T("Duplicate", "Duplicar");
    public static string ThemeEdit => T("Edit", "Editar");
    public static string ThemeDeleteButton => T("Delete", "Eliminar");
    public static string ThemeDeleteConfirm(string name) => T(
        $"Delete the theme \"{name}\"? Your notes keep their colors.",
        $"¿Eliminar el tema «{name}»? Tus notas conservan sus colores.");
    public static string ThemeCopyName(string name) => T($"{name} (copy)", $"{name} (copia)");
    public static string ThemeNewName => T("My theme", "Mi tema");
    public static string NewNoteToneLabel => T("New notes are", "Las notas nuevas son");
    public static string ToneLight => T("Light", "Claras");
    public static string ToneDark => T("Dark", "Oscuras");
    public static string ToneBoth => T("Both, alternating", "De los dos tipos, alternando");
    public static string ColorAssignmentLabel => T("Color of new notes", "Color de las notas nuevas");
    public static string AssignAvoidNeighbors => T(
        "Rotate, never the same as the one next to it (recommended)",
        "Rotar, sin repetir el de al lado (recomendado)");
    public static string AssignRotate => T("Rotate through the theme", "Rotar por el tema");
    public static string AssignMostDistinct => T("The most different from the rest", "El más distinto del resto");
    public static string AssignFixed => T("Always the same color", "Siempre el mismo color");
    public static string ApplyThemeButton => T("Apply to existing notes…", "Aplicar a las notas existentes…");
    public static string ApplyThemeConfirmTitle => T("Apply theme", "Aplicar tema");
    public static string ApplyThemeConfirm(int count) => T(
        $"The color of your {count} active notes will change, including colors you picked by hand. It will sync to your other devices.",
        $"Se cambiará el color de tus {count} notas activas, incluidos los que elegiste a mano. Se sincronizará a tus otros dispositivos.");

    /// <summary>Los de serie se traducen; los propios se enseñan con el nombre que les dio el usuario.</summary>
    internal static string ThemeDisplayName(Aldune.Core.NoteTheme theme) => theme.Id switch
    {
        Aldune.Core.NoteThemes.ClassicId => T("Classic", "Clásico"),
        Aldune.Core.NoteThemes.SereneId => T("Serene", "Sereno"),
        Aldune.Core.NoteThemes.GraphiteId => T("Graphite", "Grafito"),
        _ => theme.Name,
    };
```

- [ ] **Step 2: Repintado externo de una nota abierta**

En `NoteWindow.xaml.cs`, junto a `ApplyColor`:

```csharp
    /// <summary>El color de esta nota ha cambiado fuera de su ventana (aplicar un tema desde
    /// Ajustes). Sin esto la ventana abierta seguiría pintada con el color viejo hasta cerrarla.</summary>
    internal void ApplyExternalColor(string color)
    {
        _note.Color = color;
        ApplyColor(color);
        NoteSwatchPanel.MarkSelected(ColorSwatches, color);
    }
```

- [ ] **Step 3: Aplicar el tema en el coordinador**

En `AppCoordinator`, junto a `NextNoteColor`:

```csharp
    /// <summary>
    /// Vuelve a colorear todas las notas activas con el tema, el tono y la regla actuales, en el
    /// orden del dock y como si se crearan una tras otra: así "rotar sin repetir el de al lado"
    /// deja un dock sin dos vecinas iguales. Archivadas y papelera no se tocan. Cada cambio es un
    /// SetColor normal, así que se sincroniza como si se hubiera hecho a mano.
    /// </summary>
    internal int ApplyThemeToActiveNotes()
    {
        var theme = ActiveTheme;
        var tone = _settings?.NewNoteTone ?? NoteTone.Light;
        var rule = _settings?.ColorAssignment ?? NoteColorAssignment.RotateAvoidNeighbors;
        var assigned = new List<string>();
        int changed = 0;

        foreach (var note in _repository.GetByState(NoteState.Active))
        {
            var color = NoteColorAssigner.Assign(theme, tone, rule, _settings?.FixedNoteColor, assigned);
            assigned.Add(color);
            if (string.Equals(color, note.Color, StringComparison.OrdinalIgnoreCase)) continue;

            _repository.SetColor(note.Id, color);
            if (_openNoteWindows.TryGetValue(note.Id, out var window)) window.ApplyExternalColor(color);
            changed++;
        }

        RefreshAll();
        return changed;
    }

    /// <summary>Cuántas notas activas hay, para el mensaje de confirmación de Ajustes.</summary>
    internal int ActiveNoteCount => _repository.GetByState(NoteState.Active).Count;
```

- [ ] **Step 4: XAML de la sección**

En `SettingsWindow.xaml`, columna izquierda, justo después del `StackPanel` que contiene
`LanguageListContainer` (y antes de su `<Border ... Height="1" .../>` siguiente), insertar:

```xml
                <Border Background="#3C3730" Height="1" Margin="0,20,0,0" />

                <!-- Temas: qué colores llevan las notas nuevas y los colores rápidos del menú. -->
                <StackPanel Margin="0,20,0,0">
                    <TextBlock Text="{x:Static res:Strings.ThemesSectionTitle}"
                               Foreground="#EDE7DC" FontSize="13" FontWeight="SemiBold" />
                    <TextBlock Text="{x:Static res:Strings.ThemesSectionHint}"
                               Foreground="#8A8175" FontSize="11" TextWrapping="Wrap" Margin="0,4,0,10" />

                    <ComboBox x:Name="ThemeCombo" Style="{StaticResource SyncProfileComboStyle}"
                              SelectionChanged="OnThemeSelectionChanged" />
                    <StackPanel x:Name="ThemePreview" Margin="0,8,0,0" IsHitTestVisible="False" />

                    <WrapPanel Margin="0,8,0,0">
                        <Button Content="{x:Static res:Strings.ThemeNew}" Click="OnThemeNewClick"
                                Padding="10,6" Margin="0,0,6,6" Style="{StaticResource RecordButtonStyle}" />
                        <Button Content="{x:Static res:Strings.ThemeDuplicate}" Click="OnThemeDuplicateClick"
                                Padding="10,6" Margin="0,0,6,6" Style="{StaticResource RecordButtonStyle}" />
                        <Button x:Name="ThemeEditButton" Content="{x:Static res:Strings.ThemeEdit}" Click="OnThemeEditClick"
                                Padding="10,6" Margin="0,0,6,6" Style="{StaticResource RecordButtonStyle}" />
                        <Button x:Name="ThemeDeleteButton" Content="{x:Static res:Strings.ThemeDeleteButton}" Click="OnThemeDeleteClick"
                                Padding="10,6" Margin="0,0,6,6" Style="{StaticResource RecordButtonStyle}" />
                    </WrapPanel>

                    <TextBlock Text="{x:Static res:Strings.NewNoteToneLabel}"
                               Foreground="#EDE7DC" FontSize="13" Margin="0,12,0,6" />
                    <StackPanel x:Name="ToneListContainer" />

                    <TextBlock Text="{x:Static res:Strings.ColorAssignmentLabel}"
                               Foreground="#EDE7DC" FontSize="13" Margin="0,12,0,6" />
                    <StackPanel x:Name="AssignmentListContainer" />
                    <StackPanel x:Name="FixedColorSwatches" Margin="22,4,0,0" />

                    <Button Content="{x:Static res:Strings.ApplyThemeButton}" Click="OnApplyThemeClick"
                            HorizontalAlignment="Left" Padding="12,7" Margin="0,14,0,0"
                            Style="{StaticResource RecordButtonStyle}" />
                </StackPanel>
```

- [ ] **Step 5: Código de la sección**

En `SettingsWindow.xaml.cs`, llamar a `RefreshThemeSection();` en el constructor, junto a
`PopulateLanguages();`, y añadir:

```csharp
    // --- Temas ---------------------------------------------------------------------------------

    private bool _loadingThemes;

    private NoteTheme ActiveTheme => NoteThemes.Resolve(_settings.ActiveThemeId, _settings.CustomThemes);

    /// <summary>Rehace la sección entera a partir de los ajustes. Se llama al abrir y después de
    /// cualquier cambio: son pocos controles y así no hay estado que se desincronice.</summary>
    internal void RefreshThemeSection()
    {
        _loadingThemes = true;
        try
        {
            var active = ActiveTheme;
            ThemeCombo.Items.Clear();
            foreach (var theme in NoteThemes.All(_settings.CustomThemes))
            {
                ThemeCombo.Items.Add(new ComboBoxItem
                {
                    Content = Strings.ThemeDisplayName(theme),
                    Tag = theme.Id,
                    Style = (Style)FindResource("SyncProfileItemStyle"),
                    IsSelected = theme.Id == active.Id,
                });
            }

            NoteSwatchPanel.Fill(ThemePreview, active, selectedColor: null, onClick: (_, _) => { });
            ThemeEditButton.IsEnabled = ThemeDeleteButton.IsEnabled = !active.IsBuiltIn;

            FillRadios(ToneListContainer, "ToneGroup", _settings.NewNoteTone,
                (NoteTone.Light, Strings.ToneLight), (NoteTone.Dark, Strings.ToneDark), (NoteTone.Both, Strings.ToneBoth));
            FillRadios(AssignmentListContainer, "AssignmentGroup", _settings.ColorAssignment,
                (NoteColorAssignment.RotateAvoidNeighbors, Strings.AssignAvoidNeighbors),
                (NoteColorAssignment.Rotate, Strings.AssignRotate),
                (NoteColorAssignment.MostDistinct, Strings.AssignMostDistinct),
                (NoteColorAssignment.Fixed, Strings.AssignFixed));

            bool isFixed = _settings.ColorAssignment == NoteColorAssignment.Fixed;
            FixedColorSwatches.Visibility = isFixed ? Visibility.Visible : Visibility.Collapsed;
            if (isFixed)
            {
                var fixedColor = NoteColorAssigner.Assign(active, _settings.NewNoteTone,
                    NoteColorAssignment.Fixed, _settings.FixedNoteColor, []);
                NoteSwatchPanel.Fill(FixedColorSwatches, active, fixedColor, OnFixedColorClick);
            }
        }
        finally
        {
            _loadingThemes = false;
        }
    }

    private void FillRadios<T>(Panel host, string group, T current, params (T Value, string Label)[] options)
        where T : struct, Enum
    {
        host.Children.Clear();
        foreach (var (value, label) in options)
        {
            var radio = new RadioButton
            {
                GroupName = group,
                Style = (Style)FindResource("MonitorRadioStyle"),
                Content = label,
                Tag = value,
                IsChecked = EqualityComparer<T>.Default.Equals(value, current),
            };
            radio.Checked += OnThemeRadioChecked;
            host.Children.Add(radio);
        }
    }

    private void OnThemeRadioChecked(object sender, RoutedEventArgs e)
    {
        if (_loadingThemes || sender is not RadioButton { Tag: var tag }) return;
        if (tag is NoteTone tone) _settings.NewNoteTone = tone;
        if (tag is NoteColorAssignment rule) _settings.ColorAssignment = rule;
        SaveThemeSettings();
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingThemes || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        _settings.ActiveThemeId = id;
        SaveThemeSettings();
    }

    private void OnFixedColorClick(object sender, MouseButtonEventArgs e)
    {
        _settings.FixedNoteColor = (string)((Border)sender).Tag;
        SaveThemeSettings();
    }

    private void OnThemeNewClick(object sender, RoutedEventArgs e)
    {
        var created = ThemeEditorWindow.Show(this, new NoteTheme
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Strings.ThemeNewName,
        });
        if (created is null) return;

        _settings.CustomThemes.Add(created);
        _settings.ActiveThemeId = created.Id;
        SaveThemeSettings();
    }

    private void OnThemeDuplicateClick(object sender, RoutedEventArgs e)
    {
        var source = ActiveTheme;
        var copy = NoteThemes.Duplicate(source, Strings.ThemeCopyName(Strings.ThemeDisplayName(source)));
        _settings.CustomThemes.Add(copy);
        _settings.ActiveThemeId = copy.Id;
        SaveThemeSettings();
    }

    private void OnThemeEditClick(object sender, RoutedEventArgs e)
    {
        var current = ActiveTheme;
        if (current.IsBuiltIn) return;

        var edited = ThemeEditorWindow.Show(this, current);
        if (edited is null) return;

        int index = _settings.CustomThemes.FindIndex(theme => theme.Id == current.Id);
        if (index >= 0) _settings.CustomThemes[index] = edited;
        SaveThemeSettings();
    }

    private void OnThemeDeleteClick(object sender, RoutedEventArgs e)
    {
        var current = ActiveTheme;
        if (current.IsBuiltIn) return;

        var answer = MessageBox.Show(this, Strings.ThemeDeleteConfirm(current.Name), Strings.AppName,
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        _settings.CustomThemes.RemoveAll(theme => theme.Id == current.Id);
        _settings.ActiveThemeId = null;
        SaveThemeSettings();
    }

    private void OnApplyThemeClick(object sender, RoutedEventArgs e)
    {
        if (_coordinator is null) return;

        var answer = MessageBox.Show(this, Strings.ApplyThemeConfirm(_coordinator.ActiveNoteCount),
            Strings.ApplyThemeConfirmTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        _coordinator.ApplyThemeToActiveNotes();
    }

    private void SaveThemeSettings()
    {
        _settingsService.Save(_settings);
        RefreshThemeSection();
        _coordinator?.RefreshAll();
    }
```

(Añadir `using Aldune.Core;` y `using System.Windows.Controls;` si el fichero no los tiene.)

**Nota:** `ThemeEditorWindow` no existe hasta la tarea 9. Para que esta tarea compile sola, crear ya
`ThemeEditorWindow.xaml.cs` con este esqueleto, que la tarea 9 sustituye entero:

```csharp
using System.Windows;
using Aldune.Core;

namespace Aldune.Windowing;

internal static class ThemeEditorWindow
{
    internal static NoteTheme? Show(Window owner, NoteTheme theme) => null;
}
```

- [ ] **Step 6: Verificar**

Build y tests. Luego a mano, con la app en Debug (`dotnet run --project src/Aldune/Aldune.csproj`):
1. Ajustes enseña "Colores de las notas" con Clásico marcado y su tira de colores.
2. Elegir Sereno, "Oscuras" y crear una nota desde el dock: sale Grafito `#2E3034`. Crear otra:
   Pizarra.
3. El menú ⋯ de una nota abierta enseña dos filas (oscuros y claros) de Sereno.
4. "Aplicar a las notas existentes…" → Aceptar: las notas del dock y **las ventanas abiertas**
   cambian de color.
5. "Siempre el mismo color": aparecen las pastillas del tema y la marcada es la que usa la nota
   nueva siguiente.

---

### Task 9: Editor de temas

**Files:**
- Modify: `src/Aldune/App.xaml` (estilos compartidos), `src/Aldune/Windowing/CustomColorWindow.xaml` (quitar sus copias locales)
- Create: `src/Aldune/Windowing/ThemeEditorWindow.xaml`; replace: `src/Aldune/Windowing/ThemeEditorWindow.xaml.cs`
- Modify: `src/Aldune/Resources/Strings.cs`

**Interfaces:**
- Consumes: `CustomColorWindow.Show(Window, string) → string?`, `NoteSwatchPanel` (solo el aspecto), `NoteColorPalette.LabelFor`.
- Produces: `public static NoteTheme? ThemeEditorWindow.Show(Window owner, NoteTheme theme)`. Devuelve
  una copia editada con el mismo Id, o null si se cancela.

- [ ] **Step 1: Estilos compartidos**

Mover `ColorDialogButtonStyle`, `PrimaryColorDialogButtonStyle` y `ColorTextBoxStyle` de
`CustomColorWindow.xaml` (`Window.Resources`) a `App.xaml` (`Application.Resources`), sin cambiar
nombres ni contenido, y borrarlos de `CustomColorWindow.xaml`. Build: el diálogo de color tiene que
verse igual.

- [ ] **Step 2: Textos**

```csharp
    public static string ThemeEditorTitle => T("Edit theme", "Editar tema");
    public static string ThemeEditorNameLabel => T("Name", "Nombre");
    public static string ThemeDarkColors => T("Dark (light text)", "Oscuros (texto claro)");
    public static string ThemeLightColors => T("Light (dark text)", "Claros (texto oscuro)");
    public static string ThemeAddColor => T("Add color", "Añadir color");
    public static string ThemeMoveLeft => T("Move left", "Mover a la izquierda");
    public static string ThemeMoveRight => T("Move right", "Mover a la derecha");
    public static string ThemeRemoveColor => T("Remove", "Quitar");
    public static string ThemeEditorEmpty => T("Add at least one color.", "Añade al menos un color.");
```

- [ ] **Step 3: XAML**

`ThemeEditorWindow.xaml`:

```xml
<Window x:Class="Aldune.Windowing.ThemeEditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:res="clr-namespace:Aldune.Resources"
        Title="{x:Static res:Strings.ThemeEditorTitle}"
        Width="440" SizeToContent="Height"
        WindowStyle="None" AllowsTransparency="False"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize" ShowInTaskbar="False"
        Background="#2A261F"
        PreviewKeyDown="OnWindowKeyDown"
        SourceInitialized="OnSourceInitialized">
    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="34" ResizeBorderThickness="0" GlassFrameThickness="-1" CornerRadius="0" />
    </WindowChrome.WindowChrome>

    <Border Background="#2A261F" BorderBrush="#5A5146" BorderThickness="1" Padding="20,17,20,16">
        <StackPanel>
            <Grid>
                <TextBlock Text="{x:Static res:Strings.ThemeEditorTitle}"
                           Foreground="#F5F0E6" FontSize="17" FontWeight="SemiBold" />
                <Button Content="&#x2715;" Width="27" Height="27" HorizontalAlignment="Right" Padding="0"
                        Style="{StaticResource ColorDialogButtonStyle}" Click="OnCancelClick"
                        WindowChrome.IsHitTestVisibleInChrome="True"
                        ToolTip="{x:Static res:Strings.CloseTooltip}"
                        AutomationProperties.Name="{x:Static res:Strings.CloseTooltip}" />
            </Grid>

            <TextBlock Text="{x:Static res:Strings.ThemeEditorNameLabel}"
                       Foreground="#A79E90" FontSize="11" Margin="0,16,0,6" />
            <TextBox x:Name="NameBox" Style="{StaticResource ColorTextBoxStyle}" MaxLength="40" />

            <TextBlock Text="{x:Static res:Strings.ThemeDarkColors}"
                       Foreground="#A79E90" FontSize="11" Margin="0,16,0,6" />
            <WrapPanel x:Name="DarkRow" />

            <TextBlock Text="{x:Static res:Strings.ThemeLightColors}"
                       Foreground="#A79E90" FontSize="11" Margin="0,12,0,6" />
            <WrapPanel x:Name="LightRow" />

            <WrapPanel Margin="0,14,0,0">
                <Button Content="{x:Static res:Strings.ThemeAddColor}" Click="OnAddClick"
                        Margin="0,0,6,6" Style="{StaticResource ColorDialogButtonStyle}" />
                <Button x:Name="MoveLeftButton" Content="&#x2190;" Click="OnMoveLeftClick"
                        ToolTip="{x:Static res:Strings.ThemeMoveLeft}"
                        AutomationProperties.Name="{x:Static res:Strings.ThemeMoveLeft}"
                        Margin="0,0,6,6" Style="{StaticResource ColorDialogButtonStyle}" />
                <Button x:Name="MoveRightButton" Content="&#x2192;" Click="OnMoveRightClick"
                        ToolTip="{x:Static res:Strings.ThemeMoveRight}"
                        AutomationProperties.Name="{x:Static res:Strings.ThemeMoveRight}"
                        Margin="0,0,6,6" Style="{StaticResource ColorDialogButtonStyle}" />
                <Button x:Name="RemoveButton" Content="{x:Static res:Strings.ThemeRemoveColor}" Click="OnRemoveClick"
                        Margin="0,0,6,6" Style="{StaticResource ColorDialogButtonStyle}" />
            </WrapPanel>

            <TextBlock x:Name="ErrorText" Foreground="#E0A08A" FontSize="11" Margin="0,6,0,0"
                       Visibility="Collapsed" Text="{x:Static res:Strings.ThemeEditorEmpty}" />

            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
                <Button Content="{x:Static res:Strings.CustomColorCancel}" Click="OnCancelClick"
                        Margin="0,0,8,0" Style="{StaticResource ColorDialogButtonStyle}" />
                <Button x:Name="SaveButton" Content="{x:Static res:Strings.ReminderSave}" Click="OnSaveClick"
                        IsDefault="True" Style="{StaticResource PrimaryColorDialogButtonStyle}" />
            </StackPanel>
        </StackPanel>
    </Border>
</Window>
```

- [ ] **Step 4: Código**

`ThemeEditorWindow.xaml.cs` (sustituye el esqueleto de la tarea 8):

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Crear o editar un tema propio: nombre y dos filas de colores. Un color se selecciona pulsándolo,
/// y "←", "→" y "Quitar" actúan sobre él. Se usan botones y no arrastre porque es más fiable y va
/// con teclado. Los colores nuevos salen del CustomColorWindow de siempre, que solo deja elegir
/// colores legibles; van a la fila que les toca por su claridad.
/// </summary>
public partial class ThemeEditorWindow : Window
{
    private readonly NoteTheme _theme;
    private string? _selected;

    private ThemeEditorWindow(NoteTheme theme)
    {
        InitializeComponent();
        _theme = new NoteTheme
        {
            Id = theme.Id,
            Name = theme.Name,
            DarkColors = theme.DarkColors.ToList(),
            LightColors = theme.LightColors.ToList(),
        };
        NameBox.Text = _theme.Name;
        Render();
        Loaded += (_, _) => NameBox.Focus();
    }

    public static NoteTheme? Show(Window owner, NoteTheme theme)
    {
        var dialog = new ThemeEditorWindow(theme) { Owner = owner, Topmost = owner.Topmost };
        return dialog.ShowDialog() == true ? dialog._theme : null;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    private void Render()
    {
        FillRow(DarkRow, _theme.DarkColors);
        FillRow(LightRow, _theme.LightColors);
        MoveLeftButton.IsEnabled = MoveRightButton.IsEnabled = RemoveButton.IsEnabled = _selected is not null;
        bool empty = _theme.DarkColors.Count == 0 && _theme.LightColors.Count == 0;
        SaveButton.IsEnabled = !empty;
        ErrorText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FillRow(WrapPanel row, List<string> colors)
    {
        row.Children.Clear();
        foreach (var color in colors)
        {
            var label = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.LabelFor(color))!;
            bool selected = color == _selected;
            var swatch = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(color)!,
                Width = 30, Height = 30, Margin = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                BorderBrush = label,
                BorderThickness = new Thickness(selected ? 2 : 0),
                Cursor = Cursors.Hand,
                Tag = color,
                ToolTip = color,
            };
            swatch.MouseLeftButtonUp += (_, _) => { _selected = color; Render(); };
            row.Children.Add(swatch);
        }
    }

    private List<string>? ListOfSelected() =>
        _selected is null ? null
        : _theme.DarkColors.Contains(_selected) ? _theme.DarkColors
        : _theme.LightColors.Contains(_selected) ? _theme.LightColors
        : null;

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var color = CustomColorWindow.Show(this, _selected ?? "#EBE6D9");
        if (color is null) return;

        color = color.ToUpperInvariant();
        var row = NoteColorDerivation.IsDark(color) ? _theme.DarkColors : _theme.LightColors;
        if (!row.Contains(color)) row.Add(color);
        _selected = color;
        Render();
    }

    private void OnMoveLeftClick(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveRightClick(object sender, RoutedEventArgs e) => Move(+1);

    private void Move(int delta)
    {
        if (ListOfSelected() is not { } list) return;
        int index = list.IndexOf(_selected!);
        int target = index + delta;
        if (target < 0 || target >= list.Count) return;
        (list[index], list[target]) = (list[target], list[index]);
        Render();
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        ListOfSelected()?.Remove(_selected!);
        _selected = null;
        Render();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_theme.DarkColors.Count == 0 && _theme.LightColors.Count == 0) return;
        _theme.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? Strings.ThemeNewName : NameBox.Text.Trim();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
```

(Añadir `using Aldune.Resources;` para `Strings`.)

- [ ] **Step 5: Verificar**

Build y tests. A mano: Ajustes → Duplicar Sereno → Editar → añadir un color oscuro (va a la fila
de oscuros), moverlo con ←, quitar otro, renombrar y Guardar. El combo enseña el nombre nuevo y la
tira el orden nuevo. Crear una nota: usa el tema editado. Cerrar y abrir la app: el tema sigue ahí.
Eliminarlo: vuelve a Clásico.

---

### Task 10: Cierre

**Files:**
- Modify: `docs/superpowers/plans/2026-09-23-aldune-1.0-cierre.md` (Fase 2 → hecha)
- Modify: `docs/STATUS.md` (entrada de temas, en el estilo de las anteriores)

- [ ] **Step 1: Suite completa y build de Release**

Run: `dotnet build Aldune.slnx -c Release` y `dotnet test Aldune.slnx`
Expected: 0 errores, 0 avisos nuevos, todos los tests en verde.

- [ ] **Step 2: Recorrido manual completo** (lo hace el usuario, con la lista de las tareas 8 y 9
  más estas):
  - Actualizar desde una instalación con notas: todo se ve igual que antes (Clásico).
  - Dock en el canto izquierdo y arriba: pestañas oscuras con el filo arriba, sin reflejo.
  - Nota oscura abierta: texto, casillas, selección y barra de scroll legibles.
  - Sincronizar tras aplicar un tema: el otro dispositivo recibe los colores nuevos.

- [ ] **Step 3: Documentación**

En el plan de cierre, marcar la Fase 2 como hecha con fecha. En `STATUS.md`, una entrada con qué
se hizo, dónde vive (Core: `OklchColor`, `NoteColorDerivation`, `NoteThemes`, `NoteColorAssigner`)
y las decisiones: temas locales y no sincronizados, Clásico por defecto, filo superior solo en
caras oscuras.

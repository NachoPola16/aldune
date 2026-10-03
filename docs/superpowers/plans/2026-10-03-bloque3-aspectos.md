# Bloque 3: los seis aspectos retro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Los seis aspectos de la spec (XP claro, XP + 95 oscuro, telecomunicaciones claro y oscuro,
bash, fósforo), con sus colores personalizables, su piel, la propuesta de tema de notas al elegirlos y
la página Ajustes → Aspecto ampliada; más lo que el bloque 2 dejó para aquí.

**Architecture:** Todo lo que decide colores vive en Core y se prueba: un catálogo de aspectos (huecos de
color, valores por defecto, sugerencias, paletas predefinidas, tema propuesto), la derivación de cada
paleta a partir de Oscuro o Claro (`AppPalette.Tint`) más los colores del usuario, ajustando la claridad
cuando hace falta para que todo texto se lea (`ColorFit`), y la piel de cada aspecto (`AppSkin.For`). La
capa WPF pasa los colores elegidos a `ThemeManager.Apply` y pinta la página de Ajustes.

**Tech Stack:** C# / .NET 10, WPF, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-03-aspectos-retro-design.md` (secciones 1, 2, 3 —valores por
defecto según el aspecto—, 4 —tema propuesto— y 5). Maquetas: lienzo
<https://claude.ai/artifact/N2AdurN792fDCFb3WeiCSX> (siete mesas; los valores por defecto y las
sugerencias de color de este plan salen de sus *Tweaks*).

El bloque 4 (exportar/importar) tiene su propio plan.

## Decisiones tomadas al planificar

- **Fósforo, estilo de tarjeta propio `Mono`**: un monitor monocromo no enseña colores, así que las notas
  se pintan con el fondo y el texto del fósforo y las cápsulas en un tono apagado del mismo; el color de
  cada nota no se ve (tampoco el «mismo color»). Es lo que dibuja la maqueta. Si el usuario quiere colores
  en Fósforo, la alternativa es `Stripe`: cambiar una línea de `AppSkin.For`.
- **Esquinas y señal al cambiar de aspecto**: elegir un aspecto vuelve a poner las dos opciones en «lo
  que diga el aspecto» (`null`). Quien elige bash espera verlo como en la maqueta; después puede cambiar
  cualquiera de las dos y eso se respeta hasta el siguiente cambio de aspecto.
- **Paletas predefinidas de bash** (Gruvbox, Ubuntu, Tango) como *preajustes*: un botón que rellena a la
  vez los cuatro huecos (terminal, usuario, ruta, acento). No son un ajuste aparte.
- **Sugerencias por hueco**: cada hueco trae 3–4 colores de la maqueta (azul Luna, oliva, plata, teja…)
  como muestras de un clic, además de «Elegir color…» (el selector que ya existe).
- **Brillo de XP claro**: un indicador más en la piel (`Gloss`) que pone un reflejo en los botones del pie
  del dock.
- **Cabeceras de Ajustes y gestor** con una barra en degradado: se compactan como barra de título de
  verdad (relleno, título de 13 px en negrita, subtítulo y botones en el color del texto de la barra).
- **Diálogos menores** (AppDialog, CustomColorWindow, PasswordPrompt, SyncConflicts, ThemeEditor, Toast):
  mantienen su disposición; toman la paleta y la tipografía solos, y sus botones (los estilos compartidos)
  llevan relieve. No se les añade barra.
- **XP claro** propone el tema de notas XP; **los oscuros** (XP + 95, telecomunicaciones oscuro, bash,
  fósforo) proponen Sereno; **telecomunicaciones claro** no propone ninguno.

## Global Constraints

- Textos de interfaz siempre con `Strings.T(en, es, de, fr, pt)` en `src/Aldune/Resources/Strings.cs`, los cinco idiomas.
- Comentarios en español, explicando el porqué, con la densidad del código de alrededor.
- Lógica nueva en Core con test primero (TDD). La UI se verifica con sondas (`docs/WPF_PROBES.md`).
- `.cs` y `.xaml` con BOM UTF-8; documentación sin BOM.
- Un `settings.json` antiguo tiene que cargar sin migración: campos nuevos con valor por defecto que funcione cuando falta; enums como número (`AppearanceMode` nuevos: 5 a 10). Una versión anterior que lea un 5–10 cae en Oscuro (`_` de `AppPalette.For`): no hay que hacer nada para eso.
- Formato de sync **4**: no se sube (los ajustes no se sincronizan).
- Colores de chrome con `{DynamicResource Aldune<Clave>Brush}`; nunca hex sueltos nuevos en XAML.
- Una clave de paleta nueva va en Dark y en Light; los tests fijan su contraste (este bloque no añade claves).
- **Ningún aspecto existente cambia** (Oscuro, Claro, Como Windows, Pastel, Medianoche).
- Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`: sondas con base de datos temporal.
- Las sondas de teclado y el smoke test mueven ratón/teclado: avisar al usuario y esperar su "ok".
- Commits sin la línea `Co-Authored-By`.
- Comandos: `dotnet build Aldune.slnx -c Debug`, `dotnet test Aldune.slnx --no-build` (todos verdes; solo los 4 avisos CA1416 conocidos).

## Review Focus

1. **Colores extremos del usuario** (blanco, negro, amarillo puro en cada hueco) no dejan ningún texto
   ilegible en ninguna paleta → los tests de contraste de paleta corren también sobre cada aspecto con
   esos colores (Task 3).
2. **Un `settings.json` con colores raros** (`"rojo"`, `""`, un hueco que no existe, un aspecto que no
   existe) se ignora sin error y cae en los colores por defecto → tests de `AspectCatalog.Resolve` (Task 2).
3. **Colores sin matiz** (fósforo blanco, barra plateada, gris) no tiñen la paleta de un matiz inventado
   → test de croma (Task 3).
4. **Cambiar de aspecto en vivo** con notas y dock abiertos: paleta, piel, esquinas y señal pasan a las
   del aspecto nuevo sin reiniciar; una elección explícita del usuario sobrevive al reinicio pero no al
   siguiente cambio de aspecto → tests de `AppearanceChoice` (Task 4) y sonda (Task 7).
5. **La pregunta del tema de notas** no aparece si el tema propuesto ya es el activo, y decir que no deja
   el tema como estaba → test de `AppearanceChoice.Select` (Task 4) y sonda.

---

### Task 1: `ColorFit` — ajustar la claridad para que un texto se lea

**Files:**
- Create: `src/Aldune.Core/ColorFit.cs`
- Test: `tests/Aldune.Core.Tests/ColorFitTests.cs`

**Interfaces:**
- Produces: `ColorFit.Ratio(string a, string b) : double`; `ColorFit.Background(string background, string text, double minimum) : string`; `ColorFit.Foreground(string foreground, string background, double minimum) : string`; `ColorFit.Lighter(string color, double amount) : string`; `ColorFit.Darker(string color, double amount) : string`.

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/ColorFitTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ColorFitTests
{
    [Fact]
    public void Ratio_IsTheWcagContrast()
    {
        Assert.Equal(21, ColorFit.Ratio("#000000", "#FFFFFF"), 1);
        Assert.Equal(1, ColorFit.Ratio("#808080", "#808080"), 3);
        Assert.Equal(1, ColorFit.Ratio("rojo", "#FFFFFF"), 3);
    }

    [Theory]
    [InlineData("#FFFF00")]
    [InlineData("#FFFFFF")]
    [InlineData("#8C8CA8")]
    [InlineData("#0055E5")]
    public void Background_UnderWhiteText_DarkensJustEnough(string bar)
    {
        var fitted = ColorFit.Background(bar, "#FFFFFF", 4.5);

        Assert.True(ColorFit.Ratio(fitted, "#FFFFFF") >= 4.5, fitted);
    }

    [Fact]
    public void Background_AlreadyReadable_IsUnchanged()
    {
        Assert.Equal("#0055E5", ColorFit.Background("#0055e5", "#FFFFFF", 4.5));
    }

    [Fact]
    public void Background_UnderDarkText_Lightens()
    {
        var fitted = ColorFit.Background("#3465A4", "#201B16", 4.5);

        Assert.True(ColorFit.Ratio(fitted, "#201B16") >= 4.5, fitted);
        Assert.True(OklchColor.TryFromHex(fitted, out var after) && OklchColor.TryFromHex("#3465A4", out var before));
        Assert.True(after.L > before.L);
    }

    [Fact]
    public void Fitting_KeepsTheHue()
    {
        // Lo que el usuario elige es el matiz: la claridad se ajusta, el color se reconoce.
        Assert.True(OklchColor.TryFromHex("#E95420", out var chosen));
        Assert.True(OklchColor.TryFromHex(ColorFit.Background("#E95420", "#FFFFFF", 4.5), out var fitted));
        Assert.True(Math.Abs(chosen.H - fitted.H) < 15, $"{chosen.H} → {fitted.H}");
    }

    [Theory]
    [InlineData("#FFFFFF", "#F1EFE4")]
    [InlineData("#000000", "#171C21")]
    [InlineData("#FFFF00", "#F6F1E8")]
    public void Foreground_OnAFixedBackground_BecomesReadable(string text, string background)
    {
        Assert.True(ColorFit.Ratio(ColorFit.Foreground(text, background, 4.5), background) >= 4.5);
    }

    [Fact]
    public void LighterAndDarker_MoveTheLightness()
    {
        Assert.True(OklchColor.TryFromHex(ColorFit.Lighter("#1C4FA8", 0.1), out var lighter));
        Assert.True(OklchColor.TryFromHex(ColorFit.Darker("#1C4FA8", 0.1), out var darker));
        Assert.True(OklchColor.TryFromHex("#1C4FA8", out var original));
        Assert.True(lighter.L > original.L && darker.L < original.L);
    }
}
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~ColorFitTests"`. Expected: no compila.

- [ ] **Step 3: Implementar**

`src/Aldune.Core/ColorFit.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>
/// Un color elegido por el usuario tiene que seguir dejando leer el texto que va encima o al lado (spec,
/// sección 2): se mueve su claridad OKLCH lo justo, conservando matiz y croma, que es lo que el usuario
/// está eligiendo. Una barra demasiado clara para texto blanco se oscurece hasta 4.5:1; un acento
/// demasiado oscuro bajo texto oscuro se aclara.
/// </summary>
public static class ColorFit
{
    /// <summary>Contraste WCAG entre dos #RRGGBB; 1 si alguno no es válido.</summary>
    public static double Ratio(string a, string b)
    {
        if (!NoteColorContrast.TryGetLuminance(a, out var la) || !NoteColorContrast.TryGetLuminance(b, out var lb)) return 1;
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Mueve el fondo hasta que <paramref name="text"/> se lea con al menos <paramref name="minimum"/>.</summary>
    public static string Background(string background, string text, double minimum) => Adjust(background, text, minimum);

    /// <summary>Mueve el texto hasta que se lea sobre <paramref name="background"/> con al menos <paramref name="minimum"/>.</summary>
    public static string Foreground(string foreground, string background, double minimum) => Adjust(foreground, background, minimum);

    public static string Lighter(string color, double amount) => Shift(color, amount);

    public static string Darker(string color, double amount) => Shift(color, -amount);

    private static string Shift(string color, double amount) =>
        OklchColor.TryFromHex(color, out var c) ? (c with { L = Math.Clamp(c.L + amount, 0, 1) }).ToHex() : color;

    private static string Adjust(string movable, string fixedColor, double minimum)
    {
        if (!OklchColor.TryFromHex(movable, out var color) || !NoteColorContrast.TryGetLuminance(fixedColor, out var fixedLuminance))
            return movable;
        if (Ratio(movable, fixedColor) >= minimum) return movable.ToUpperInvariant();

        // Contra un color claro se oscurece; contra uno oscuro se aclara. 0.18 es la luminancia en la
        // que el negro y el blanco dan el mismo contraste.
        bool darken = fixedLuminance > 0.18;
        double low = darken ? 0 : color.L, high = darken ? color.L : 1;
        string best = new OklchColor(darken ? 0 : 1, color.C, color.H).ToHex();
        // Búsqueda binaria de la claridad más cercana a la elegida que todavía llega al mínimo.
        for (int i = 0; i < 30; i++)
        {
            double middle = (low + high) / 2;
            var candidate = (color with { L = middle }).ToHex();
            bool readable = Ratio(candidate, fixedColor) >= minimum;
            if (darken)
            {
                if (readable) { best = candidate; low = middle; } else high = middle;
            }
            else
            {
                if (readable) { best = candidate; high = middle; } else low = middle;
            }
        }
        return best;
    }
}
```

(Si `OklchColor.ToHex()` no recorta al gamut sRGB, comprobarlo con el test de amarillo puro: si da un
hex inválido o el matiz salta, recortar el croma en `Adjust` antes de convertir y anotarlo.)

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/ColorFit.cs tests/Aldune.Core.Tests/ColorFitTests.cs
git commit -m "ColorFit: ajusta la claridad de un color elegido para que el texto se lea, conservando su matiz"
```

---

### Task 2: Aspectos nuevos y su catálogo de colores

**Files:**
- Modify: `src/Aldune.Core/AppPalette.cs` (enum `AppearanceMode`, `IsLight`), `src/Aldune.Core/AppSettings.cs`
- Create: `src/Aldune.Core/AspectCatalog.cs`
- Test: `tests/Aldune.Core.Tests/AspectCatalogTests.cs`, `tests/Aldune.Core.Tests/SettingsServiceTests.cs`, `tests/Aldune.Core.Tests/AppPaletteTests.cs` (`IsLight`)

**Interfaces:**
- Produces: `AppearanceMode.XpLight = 5, XpDark = 6, TelecomLight = 7, TelecomDark = 8, Bash = 9, Phosphor = 10`; `sealed record AspectColorSlot(string Id, string Default, IReadOnlyList<string> Suggestions)`; `sealed record AspectPreset(string Id, string Name, IReadOnlyDictionary<string, string> Colors)`; `sealed record AspectDefinition(AppearanceMode Mode, string Id, IReadOnlyList<AspectColorSlot> Slots, IReadOnlyList<AspectPreset> Presets, string? SuggestedThemeId)`; `AspectCatalog.Retro : IReadOnlyList<AspectDefinition>`, `AspectCatalog.For(AppearanceMode) : AspectDefinition?`, `AspectCatalog.Resolve(AppearanceMode, IReadOnlyDictionary<string, string>?) : IReadOnlyDictionary<string, string>`; `AppSettings.AspectColors : Dictionary<string, Dictionary<string, string>>?`, `AppSettings.ColorsFor(AppearanceMode) : IReadOnlyDictionary<string, string>?`.

Ids de hueco (los usan la Task 3 y la Task 6): `xp-light` → `titleBar`, `accent`; `xp-dark` → `titleBar`,
`accent`; `telecom-light` → `ink`, `accent`; `telecom-dark` → `panel`, `trace`; `bash` → `background`,
`user`, `path`, `accent`; `phosphor` → `phosphor`, `details`.

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/AspectCatalogTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AspectCatalogTests
{
    [Fact]
    public void Retro_AreTheSixAspectsOfTheSpec_WithNumbersFrom5()
    {
        Assert.Equal(
            new[] { AppearanceMode.XpLight, AppearanceMode.XpDark, AppearanceMode.TelecomLight,
                    AppearanceMode.TelecomDark, AppearanceMode.Bash, AppearanceMode.Phosphor },
            AspectCatalog.Retro.Select(a => a.Mode));
        Assert.Equal(new[] { 5, 6, 7, 8, 9, 10 }, AspectCatalog.Retro.Select(a => (int)a.Mode));
        Assert.Equal(6, AspectCatalog.Retro.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public void ExistingAspects_HaveNoCatalogEntry()
    {
        foreach (var mode in new[] { AppearanceMode.Dark, AppearanceMode.Light, AppearanceMode.System, AppearanceMode.Pastel, AppearanceMode.Midnight })
            Assert.Null(AspectCatalog.For(mode));
    }

    [Fact]
    public void EverySlot_HasAValidDefaultAndSuggestions()
    {
        foreach (var aspect in AspectCatalog.Retro)
            foreach (var slot in aspect.Slots)
            {
                Assert.True(NoteDisplayColor.IsActive(slot.Default), $"{aspect.Id}.{slot.Id}");
                Assert.All(slot.Suggestions, s => Assert.True(NoteDisplayColor.IsActive(s), $"{aspect.Id}.{slot.Id} {s}"));
            }
    }

    [Fact]
    public void BashPresets_FillEverySlot()
    {
        var bash = AspectCatalog.For(AppearanceMode.Bash)!;
        Assert.Equal(new[] { "gruvbox", "ubuntu", "tango" }, bash.Presets.Select(p => p.Id));
        foreach (var preset in bash.Presets)
            Assert.Equal(bash.Slots.Select(s => s.Id).Order(), preset.Colors.Keys.Order());
    }

    [Fact]
    public void Resolve_WithNothingChosen_GivesTheDefaults()
    {
        var colors = AspectCatalog.Resolve(AppearanceMode.XpLight, null);

        Assert.Equal("#0055E5", colors["titleBar"]);
        Assert.Equal("#3C9A3C", colors["accent"]);
    }

    [Fact]
    public void Resolve_KeepsValidChoices_AndIgnoresTheRest()
    {
        // Un settings.json editado a mano no puede romper el aspecto: lo inválido cae en el valor por defecto.
        var chosen = new Dictionary<string, string>
        {
            ["titleBar"] = "#5a7a2e",
            ["accent"] = "rojo",
            ["unknown"] = "#123456",
        };

        var colors = AspectCatalog.Resolve(AppearanceMode.XpLight, chosen);

        Assert.Equal("#5A7A2E", colors["titleBar"]);
        Assert.Equal("#3C9A3C", colors["accent"]);
        Assert.False(colors.ContainsKey("unknown"));
    }

    [Fact]
    public void SuggestedThemes_FollowTheSpec()
    {
        Assert.Equal(NoteThemes.XpId, AspectCatalog.For(AppearanceMode.XpLight)!.SuggestedThemeId);
        Assert.Null(AspectCatalog.For(AppearanceMode.TelecomLight)!.SuggestedThemeId);
        foreach (var mode in new[] { AppearanceMode.XpDark, AppearanceMode.TelecomDark, AppearanceMode.Bash, AppearanceMode.Phosphor })
            Assert.Equal(NoteThemes.SereneId, AspectCatalog.For(mode)!.SuggestedThemeId);
    }
}
```

En `SettingsServiceTests` (mismo estilo que los existentes):

```csharp
    [Fact]
    public void AspectColors_RoundTrip_AndAnOldFileHasNone()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{\"AutoHideCompletedTasks\":true}");
        var service = new SettingsService(_settingsPath);
        var settings = service.Load();
        Assert.Null(settings.AspectColors);
        Assert.Null(settings.ColorsFor(AppearanceMode.Bash));

        settings.Appearance = AppearanceMode.Bash;
        settings.AspectColors = new() { ["bash"] = new() { ["accent"] = "#458588" } };
        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(AppearanceMode.Bash, loaded.Appearance);
        Assert.Equal("#458588", loaded.ColorsFor(AppearanceMode.Bash)!["accent"]);
        Assert.Contains("\"Appearance\":9", File.ReadAllText(_settingsPath).Replace(" ", ""));
    }
```

En `AppPaletteTests.IsLight_FollowsTheChoiceOrWindows`, añadir:

```csharp
    [InlineData(AppearanceMode.XpLight, false, true)]
    [InlineData(AppearanceMode.TelecomLight, false, true)]
    [InlineData(AppearanceMode.XpDark, true, false)]
    [InlineData(AppearanceMode.TelecomDark, true, false)]
    [InlineData(AppearanceMode.Bash, true, false)]
    [InlineData(AppearanceMode.Phosphor, true, false)]
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~AspectCatalog|FullyQualifiedName~AspectColors_RoundTrip|FullyQualifiedName~IsLight"`. Expected: no compila.

- [ ] **Step 3: Implementar**

En `AppPalette.cs`, el enum (tras `Midnight = 4`):

```csharp
    /// <summary>Windows XP (Luna): beige, barra azul en degradado vertical, Tahoma.</summary>
    XpLight = 5,
    /// <summary>Windows 95 con la barra de Windows 98 y el acento de XP, en grafito oscuro.</summary>
    XpDark = 6,
    /// <summary>Papel de instrumento de laboratorio: tinta, líneas de 1 px, canales.</summary>
    TelecomLight = 7,
    /// <summary>Pantalla de osciloscopio con retícula.</summary>
    TelecomDark = 8,
    /// <summary>Terminal de Linux (Gruvbox por defecto).</summary>
    Bash = 9,
    /// <summary>Monitor de fósforo antiguo, monocromo.</summary>
    Phosphor = 10,
```

(y actualizar el comentario del enum: "Una versión anterior que lea 5–10 cae en Oscuro").

`IsLight`:

```csharp
    public static bool IsLight(AppearanceMode mode, bool windowsUsesLight) => mode switch
    {
        AppearanceMode.Light or AppearanceMode.Pastel or AppearanceMode.XpLight or AppearanceMode.TelecomLight => true,
        AppearanceMode.System => windowsUsesLight,
        _ => false
    };
```

`src/Aldune.Core/AspectCatalog.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Un color que el usuario puede cambiar en un aspecto, con muestras de un clic.</summary>
public sealed record AspectColorSlot(string Id, string Default, IReadOnlyList<string> Suggestions);

/// <summary>Varios huecos de una vez (las paletas de terminal de bash).</summary>
public sealed record AspectPreset(string Id, string Name, IReadOnlyDictionary<string, string> Colors);

/// <param name="Id">Clave estable en settings.json (<c>AspectColors</c>); no cambiar nunca.</param>
/// <param name="SuggestedThemeId">Tema de notas que el aspecto propone al elegirlo, o null.</param>
public sealed record AspectDefinition(
    AppearanceMode Mode, string Id, IReadOnlyList<AspectColorSlot> Slots, IReadOnlyList<AspectPreset> Presets,
    string? SuggestedThemeId);

/// <summary>
/// Los aspectos retro y sus colores personalizables (spec, sección 2). Valores por defecto y muestras
/// sacados de las maquetas. Los colores se guardan como <c>AspectColors: { "bash": { "accent": "#…" } }</c>;
/// lo que falta o no es un #RRGGBB válido cae en el valor por defecto.
/// </summary>
public static class AspectCatalog
{
    public static IReadOnlyList<AspectDefinition> Retro { get; } =
    [
        new(AppearanceMode.XpLight, "xp-light",
        [
            new("titleBar", "#0055E5", ["#0055E5", "#5A7A2E", "#8C8CA8", "#A8452A"]), // Luna azul, oliva, plata, teja
            new("accent", "#3C9A3C", ["#3C9A3C", "#0055E5", "#C46A1C"]),
        ], [], NoteThemes.XpId),
        new(AppearanceMode.XpDark, "xp-dark",
        [
            new("titleBar", "#1C4FA8", ["#1C4FA8", "#008080", "#6B2E8A", "#8A2E2E"]), // azul marino, verde azulado, morado, granate
            new("accent", "#4C86E8", ["#4C86E8", "#2EA8A8", "#C9A227"]),
        ], [], NoteThemes.SereneId),
        new(AppearanceMode.TelecomLight, "telecom-light",
        [
            new("ink", "#1C2A22", ["#1C2A22", "#1F2A44", "#3A2418"]),
            new("accent", "#2F7A3C", ["#2F7A3C", "#1F57B5", "#9C1F6E"]),
        ], [], null),
        new(AppearanceMode.TelecomDark, "telecom-dark",
        [
            new("panel", "#171C21", ["#171C21", "#10161C", "#1B1B1B"]),
            new("trace", "#F2D338", ["#F2D338", "#3FD0E0", "#7CFC7C"]),
        ], [], NoteThemes.SereneId),
        new(AppearanceMode.Bash, "bash",
        [
            new("background", "#282828", ["#282828", "#300A24", "#1E1E1E"]),
            new("user", "#B8BB26", ["#B8BB26", "#8AE234", "#FABD2F", "#8EC07C"]),
            new("path", "#83A598", ["#83A598", "#729FCF", "#D3869B", "#FE8019"]),
            new("accent", "#D79921", ["#D79921", "#458588", "#98971A", "#B16286"]),
        ],
        [
            // Gruvbox por defecto: cálido y apagado, nada de colores fuertes (decidido con el usuario).
            new("gruvbox", "Gruvbox", new Dictionary<string, string> { ["background"] = "#282828", ["user"] = "#B8BB26", ["path"] = "#83A598", ["accent"] = "#D79921" }),
            new("ubuntu", "Ubuntu", new Dictionary<string, string> { ["background"] = "#300A24", ["user"] = "#8AE234", ["path"] = "#729FCF", ["accent"] = "#E95420" }),
            new("tango", "Tango", new Dictionary<string, string> { ["background"] = "#1E1E1E", ["user"] = "#8AE234", ["path"] = "#729FCF", ["accent"] = "#3465A4" }),
        ], NoteThemes.SereneId),
        new(AppearanceMode.Phosphor, "phosphor",
        [
            new("phosphor", "#A8E6B4", ["#A8E6B4", "#FFB547", "#8EC9FF", "#E8E8E8"]), // verde, ámbar, azul, blanco
            new("details", "#D9A441", ["#D9A441", "#A8E6B4", "#E8E8E8"]),
        ], [], NoteThemes.SereneId),
    ];

    public static AspectDefinition? For(AppearanceMode mode) => Retro.FirstOrDefault(aspect => aspect.Mode == mode);

    /// <summary>Colores efectivos de un aspecto: los elegidos que sean válidos y, para el resto, los de
    /// fábrica. Vacío para los aspectos que no tienen huecos.</summary>
    public static IReadOnlyDictionary<string, string> Resolve(AppearanceMode mode, IReadOnlyDictionary<string, string>? chosen)
    {
        var result = new Dictionary<string, string>();
        if (For(mode) is not { } aspect) return result;
        foreach (var slot in aspect.Slots)
        {
            result[slot.Id] = chosen is not null && chosen.TryGetValue(slot.Id, out var value) && NoteDisplayColor.IsActive(value)
                ? value.ToUpperInvariant()
                : slot.Default;
        }
        return result;
    }
}
```

En `AppSettings.cs`, junto a `Appearance`:

```csharp
    /// <summary>
    /// Colores elegidos para cada aspecto retro: id del aspecto → id del hueco → #RRGGBB (ver
    /// <see cref="AspectCatalog"/>). Nulo o sin entrada = los de fábrica. Se guardan los de todos los
    /// aspectos, no solo el activo: volver a uno ya personalizado lo encuentra como se dejó.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? AspectColors { get; set; }

    /// <summary>Lo elegido para <paramref name="mode"/>, o null. Sin validar: eso lo hace AspectCatalog.Resolve.</summary>
    public IReadOnlyDictionary<string, string>? ColorsFor(AppearanceMode mode) =>
        AspectCatalog.For(mode) is { } aspect && AspectColors?.GetValueOrDefault(aspect.Id) is { } colors ? colors : null;
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2 y la suite de Core. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core tests/Aldune.Core.Tests
git commit -m "Seis aspectos nuevos (5 a 10) y su catalogo de colores personalizables, con valores de las maquetas"
```

---

### Task 3: Paletas de los seis aspectos

**Files:**
- Create: `src/Aldune.Core/AspectPalettes.cs`
- Modify: `src/Aldune.Core/AppPalette.cs` (`For` con colores; `Tint`, `Dark`, `Light` pasan a `internal`)
- Test: `tests/Aldune.Core.Tests/AppPaletteTests.cs`, `tests/Aldune.Core.Tests/NoteFaceTests.cs`

**Interfaces:**
- Consumes: `ColorFit` (Task 1), `AspectCatalog.Resolve` (Task 2), `ColorMix.Toward` (bloque 2).
- Produces: `AppPalette.For(AppearanceMode mode, bool windowsUsesLight, IReadOnlyDictionary<string, string>? aspectColors = null)`; las claves de siempre en cada aspecto.

- [ ] **Step 1: Tests de paleta sobre todos los aspectos y colores extremos**

En `AppPaletteTests`, sustituir el `TheoryData` `Palettes` y el helper `Palette` para que los tests que
ya existen (`EveryPalette_DefinesExactlyTheSameTokensAsDark`, `EveryToken_IsAValidHexColor`,
`Text_IsReadableOnEveryChromeBackground`, `Buttons_AreReadable`, `TitleBarAndPrompt_AreReadable`)
corran sobre todos los aspectos, cada retro con sus colores de fábrica y con blanco, negro y amarillo
puro en todos sus huecos. Los tests que reciben `AppearanceMode` pasan a recibir un `string` con el
nombre del caso:

```csharp
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
```

(El resto de tests que usan `Palette(AppearanceMode)` —`TintedPalettes_AreVisiblyTinted`,
`SkinTokens_AreNeutralInTheExistingPalettes`, `ChannelAndPromptColors_AreNotTinted`, `System_…`— siguen
con la sobrecarga de `AppearanceMode`.)

Tests nuevos en `AppPaletteTests`:

```csharp
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
```

En `NoteFaceTests.Tinted_TitleInTheChannelColor_IsReadable`, ampliar `Palettes` con
`AppearanceMode.TelecomLight` y `AppearanceMode.TelecomDark` (los que usan tarjeta tintada).

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~AppPaletteTests|FullyQualifiedName~NoteFaceTests"`. Expected: no compila (`For` con colores).

- [ ] **Step 3: Implementar**

En `AppPalette.cs`: los diccionarios `Dark` y `Light` y el método `Tint` pasan de `private` a
`internal` (los usa `AspectPalettes`), y `For` gana los colores:

```csharp
    public static IReadOnlyDictionary<string, string> For(
        AppearanceMode mode, bool windowsUsesLight, IReadOnlyDictionary<string, string>? aspectColors = null) => mode switch
    {
        AppearanceMode.Pastel => Pastel.Value,
        AppearanceMode.Midnight => Midnight.Value,
        AppearanceMode.XpLight or AppearanceMode.XpDark or AppearanceMode.TelecomLight
            or AppearanceMode.TelecomDark or AppearanceMode.Bash or AppearanceMode.Phosphor
            => AspectPalettes.Derive(mode, AspectCatalog.Resolve(mode, aspectColors)),
        _ => For(IsLight(mode, windowsUsesLight))
    };
```

`src/Aldune.Core/AspectPalettes.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>
/// Paletas de los aspectos retro. Se derivan de Oscuro o Claro como Pastel y Medianoche (se conserva la
/// claridad de cada clave y se cambia el matiz, <see cref="AppPalette.Tint"/>), así que heredan su
/// contraste ya comprobado; encima van los colores del usuario, ajustados con <see cref="ColorFit"/>
/// cuando no dejarían leer el texto. Los tests de contraste corren sobre cada aspecto con colores extremos.
/// </summary>
internal static class AspectPalettes
{
    // Fondos que se mueven juntos cuando el usuario elige el fondo (panel de osciloscopio, terminal).
    private static readonly string[] Backgrounds =
    [
        "Ground", "GroundDeep", "Surface", "Popup", "Raised", "RaisedHover", "Hover", "Selected", "Pressed",
        "Track", "Divider", "TitleBar", "TitleBarEnd", "DockPrimary",
    ];

    private const string White = "#FFFFFF";

    public static IReadOnlyDictionary<string, string> Derive(AppearanceMode mode, IReadOnlyDictionary<string, string> c) => mode switch
    {
        AppearanceMode.XpLight => XpLight(c["titleBar"], c["accent"]),
        AppearanceMode.XpDark => XpDark(c["titleBar"], c["accent"]),
        AppearanceMode.TelecomLight => TelecomLight(c["ink"], c["accent"]),
        AppearanceMode.TelecomDark => TelecomDark(c["panel"], c["trace"]),
        AppearanceMode.Bash => Bash(c["background"], c["user"], c["path"], c["accent"]),
        AppearanceMode.Phosphor => Phosphor(c["phosphor"], c["details"]),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static Dictionary<string, string> XpLight(string bar, string accent)
    {
        // El beige de XP (#ECE9D8): el claro de siempre teñido hacia el amarillo grisáceo.
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Light, neutralHue: 95, neutralChroma: 0.018, accentHue: Hue(accent)));
        SetAccent(p, accent);
        // Degradado vertical de Luna: arriba más claro, abajo el color elegido; texto blanco sobre los dos.
        p["TitleBar"] = ColorFit.Background(ColorFit.Lighter(bar, 0.08), White, 4.5);
        p["TitleBarEnd"] = ColorFit.Background(bar, White, 4.5);
        p["OnTitleBar"] = White;
        p["BevelLight"] = White;
        p["BevelDark"] = "#ACA899";
        return p;
    }

    private static Dictionary<string, string> XpDark(string bar, string accent)
    {
        // Grafito neutro, sin el marrón/naranja de Royale Noir (decidido con el usuario).
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: 255, neutralChroma: 0.012, accentHue: Hue(accent)));
        SetAccent(p, accent);
        // Degradado horizontal de Windows 98: oscuro a la izquierda, el color elegido más claro a la derecha.
        p["TitleBar"] = ColorFit.Background(ColorFit.Darker(bar, 0.12), White, 4.5);
        p["TitleBarEnd"] = ColorFit.Background(ColorFit.Lighter(bar, 0.08), White, 4.5);
        p["OnTitleBar"] = White;
        p["BevelLight"] = "#6A6E76";
        p["BevelDark"] = "#121316";
        return p;
    }

    private static Dictionary<string, string> TelecomLight(string ink, string accent)
    {
        // Papel de laboratorio: el claro de siempre con un gris verdoso apenas teñido; la tinta elegida
        // es el texto y tiene que leerse sobre el más oscuro de los fondos de texto (Raised).
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Light, neutralHue: NeutralHue(ink, 100), neutralChroma: 0.012, accentHue: Hue(accent)));
        var text = ColorFit.Foreground(ink, p["Raised"], 4.5);
        p["Text"] = p["TextStrong"] = p["OnTitleBar"] = text;
        SetAccent(p, accent);
        return p;
    }

    private static Dictionary<string, string> TelecomDark(string panel, string trace)
    {
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: NeutralHue(panel, 240), neutralChroma: NeutralChroma(panel, 0.02), accentHue: Hue(trace)));
        MoveBackgrounds(p, panel);
        // La traza es CH1 y el acento: un amarillo de osciloscopio que se lee sobre el panel.
        p["Channel1"] = ColorFit.Foreground(trace, p["Raised"], 4.5);
        SetAccent(p, trace);
        return p;
    }

    private static Dictionary<string, string> Bash(string background, string user, string path, string accent)
    {
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: NeutralHue(background, 60), neutralChroma: NeutralChroma(background, 0.03), accentHue: Hue(accent)));
        MoveBackgrounds(p, background);
        p["PromptUser"] = ColorFit.Foreground(user, p["Ground"], 4.5);
        p["PromptPath"] = ColorFit.Foreground(path, p["Ground"], 4.5);
        // Como en la maqueta: botón del color del acento con el texto del color de la terminal.
        p["OnAccent"] = p["Ground"];
        SetAccent(p, accent);
        return p;
    }

    private static Dictionary<string, string> Phosphor(string phosphor, string details)
    {
        var p = new Dictionary<string, string>(AppPalette.Tint(AppPalette.Dark, neutralHue: NeutralHue(phosphor, 150), neutralChroma: NeutralChroma(phosphor, 0.025), accentHue: Hue(details)));
        // Fondos casi negros del mismo matiz que el fósforo, como el cristal de un monitor apagado.
        MoveBackgrounds(p, ColorMix.Toward(phosphor, "#000000", 0.9));
        var text = ColorFit.Foreground(phosphor, p["Raised"], 4.5);
        p["Text"] = p["TextStrong"] = p["OnTitleBar"] = text;
        p["TextSoft"] = p["TextWarm"] = ColorFit.Foreground(ColorMix.Toward(text, p["Ground"], 0.15), p["Raised"], 4.5);
        p["OnAccent"] = p["Ground"];
        SetAccent(p, details);
        return p;
    }

    // El acento y sus acompañantes, con el texto del botón (OnAccent) legible encima.
    private static void SetAccent(Dictionary<string, string> p, string accent)
    {
        var onAccent = p["OnAccent"];
        var fitted = ColorFit.Background(accent, onAccent, 4.5);
        bool darkText = ColorFit.Ratio(onAccent, "#000000") < ColorFit.Ratio(onAccent, White);
        p["Accent"] = p["AccentBorder"] = p["FieldFocus"] = fitted;
        // Al pasar el ratón, un paso más lejos del texto: más claro bajo texto oscuro, más oscuro bajo claro.
        p["AccentHover"] = ColorFit.Background(darkText ? ColorFit.Lighter(fitted, 0.05) : ColorFit.Darker(fitted, 0.05), onAccent, 4.5);
    }

    /// <summary>
    /// Pone el fondo elegido como Ground y mueve todos los fondos lo mismo en claridad, para que sigan
    /// escalonados. La claridad se limita a [0.14, 0.30]: más claro, los textos del oscuro dejarían de
    /// leerse; más oscuro, los escalones se pierden contra el negro.
    /// </summary>
    private static void MoveBackgrounds(Dictionary<string, string> p, string ground)
    {
        if (!OklchColor.TryFromHex(ground, out var chosen) || !OklchColor.TryFromHex(p["Ground"], out var current)) return;
        double target = Math.Clamp(chosen.L, 0.14, 0.30);
        double delta = target - current.L;
        foreach (var key in Backgrounds)
        {
            if (!p.TryGetValue(key, out var value) || value.Length != 7 || !OklchColor.TryFromHex(value, out var color)) continue;
            p[key] = (color with { L = Math.Clamp(color.L + delta, 0, 1) }).ToHex();
        }
        p["Ground"] = (chosen with { L = target }).ToHex();
        p["TitleBar"] = p["TitleBarEnd"] = p["Ground"];
    }

    private static double Hue(string color) => OklchColor.TryFromHex(color, out var c) ? c.H : 0;

    // Un color sin matiz (gris, blanco) no puede teñir la paleta de un matiz inventado: su "matiz" es ruido.
    private static double NeutralChroma(string color, double maximum) =>
        OklchColor.TryFromHex(color, out var c) && c.C >= 0.02 ? Math.Min(c.C, maximum) : 0;

    private static double NeutralHue(string color, double fallback) =>
        OklchColor.TryFromHex(color, out var c) && c.C >= 0.02 ? c.H : fallback;
}
```

`TheChosenBackground_IsTheGround` espera el hex tal cual: `#282828` (L≈0.27) y `#171C21` (L≈0.21) caen
dentro de [0.14, 0.30], y `(chosen with { L = chosen.L }).ToHex()` tiene que devolver el mismo hex; si la
ida y vuelta OKLCH cambia un dígito, devolver el hex original cuando `target == chosen.L` y anotarlo.

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS. Si un caso extremo falla un
  contraste, **no se baja el listón**: se corrige la derivación de ese aspecto (casi siempre, un `ColorFit`
  que falta sobre la clave que falla, o el límite de `MoveBackgrounds`) y se anota en el comentario.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core tests/Aldune.Core.Tests
git commit -m "Paletas de los seis aspectos retro, derivadas de Oscuro y Claro con los colores del usuario ajustados para que todo se lea"
```

---

### Task 4: Piel de cada aspecto y elección de aspecto

**Files:**
- Modify: `src/Aldune.Core/AppSkin.cs`, `src/Aldune.Core/NoteFace.cs` (estilo `Mono`), `src/Aldune.Core/AppSettings.cs` (`CornersSquare`, `SyncSignalVisible`)
- Create: `src/Aldune.Core/AppearanceChoice.cs`
- Test: `tests/Aldune.Core.Tests/AppSkinTests.cs`, `tests/Aldune.Core.Tests/NoteFaceTests.cs`, `tests/Aldune.Core.Tests/AppearanceChoiceTests.cs`

**Interfaces:**
- Consumes: `AspectCatalog` (Task 2).
- Produces: `SkinCard.Mono = 3`; `AppSkin` gana `bool Gloss`, `bool SquareCorners`, `bool SyncSignal` (al final del registro); `AppSkin.For(mode)` para los seis; `AppearanceChoice.Select(AppSettings, AppearanceMode) : string?` (tema propuesto o null).

- [ ] **Step 1: Tests**

En `AppSkinTests`:

```csharp
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
```

En `NoteFaceTests`:

```csharp
    [Fact]
    public void Mono_ShowsNoNoteColor_NotEvenTheUniformOne()
    {
        // Un monitor monocromo no enseña colores: fondo y texto del fósforo, cápsula apagada.
        var palette = AppPalette.For(AppearanceMode.Phosphor, false);
        var a = NoteFace.For(SkinCard.Mono, "#EBD38B", null, 0, palette);
        var b = NoteFace.For(SkinCard.Mono, "#472525", "#33363A", 2, palette);

        Assert.Equal(a, b);
        Assert.Equal(palette["Ground"], a.Face);
        Assert.Equal(palette["Text"], a.Ink);
        Assert.Null(a.Accent);
        Assert.True(NoteColorContrast.IsReadable(a.Face, a.Ink));
    }
```

`tests/Aldune.Core.Tests/AppearanceChoiceTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AppearanceChoiceTests
{
    [Fact]
    public void Select_ResetsCornersAndSignal_ToTheAspectsDefaults()
    {
        var settings = new AppSettings { SquareCorners = false, ShowSyncSignal = false };

        AppearanceChoice.Select(settings, AppearanceMode.Bash);

        Assert.Equal(AppearanceMode.Bash, settings.Appearance);
        Assert.Null(settings.SquareCorners);
        Assert.Null(settings.ShowSyncSignal);
        Assert.True(settings.CornersSquare);
        Assert.True(settings.SyncSignalVisible);
    }

    [Fact]
    public void AnExplicitChoice_WinsUntilTheNextAspectChange()
    {
        var settings = new AppSettings();
        AppearanceChoice.Select(settings, AppearanceMode.Bash);
        settings.SquareCorners = false;

        Assert.False(settings.CornersSquare);
        AppearanceChoice.Select(settings, AppearanceMode.Dark);
        Assert.False(settings.CornersSquare);
        Assert.False(settings.SyncSignalVisible);
    }

    [Fact]
    public void Select_ProposesTheAspectsTheme_UnlessItIsAlreadyActive()
    {
        var settings = new AppSettings();

        Assert.Equal(NoteThemes.XpId, AppearanceChoice.Select(settings, AppearanceMode.XpLight));
        settings.ActiveThemeId = NoteThemes.XpId;
        Assert.Null(AppearanceChoice.Select(settings, AppearanceMode.XpLight));
        Assert.Null(AppearanceChoice.Select(settings, AppearanceMode.TelecomLight));
        Assert.Null(AppearanceChoice.Select(settings, AppearanceMode.Dark));
    }

    [Fact]
    public void Select_NeverChangesTheNoteTheme()
    {
        var settings = new AppSettings { ActiveThemeId = NoteThemes.OceanId };

        AppearanceChoice.Select(settings, AppearanceMode.Bash);

        Assert.Equal(NoteThemes.OceanId, settings.ActiveThemeId);
    }
}
```

(Si `NoteThemes` no tiene `OceanId`, usar cualquier id de tema de serie que exista.)

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~AppSkinTests|FullyQualifiedName~NoteFaceTests|FullyQualifiedName~AppearanceChoice"`. Expected: no compila.

- [ ] **Step 3: Implementar**

`SkinCard` (en `AppSkin.cs`), añadir:

```csharp
    /// <summary>Monitor monocromo (fósforo): sin color de nota, todo del color del fósforo.</summary>
    Mono = 3,
```

`AppSkin`: tres parámetros más al final del registro, con su documentación:

```csharp
    bool NoteGrid,
    bool Gloss,
    bool SquareCorners,
    bool SyncSignal)
```

```csharp
/// <param name="Gloss">Reflejo en los botones del pie del dock (XP claro).</param>
/// <param name="SquareCorners">Esquinas rectas si el usuario no ha elegido (ajuste nulo).</param>
/// <param name="SyncSignal">Señal de sincronización si el usuario no ha elegido (ajuste nulo).</param>
```

En `Default`, `Gloss: false, SquareCorners: false, SyncSignal: false`. Y `For`:

```csharp
    private const string Tahoma = "Tahoma, Verdana, Segoe UI";
    private const string Mono = "Cascadia Mono, Consolas";

    /// <summary>La piel de cada aspecto (spec, sección 2). Los de antes de la 1.5 usan la de siempre.</summary>
    public static AppSkin For(AppearanceMode mode) => mode switch
    {
        AppearanceMode.XpLight => Default with
        {
            ChromeFont = Tahoma, NoteFont = Tahoma, NoteTitleFont = Tahoma, NoteTitleFontSize = 14,
            TitleBar = SkinTitleBar.GradientVertical, Gloss = true,
        },
        AppearanceMode.XpDark => Default with
        {
            ChromeFont = Tahoma, NoteFont = Tahoma, NoteTitleFont = Tahoma, NoteTitleFontSize = 14,
            Border = SkinBorder.Bevel, TitleBar = SkinTitleBar.GradientHorizontal, SquareCorners = true,
        },
        AppearanceMode.TelecomLight => Terminal with { Card = SkinCard.Tinted, Adornment = SkinTitleAdornment.Channel },
        AppearanceMode.TelecomDark => Terminal with { Card = SkinCard.Tinted, Adornment = SkinTitleAdornment.Channel, NoteGrid = true },
        AppearanceMode.Bash => Terminal with { Card = SkinCard.Stripe, Adornment = SkinTitleAdornment.Folder, PromptLine = true },
        AppearanceMode.Phosphor => Terminal with { Card = SkinCard.Mono, Adornment = SkinTitleAdornment.Uppercase },
        _ => Default,
    };

    // Lo común a los aspectos de terminal: monoespaciada en todo, rectas y con la señal de sync.
    private static readonly AppSkin Terminal = Default with
    {
        ChromeFont = Mono, NoteFont = Mono, NoteTitleFont = Mono, NoteTitleFontSize = 14,
        SquareCorners = true, SyncSignal = true,
    };
```

(Ojo con el orden de inicialización estática: `Terminal` usa `Default`; declarar `Terminal` después de
`Default` en el fichero, o como propiedad calculada, y comprobarlo con el test.)

`NoteFace.For`, nuevo caso antes del `default`:

```csharp
            case SkinCard.Mono:
            {
                // Monitor monocromo: no enseña colores, ni el de la nota ni el único.
                var border = palette["Border"];
                return new NoteFaceColors(ground, text, border, text, text, null, palette["Raised"], border);
            }
```

`AppSettings`: los valores por defecto pasan a depender del aspecto (y se actualizan los comentarios):

```csharp
    public bool SyncSignalVisible => ShowSyncSignal ?? AppSkin.For(Appearance).SyncSignal;
    public bool CornersSquare => SquareCorners ?? AppSkin.For(Appearance).SquareCorners;
```

`src/Aldune.Core/AppearanceChoice.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Lo que pasa al elegir un aspecto en Ajustes.</summary>
public static class AppearanceChoice
{
    /// <summary>
    /// Cambia el aspecto. Esquinas y señal vuelven a «lo que diga el aspecto»: quien elige bash espera
    /// verlo como en las maquetas, y puede cambiar las dos después. Devuelve el tema de notas que el
    /// aspecto propone, o null si no propone ninguno o ya es el activo; el tema no se cambia aquí (spec:
    /// se pregunta, nunca se cambia sin preguntar).
    /// </summary>
    public static string? Select(AppSettings settings, AppearanceMode mode)
    {
        settings.Appearance = mode;
        settings.SquareCorners = null;
        settings.ShowSyncSignal = null;

        var suggested = AspectCatalog.For(mode)?.SuggestedThemeId;
        var active = NoteThemes.Resolve(settings.ActiveThemeId, settings.CustomThemes).Id;
        return suggested is null || string.Equals(suggested, active, StringComparison.Ordinal) ? null : suggested;
    }
}
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2 y la suite de Core (los tests de compatibilidad de `settings.json` del bloque 1 —sin esquinas ni señal = redondeadas y sin señal— siguen en verde: `Appearance` por defecto es Oscuro). Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core tests/Aldune.Core.Tests
git commit -m "Piel de los seis aspectos (fuentes, relieve, barra, tarjetas, adornos, brillo, rectas y senal por defecto) y eleccion de aspecto que propone su tema"
```

---

### Task 5: Capa WPF: colores elegidos, brillo, tarjeta monocroma, cabeceras y botones con relieve

**Files:**
- Modify: `src/Aldune/Windowing/ThemeManager.cs`, `src/Aldune/App.xaml.cs`, `src/Aldune/Windowing/SettingsWindow.xaml(.cs)`, `src/Aldune/Windowing/NotesManagerWindow.xaml`, `src/Aldune/Windowing/SkinState.cs`, `src/Aldune/Windowing/EdgeDockWindow.xaml`, `src/Aldune/App.xaml`

**Interfaces:**
- Consumes: `AppPalette.For(…, aspectColors)` (Task 3), `AppSkin.Gloss`, `SkinCard.Mono` (Task 4).
- Produces: `ThemeManager.Apply(Application, AppearanceMode, IReadOnlyDictionary<string, string>? aspectColors = null)`; `SkinState.Gloss`.

- [ ] **Step 1: Colores elegidos hasta la paleta**

`ThemeManager.Apply(Application app, AppearanceMode mode)` gana un tercer parámetro opcional
`IReadOnlyDictionary<string, string>? aspectColors = null` y lo pasa a `AppPalette.For(mode, windowsLight, aspectColors)`.
En sus llamadas (`grep -n "ThemeManager.Apply(" src/Aldune`): arranque en `App.xaml.cs`
(`settings.ColorsFor(settings.Appearance)`), `OnUserPreferenceChanged` (es Como Windows: sin colores) y
Ajustes (la Task 6 la reescribe).

- [ ] **Step 2: Brillo (XP claro)**

`SkinState`: `public bool Gloss => _skin.Gloss;`. En `CircularIconButtonStyle` (`EdgeDockWindow.xaml`),
dentro del `Grid` de la plantilla, después de `SquareBg` y antes del `BevelEdge`:

```xml
                            <!-- Brillo de los botones de XP: reflejo blanco en la mitad de arriba. -->
                            <Ellipse x:Name="GlossRound" IsHitTestVisible="False" Visibility="Collapsed">
                                <Ellipse.Fill>
                                    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                        <GradientStop Offset="0" Color="#66FFFFFF" />
                                        <GradientStop Offset="0.5" Color="#14FFFFFF" />
                                        <GradientStop Offset="0.51" Color="#00FFFFFF" />
                                    </LinearGradientBrush>
                                </Ellipse.Fill>
                            </Ellipse>
                            <Rectangle x:Name="GlossSquare" IsHitTestVisible="False" Visibility="Collapsed"
                                       Fill="{Binding Fill, ElementName=GlossRound}" />
```

y en sus disparadores:

```xml
                            <DataTrigger Binding="{Binding Gloss, Source={x:Static local:SkinState.Current}}" Value="True">
                                <Setter TargetName="GlossRound" Property="Visibility" Value="Visible" />
                            </DataTrigger>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding Gloss, Source={x:Static local:SkinState.Current}}" Value="True" />
                                    <Condition Binding="{Binding Square, Source={x:Static local:SkinState.Current}}" Value="True" />
                                </MultiDataTrigger.Conditions>
                                <Setter TargetName="GlossRound" Property="Visibility" Value="Collapsed" />
                                <Setter TargetName="GlossSquare" Property="Visibility" Value="Visible" />
                            </MultiDataTrigger>
```

(Los blancos translúcidos son un reflejo, no un color de paleta: mismo criterio que `TabSheen`, que ya
vive en este fichero con hex.)

- [ ] **Step 3: Tarjeta monocroma**

En `NoteTabButtonStyle`, junto a los disparadores de `Stripe` y `Tinted`:

```xml
                            <DataTrigger Binding="{Binding Card, Source={x:Static local:SkinState.Current}}" Value="{x:Static core:SkinCard.Mono}">
                                <Setter TargetName="SheenBorder" Property="Visibility" Value="Collapsed" />
                            </DataTrigger>
```

(El resto lo hace `NoteFace`: cara, tinta, borde y cápsula.)

- [ ] **Step 4: Cabeceras como barra de título**

En `SettingsWindow.xaml` y `NotesManagerWindow.xaml`, a `TitleBarStrip` (bloque 2) un estilo que la
compacta cuando la piel lleva barra en degradado:

```xml
            <Border.Style>
                <Style TargetType="Border">
                    <Setter Property="Padding" Value="24,18,14,0" />
                    <Style.Triggers>
                        <!-- Con barra en degradado (XP) la cabecera es una barra de título de verdad:
                             compacta, de borde a borde y con el texto de la barra. -->
                        <DataTrigger Binding="{Binding GradientTitleBar, Source={x:Static local:SkinState.Current}}" Value="True">
                            <Setter Property="Padding" Value="12,6,8,6" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </Border.Style>
```

(quitando el `Padding` del atributo, que pasa al `Setter`; en el gestor, `Padding` `20,18,14,0`). El
título (`FontSize="17" FontWeight="SemiBold"`) pasa a un estilo con `Setter` de `FontSize` 17 y un
`DataTrigger` igual que pone `FontSize` 13 y `FontWeight` `Bold`; el subtítulo del gestor
(`SubtitleText`) y el `Foreground` de los botones de esa cabecera (`CloseButtonStyle` de cada ventana y
los botones con estilo propio que haya en ella) toman `AlduneOnTitleBarBrush` con el mismo
`DataTrigger`. Fuera de la barra en degradado nada cambia (los `Setter` por defecto son los valores de
hoy).

- [ ] **Step 5: Botones con relieve**

En las plantillas de `ColorDialogButtonStyle` y `PrimaryColorDialogButtonStyle` (`App.xaml`, que
necesita `xmlns:local="clr-namespace:Aldune.Windowing"`), `RecordButtonStyle` (`SettingsWindow.xaml`) y
`ToolbarButtonStyle` (`NotesManagerWindow.xaml`): el contenido de la plantilla se envuelve en un `Grid`
con `<local:BevelEdge />` encima (solo dibuja con la piel `Bevel`; con las demás no cambia nada).

- [ ] **Step 6: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 7: Commit**

```bash
git add -A src
git commit -m "Aspectos en WPF: colores elegidos en la paleta, brillo de XP, tarjeta monocroma, cabeceras como barra y botones con relieve"
```

---

### Task 6: Ajustes → Aspecto ampliada

**Files:**
- Modify: `src/Aldune/Windowing/SettingsWindow.xaml`, `src/Aldune/Windowing/SettingsWindow.xaml.cs`, `src/Aldune/Resources/Strings.cs`

**Interfaces:**
- Consumes: `AspectCatalog`, `AppearanceChoice.Select`, `AppSettings.AspectColors/ColorsFor` (Tasks 2 y 4), `ThemeManager.Apply(…, aspectColors)` (Task 5), `CustomColorWindow.Show(Window, string) : string?`, `AppDialog.Show(Window?, string message, string title, MessageBoxButton …)`.

- [ ] **Step 1: Textos** (en `Strings.cs`, junto a `AppearanceMidnight`)

```csharp
    public static string AppearanceXpLight => T("XP Light", "XP claro", "XP hell", "XP clair", "XP claro");
    public static string AppearanceXpDark => T("XP + 95 Dark", "XP + 95 oscuro", "XP + 95 dunkel", "XP + 95 sombre", "XP + 95 escuro");
    public static string AppearanceTelecomLight => T("Telecom Light (lab)", "Telecomunicaciones claro (laboratorio)",
        "Telekom hell (Labor)", "Télécom clair (labo)", "Telecom claro (laboratório)");
    public static string AppearanceTelecomDark => T("Telecom Dark (oscilloscope)", "Telecomunicaciones oscuro (osciloscopio)",
        "Telekom dunkel (Oszilloskop)", "Télécom sombre (oscilloscope)", "Telecom escuro (osciloscópio)");
    public static string AppearanceBash => T("Bash", "Bash", "Bash", "Bash", "Bash");
    public static string AppearancePhosphor => T("Phosphor (old monitor)", "Fósforo (monitor antiguo)",
        "Phosphor (alter Monitor)", "Phosphore (vieux moniteur)", "Fósforo (monitor antigo)");

    public static string AspectSlotName(string slot) => slot switch
    {
        "titleBar" => T("Title bar", "Barra de título", "Titelleiste", "Barre de titre", "Barra de título"),
        "accent" => T("Accent", "Acento", "Akzent", "Accent", "Destaque"),
        "ink" => T("Ink", "Tinta", "Tinte", "Encre", "Tinta"),
        "panel" => T("Panel", "Panel", "Bedienfeld", "Panneau", "Painel"),
        "trace" => T("Trace (CH1)", "Traza (CH1)", "Spur (CH1)", "Trace (CH1)", "Traço (CH1)"),
        "background" => T("Terminal", "Terminal", "Terminal", "Terminal", "Terminal"),
        "user" => T("User", "Usuario", "Benutzer", "Utilisateur", "Usuário"),
        "path" => T("Path", "Ruta", "Pfad", "Chemin", "Caminho"),
        "phosphor" => T("Phosphor", "Fósforo", "Phosphor", "Phosphore", "Fósforo"),
        "details" => T("Details", "Detalles", "Details", "Détails", "Detalhes"),
        _ => slot,
    };

    public static string AspectPalettePresets => T("Palette", "Paleta", "Palette", "Palette", "Paleta");
    public static string AspectResetColors => T("Reset colors", "Restablecer colores", "Farben zurücksetzen",
        "Réinitialiser les couleurs", "Redefinir cores");
    public static string AspectSuggestTheme(string theme) => T(
        $"Also use the “{theme}” note theme?",
        $"¿Usar también el tema de notas «{theme}»?",
        $"Auch das Notizthema „{theme}“ verwenden?",
        $"Utiliser aussi le thème de notes « {theme} » ?",
        $"Usar também o tema de notas “{theme}”?");
```

(Comprobar que `Strings.T` admite cadenas interpoladas como otras de su estilo —`SyncLastSyncAt`— y
que el nombre visible de un tema sale de `Strings.ThemeDisplayName`.)

- [ ] **Step 2: Lista de aspectos y colores**

`SettingsWindow.xaml`, debajo de `AppearanceListContainer` (en el mismo `StackPanel`):

```xml
                        <!-- Colores del aspecto elegido (solo los retro tienen): cada hueco con su muestra,
                             su valor, muestras de un clic y "Elegir color…"; paletas de bash; restablecer. -->
                        <StackPanel x:Name="AspectColorsContainer" Margin="27,8,0,0" />
```

`PopulateAppearance()`: la lista añade, después de los cinco de siempre, los seis nuevos
(`(AppearanceMode.XpLight, Strings.AppearanceXpLight)`, …, `(AppearanceMode.Phosphor, Strings.AppearancePhosphor)`)
y al final llama a `PopulateAspectColors()`.

```csharp
    private void PopulateAspectColors()
    {
        AspectColorsContainer.Children.Clear();
        if (AspectCatalog.For(_settings.Appearance) is not { } aspect) return;
        var colors = AspectCatalog.Resolve(aspect.Mode, _settings.ColorsFor(aspect.Mode));

        if (aspect.Presets.Count > 0)
        {
            var presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            presets.Children.Add(Label(Strings.AspectPalettePresets));
            foreach (var preset in aspect.Presets)
            {
                var button = AspectButton(preset.Name);
                button.Click += (_, _) => SetAspectColors(aspect, preset.Colors);
                presets.Children.Add(button);
            }
            AspectColorsContainer.Children.Add(presets);
        }

        foreach (var slot in aspect.Slots)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(Label(Strings.AspectSlotName(slot.Id)));
            row.Children.Add(Swatch(colors[slot.Id], selected: true));
            row.Children.Add(new TextBlock
            {
                Text = colors[slot.Id], FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0),
            });
            foreach (var suggestion in slot.Suggestions)
            {
                var swatch = Swatch(suggestion, selected: false);
                swatch.Cursor = Cursors.Hand;
                swatch.ToolTip = suggestion;
                swatch.MouseLeftButtonUp += (_, _) => SetAspectColors(aspect, new Dictionary<string, string> { [slot.Id] = suggestion });
                row.Children.Add(swatch);
            }
            var pick = AspectButton(Strings.AppearanceUniformColorPick);
            pick.Click += (_, _) =>
            {
                var picked = CustomColorWindow.Show(this, colors[slot.Id]);
                if (picked is not null) SetAspectColors(aspect, new Dictionary<string, string> { [slot.Id] = picked });
            };
            row.Children.Add(pick);
            AspectColorsContainer.Children.Add(row);
        }

        var reset = AspectButton(Strings.AspectResetColors);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Click += (_, _) =>
        {
            _settings.AspectColors?.Remove(aspect.Id);
            SaveAndApplyAppearance();
        };
        AspectColorsContainer.Children.Add(reset);

        TextBlock Label(string text) => new()
        {
            Text = text, Width = 110, VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
        };
    }

    private void SetAspectColors(AspectDefinition aspect, IReadOnlyDictionary<string, string> changes)
    {
        _settings.AspectColors ??= [];
        if (!_settings.AspectColors.TryGetValue(aspect.Id, out var colors))
            _settings.AspectColors[aspect.Id] = colors = [];
        foreach (var (slot, value) in changes) colors[slot] = value;
        SaveAndApplyAppearance();
    }
```

Los ayudantes `Swatch(string color, bool selected)` (un `Border` de 22×16 con borde
`AlduneBorderBrush`, margen derecho 4; el seleccionado con borde 2) y `AspectButton(string text)` (un
`Button` con `Style = (Style)FindResource("RecordButtonStyle")`, `Padding = new Thickness(10, 4, 10, 4)`,
margen derecho 6) se crean con `SetResourceReference` para los pinceles, nunca con hex. Las `TextBlock`
toman el `Foreground` con `SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextBrush")`.

- [ ] **Step 3: Elegir aspecto y aplicar**

```csharp
    // A diferencia del idioma, se aplica en el acto: todas las ventanas usan los pinceles de la
    // paleta por referencia (DynamicResource), así que cambian solas.
    private void OnAppearanceSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: AppearanceMode mode } || _settings.Appearance == mode) return;
        var suggestedTheme = AppearanceChoice.Select(_settings, mode);
        SaveAndApplyAppearance();

        // La spec: cada aspecto propone su tema de notas, pero nunca lo cambia sin preguntar.
        if (suggestedTheme is not null &&
            AppDialog.Show(this, Strings.AspectSuggestTheme(Strings.ThemeDisplayName(suggestedTheme)),
                Strings.AppearanceSectionTitle, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            // Mismo camino que elegir el tema en la lista de temas de Ajustes (buscar su manejador y
            // reutilizarlo, para que las notas abiertas y el dock se repinten igual).
            ActivateTheme(suggestedTheme);
        }
    }

    private void SaveAndApplyAppearance()
    {
        _settingsService.Save(_settings);
        var mode = _settings.Appearance;
        ThemeManager.Apply(Application.Current, mode, _settings.ColorsFor(mode));
        ThemeManager.ApplySkin(Application.Current, AppSkin.For(mode));
        ThemeManager.ApplyShape(Application.Current, _settings.CornersSquare);
        SquareCornersCheck.IsChecked = _settings.CornersSquare;
        SyncSignalCheck.IsChecked = _settings.SyncSignalVisible;
        // Esquinas y pestañas espejadas se fijan al crear cada pestaña: hay que rehacer el dock.
        _coordinator?.RebuildDocks();
        _coordinator?.RefreshNoteAppearance();
        PopulateAspectColors();
    }
```

`ActivateTheme(string id)` es el nombre que se le dé al camino que ya existe para activar un tema en
Ajustes (si hoy está dentro de un manejador de clic, sacarlo a un método y usarlo desde los dos sitios).

- [ ] **Step 4: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 5: Commit**

```bash
git add -A src
git commit -m "Ajustes, Aspecto: los seis aspectos nuevos, sus colores con muestras y paletas de bash, restablecer, y la pregunta del tema de notas"
```

---

### Task 7: Verificación con sonda y cierre

**Files:**
- Modify: `docs/STATUS.md`
- Sonda desechable fuera del repo (`docs/WPF_PROBES.md`; reutilizar la del bloque 2 si sigue en el scratchpad)

- [ ] **Step 1: Sonda** (sin teclas ni ratón; base de datos temporal; dock con `KeepDockOpen = true`):
  1. Para cada uno de los seis aspectos con sus colores de fábrica, aplicado como lo hace Ajustes
     (`AppearanceChoice.Select` + `Apply(…, ColorsFor)` + `ApplySkin` + `ApplyShape` + `RebuildDocks` +
     `RefreshNoteAppearance`), **con la nota y el dock ya abiertos**: capturas de una nota con tareas, el
     dock desplegado a la derecha y a la izquierda, el dock en reposo, Ajustes en la página Aspecto y el
     gestor. Compararlas a ojo con la mesa de la maqueta de ese aspecto (forma y colores).
  2. XP claro con la barra en teja (`#A8452A`) y en plata (`#8C8CA8`); bash con las paletas Ubuntu y
     Tango; fósforo en ámbar y en blanco; telecomunicaciones oscuro con la traza en cian.
  3. Colores extremos (blanco en todos los huecos de cada aspecto): ningún texto ilegible a la vista.
  4. Volver a Oscuro: captura igual que la de antes del bloque (diferencia de píxeles 0, como en el bloque 2).
  5. «Mismo color» activado con bash y con telecomunicaciones.

  Comprobaciones automáticas: en cada aspecto, `TitleBox.FontFamily` es la del aspecto; con fósforo, el
  fondo de la nota es `Ground` de su paleta; con bash, `PromptLine` visible y `FaceStripe` visible; con
  XP + 95, botones del pie cuadrados.

- [ ] **Step 2: Smoke test** — avisar al usuario (mueve el ratón) y, con su "ok": `dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug`. Expected: verde.

- [ ] **Step 3: STATUS.md** — sección "## 2026-10-0X: bloque 3 de aspectos (los seis aspectos)": los
  aspectos, cómo se derivan sus paletas, las decisiones de este plan (Fósforo `Mono`, reinicio de
  esquinas y señal al cambiar de aspecto, preajustes de bash), lo que no se tocó (diálogos menores sin
  barra) y el número de tests.

- [ ] **Step 4: Commit**

```bash
git add docs/STATUS.md
git commit -m "Docs: estado tras el bloque 3 de aspectos"
```

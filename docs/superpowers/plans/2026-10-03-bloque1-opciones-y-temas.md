# Bloque 1: opciones transversales y temas — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tres opciones que valen con cualquier aspecto (mismo color en todas las notas, señal de
sincronización, esquinas rectas), los temas de notas revisados (Sereno renovado con recoloreo, Grafito
retirado, XP nuevo) y la sincronización tolerante a cambios idénticos.

**Architecture:** La lógica vive en `Aldune.Core` con tests xUnit (resolución del color visible, estado
de la señal, equivalencia de notas, migraciones). La capa WPF solo traduce: un convertidor y un punto
estático para el color visible (mismo patrón que `NoteSnippetConverter.Enabled`), un `TextBlock` en la
nota para la señal, y radios de esquina como recursos dinámicos que `ThemeManager` pone a 0 o a su valor.

**Tech Stack:** C# / .NET 10, WPF, xUnit, SQLite (repositorio existente).

**Spec:** `docs/superpowers/specs/2026-10-03-aspectos-retro-design.md` (secciones 3 y 4).

Los bloques 2 (piel), 3 (seis aspectos) y 4 (exportar/importar) tendrán su propio plan.

## Global Constraints

- Textos de interfaz siempre con `Strings.T(en, es, de, fr, pt)` en `src/Aldune/Resources/Strings.cs`, los cinco idiomas.
- Comentarios en español, explicando el porqué, con la densidad del código de alrededor.
- Lógica nueva en Core con test primero (TDD).
- `.cs` y `.xaml` con BOM UTF-8; documentación sin BOM.
- Un `settings.json` antiguo tiene que cargar sin migración: campos nuevos con valor por defecto que funcione cuando falta; enums como número.
- Formato de sync **4**: no se sube.
- Colores de chrome con `{DynamicResource Aldune<Clave>Brush}`; colores de nota por temas, nunca hex sueltos en XAML.
- Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`: sondas con base de datos temporal.
- Las sondas de teclado y el smoke test mueven ratón/teclado: avisar al usuario y esperar su "ok" antes de lanzarlos.
- Commits sin la línea `Co-Authored-By`.
- Comandos: `dotnet build Aldune.slnx -c Debug`, `dotnet test Aldune.slnx --no-build` (todos verdes; solo los 4 avisos CA1416 conocidos).

## Review Focus

1. Un `settings.json` con `UniformNoteColor` inválido (`"rojo"`, `""`) → se ignora y cada nota se ve con su color (test en Task 3).
2. Un equipo recolorea y el otro edita el texto de la misma nota → **sigue** siendo conflicto: la equivalencia no puede tragarse ediciones reales (test en Task 1).
3. Una nota sale de la cola de conflictos (resuelta o descartada) → la señal deja de decir "conflicto" (test en Task 5).
4. Dock en el borde izquierdo (y arriba/abajo) con esquinas rectas → las pestañas, cuyo radio se fija por código, también salen rectas (comprobación en la sonda de Task 6).
5. La migración de temas se ejecuta dos veces, o en una instalación sin notas, o con temas propios que usan colores del Sereno antiguo → idempotente, sin errores, temas propios intactos (tests en Task 4).

---

### Task 1: Un mismo cambio en dos equipos no es un conflicto

**Files:**
- Create: `src/Aldune.Core/NoteEquivalence.cs`
- Modify: `src/Aldune.Core/SyncService.cs` (bloque del bucle de merge, ~líneas 360-372)
- Test: `tests/Aldune.Core.Tests/NoteEquivalenceTests.cs`, `tests/Aldune.Core.Tests/SyncConflictTests.cs`

**Interfaces:**
- Produces: `public static bool NoteEquivalence.SameContent(Note? a, Note? b)` — usada por Task 4 indirectamente (la migración confía en que el recoloreo en dos equipos no genere conflictos).

- [ ] **Step 1: Tests unitarios de la equivalencia**

`tests/Aldune.Core.Tests/NoteEquivalenceTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteEquivalenceTests
{
    private static Note Make(string text = "texto", string color = "#EBD38B", NoteState state = NoteState.Active,
        string[]? tags = null, double? dock = null, DateTimeOffset? updatedAt = null) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Text = text,
        Color = color,
        CreatedAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        UpdatedAt = updatedAt ?? new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
        State = state,
        ScreenOrigin = "primary",
        Tags = tags ?? [],
        DockPosition = dock,
    };

    [Fact]
    public void SameContent_DifferentDatesAndColorCase_AreEquivalent()
    {
        var a = Make(color: "#ebd38b");
        var b = Make(color: "#EBD38B", updatedAt: new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

        Assert.True(NoteEquivalence.SameContent(a, b));
    }

    [Fact]
    public void SameContent_TagsInAnotherOrder_AreEquivalent()
    {
        Assert.True(NoteEquivalence.SameContent(Make(tags: ["uni", "casa"]), Make(tags: ["casa", "uni"])));
    }

    [Theory]
    [InlineData("otro texto", "#EBD38B", NoteState.Active)]
    [InlineData("texto", "#C2D4FF", NoteState.Active)]
    [InlineData("texto", "#EBD38B", NoteState.Archived)]
    public void SameContent_AnyRealDifference_IsNotEquivalent(string text, string color, NoteState state)
    {
        Assert.False(NoteEquivalence.SameContent(Make(), Make(text, color, state)));
    }

    [Fact]
    public void SameContent_DifferentTagsOrDockPosition_IsNotEquivalent()
    {
        Assert.False(NoteEquivalence.SameContent(Make(tags: ["uni"]), Make(tags: ["casa"])));
        Assert.False(NoteEquivalence.SameContent(Make(dock: 1), Make(dock: 2)));
    }

    [Fact]
    public void SameContent_WithAMissingSide_IsNotEquivalent()
    {
        // Un borrado (sin nota) frente a una edición nunca es "lo mismo".
        Assert.False(NoteEquivalence.SameContent(Make(), null));
        Assert.False(NoteEquivalence.SameContent(null, null));
    }
}
```

- [ ] **Step 2: Test de integración en `SyncConflictTests`** (añadir junto a `ConcurrentEdits_UseTheNewestVersionAndConverge`)

```csharp
    [Fact]
    public void SameChangeOnBothDevices_IsNotAConflict()
    {
        // La migración de temas recolorea la misma nota en cada equipo que actualiza: mismo
        // resultado, fechas distintas. Antes eso contaba como conflicto (IsRealConflict solo mira
        // fechas) y llenaba la cola con falsos conflictos.
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        _deviceB.Repository.SetColor(note.Id, "#C2D4FF");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.SetColor(note.Id, "#C2D4FF");

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(0, uploaded.ConflictsResolved);
        Assert.Empty(_deviceA.Sync.GetConflicts());
        Assert.Empty(_deviceB.Sync.GetConflicts());
        Assert.Equal("#C2D4FF", _deviceB.Repository.GetAllForSync().Single().Color);
    }

    [Fact]
    public void RecolorOnOneDeviceAndEditOnTheOther_IsStillAConflict()
    {
        // La equivalencia no puede tragarse una edición real: color en B, texto en A.
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        _deviceB.Repository.SetColor(note.Id, "#C2D4FF");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, "edit from A");

        var uploaded = _deviceA.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.Equal(1, uploaded.ConflictsResolved);
    }
```

- [ ] **Step 3: Ver que fallan**

Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~NoteEquivalence|FullyQualifiedName~SameChangeOnBothDevices|FullyQualifiedName~RecolorOnOneDevice"`
Expected: no compila (`NoteEquivalence` no existe). Tras crear un esqueleto que devuelva `false`, `SameChangeOnBothDevices_IsNotAConflict` falla con `ConflictsResolved` = 1.

- [ ] **Step 4: Implementar `NoteEquivalence`**

`src/Aldune.Core/NoteEquivalence.cs` (con BOM):

```csharp
using System.Text.Json;

namespace Aldune.Core;

/// <summary>
/// ¿Dos versiones de una nota dicen lo mismo? Para la sincronización: dos equipos que hacen el mismo
/// cambio (la migración de temas recolorea igual en cada uno) llegan con fechas distintas, y por fechas
/// eso es un conflicto aunque no haya nada que elegir. Compara lo que el usuario ve y edita; no la
/// fecha ni el dispositivo que firmó la versión.
/// </summary>
public static class NoteEquivalence
{
    public static bool SameContent(Note? a, Note? b)
    {
        if (a is null || b is null) return false;

        return a.Text == b.Text
            && string.Equals(a.Color, b.Color, StringComparison.OrdinalIgnoreCase)
            && a.State == b.State
            && a.DockPosition == b.DockPosition
            && a.IsProtected == b.IsProtected
            && a.Tags.OrderBy(tag => tag, StringComparer.Ordinal)
                .SequenceEqual(b.Tags.OrderBy(tag => tag, StringComparer.Ordinal), StringComparer.Ordinal)
            && SameProtectedContent(a.ProtectedContent, b.ProtectedContent);
    }

    // El contenido protegido va cifrado con su propia sal: dos copias del mismo cifrado serializan
    // igual; dos cifrados distintos del mismo texto no, y eso está bien (no se puede saber sin la
    // contraseña, así que se trata como cambio real).
    private static bool SameProtectedContent(ProtectedNoteContent? a, ProtectedNoteContent? b) =>
        a is null || b is null
            ? a is null && b is null
            : JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
}
```

- [ ] **Step 5: Usarla en el merge de `SyncService`**

En `src/Aldune.Core/SyncService.cs`, sustituir el bloque:

```csharp
                    if (comparison != 0 && IsRealConflict(localEnvelope!, remoteObject.Envelope, bases.GetValueOrDefault(id)))
                    {
                        var localVersion = ToConflictVersion(localEnvelope!, localNotes, key);
                        var remoteVersion = ToConflictVersion(remoteObject.Envelope, null, key);
                        var winner = comparison > 0 ? localVersion : remoteVersion;
                        var losing = comparison > 0 ? remoteVersion : localVersion;
                        _repository.SaveSyncConflict(new SyncConflict(
                            Guid.NewGuid(), id, DateTimeOffset.UtcNow, winner, losing));
                        conflicts++;
                    }
```

por:

```csharp
                    if (comparison != 0 && IsRealConflict(localEnvelope!, remoteObject.Envelope, bases.GetValueOrDefault(id)))
                    {
                        var localVersion = ToConflictVersion(localEnvelope!, localNotes, key);
                        var remoteVersion = ToConflictVersion(remoteObject.Envelope, null, key);
                        // Los dos lados cambiaron, pero al mismo contenido: no hay nada que elegir. Se
                        // sigue el camino normal (gana la más reciente) sin dejar un falso conflicto.
                        if (!NoteEquivalence.SameContent(localVersion.Note, remoteVersion.Note))
                        {
                            var winner = comparison > 0 ? localVersion : remoteVersion;
                            var losing = comparison > 0 ? remoteVersion : localVersion;
                            _repository.SaveSyncConflict(new SyncConflict(
                                Guid.NewGuid(), id, DateTimeOffset.UtcNow, winner, losing));
                            conflicts++;
                        }
                    }
```

- [ ] **Step 6: Ver que pasan, y la suite entera**

Run: `dotnet test tests/Aldune.Core.Tests -c Debug`
Expected: todo en verde (867 + 8 nuevos).

- [ ] **Step 7: Commit**

```bash
git add src/Aldune.Core/NoteEquivalence.cs src/Aldune.Core/SyncService.cs tests/Aldune.Core.Tests/NoteEquivalenceTests.cs tests/Aldune.Core.Tests/SyncConflictTests.cs
git commit -m "Sync: dos equipos que hacen el mismo cambio en una nota ya no generan un falso conflicto"
```

---

### Task 2: Temas de notas — Sereno renovado, Grafito retirado, XP nuevo

**Files:**
- Modify: `src/Aldune.Core/NoteThemes.cs`
- Modify: `src/Aldune/Resources/Strings.cs` (`ThemeDisplayName`, ~línea 1081)
- Test: `tests/Aldune.Core.Tests/NoteThemesTests.cs`, `tests/Aldune.Core.Tests/NoteColorAssignerTests.cs` (~línea 90)

**Interfaces:**
- Produces: `NoteThemes.XpId = "xp"`; `NoteThemes.GraphiteId` se conserva como constante (para migrar), fuera de `BuiltIn`; `NoteThemes.LegacySereneDarkColors` y `NoteThemes.LegacySereneLightColors` (`IReadOnlyList<string>`, los colores antiguos, para Task 4); `NoteThemes.SereneRecolorMap` (`IReadOnlyDictionary<string, string>`, claves en mayúsculas, antiguo → nuevo del mismo puesto y tono).

- [ ] **Step 1: Tests nuevos y ajustados en `NoteThemesTests`**

Sustituir `BuiltIn_AreTheSixFactoryThemes_InThatOrder` por:

```csharp
    [Fact]
    public void BuiltIn_AreTheFactoryThemes_InThatOrder()
    {
        // Grafito se retiró en la 1.5: su papel (monocromo) lo cubre "mismo color en todas las notas".
        Assert.Equal(new[] { "classic", "serene", "pastel", "autumn", "ocean", "xp" }, NoteThemes.BuiltIn.Select(t => t.Id));
        Assert.All(NoteThemes.BuiltIn, theme => Assert.True(theme.IsBuiltIn));
    }
```

En `CalculatedThemes_KeepOneLightnessPerGroup` quitar `[InlineData("graphite")]` y añadir `[InlineData("xp")]`.

Sustituir `NewThemes_ColorsAreDistinguishable` (que solo cubría pastel, otoño y océano; por eso Sereno y
Grafito pasaban con colores casi idénticos) por:

```csharp
    // Que se distingan entre sí: dos notas seguidas no pueden parecer del mismo color. Todos los de
    // serie, no solo los nuevos: Sereno pasaba sin cubrir con colores a 0.008 de distancia.
    [Theory]
    [InlineData("classic")]
    [InlineData("serene")]
    [InlineData("pastel")]
    [InlineData("autumn")]
    [InlineData("ocean")]
    [InlineData("xp")]
    public void BuiltInThemes_ColorsAreDistinguishable(string id)
    {
        var theme = NoteThemes.Resolve(id, null);
        Assert.Equal(id, theme.Id);
        foreach (var group in new[] { theme.DarkColors, theme.LightColors }.Where(g => g.Count > 0))
        {
            Assert.True(group.Count >= 4, id);
            var colors = group.Select(Oklch).ToList();
            for (int i = 0; i < colors.Count; i++)
                for (int j = i + 1; j < colors.Count; j++)
                    Assert.True(colors[i].DistanceTo(colors[j]) >= 0.03, $"{id}: {group[i]} y {group[j]} se parecen demasiado");
        }
    }

    // La tinta que la app elige para cada color se tiene que leer bien (AA, 4.5:1).
    [Theory]
    [InlineData("serene")]
    [InlineData("xp")]
    public void BuiltInThemes_InkIsReadable(string id)
    {
        var theme = NoteThemes.Resolve(id, null);
        foreach (var color in theme.DarkColors.Concat(theme.LightColors))
            Assert.True(NoteColorContrast.IsReadable(color, NoteColorContrast.ForegroundFor(color)), $"{id}: {color}");
    }

    [Fact]
    public void Graphite_IsRetired_AndResolvesToClassic()
    {
        Assert.Equal(NoteThemes.ClassicId, NoteThemes.Resolve(NoteThemes.GraphiteId, null).Id);
    }

    [Fact]
    public void SereneRecolorMap_SendsEachOldColorToTheNewOneOfItsSlotAndTone()
    {
        var serene = NoteThemes.Resolve(NoteThemes.SereneId, null);
        for (int i = 0; i < NoteThemes.LegacySereneDarkColors.Count; i++)
            Assert.Equal(serene.DarkColors[i], NoteThemes.SereneRecolorMap[NoteThemes.LegacySereneDarkColors[i]]);
        for (int i = 0; i < NoteThemes.LegacySereneLightColors.Count; i++)
            Assert.Equal(serene.LightColors[i], NoteThemes.SereneRecolorMap[NoteThemes.LegacySereneLightColors[i]]);
        Assert.Equal(14, NoteThemes.SereneRecolorMap.Count);
    }
```

En `NoteColorAssignerTests.Fixed_WithAColorFromAnotherTheme_FallsBackToTheFirstCandidate`, cambiar
Grafito por Pastel. `#262F47` (Tinta, del Sereno antiguo) no está en Pastel, así que `FixedColor`
devuelve null y se usa el primer candidato del tono claro:

```csharp
        var pastel = NoteThemes.Resolve(NoteThemes.PastelId, null);

        var color = NoteColorAssigner.Assign(pastel, NoteTone.Light, NoteColorAssignment.Fixed, "#262F47", []);

        Assert.Equal("#FFD3E6", color);
```

- [ ] **Step 2: Ver que fallan**

Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~NoteThemes|FullyQualifiedName~NoteColorAssigner"`
Expected: no compila (`XpId`, `LegacySerene*`, `SereneRecolorMap` no existen).

- [ ] **Step 3: Cambiar `NoteThemes`**

En `src/Aldune.Core/NoteThemes.cs`:

- Añadir junto a las demás constantes:

```csharp
    /// <summary>Retirado en la 1.5 (lo sustituye "mismo color en todas las notas"). Se conserva el id
    /// para migrar a quien lo tenga activo (ver ThemeMigrations) y para que Resolve caiga en Clásico.</summary>
    public const string GraphiteId = "graphite";
    public const string XpId = "xp";
```

  (la línea `public const string GraphiteId = "graphite";` que ya existe se sustituye por la de arriba).

- Sustituir la entrada de Sereno y quitar la de Grafito en `BuiltIn`; añadir XP al final:

```csharp
        new NoteTheme
        {
            Id = SereneId, Name = "Sereno", IsBuiltIn = true,
            // Renovado en la 1.5: los antiguos estaban a 0.020 (oscuros) y 0.008 (claros) de distancia
            // y se confundían. Misma claridad por grupo, croma justo para separarse y matices
            // repartidos por igual: oscuros L 0.31 C 0.052 cada 45°, claros L 0.925 C 0.042 cada 60°.
            DarkColors = ["#472525", "#422A11", "#36310E", "#1F371F", "#033936", "#113447", "#2B2D4A", "#3E273F"],
            LightColors = ["#FFDDD4", "#EFE7C7", "#D3EFD8", "#C7EFF4", "#D9E7FF", "#F5DDF6"],
        },
```

```csharp
        new NoteTheme
        {
            Id = XpId, Name = "XP", IsBuiltIn = true,
            // Los colores de nota de los aspectos XP: el amarillo de los avisos de Windows XP y tonos
            // de la misma familia. Solo claros, como Pastel.
            LightColors = ["#FFFFE1", "#E1F0FF", "#E8F5D8", "#FCE4D6", "#EDE3F7", "#DDEFEF"],
        },
```

- Añadir debajo de `BuiltIn`:

```csharp
    /// <summary>Colores de Sereno hasta la 1.4: los tienen guardados las notas que se crearon con él.</summary>
    public static IReadOnlyList<string> LegacySereneDarkColors { get; } =
        ["#2E3034", "#26323E", "#262F47", "#1D3538", "#283426", "#3C2D21", "#462527", "#392A3C"];

    public static IReadOnlyList<string> LegacySereneLightColors { get; } =
        ["#EBE6D9", "#EBE5E0", "#DEE8F0", "#DEEADE", "#F0E4D7", "#F2E2E1"];

    /// <summary>Color antiguo de Sereno → el nuevo del mismo puesto y tono. Claves en mayúsculas.</summary>
    public static IReadOnlyDictionary<string, string> SereneRecolorMap { get; } = BuildSereneRecolorMap();

    private static IReadOnlyDictionary<string, string> BuildSereneRecolorMap()
    {
        var serene = BuiltIn.First(theme => theme.Id == SereneId);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < LegacySereneDarkColors.Count; i++) map[LegacySereneDarkColors[i]] = serene.DarkColors[i];
        for (int i = 0; i < LegacySereneLightColors.Count; i++) map[LegacySereneLightColors[i]] = serene.LightColors[i];
        return map;
    }
```

- Comprobar los XP: si `BuiltInThemes_ColorsAreDistinguishable("xp")` falla por dos colores demasiado
  cercanos, ajustar el más parecido conservando su matiz (subir croma) hasta ≥ 0.03, y anotar el valor
  final en el comentario. Lo mismo con `CalculatedThemes_KeepOneLightnessPerGroup("xp")` (claridad
  dentro de ±0.02).

- [ ] **Step 4: Nombres visibles**

En `src/Aldune/Resources/Strings.cs`, en `ThemeDisplayName`, quitar la rama de `GraphiteId` y añadir:

```csharp
        Aldune.Core.NoteThemes.XpId => T("XP", "XP", "XP", "XP", "XP"),
```

- [ ] **Step 5: Tests en verde**

Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test tests/Aldune.Core.Tests -c Debug --no-build`
Expected: verde. Si algo referencia `NoteThemes.GraphiteId` en la app aparte de Strings, sigue compilando
(la constante existe).

- [ ] **Step 6: Commit**

```bash
git add src/Aldune.Core/NoteThemes.cs src/Aldune/Resources/Strings.cs tests/Aldune.Core.Tests/NoteThemesTests.cs tests/Aldune.Core.Tests/NoteColorAssignerTests.cs
git commit -m "Temas de notas: Sereno renovado (colores que se distinguen), Grafito retirado, tema XP nuevo; el test de distancia cubre todos los de serie"
```

---

### Task 3: Mismo color en todas las notas

**Files:**
- Create: `src/Aldune.Core/NoteDisplayColor.cs`, `src/Aldune/Windowing/NoteColorDisplay.cs`, `src/Aldune/Windowing/NoteDisplayColorConverter.cs`
- Modify: `src/Aldune.Core/AppSettings.cs`, `src/Aldune/Windowing/NoteRimConverter.cs`, `NoteLabelColorConverter.cs`, `NoteRestOutlineConverter.cs`, `src/Aldune/Windowing/EdgeDockWindow.xaml` (líneas 42, 317, 382), `src/Aldune/Windowing/NotesManagerWindow.xaml` (líneas 421, 463), `src/Aldune/App.xaml` (recurso del convertidor), `src/Aldune/Windowing/NoteWindow.xaml.cs` (`ApplyColor`, nuevo `ReapplyAppearance`), `src/Aldune/Windowing/AppCoordinator.cs` (nuevo `RefreshNoteAppearance`), `src/Aldune/App.xaml.cs` (arranque), `src/Aldune/Windowing/SettingsWindow.xaml(.cs)`, `src/Aldune/Resources/Strings.cs`
- Test: `tests/Aldune.Core.Tests/NoteDisplayColorTests.cs`, `tests/Aldune.Core.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `AppSettings.UniformNoteColor` (`string?`, null = desactivado); `NoteDisplayColor.Resolve(string color, string? uniform) : string`; `NoteColorDisplay.Uniform` (estático, `string?`) y `NoteColorDisplay.Resolve(string color)`; `NoteWindow.ReapplyAppearance()`; `AppCoordinator.RefreshNoteAppearance()` (Task 5 lo amplía).

- [ ] **Step 1: Tests de Core**

`tests/Aldune.Core.Tests/NoteDisplayColorTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteDisplayColorTests
{
    [Fact]
    public void Resolve_Disabled_KeepsTheNoteColor()
    {
        Assert.Equal("#EBD38B", NoteDisplayColor.Resolve("#EBD38B", null));
    }

    [Fact]
    public void Resolve_WithAUniformColor_UsesItForEveryNote()
    {
        Assert.Equal("#33363A", NoteDisplayColor.Resolve("#EBD38B", "#33363a"));
        Assert.Equal("#33363A", NoteDisplayColor.Resolve("#C2D4FF", "#33363A"));
    }

    [Theory]
    [InlineData("rojo")]
    [InlineData("")]
    [InlineData("#12345")]
    public void Resolve_InvalidUniformColor_IsIgnored(string uniform)
    {
        // Un settings.json editado a mano no puede dejar las notas sin color.
        Assert.Equal("#EBD38B", NoteDisplayColor.Resolve("#EBD38B", uniform));
    }
}
```

En `SettingsServiceTests` (mismo estilo que los existentes):

```csharp
    [Fact]
    public void Load_OldFileWithoutUniformNoteColor_KeepsEachNoteColor()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{\"AutoHideCompletedTasks\":true}");

        Assert.Null(new SettingsService(_settingsPath).Load().UniformNoteColor);
    }
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~NoteDisplayColor|FullyQualifiedName~UniformNoteColor"`. Expected: no compila.

- [ ] **Step 3: Implementar en Core**

`src/Aldune.Core/NoteDisplayColor.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>
/// El color con el que se pinta una nota, que no siempre es el que tiene guardado: con "mismo color
/// en todas las notas" se ven todas del color elegido. Solo cambia la presentación; la nota conserva
/// su color, así que desactivarlo lo devuelve todo y la sincronización no ve cambios.
/// </summary>
public static class NoteDisplayColor
{
    public static string Resolve(string color, string? uniform) =>
        uniform is not null && OklchColor.TryFromHex(uniform, out _) && uniform.Length == 7
            ? uniform.ToUpperInvariant()
            : color;
}
```

En `AppSettings.cs`, junto a `FixedNoteColor`:

```csharp
    /// <summary>
    /// "Mismo color en todas las notas": nulo = desactivado (cada nota con el suyo, como siempre).
    /// Solo cambia cómo se pintan; ver <see cref="NoteDisplayColor"/>.
    /// </summary>
    public string? UniformNoteColor { get; set; }
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Punto único en la capa WPF**

`src/Aldune/Windowing/NoteColorDisplay.cs` (con BOM):

```csharp
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Color único activo para pintar las notas, visible para los convertidores de XAML. Estático por el
/// mismo motivo que <see cref="NoteSnippetConverter.Enabled"/>: los convertidores los crea XAML y no
/// reciben los ajustes. Lo fijan el arranque y Ajustes; después hay que llamar a
/// <c>AppCoordinator.RefreshNoteAppearance</c> para repintar.
/// </summary>
internal static class NoteColorDisplay
{
    public static string? Uniform { get; set; }

    public static string Resolve(string? color) => NoteDisplayColor.Resolve(color ?? string.Empty, Uniform);
}
```

`src/Aldune/Windowing/NoteDisplayColorConverter.cs` (con BOM):

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>Color guardado de la nota → pincel con el que se pinta (ver <see cref="NoteColorDisplay"/>).</summary>
public sealed class NoteDisplayColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(NoteColorDisplay.Resolve(value as string))!;
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

En `NoteRimConverter`, `NoteLabelColorConverter` y `NoteRestOutlineConverter`, la primera línea de
`Convert` pasa el valor por `NoteColorDisplay.Resolve` antes de usarlo, por ejemplo en
`NoteLabelColorConverter`:

```csharp
                NoteColorPalette.LabelFor(NoteColorDisplay.Resolve(value as string)))!;
```

(en los otros dos, sustituir `value as string ?? string.Empty` —o la forma equivalente que usen— por
`NoteColorDisplay.Resolve(value as string)`).

Registrar el convertidor donde se registran los demás (buscar `NoteRimConverter` en `App.xaml` o en los
`Resources` de cada ventana y añadir al lado `<local:NoteDisplayColorConverter x:Key="NoteDisplayColorConverter" />`).

Sustituir `Background="{Binding Color}"` por `Background="{Binding Color, Converter={StaticResource NoteDisplayColorConverter}}"`
en `EdgeDockWindow.xaml` (líneas 42, 317, 382) y `{Binding Note.Color}` como fondo por
`{Binding Note.Color, Converter={StaticResource NoteDisplayColorConverter}}` en `NotesManagerWindow.xaml`
(líneas 421, 463).

En `NoteWindow.xaml.cs`, primera línea de `ApplyColor(string color)`:

```csharp
        color = NoteColorDisplay.Resolve(color);
```

y un método nuevo junto a `ApplyColor`:

```csharp
    /// <summary>Repinta con los ajustes de aspecto actuales (color único y, desde la señal, su estado).</summary>
    internal void ReapplyAppearance()
    {
        ApplyColor(_note.Color);
    }
```

En `AppCoordinator.cs`, junto a `RefreshAll`:

```csharp
    /// <summary>Tras cambiar una opción de aspecto de las notas: notas abiertas, docks y gestor.</summary>
    public void RefreshNoteAppearance()
    {
        foreach (var window in _openNoteWindows.Values) window.ReapplyAppearance();
        RefreshAll();
    }
```

En `App.xaml.cs`, junto a `NoteSnippetConverter.Enabled = settings.ShowNotePreview;`:

```csharp
        NoteColorDisplay.Uniform = settings.UniformNoteColor;
```

- [ ] **Step 6: Ajustes**

Strings (en `Strings.cs`, cerca de `AppearanceSystem`):

```csharp
    public static string AppearanceUniformColor => T("Same color for every note", "Mismo color en todas las notas",
        "Gleiche Farbe für alle Notizen", "Même couleur pour toutes les notes", "Mesma cor em todas as notas");
    public static string AppearanceUniformColorHint => T("Only changes how they look: each note keeps its own color.",
        "Solo cambia cómo se ven: cada nota conserva su color.",
        "Ändert nur das Aussehen: Jede Notiz behält ihre eigene Farbe.",
        "Ne change que l'apparence : chaque note garde sa couleur.",
        "Só muda a aparência: cada nota mantém a sua cor.");
    public static string AppearanceUniformColorPick => T("Choose color…", "Elegir color…",
        "Farbe wählen…", "Choisir la couleur…", "Escolher cor…");
```

En `SettingsWindow.xaml`, justo después del `StackPanel` que contiene `AppearanceListContainer`
(mismo estilo de casilla que `HotkeyCheck`):

```xml
                    <StackPanel Margin="0,14,0,0">
                        <CheckBox x:Name="UniformColorCheck"
                                  Content="{x:Static res:Strings.AppearanceUniformColor}"
                                  Foreground="{DynamicResource AlduneTextBrush}" Background="{DynamicResource AlduneGroundBrush}" FontSize="13"
                                  Style="{StaticResource AppCheckBoxStyle}"
                                  Click="OnUniformColorToggled" />
                        <StackPanel Orientation="Horizontal" Margin="27,8,0,0">
                            <Border x:Name="UniformColorSwatch" Width="22" Height="16" VerticalAlignment="Center"
                                    BorderBrush="{DynamicResource AlduneBorderBrush}" BorderThickness="1" />
                            <Button x:Name="UniformColorButton" Content="{x:Static res:Strings.AppearanceUniformColorPick}"
                                    Click="OnPickUniformColorClick" Margin="8,0,0,0" Padding="12,7"
                                    Style="{StaticResource RecordButtonStyle}" />
                        </StackPanel>
                        <TextBlock Text="{x:Static res:Strings.AppearanceUniformColorHint}"
                                   Foreground="{DynamicResource AlduneHintBrush}" FontSize="11" TextWrapping="Wrap" Margin="27,6,0,0" />
                    </StackPanel>
```

En `SettingsWindow.xaml.cs`, en el constructor junto a las demás casillas: `UpdateUniformColorUi();`, y:

```csharp
    // Al activarlo sin color elegido, parte del primer color del tema activo en el tono de las notas
    // nuevas: el usuario ve enseguida el efecto y lo cambia si quiere.
    private void OnUniformColorToggled(object sender, RoutedEventArgs e)
    {
        if (UniformColorCheck.IsChecked == true)
        {
            var theme = NoteThemes.Resolve(_settings.ActiveThemeId, _settings.CustomThemes);
            var preferred = _settings.NewNoteTone == NoteTone.Dark ? theme.DarkColors : theme.LightColors;
            _settings.UniformNoteColor = preferred.FirstOrDefault()
                ?? theme.LightColors.Concat(theme.DarkColors).First();
        }
        else
        {
            _settings.UniformNoteColor = null;
        }
        ApplyUniformColor();
    }

    private void OnPickUniformColorClick(object sender, RoutedEventArgs e)
    {
        var picked = CustomColorWindow.Show(this, _settings.UniformNoteColor ?? "#EBD38B");
        if (picked is null) return;
        _settings.UniformNoteColor = picked;
        ApplyUniformColor();
    }

    private void ApplyUniformColor()
    {
        _settingsService.Save(_settings);
        NoteColorDisplay.Uniform = _settings.UniformNoteColor;
        _coordinator?.RefreshNoteAppearance();
        UpdateUniformColorUi();
    }

    private void UpdateUniformColorUi()
    {
        bool on = _settings.UniformNoteColor is not null;
        UniformColorCheck.IsChecked = on;
        UniformColorButton.IsEnabled = on;
        UniformColorSwatch.Background = on
            ? (Brush)new BrushConverter().ConvertFromString(NoteColorDisplay.Resolve("#00000000"))!
            : Brushes.Transparent;
    }
```

(`NoteColorDisplay.Resolve` con un color cualquiera devuelve el único cuando está activo; si `Brush` /
`BrushConverter` no están importados en el fichero, añadir `using System.Windows.Media;`.)

- [ ] **Step 7: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 8: Commit**

```bash
git add -A src tests
git commit -m "Mismo color en todas las notas: opción de presentación en Ajustes, cada nota conserva su color"
```

---

### Task 4: Migraciones de temas al arrancar

**Files:**
- Create: `src/Aldune.Core/ThemeMigrations.cs`
- Modify: `src/Aldune.Core/AppSettings.cs`, `src/Aldune/App.xaml.cs` (antes de crear el `AppCoordinator`)
- Test: `tests/Aldune.Core.Tests/ThemeMigrationsTests.cs`

**Interfaces:**
- Consumes: `NoteThemes.SereneRecolorMap`, `NoteThemes.GraphiteId` (Task 2); `AppSettings.UniformNoteColor` (Task 3); `NotesRepository.GetAllForSync()`, `NotesRepository.SetColor(Guid, string)`.
- Produces: `AppSettings.SereneRecolored` (`bool`, por defecto false); `ThemeMigrations.Run(AppSettings, NotesRepository) : bool` (true = hay que guardar los ajustes).

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/ThemeMigrationsTests.cs` (con BOM):

```csharp
using System.Security.Cryptography;
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class ThemeMigrationsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-theme-migrations-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));
    private readonly NotesRepository _repository;

    public ThemeMigrationsTests()
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

    private string ColorOf(Guid id) => _repository.GetAllForSync().Single(n => n.Id == id).Color;

    [Fact]
    public void Run_RecolorsOldSereneNotes_AndLeavesTheRestAlone()
    {
        var oldDark = _repository.Create("a", NoteThemes.LegacySereneDarkColors[2], "primary");
        var oldLight = _repository.Create("b", NoteThemes.LegacySereneLightColors[0].ToLowerInvariant(), "primary");
        var classic = _repository.Create("c", "#EBD38B", "primary");
        var settings = new AppSettings();

        Assert.True(ThemeMigrations.Run(settings, _repository));

        var serene = NoteThemes.Resolve(NoteThemes.SereneId, null);
        Assert.Equal(serene.DarkColors[2], ColorOf(oldDark.Id));
        Assert.Equal(serene.LightColors[0], ColorOf(oldLight.Id));
        Assert.Equal("#EBD38B", ColorOf(classic.Id));
        Assert.True(settings.SereneRecolored);
    }

    [Fact]
    public void Run_Twice_DoesNothingTheSecondTime()
    {
        var note = _repository.Create("a", NoteThemes.LegacySereneDarkColors[0], "primary");
        var settings = new AppSettings();
        ThemeMigrations.Run(settings, _repository);
        var afterFirst = _repository.GetAllForSync().Single().UpdatedAt;

        Assert.False(ThemeMigrations.Run(settings, _repository));
        Assert.Equal(afterFirst, _repository.GetAllForSync().Single(n => n.Id == note.Id).UpdatedAt);
    }

    [Fact]
    public void Run_WithNoNotes_OnlyMarksItAsDone()
    {
        var settings = new AppSettings();

        Assert.True(ThemeMigrations.Run(settings, _repository));
        Assert.True(settings.SereneRecolored);
    }

    [Fact]
    public void Run_LeavesCustomThemesUntouched()
    {
        var custom = new NoteTheme { Id = "mio", Name = "Mío", LightColors = [NoteThemes.LegacySereneLightColors[1]] };
        var settings = new AppSettings { CustomThemes = [custom] };

        ThemeMigrations.Run(settings, _repository);

        Assert.Equal(NoteThemes.LegacySereneLightColors[1], settings.CustomThemes.Single().LightColors.Single());
    }

    [Fact]
    public void Run_MapsAnOldSereneFixedColor()
    {
        var settings = new AppSettings { FixedNoteColor = NoteThemes.LegacySereneDarkColors[5] };

        ThemeMigrations.Run(settings, _repository);

        Assert.Equal(NoteThemes.Resolve(NoteThemes.SereneId, null).DarkColors[5], settings.FixedNoteColor);
    }

    [Theory]
    [InlineData(NoteTone.Dark, "#2E2E2E")]
    [InlineData(NoteTone.Light, "#E8E8E8")]
    [InlineData(NoteTone.Both, "#E8E8E8")]
    public void Run_GraphiteUser_GetsTheSameGreyLookWithUniformColor(NoteTone tone, string grey)
    {
        var settings = new AppSettings { ActiveThemeId = NoteThemes.GraphiteId, NewNoteTone = tone, SereneRecolored = true };

        Assert.True(ThemeMigrations.Run(settings, _repository));

        Assert.Equal(grey, settings.UniformNoteColor);
        Assert.Null(settings.ActiveThemeId);
    }

    [Fact]
    public void Run_GraphiteUserWhoAlreadyHasAUniformColor_KeepsIt()
    {
        var settings = new AppSettings { ActiveThemeId = NoteThemes.GraphiteId, UniformNoteColor = "#33363A", SereneRecolored = true };

        ThemeMigrations.Run(settings, _repository);

        Assert.Equal("#33363A", settings.UniformNoteColor);
    }
}
```

(Comprobar al escribirlo que `ContentCipher` es `IDisposable` y la firma del constructor de
`NotesRepository` en `tests/Aldune.Core.Tests/NotesRepositoryTaskCompletionTests.cs`, y copiar su forma
exacta si difiere.)

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~ThemeMigrations"`. Expected: no compila.

- [ ] **Step 3: Implementar**

En `AppSettings.cs`, junto a `UniformNoteColor`:

```csharp
    /// <summary>Ya se pasaron las notas del Sereno antiguo al renovado (1.5). Ver ThemeMigrations.</summary>
    public bool SereneRecolored { get; set; }
```

`src/Aldune.Core/ThemeMigrations.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>
/// Cambios de temas de la 1.5 que tocan datos ya guardados. Se ejecuta al arrancar, antes de abrir
/// ventanas, y es idempotente: cada paso deja una marca o se reconoce por el estado de los ajustes.
/// </summary>
public static class ThemeMigrations
{
    /// <summary>Devuelve true si cambió algo de <paramref name="settings"/> (hay que guardarlos).</summary>
    public static bool Run(AppSettings settings, NotesRepository repository)
    {
        bool changed = false;

        if (!settings.SereneRecolored)
        {
            // Cambio de color normal: se sincroniza, y si otro equipo hace lo mismo la sync no lo
            // trata como conflicto (NoteEquivalence). Las notas protegidas también: su color no va cifrado.
            foreach (var note in repository.GetAllForSync())
            {
                if (NoteThemes.SereneRecolorMap.TryGetValue(note.Color, out var renewed))
                    repository.SetColor(note.Id, renewed);
            }
            if (settings.FixedNoteColor is { } fixedColor &&
                NoteThemes.SereneRecolorMap.TryGetValue(fixedColor, out var renewedFixed))
            {
                settings.FixedNoteColor = renewedFixed;
            }
            settings.SereneRecolored = true;
            changed = true;
        }

        if (settings.ActiveThemeId == NoteThemes.GraphiteId)
        {
            // Grafito se retiró: el mismo aspecto gris con "mismo color en todas las notas". Sus notas
            // guardan su color y no se tocan.
            settings.UniformNoteColor ??= settings.NewNoteTone == NoteTone.Dark ? "#2E2E2E" : "#E8E8E8";
            settings.ActiveThemeId = null;
            changed = true;
        }

        return changed;
    }
}
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Llamarla al arrancar**

En `src/Aldune/App.xaml.cs`, justo antes de `var syncService = new SyncService(repository, settings, settingsService);`:

```csharp
        // Cambios de temas de la 1.5 (Sereno renovado, Grafito retirado) sobre los datos ya guardados.
        if (ThemeMigrations.Run(settings, repository)) settingsService.Save(settings);
```

(debe quedar antes de `NoteColorDisplay.Uniform = settings.UniformNoteColor;`, que Task 3 puso más
abajo: la migración de Grafito puede fijar el color único).

- [ ] **Step 6: Suite completa** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde.

- [ ] **Step 7: Commit**

```bash
git add src/Aldune.Core/ThemeMigrations.cs src/Aldune.Core/AppSettings.cs src/Aldune/App.xaml.cs tests/Aldune.Core.Tests/ThemeMigrationsTests.cs
git commit -m "Migraciones de temas al arrancar: las notas del Sereno antiguo pasan al renovado y quien usaba Grafito conserva su gris con mismo color"
```

---

### Task 5: Señal de sincronización

**Files:**
- Create: `src/Aldune.Core/SyncScopeFilter.cs`, `src/Aldune.Core/NoteSyncSignal.cs`
- Modify: `src/Aldune.Core/SyncService.cs` (filtros de alcance de notas, ~líneas 320 y 435), `src/Aldune.Core/AppSettings.cs`, `src/Aldune/Windowing/NoteWindow.xaml` (fila 2), `NoteWindow.xaml.cs`, `AppCoordinator.cs`, `SettingsWindow.xaml(.cs)`, `Strings.cs`
- Test: `tests/Aldune.Core.Tests/NoteSyncSignalTests.cs`, `tests/Aldune.Core.Tests/SyncConflictTests.cs`

**Interfaces:**
- Consumes: `NotesRepository.GetSyncBases()`, `NotesRepository.GetSyncConflicts()` (registro `SyncConflict` con `NoteId`), `AppCoordinator.RefreshNoteAppearance()` (Task 3).
- Produces: `SyncScopeFilter.Includes(Note, AppSettings) : bool`; `enum SyncSignalState { Hidden, Synced, Pending, Conflict, Excluded }`; `NoteSyncSignal.For(Note, AppSettings, SyncBaseVersion?, bool hasConflict) : SyncSignalState`; `NoteSyncSignal.Glyph(SyncSignalState) : string`; `AppSettings.ShowSyncSignal` (`bool?`) y `AppSettings.SyncSignalVisible` (`bool`, `ShowSyncSignal ?? false`; el bloque 3 hará que el valor por defecto dependa del aspecto); `AppCoordinator.SyncSignalFor(Guid) : SyncSignalState`.

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/NoteSyncSignalTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteSyncSignalTests
{
    private static readonly DateTimeOffset Edited = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static Note Make(params string[] tags) => new()
    {
        Id = Guid.NewGuid(), Text = "t", Color = "#EBD38B", CreatedAt = Edited, UpdatedAt = Edited,
        State = NoteState.Active, ScreenOrigin = "primary", Tags = tags,
    };

    private static AppSettings Syncing() => new() { SyncEnabled = true };

    [Fact]
    public void WithoutSync_IsHidden()
    {
        Assert.Equal(SyncSignalState.Hidden, NoteSyncSignal.For(Make(), new AppSettings(), null, false));
    }

    [Fact]
    public void BaseMatchesTheNoteDate_IsSynced()
    {
        Assert.Equal(SyncSignalState.Synced, NoteSyncSignal.For(Make(), Syncing(), new SyncBaseVersion(Edited, "a"), false));
    }

    [Fact]
    public void NoBaseOrAnOlderBase_IsPending()
    {
        Assert.Equal(SyncSignalState.Pending, NoteSyncSignal.For(Make(), Syncing(), null, false));
        Assert.Equal(SyncSignalState.Pending, NoteSyncSignal.For(Make(), Syncing(), new SyncBaseVersion(Edited.AddMinutes(-1), "a"), false));
    }

    [Fact]
    public void InTheConflictQueue_IsConflict_AndGoesBackWhenItLeaves()
    {
        var note = Make();
        var synced = new SyncBaseVersion(Edited, "a");
        Assert.Equal(SyncSignalState.Conflict, NoteSyncSignal.For(note, Syncing(), synced, true));
        Assert.Equal(SyncSignalState.Synced, NoteSyncSignal.For(note, Syncing(), synced, false));
    }

    [Fact]
    public void OutsideTheSelectiveScope_IsExcluded()
    {
        var bySelection = new AppSettings { SyncEnabled = true, SyncScope = SyncScopeKind.SelectedNotes };
        var byTag = new AppSettings { SyncEnabled = true, SyncScope = SyncScopeKind.Tag, SyncTag = "uni" };

        Assert.Equal(SyncSignalState.Excluded, NoteSyncSignal.For(Make(), bySelection, null, false));
        Assert.Equal(SyncSignalState.Excluded, NoteSyncSignal.For(Make("casa"), byTag, null, false));
        Assert.Equal(SyncSignalState.Pending, NoteSyncSignal.For(Make("UNI"), byTag, null, false));
    }

    [Fact]
    public void Glyph_IsEmptyOnlyWhenHidden()
    {
        Assert.Equal("", NoteSyncSignal.Glyph(SyncSignalState.Hidden));
        Assert.Equal("▂▄▆█", NoteSyncSignal.Glyph(SyncSignalState.Synced));
        Assert.All(new[] { SyncSignalState.Pending, SyncSignalState.Conflict, SyncSignalState.Excluded },
            state => Assert.NotEqual("", NoteSyncSignal.Glyph(state)));
    }
}
```

En `SyncConflictTests`, el estado real tras sincronizar y tras editar (comprueba que la fecha de la base
coincide con la de la nota guardada, que es lo que la señal compara):

```csharp
    [Fact]
    public void Signal_IsSyncedAfterSyncing_AndPendingAfterALocalEdit()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        SyncSignalState SignalOnA() => NoteSyncSignal.For(
            _deviceA.Repository.GetAllForSync().Single(), _deviceA.Settings,
            _deviceA.Repository.GetSyncBases().GetValueOrDefault(note.Id), false);

        Assert.Equal(SyncSignalState.Synced, SignalOnA());
        _deviceA.Repository.UpdateText(note.Id, "editada");
        Assert.Equal(SyncSignalState.Pending, SignalOnA());
    }
```

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~NoteSyncSignal|FullyQualifiedName~Signal_IsSynced"`. Expected: no compila.

- [ ] **Step 3: Implementar en Core**

`src/Aldune.Core/SyncScopeFilter.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Qué notas entran en la sincronización según el alcance elegido. Un solo sitio para la sync
/// y para la señal de cada nota, que tienen que estar de acuerdo.</summary>
public static class SyncScopeFilter
{
    public static bool Includes(Note note, AppSettings settings) => settings.SyncScope switch
    {
        SyncScopeKind.SelectedNotes => settings.SyncNoteIds.Contains(note.Id),
        SyncScopeKind.Tag => string.IsNullOrWhiteSpace(settings.SyncTag) ||
            note.Tags.Any(tag => string.Equals(tag, settings.SyncTag, StringComparison.OrdinalIgnoreCase)),
        _ => true,
    };
}
```

En `SyncService.cs`, en los dos sitios donde se filtran las notas locales
(`.Where(note => (scopedIds is null || scopedIds.Contains(note.Id)) && (... SyncTag ...))`), sustituir la
condición por `.Where(note => SyncScopeFilter.Includes(note, _settings))`. Los filtros de tombstones y
de objetos remotos se quedan como están. Correr `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~Sync"` antes de seguir: tiene que seguir en verde.

`src/Aldune.Core/NoteSyncSignal.cs` (con BOM):

```csharp
namespace Aldune.Core;

public enum SyncSignalState
{
    Hidden,
    Synced,
    Pending,
    Conflict,
    Excluded,
}

/// <summary>
/// La señal ▂▄▆█ al pie de cada nota: estado real de su sincronización, no decoración. Sale de datos
/// que ya existen: la última versión acordada con el almacén (la base) frente a la fecha de la nota,
/// la cola de conflictos y el alcance de la sync selectiva.
/// </summary>
public static class NoteSyncSignal
{
    public static SyncSignalState For(Note note, AppSettings settings, SyncBaseVersion? baseVersion, bool hasConflict)
    {
        if (!settings.SyncEnabled) return SyncSignalState.Hidden;
        if (!SyncScopeFilter.Includes(note, settings)) return SyncSignalState.Excluded;
        if (hasConflict) return SyncSignalState.Conflict;
        return baseVersion is not null && baseVersion.UpdatedAt == note.UpdatedAt
            ? SyncSignalState.Synced
            : SyncSignalState.Pending;
    }

    public static string Glyph(SyncSignalState state) => state switch
    {
        SyncSignalState.Synced => "▂▄▆█",
        SyncSignalState.Pending => "▂▄▆_",
        SyncSignalState.Conflict => "▂▄!_",
        SyncSignalState.Excluded => "▂___",
        _ => "",
    };
}
```

En `AppSettings.cs`:

```csharp
    /// <summary>Señal de sincronización al pie de cada nota. Nulo = lo que diga el aspecto (de momento,
    /// apagada en todos; los aspectos retro la traerán encendida).</summary>
    public bool? ShowSyncSignal { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool SyncSignalVisible => ShowSyncSignal ?? false;
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2. Expected: PASS.

- [ ] **Step 5: Mostrarla en la nota**

Strings:

```csharp
    public static string AppearanceSyncSignal => T("Show sync status on each note", "Señal de sincronización en cada nota",
        "Synchronisierungsstatus auf jeder Notiz", "Indicateur de synchronisation sur chaque note", "Sinal de sincronização em cada nota");
    public static string SyncSignalLabel(Aldune.Core.SyncSignalState state) => state switch
    {
        Aldune.Core.SyncSignalState.Synced => T("synced", "sincronizada", "synchronisiert", "synchronisée", "sincronizada"),
        Aldune.Core.SyncSignalState.Pending => T("pending", "pendiente", "ausstehend", "en attente", "pendente"),
        Aldune.Core.SyncSignalState.Conflict => T("conflict", "conflicto", "Konflikt", "conflit", "conflito"),
        Aldune.Core.SyncSignalState.Excluded => T("not synced", "no se sincroniza", "wird nicht synchronisiert", "non synchronisée", "não sincronizada"),
        _ => "",
    };
```

En `NoteWindow.xaml`, dentro del `Grid` de filas, después de `AllDoneBar`:

```xml
        <!-- Señal de sincronización (opción de Ajustes → Aspecto). En la fila de abajo, a la derecha,
             para no tapar texto; monoespaciada para que las barras ▂▄▆█ queden alineadas. -->
        <TextBlock x:Name="SyncSignalText" Grid.Row="2" HorizontalAlignment="Right" VerticalAlignment="Bottom"
                   Margin="0,-8,14,10" FontFamily="Cascadia Mono, Consolas" FontSize="11"
                   Foreground="{DynamicResource NoteInkBrush}" Opacity="0.7" Visibility="Collapsed" />
```

En `AppCoordinator.cs`:

```csharp
    /// <summary>Estado de la señal de sincronización de una nota (ver <see cref="NoteSyncSignal"/>).</summary>
    internal SyncSignalState SyncSignalFor(Guid noteId)
    {
        if (_settings is null || _repository.GetById(noteId) is not { } note) return SyncSignalState.Hidden;
        bool hasConflict = _repository.GetSyncConflicts().Any(conflict => conflict.NoteId == noteId);
        return NoteSyncSignal.For(note, _settings, _repository.GetSyncBases().GetValueOrDefault(noteId), hasConflict);
    }
```

y en `AfterSuccessfulSync()` cambiar `RefreshAll();` por `RefreshNoteAppearance();` (repinta también las
señales de las notas abiertas).

En `NoteWindow.xaml.cs`:

```csharp
    private void UpdateSyncSignal()
    {
        var state = _settings?.SyncSignalVisible == true ? _coordinator.SyncSignalFor(_note.Id) : SyncSignalState.Hidden;
        SyncSignalText.Visibility = state == SyncSignalState.Hidden ? Visibility.Collapsed : Visibility.Visible;
        SyncSignalText.Text = $"{NoteSyncSignal.Glyph(state)} {Strings.SyncSignalLabel(state)}";
    }
```

(usar el nombre real del campo de ajustes y del coordinador de `NoteWindow`; el constructor recibe
`AppSettings? settings` y `AppCoordinator coordinator`). Llamarlo al final del constructor, al final de
`Flush()` (tras guardar, la nota pasa a pendiente) y dentro de `ReapplyAppearance()`.

- [ ] **Step 6: Casilla en Ajustes**

En `SettingsWindow.xaml`, en el `StackPanel` que añadió Task 3, antes de `UniformColorCheck`:

```xml
                        <CheckBox x:Name="SyncSignalCheck"
                                  Content="{x:Static res:Strings.AppearanceSyncSignal}"
                                  Foreground="{DynamicResource AlduneTextBrush}" Background="{DynamicResource AlduneGroundBrush}" FontSize="13"
                                  Style="{StaticResource AppCheckBoxStyle}" Margin="0,0,0,10"
                                  Click="OnSyncSignalToggled" />
```

En `SettingsWindow.xaml.cs`: en el constructor `SyncSignalCheck.IsChecked = _settings.SyncSignalVisible;` y

```csharp
    private void OnSyncSignalToggled(object sender, RoutedEventArgs e)
    {
        _settings.ShowSyncSignal = SyncSignalCheck.IsChecked == true;
        _settingsService.Save(_settings);
        _coordinator?.RefreshNoteAppearance();
    }
```

- [ ] **Step 7: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde.

- [ ] **Step 8: Commit**

```bash
git add -A src tests
git commit -m "Senal de sincronizacion opcional al pie de cada nota: sincronizada, pendiente, conflicto o fuera del alcance"
```

---

### Task 6: Esquinas rectas

**Files:**
- Modify: `src/Aldune/App.xaml` (recursos de radio), los 12 `.xaml` con `CornerRadius` (lista abajo), `src/Aldune/Windowing/ThemeManager.cs`, `src/Aldune/Interop/NativeMethods.cs` (`ApplyRoundedCorners`), `EdgeDockWindow.xaml.cs` (`ApplyLeftEdgeTabShape`, `ApplyTopBottomTabShape`), `AutoScrollManager.cs:86`, `NotesManagerWindow.xaml.cs:337`, `NoteSwatchPanel.cs:65`, `ThemeEditorWindow.xaml.cs:82`, `src/Aldune.Core/AppSettings.cs`, `App.xaml.cs`, `SettingsWindow.xaml(.cs)`, `Strings.cs`
- Test: `tests/Aldune.Core.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `AppSettings.SquareCorners` (`bool?`) y `AppSettings.CornersSquare` (`bool`, `SquareCorners ?? false`; el bloque 3 hará el valor por defecto por aspecto); recursos `AlduneRadius3` … `AlduneRadius27` (los valores que hoy existen: 3, 4, 5, 6, 7, 8, 9, 10, 11, 27), `AlduneRadiusTabRight` (10,0,0,10), `AlduneRadiusEndRight` (0,5,5,0); `ThemeManager.ApplyShape(Application, bool square)`, `ThemeManager.IsSquare`, `ThemeManager.Radius(double) : CornerRadius`, `ThemeManager.Radius(double l, double t, double r, double b) : CornerRadius`; `NativeMethods.ReapplyCornerPreference()`.

Los radios 0 y 1 se quedan como están (0 ya es recto; 1 es un remate de un píxel que no se percibe como esquina).

- [ ] **Step 1: Test de compatibilidad de ajustes**

```csharp
    [Fact]
    public void Load_OldFileWithoutCornersOrSignal_KeepsTheRoundedLookAndNoSignal()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{\"AutoHideCompletedTasks\":true}");

        var loaded = new SettingsService(_settingsPath).Load();
        Assert.False(loaded.CornersSquare);
        Assert.False(loaded.SyncSignalVisible);
    }
```

Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~KeepsTheRoundedLook"` → no compila; añadir en `AppSettings.cs`:

```csharp
    /// <summary>Esquinas rectas en notas, dock y ventanas. Nulo = lo que diga el aspecto (de momento,
    /// redondeadas en todos; los aspectos retro las traerán rectas).</summary>
    public bool? SquareCorners { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool CornersSquare => SquareCorners ?? false;
```

→ PASS.

- [ ] **Step 2: Recursos de radio y `ThemeManager.ApplyShape`**

En `ThemeManager.cs`:

```csharp
    private static ResourceDictionary? _shape;

    // Los radios que usa la app. Con esquinas rectas todos valen 0; si no, su valor. Recursos y no
    // números escritos en cada XAML para que el cambio se vea en vivo, como la paleta.
    private static readonly double[] Radii = [3, 4, 5, 6, 7, 8, 9, 10, 11, 27];

    public static bool IsSquare { get; private set; }

    public static void ApplyShape(Application app, bool square)
    {
        var dictionary = new ResourceDictionary();
        foreach (var radius in Radii)
            dictionary[$"AlduneRadius{radius}"] = new CornerRadius(square ? 0 : radius);
        // Pestaña pegada al borde derecho de la pantalla (el dock la espeja por código a la izquierda).
        dictionary["AlduneRadiusTabRight"] = square ? new CornerRadius(0) : new CornerRadius(10, 0, 0, 10);
        dictionary["AlduneRadiusEndRight"] = square ? new CornerRadius(0) : new CornerRadius(0, 5, 5, 0);

        if (_shape is not null) app.Resources.MergedDictionaries.Remove(_shape);
        app.Resources.MergedDictionaries.Add(dictionary);
        _shape = dictionary;
        IsSquare = square;
        NativeMethods.ReapplyCornerPreference();
        Changed?.Invoke();
    }

    /// <summary>Radio para quien lo fija por código (pestañas espejadas, paneles creados a mano).</summary>
    public static CornerRadius Radius(double uniform) => new(IsSquare ? 0 : uniform);

    public static CornerRadius Radius(double left, double top, double right, double bottom) =>
        IsSquare ? new CornerRadius(0) : new CornerRadius(left, top, right, bottom);
```

En `App.xaml.cs`, justo después de `ThemeManager.Apply(this, settings.Appearance);` (línea ~83):

```csharp
            ThemeManager.ApplyShape(this, settings.CornersSquare);
```

(y lo mismo en la otra llamada a `ThemeManager.Apply` de la línea ~562, con `_settings?.CornersSquare ?? false`).

- [ ] **Step 3: Sustitución mecánica en los XAML** (delegable en el agente `mecanico`)

Script de una sola vez (fuera del repo, en el scratchpad), que conserva el BOM:

```python
import io, re, pathlib
root = pathlib.Path("src/Aldune")
values = {"3","4","5","6","7","8","9","10","11","27"}
special = {"10,0,0,10": "AlduneRadiusTabRight", "0,5,5,0": "AlduneRadiusEndRight"}
for path in list(root.rglob("*.xaml")):
    if "obj" in path.parts or "bin" in path.parts: continue
    text = io.open(path, encoding="utf-8-sig").read()
    def attr(m):
        v = m.group(1)
        if v in values: return f'CornerRadius="{{DynamicResource AlduneRadius{v}}}"'
        if v in special: return f'CornerRadius="{{DynamicResource {special[v]}}}"'
        return m.group(0)
    new = re.sub(r'CornerRadius="([0-9,]+)"', attr, text)
    def setter(m):
        v = m.group(1)
        key = f"AlduneRadius{v}" if v in values else special.get(v)
        return m.group(0) if key is None else f'Property="CornerRadius" Value="{{DynamicResource {key}}}"'
    new = re.sub(r'Property="CornerRadius" Value="([0-9,]+)"', setter, new)
    if new != text:
        io.open(path, "w", encoding="utf-8-sig", newline="").write(new)
        print(path)
```

Después, `grep -rn 'CornerRadius="[0-9]' src/Aldune --include=*.xaml` solo debe mostrar valores 0 y 1, y
`git diff --stat` los 12 ficheros (App.xaml, AppDialog, CustomColorWindow, EdgeDockWindow,
NotesManagerWindow, NoteWindow, PasswordPromptWindow, SettingsWindow, SyncConflictsWindow,
SyncNotesWindow, ThemeEditorWindow, ToastWindow). Revisar a mano que ninguna sustitución quedó dentro
de un `Binding` (por ejemplo `CornerRadius="{Binding ElementName=CardBorder, ...}"`: no casa con el
patrón numérico, así que no se toca).

- [ ] **Step 4: Radios fijados por código**

- `AutoScrollManager.cs:86` → `CornerRadius = ThemeManager.Radius(13),` (13 no está en la lista: `Radius` lo calcula, no hace falta recurso).
- `NotesManagerWindow.xaml.cs:337` y `ThemeEditorWindow.xaml.cs:82` → `ThemeManager.Radius(6)`.
- `NoteSwatchPanel.cs:65` → `ThemeManager.Radius(5)`.
- `EdgeDockWindow.xaml.cs`, `ApplyTopBottomTabShape`: `var radius = ThemeManager.Radius(10);`.
- `EdgeDockWindow.xaml.cs`, `ApplyLeftEdgeTabShape` (buscar su `new CornerRadius(...)` o la asignación del radio espejado, típicamente `0,10,10,0`) → `ThemeManager.Radius(0, 10, 10, 0)` con los mismos números que tenga.
- Como el dock aplica esas formas al cargar cada pestaña, al cambiar la opción hay que reconstruir los docks: en el manejador de Ajustes (Step 6) se llama a `_coordinator?.RebuildDocks()`.

- [ ] **Step 5: Esquinas de ventana en DWM**

En `NativeMethods.cs`, junto a `DWMWCP_ROUNDSMALL`:

```csharp
    private const int DWMWCP_DONOTROUND = 1;

    // Ventanas que pidieron esquinas propias, para poder cambiarlas en vivo con la opción de
    // esquinas rectas. Se guardan los HWND, no las ventanas: al cerrarse, la llamada a DWM sobre un
    // handle muerto simplemente falla y se descarta.
    private static readonly HashSet<IntPtr> RoundedWindows = [];
```

En `ApplyRoundedCorners(IntPtr hWnd)`: registrar `RoundedWindows.Add(hWnd);` y usar
`ThemeManager.IsSquare ? DWMWCP_DONOTROUND : DWMWCP_ROUNDSMALL` como preferencia (ajustar al nombre de
la variable local que use hoy). Y:

```csharp
    internal static void ReapplyCornerPreference()
    {
        foreach (var hWnd in RoundedWindows.ToList())
        {
            if (!IsWindow(hWnd)) { RoundedWindows.Remove(hWnd); continue; }
            ApplyRoundedCorners(hWnd);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
```

(si `NativeMethods` ya declara `IsWindow`, reutilizarlo; `ThemeManager` está en `Aldune.Windowing`:
añadir el `using` si hace falta).

- [ ] **Step 6: Casilla en Ajustes**

Strings:

```csharp
    public static string AppearanceSquareCorners => T("Square corners on notes, dock and windows", "Esquinas rectas en notas, dock y ventanas",
        "Eckige Ecken bei Notizen, Dock und Fenstern", "Coins carrés pour les notes, le dock et les fenêtres", "Cantos retos nas notas, no dock e nas janelas");
```

En `SettingsWindow.xaml`, primera casilla del `StackPanel` de Task 3:

```xml
                        <CheckBox x:Name="SquareCornersCheck"
                                  Content="{x:Static res:Strings.AppearanceSquareCorners}"
                                  Foreground="{DynamicResource AlduneTextBrush}" Background="{DynamicResource AlduneGroundBrush}" FontSize="13"
                                  Style="{StaticResource AppCheckBoxStyle}" Margin="0,0,0,10"
                                  Click="OnSquareCornersToggled" />
```

En `SettingsWindow.xaml.cs`: en el constructor `SquareCornersCheck.IsChecked = _settings.CornersSquare;` y

```csharp
    private void OnSquareCornersToggled(object sender, RoutedEventArgs e)
    {
        _settings.SquareCorners = SquareCornersCheck.IsChecked == true;
        _settingsService.Save(_settings);
        ThemeManager.ApplyShape(Application.Current, _settings.CornersSquare);
        // Las pestañas del dock fijan su forma por código al crearse: hay que rehacerlas.
        _coordinator?.RebuildDocks();
    }
```

- [ ] **Step 7: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 8: Commit**

```bash
git add -A src tests
git commit -m "Esquinas rectas opcionales: los radios pasan a recursos que se ponen a 0 en vivo, tambien las pestanas del dock y las esquinas de DWM"
```

---

### Task 7: Verificación con sonda y cierre

**Files:**
- Modify: `docs/STATUS.md`
- Sonda desechable fuera del repo (técnica en `docs/WPF_PROBES.md`)

- [ ] **Step 1: Sonda visual** (sin teclas, solo capturas; no mueve el ratón)

Con la base de datos temporal y `ThemeManager.Apply(app, AppearanceMode.Dark)`, crear 6 notas con
colores de Sereno (nuevos), abrir una `NoteWindow` y un `EdgeDockWindow` en un monitor real (los `Popup`
necesitan uno) con `KeepDockOpen = true` para que se vea desplegado, y guardar capturas con `Save(...)`
de la guía en estos estados:

1. Por defecto (redondeado, sin señal, colores propios).
2. `ThemeManager.ApplyShape(app, true)` + `coord.RebuildDocks()`: esquinas rectas en nota, pestañas y pie.
3. Lo mismo con el dock en `EdgePosition.Left` y en `EdgePosition.Top`: pestañas rectas (Review Focus 4).
4. `settings.ShowSyncSignal = true` y `SyncEnabled = true` sin base: la nota muestra `▂▄▆_ pendiente`.
5. `NoteColorDisplay.Uniform = "#33363A"` + `coord.RefreshNoteAppearance()`: nota, pestañas y cápsulas del mismo color; tinta legible.

Comprobaciones automáticas en la sonda: en (2), `CardBorder.CornerRadius` de cada pestaña es 0; en (4),
`SyncSignalText.Visibility == Visible`; en (5), el `Background` de la nota es `#33363A`. Revisar las
capturas a ojo contra la maqueta "Oscuro de siempre con esquinas rectas y señal de sync" del lienzo.

- [ ] **Step 2: Smoke test** — avisar al usuario (mueve el ratón) y, con su "ok": `dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug`. Expected: verde.

- [ ] **Step 3: STATUS.md** — sección "## 2026-10-0X: bloque 1 de aspectos (opciones transversales y temas)" con lo hecho, la causa del falso conflicto arreglado, la migración (qué pasa en equipos con versiones anteriores: siguen viendo los colores antiguos hasta recibir el recoloreo por sync) y el número de tests.

- [ ] **Step 4: Commit**

```bash
git add docs/STATUS.md
git commit -m "Docs: estado tras el bloque 1 de aspectos"
```

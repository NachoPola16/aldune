# Bloque 4: exportar e importar la configuración — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un archivo `*.aldune-config.json` con la configuración visual y los ajustes, que se exporta y se
importa desde Ajustes → Acerca de, con vista previa de lo que cambiaría, copia de seguridad antes de
aplicar y aplicación en vivo; sin que salga nunca nada de la sincronización ni de la pantalla elegida.

**Architecture:** Toda la lógica vive en Core y se prueba: una tabla de campos (`ConfigField`: id estable,
sección, cómo leer, cómo validar, cómo aplicar, cómo mostrar), `ConfigExport.Build`, `ConfigImport.Plan`
(devuelve la lista de cambios y sabe aplicarlos a unos ajustes) y `ConfigBackup`. La capa WPF solo pide
las secciones, enseña el plan y llama a lo de siempre para aplicar en vivo.

**Tech Stack:** C# / .NET 10, WPF, xUnit, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-10-03-aspectos-retro-design.md`, sección 6.

## Decisiones tomadas al planificar

- **Qué campo va en qué sección** (la spec lista los grupos; esto fija los campos de `AppSettings`):
  - **Aspecto**: `Appearance`, `AspectColors`, `SquareCorners`, `ShowSyncSignal`, `UniformNoteColor`,
    `ActiveThemeId`, `CustomThemes`, `NewNoteTone`, `ColorAssignment`, `FixedNoteColor`.
  - **Ajustes**: `Language`, `SimplifiedMode` (modo de interfaz), atajos (`GlobalHotkeyEnabled`,
    `HotkeyModifiers`, `HotkeyKey`, `RecentNoteHotkeyEnabled`, `RecentNoteHotkeyModifiers`,
    `RecentNoteHotkeyKey`), dock (`DockEdge`, `DockView`, `KeepDockOpen`, `ShowNotePreview`,
    `HideOnFullscreen`, `TrackpadGestures`), tareas (`MoveCompletedTasksToEnd`, `AutoHideCompletedTasks`,
    `AutoHideCompletedTasksDelayValue`, `AutoHideCompletedTasksDelayUnit`), papelera (`TrashRetentionDays`),
    `RememberNotePositions` y `CheckForUpdatesAutomatically`.
  - **Nunca**: nada de sincronización (`Sync*`, `Wrapped*`, perfiles, claves, servidor, `LastSyncAt`), la
    pantalla elegida (`TargetMonitorId`, `TargetMonitorIndex`, `DockFollowsMouse`), `DockTagFilter` (una
    etiqueta es dato de este equipo), `DefaultNoteLayout`, `NotesManagerOrder`, `SereneRecolored` (marca
    interna de la migración), el arranque con Windows y las posiciones de ventanas.
- **Claves del JSON** en camelCase, fijas (no cambiar nunca): `appearance`, `aspectColors`, `squareCorners`,
  `syncSignal`, `uniformNoteColor`, `noteTheme`, `customThemes`, `newNoteTone`, `colorAssignment`,
  `fixedNoteColor`, `language`, `simplifiedMode`, `hotkeyEnabled`, `hotkeyModifiers`, `hotkeyKey`,
  `recentHotkeyEnabled`, `recentHotkeyModifiers`, `recentHotkeyKey`, `dockEdge`, `dockView`, `keepDockOpen`,
  `showNotePreview`, `hideOnFullscreen`, `trackpadGestures`, `moveCompletedTasksToEnd`,
  `autoHideCompletedTasks`, `autoHideDelayValue`, `autoHideDelayUnit`, `trashRetentionDays`,
  `rememberNotePositions`, `checkForUpdates`. Los enums van como número, igual que en `settings.json`.
- **Al importar**: campo que falta = no se toca; sección que falta = no se toca; campo desconocido = se
  ignora; color inválido o tipo equivocado = el campo se descarta (no cambia); enum fuera de rango = el
  valor por defecto de ese ajuste; `version` mayor que 1 = se lee con lo que se entienda; JSON roto o sin
  `"format": "aldune-config"` = error (no cambia nada).
- **Idioma**: como en Ajustes, solo se aplica al reiniciar; el aviso de la vista previa lo dice.
- **Copia antes de aplicar**: `settings.json.antes-de-importar-aaaammdd-hhmmss` junto al original.

## Global Constraints

- Textos de interfaz siempre con `Strings.T(en, es, de, fr, pt)` en `src/Aldune/Resources/Strings.cs`, los cinco idiomas.
- Comentarios en español, explicando el porqué, con la densidad del código de alrededor.
- Lógica nueva en Core con test primero (TDD). La UI se verifica con sondas (`docs/WPF_PROBES.md`).
- `.cs` y `.xaml` con BOM UTF-8; documentación sin BOM.
- Un `settings.json` antiguo carga sin migración; este bloque no añade campos a `AppSettings`.
- Formato de sync **4**: no se sube (los ajustes no viajan por la sync).
- **Seguridad**: nunca sale del equipo una credencial, una clave, una ruta de sincronización ni la pantalla elegida. Hay un test que lo comprueba con todos esos campos rellenos.
- Colores de chrome con `{DynamicResource Aldune<Clave>Brush}`; nunca hex sueltos en XAML.
- Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`: sondas con base de datos temporal y `settings.json` temporal.
- Las sondas de teclado y el smoke test mueven ratón/teclado: avisar al usuario y esperar su "ok".
- Commits sin la línea `Co-Authored-By`.
- Comandos: `dotnet build Aldune.slnx -c Debug`, `dotnet test Aldune.slnx --no-build` (todos verdes; solo los 4 avisos CA1416 conocidos).

## Review Focus

1. **Nunca sale una credencial ni la pantalla** aunque todos esos campos estén rellenos, con todas las secciones marcadas → test de exportación (Task 1).
2. **Archivo corrupto, vacío, de otro formato, con un array en vez de objeto, con campos raros** → error claro o campo ignorado, sin excepción y sin cambiar nada (Task 1).
3. **Importar dos veces el mismo archivo** → la segunda vez el plan no trae cambios (Task 1).
4. **Importar un archivo exportado en otro equipo con otro idioma o atajo** → se aplica lo importado y no se pisa lo que no venía (Tasks 1 y 2).
5. **Cancelar la vista previa** no cambia ni guarda nada, y aplicar deja la copia de seguridad antes de escribir (Task 2).

---

### Task 1: Core — campos, exportar, importar, copia

**Files:**
- Create: `src/Aldune.Core/ConfigFields.cs`, `src/Aldune.Core/ConfigExport.cs`, `src/Aldune.Core/ConfigImport.cs`, `src/Aldune.Core/ConfigBackup.cs`
- Test: `tests/Aldune.Core.Tests/ConfigExportImportTests.cs`, `tests/Aldune.Core.Tests/ConfigBackupTests.cs`

**Interfaces:**
- Produces: `[Flags] enum ConfigSections { None = 0, Appearance = 1, Settings = 2, All = 3 }`; `ConfigFormat.Name = "aldune-config"`, `ConfigFormat.Version = 1`; `ConfigField(string Id, ConfigSections Section, ...)` con `ConfigFields.All : IReadOnlyList<ConfigField>` y `ConfigFields.Find(string id)`; `ConfigExport.Build(AppSettings settings, ConfigSections sections, string appVersion) : string`; `sealed record ConfigChange(string FieldId, ConfigSections Section, string OldValue, string NewValue)`; `sealed class ConfigImportPlan { bool IsValid; string? Error; int FileVersion; string? FileApp; IReadOnlyList<ConfigChange> Changes; ConfigSections SectionsInFile; void ApplyTo(AppSettings target); bool ChangesLanguage }`; `ConfigImport.Plan(string json, AppSettings current) : ConfigImportPlan`; `ConfigBackup.Create(string settingsPath, DateTimeOffset now) : string?`.

- [ ] **Step 1: Tests**

`tests/Aldune.Core.Tests/ConfigExportImportTests.cs` (con BOM):

```csharp
using System.Text.Json;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ConfigExportImportTests
{
    private static AppSettings Customized() => new()
    {
        Appearance = AppearanceMode.Bash,
        AspectColors = new() { ["bash"] = new() { ["accent"] = "#458588" } },
        SquareCorners = false,
        ShowSyncSignal = true,
        UniformNoteColor = "#33363A",
        ActiveThemeId = NoteThemes.SereneId,
        CustomThemes = [new NoteTheme { Id = "mio", Name = "Mío", LightColors = ["#FFDDD4", "#D3EFD8"] }],
        NewNoteTone = NoteTone.Dark,
        ColorAssignment = NoteColorAssignment.Fixed,
        FixedNoteColor = "#472525",
        Language = "de",
        SimplifiedMode = true,
        HotkeyModifiers = 3,
        HotkeyKey = 78,
        DockEdge = EdgePosition.Left,
        KeepDockOpen = true,
        ShowNotePreview = false,
        TrashRetentionDays = 45,
        AutoHideCompletedTasks = true,
        AutoHideCompletedTasksDelayValue = 3,
        AutoHideCompletedTasksDelayUnit = TaskDelayUnit.Hours,
    };

    // Todo lo que no puede salir de este equipo, rellenado.
    private static AppSettings WithSecrets()
    {
        var s = Customized();
        s.SyncEnabled = true;
        s.SyncFolderPath = @"Z:\secreto\sync";
        s.SyncServerUrl = "https://sync.ejemplo.invalid";
        s.WrappedSyncServerToken = [1, 2, 3, 4];
        s.WrappedSyncKey = [5, 6, 7];
        s.WrappedDatabaseKey = [9, 9, 9];
        s.WrappedSyncWebDavPassword = [8, 8];
        s.SyncWebDavUsername = "usuario-webdav";
        s.SyncDeviceId = "dispositivo-123";
        s.TargetMonitorId = "MONITOR-ID-SECRETO";
        s.TargetMonitorIndex = 2;
        s.DockTagFilter = "etiqueta-local";
        s.DockFollowsMouse = true;
        return s;
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Export_HasTheDocumentedShape()
    {
        var root = Parse(ConfigExport.Build(Customized(), ConfigSections.All, "1.5.0"));

        Assert.Equal("aldune-config", root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("1.5.0", root.GetProperty("app").GetString());
        Assert.Equal(9, root.GetProperty("appearance").GetProperty("appearance").GetInt32()); // enum como número
        Assert.Equal("#458588", root.GetProperty("appearance").GetProperty("aspectColors").GetProperty("bash").GetProperty("accent").GetString());
        Assert.Equal("de", root.GetProperty("settings").GetProperty("language").GetString());
    }

    [Fact]
    public void Export_NeverLeaksCredentialsKeysPathsOrTheChosenScreen()
    {
        var json = ConfigExport.Build(WithSecrets(), ConfigSections.All, "1.5.0");

        foreach (var secret in new[]
                 {
                     "secreto", "sync.ejemplo", "usuario-webdav", "dispositivo-123", "MONITOR-ID", "etiqueta-local",
                     "Wrapped", "SyncFolder", "SyncServer", "SyncWebDav", "SyncDevice", "SyncKey", "TargetMonitor", "DockFollowsMouse", "token", "password", "AQIDBA", "BQYH",
                 })
            Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);

        // Y tampoco por clave: solo claves de la lista documentada.
        var root = Parse(json);
        var keys = root.GetProperty("appearance").EnumerateObject().Concat(root.GetProperty("settings").EnumerateObject()).Select(p => p.Name);
        Assert.All(keys, key => Assert.NotNull(ConfigFields.Find(key)));
    }

    [Fact]
    public void Export_PartialSections_OnlyWritesTheMarkedOnes()
    {
        var appearanceOnly = Parse(ConfigExport.Build(Customized(), ConfigSections.Appearance, "1.5.0"));
        Assert.True(appearanceOnly.TryGetProperty("appearance", out _));
        Assert.False(appearanceOnly.TryGetProperty("settings", out _));

        var settingsOnly = Parse(ConfigExport.Build(Customized(), ConfigSections.Settings, "1.5.0"));
        Assert.False(settingsOnly.TryGetProperty("appearance", out _));
        Assert.True(settingsOnly.TryGetProperty("settings", out _));
    }

    [Fact]
    public void RoundTrip_ReproducesTheExportedSettings_InAFreshInstall()
    {
        var json = ConfigExport.Build(Customized(), ConfigSections.All, "1.5.0");
        var fresh = new AppSettings();

        var plan = ConfigImport.Plan(json, fresh);
        plan.ApplyTo(fresh);

        Assert.True(plan.IsValid, plan.Error);
        Assert.Equal(AppearanceMode.Bash, fresh.Appearance);
        Assert.Equal("#458588", fresh.ColorsFor(AppearanceMode.Bash)!["accent"]);
        Assert.False(fresh.SquareCorners);
        Assert.True(fresh.ShowSyncSignal);
        Assert.Equal("#33363A", fresh.UniformNoteColor);
        Assert.Equal(NoteThemes.SereneId, fresh.ActiveThemeId);
        Assert.Equal("mio", fresh.CustomThemes.Single().Id);
        Assert.Equal(NoteTone.Dark, fresh.NewNoteTone);
        Assert.Equal(NoteColorAssignment.Fixed, fresh.ColorAssignment);
        Assert.Equal("de", fresh.Language);
        Assert.True(fresh.SimplifiedMode);
        Assert.Equal(EdgePosition.Left, fresh.DockEdge);
        Assert.True(fresh.KeepDockOpen);
        Assert.False(fresh.ShowNotePreview);
        Assert.Equal(45, fresh.TrashRetentionDays);
        Assert.Equal(3, fresh.AutoHideCompletedTasksDelayValue);
        Assert.Equal(TaskDelayUnit.Hours, fresh.AutoHideCompletedTasksDelayUnit);
        Assert.Equal(3u, fresh.HotkeyModifiers);
        Assert.Equal(78u, fresh.HotkeyKey);
    }

    [Fact]
    public void Import_NeverTouchesSyncOrScreenSettings()
    {
        var target = WithSecrets();
        target.Language = "es"; // distinto del archivo
        var json = ConfigExport.Build(new AppSettings { Language = "fr" }, ConfigSections.All, "1.5.0");

        ConfigImport.Plan(json, target).ApplyTo(target);

        Assert.Equal("fr", target.Language);
        Assert.Equal(@"Z:\secreto\sync", target.SyncFolderPath);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, target.WrappedSyncServerToken);
        Assert.Equal("MONITOR-ID-SECRETO", target.TargetMonitorId);
        Assert.Equal("etiqueta-local", target.DockTagFilter);
        Assert.True(target.SyncEnabled);
    }

    [Fact]
    public void Import_ASectionThatIsMissing_IsNotTouched()
    {
        var target = Customized();
        var json = ConfigExport.Build(new AppSettings { Language = "pt" }, ConfigSections.Settings, "1.5.0");

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        Assert.Equal(AppearanceMode.Bash, target.Appearance);
        Assert.Equal("#33363A", target.UniformNoteColor);
        Assert.Equal("pt", target.Language);
        Assert.Equal(ConfigSections.Settings, plan.SectionsInFile);
    }

    [Fact]
    public void Plan_ListsOnlyWhatWouldChange_AndImportingTwiceChangesNothing()
    {
        var json = ConfigExport.Build(Customized(), ConfigSections.All, "1.5.0");
        var target = new AppSettings();

        var first = ConfigImport.Plan(json, target);
        Assert.NotEmpty(first.Changes);
        Assert.All(first.Changes, c => Assert.NotEqual(c.OldValue, c.NewValue));
        first.ApplyTo(target);

        Assert.Empty(ConfigImport.Plan(json, target).Changes);
    }

    [Fact]
    public void Plan_ChangesLanguage_IsFlaggedSoTheUiCanSayItNeedsARestart()
    {
        var json = ConfigExport.Build(new AppSettings { Language = "de" }, ConfigSections.Settings, "1.5.0");

        Assert.True(ConfigImport.Plan(json, new AppSettings { Language = "es" }).ChangesLanguage);
        Assert.False(ConfigImport.Plan(json, new AppSettings { Language = "de" }).ChangesLanguage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es json")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("{\"format\":\"otra-cosa\",\"version\":1}")]
    [InlineData("{\"format\":\"aldune-config\"")] // cortado
    public void Plan_CorruptOrForeignFiles_AreAnErrorAndChangeNothing(string json)
    {
        var target = Customized();

        var plan = ConfigImport.Plan(json, target);

        Assert.False(plan.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(plan.Error));
        Assert.Empty(plan.Changes);
        plan.ApplyTo(target); // inocuo
        Assert.Equal(AppearanceMode.Bash, target.Appearance);
    }

    [Fact]
    public void Plan_StrangeFields_AreIgnoredOrDropped_NeverThrow()
    {
        var target = new AppSettings();
        var json = """
        {
          "format": "aldune-config", "version": 7, "app": "9.9.9", "futuro": {"x": 1},
          "appearance": {
            "appearance": 99, "uniformNoteColor": "rojo", "fixedNoteColor": "", "squareCorners": "si",
            "aspectColors": { "bash": { "accent": "#12", "user": "#8ae234", "inventado": "#FFFFFF" }, "noExiste": { "accent": "#FFFFFF" } },
            "customThemes": [ { "Id": "malo", "LightColors": ["rojo"] }, { "Id": "bueno", "Name": "Bueno", "DarkColors": ["#112233"] } ],
            "newNoteTone": 77, "campoNuevo": true
          },
          "settings": { "dockEdge": 42, "language": "klingon", "trashRetentionDays": -5, "keepDockOpen": 3, "hotkeyKey": "A" }
        }
        """;

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        Assert.True(plan.IsValid, plan.Error);          // versión futura: se lee lo que se entiende
        Assert.Equal(7, plan.FileVersion);
        Assert.Equal(AppearanceMode.Dark, target.Appearance);   // enum fuera de rango: su valor por defecto
        Assert.Null(target.UniformNoteColor);           // color inválido: se descarta
        Assert.Null(target.FixedNoteColor);
        Assert.Null(target.SquareCorners);              // tipo equivocado: se descarta
        Assert.Equal("#8AE234", target.ColorsFor(AppearanceMode.Bash)!["user"]);
        Assert.False(target.ColorsFor(AppearanceMode.Bash)!.ContainsKey("accent"));
        Assert.False(target.ColorsFor(AppearanceMode.Bash)!.ContainsKey("inventado"));
        Assert.Equal("bueno", target.CustomThemes.Single().Id);  // NoteThemes.Sanitize
        Assert.Equal(NoteTone.Light, target.NewNoteTone);
        Assert.Equal(EdgePosition.Right, target.DockEdge);
        Assert.Null(target.Language);                   // idioma que no existe: se descarta
        Assert.Equal(NotesRepository.DefaultTrashRetentionDays, target.TrashRetentionDays);
        Assert.False(target.KeepDockOpen);
        Assert.Null(target.HotkeyKey);
    }

    [Fact]
    public void Plan_ReadsAFileExportedOnAnotherMachine_WithoutOverwritingWhatItDoesNotCarry()
    {
        var target = Customized();
        target.HotkeyModifiers = 5; // atajo propio de este equipo
        var other = new AppSettings { Appearance = AppearanceMode.Phosphor, Language = "fr" };
        var json = ConfigExport.Build(other, ConfigSections.Appearance, "1.5.0");

        ConfigImport.Plan(json, target).ApplyTo(target);

        Assert.Equal(AppearanceMode.Phosphor, target.Appearance);
        Assert.Equal("de", target.Language);          // la sección Ajustes no venía
        Assert.Equal(5u, target.HotkeyModifiers);
    }
}
```

`tests/Aldune.Core.Tests/ConfigBackupTests.cs` (con BOM):

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class ConfigBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-config-backup-{Guid.NewGuid():N}");

    public ConfigBackupTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void Create_CopiesSettingsNextToIt_WithTheTimestamp()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{\"Language\":\"es\"}");

        var copy = ConfigBackup.Create(path, new DateTimeOffset(2026, 10, 4, 9, 30, 15, TimeSpan.Zero));

        Assert.Equal(Path.Combine(_dir, "settings.json.antes-de-importar-20261004-093015"), copy);
        Assert.Equal("{\"Language\":\"es\"}", File.ReadAllText(copy!));
        Assert.Equal("{\"Language\":\"es\"}", File.ReadAllText(path)); // el original no se toca
    }

    [Fact]
    public void Create_WithoutASettingsFile_ReturnsNull()
    {
        Assert.Null(ConfigBackup.Create(Path.Combine(_dir, "no-existe.json"), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_TwiceInTheSameSecond_DoesNotOverwriteTheFirstCopy()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "uno");
        var now = new DateTimeOffset(2026, 10, 4, 9, 30, 15, TimeSpan.Zero);
        var first = ConfigBackup.Create(path, now);
        File.WriteAllText(path, "dos");

        var second = ConfigBackup.Create(path, now);

        Assert.NotEqual(first, second);
        Assert.Equal("uno", File.ReadAllText(first!));
        Assert.Equal("dos", File.ReadAllText(second!));
    }
}
```

Antes de ejecutar, comprobar en el código real: `UiLanguages.All` (códigos de idioma válidos), `TaskDelayUnit.Hours`, `NoteColorAssignment.Fixed`, `EdgePosition.Left/Right`, `NotesRepository.DefaultTrashRetentionDays`, y los nombres y valores por defecto de las propiedades de `AppSettings`; adaptar los tests a lo que exista (si un valor no existe, usar otro del mismo enum) sin cambiar lo que comprueban.

- [ ] **Step 2: Ver que fallan** — Run: `dotnet test tests/Aldune.Core.Tests -c Debug --filter "FullyQualifiedName~ConfigExportImport|FullyQualifiedName~ConfigBackup"`. Expected: no compila (`ConfigExport`… no existen).

- [ ] **Step 3: Implementar**

`src/Aldune.Core/ConfigFields.cs` (con BOM):

```csharp
using System.Text.Json;

namespace Aldune.Core;

[Flags]
public enum ConfigSections
{
    None = 0,
    Appearance = 1,
    Settings = 2,
    All = Appearance | Settings,
}

public static class ConfigFormat
{
    public const string Name = "aldune-config";
    public const int Version = 1;
}

/// <summary>
/// Un ajuste que puede viajar en el archivo de configuración. La lista de <see cref="ConfigFields.All"/>
/// es lo único que sale al exportar y lo único que se toca al importar: lo que no está aquí (sincronización,
/// claves, pantalla elegida…) no puede salir ni entrar por esta vía. El id es la clave del JSON y no se
/// cambia nunca.
/// </summary>
public sealed record ConfigField(
    string Id,
    ConfigSections Section,
    Func<AppSettings, object?> Get,
    Func<JsonElement, (bool Ok, object? Value)> Read,
    Action<AppSettings, object?> Set,
    Func<object?, string> Show);

public static class ConfigFields
{
    public static IReadOnlyList<ConfigField> All { get; } = Build();

    public static ConfigField? Find(string id) => All.FirstOrDefault(field => field.Id == id);

    private static readonly AppSettings Defaults = new();

    private static List<ConfigField> Build() =>
    [
        // — Aspecto —
        Enum<AppearanceMode>("appearance", ConfigSections.Appearance, s => s.Appearance, (s, v) => s.Appearance = v),
        Custom("aspectColors", ConfigSections.Appearance, s => s.AspectColors, ReadAspectColors, (s, v) => s.AspectColors = (Dictionary<string, Dictionary<string, string>>?)v, ShowAspectColors),
        NullableBool("squareCorners", ConfigSections.Appearance, s => s.SquareCorners, (s, v) => s.SquareCorners = v),
        NullableBool("syncSignal", ConfigSections.Appearance, s => s.ShowSyncSignal, (s, v) => s.ShowSyncSignal = v),
        Color("uniformNoteColor", ConfigSections.Appearance, s => s.UniformNoteColor, (s, v) => s.UniformNoteColor = v),
        Text("noteTheme", ConfigSections.Appearance, s => s.ActiveThemeId, (s, v) => s.ActiveThemeId = v, valid: _ => true),
        Custom("customThemes", ConfigSections.Appearance, s => s.CustomThemes, ReadThemes, (s, v) => s.CustomThemes = (List<NoteTheme>)v!, ShowThemes),
        Enum<NoteTone>("newNoteTone", ConfigSections.Appearance, s => s.NewNoteTone, (s, v) => s.NewNoteTone = v),
        Enum<NoteColorAssignment>("colorAssignment", ConfigSections.Appearance, s => s.ColorAssignment, (s, v) => s.ColorAssignment = v),
        Color("fixedNoteColor", ConfigSections.Appearance, s => s.FixedNoteColor, (s, v) => s.FixedNoteColor = v),

        // — Ajustes —
        Text("language", ConfigSections.Settings, s => s.Language, (s, v) => s.Language = v, valid: code => UiLanguages.All.Any(language => language.Code == code)),
        Bool("simplifiedMode", ConfigSections.Settings, s => s.SimplifiedMode, (s, v) => s.SimplifiedMode = v),
        Bool("hotkeyEnabled", ConfigSections.Settings, s => s.GlobalHotkeyEnabled, (s, v) => s.GlobalHotkeyEnabled = v),
        NullableUInt("hotkeyModifiers", ConfigSections.Settings, s => s.HotkeyModifiers, (s, v) => s.HotkeyModifiers = v),
        NullableUInt("hotkeyKey", ConfigSections.Settings, s => s.HotkeyKey, (s, v) => s.HotkeyKey = v),
        Bool("recentHotkeyEnabled", ConfigSections.Settings, s => s.RecentNoteHotkeyEnabled, (s, v) => s.RecentNoteHotkeyEnabled = v),
        NullableUInt("recentHotkeyModifiers", ConfigSections.Settings, s => s.RecentNoteHotkeyModifiers, (s, v) => s.RecentNoteHotkeyModifiers = v),
        NullableUInt("recentHotkeyKey", ConfigSections.Settings, s => s.RecentNoteHotkeyKey, (s, v) => s.RecentNoteHotkeyKey = v),
        Enum<EdgePosition>("dockEdge", ConfigSections.Settings, s => s.DockEdge, (s, v) => s.DockEdge = v),
        Enum<DockViewKind>("dockView", ConfigSections.Settings, s => s.DockView, (s, v) => s.DockView = v),
        Bool("keepDockOpen", ConfigSections.Settings, s => s.KeepDockOpen, (s, v) => s.KeepDockOpen = v),
        Bool("showNotePreview", ConfigSections.Settings, s => s.ShowNotePreview, (s, v) => s.ShowNotePreview = v),
        Bool("hideOnFullscreen", ConfigSections.Settings, s => s.HideOnFullscreen, (s, v) => s.HideOnFullscreen = v),
        Bool("trackpadGestures", ConfigSections.Settings, s => s.TrackpadGestures, (s, v) => s.TrackpadGestures = v),
        Bool("moveCompletedTasksToEnd", ConfigSections.Settings, s => s.MoveCompletedTasksToEnd, (s, v) => s.MoveCompletedTasksToEnd = v),
        Bool("autoHideCompletedTasks", ConfigSections.Settings, s => s.AutoHideCompletedTasks, (s, v) => s.AutoHideCompletedTasks = v),
        Int("autoHideDelayValue", ConfigSections.Settings, 1, 999, s => s.AutoHideCompletedTasksDelayValue, (s, v) => s.AutoHideCompletedTasksDelayValue = v),
        Enum<TaskDelayUnit>("autoHideDelayUnit", ConfigSections.Settings, s => s.AutoHideCompletedTasksDelayUnit, (s, v) => s.AutoHideCompletedTasksDelayUnit = v),
        Int("trashRetentionDays", ConfigSections.Settings, 1, 3650, s => s.TrashRetentionDays, (s, v) => s.TrashRetentionDays = v),
        Bool("rememberNotePositions", ConfigSections.Settings, s => s.RememberNotePositions, (s, v) => s.RememberNotePositions = v),
        Bool("checkForUpdates", ConfigSections.Settings, s => s.CheckForUpdatesAutomatically, (s, v) => s.CheckForUpdatesAutomatically = v),
    ];

    private static readonly (bool, object?) Invalid = (false, null);

    private static ConfigField Custom(string id, ConfigSections section, Func<AppSettings, object?> get,
        Func<JsonElement, (bool, object?)> read, Action<AppSettings, object?> set, Func<object?, string> show) =>
        new(id, section, get, read, set, show);

    private static ConfigField Bool(string id, ConfigSections section, Func<AppSettings, bool> get, Action<AppSettings, bool> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind is JsonValueKind.True or JsonValueKind.False ? (true, e.GetBoolean()) : Invalid,
            (s, v) => set(s, (bool)v!), v => v is true ? "true" : "false");

    private static ConfigField NullableBool(string id, ConfigSections section, Func<AppSettings, bool?> get, Action<AppSettings, bool?> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.True => (true, true),
                JsonValueKind.False => (true, false),
                JsonValueKind.Null => (true, null),
                _ => Invalid,
            },
            (s, v) => set(s, (bool?)v), v => v is null ? "—" : v is true ? "true" : "false");

    private static ConfigField Int(string id, ConfigSections section, int min, int max, Func<AppSettings, int> get, Action<AppSettings, int> set)
    {
        var fallback = get(Defaults);
        return new(id, section, s => get(s),
            e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)
                ? (true, n >= min && n <= max ? n : fallback)   // fuera de rango: el valor por defecto
                : Invalid,
            (s, v) => set(s, (int)v!), v => v?.ToString() ?? "—");
    }

    private static ConfigField NullableUInt(string id, ConfigSections section, Func<AppSettings, uint?> get, Action<AppSettings, uint?> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.Null => (true, null),
                JsonValueKind.Number when e.TryGetUInt32(out var n) => (true, (uint?)n),
                _ => Invalid,
            },
            (s, v) => set(s, (uint?)v), v => v?.ToString() ?? "—");

    private static ConfigField Enum<T>(string id, ConfigSections section, Func<AppSettings, T> get, Action<AppSettings, T> set) where T : struct, System.Enum
    {
        var fallback = get(Defaults);
        return new(id, section, s => get(s),
            e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)
                ? (true, System.Enum.IsDefined(typeof(T), n) ? (T)System.Enum.ToObject(typeof(T), n) : fallback)
                : Invalid,
            (s, v) => set(s, (T)v!), v => v is T t ? t.ToString() : v?.ToString() ?? "—");
    }

    private static ConfigField Color(string id, ConfigSections section, Func<AppSettings, string?> get, Action<AppSettings, string?> set) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.Null => (true, null),
                JsonValueKind.String when NoteDisplayColor.IsActive(e.GetString()) => (true, e.GetString()!.ToUpperInvariant()),
                _ => Invalid,   // un color inválido se descarta, no cambia nada
            },
            (s, v) => set(s, (string?)v), v => v as string ?? "—");

    private static ConfigField Text(string id, ConfigSections section, Func<AppSettings, string?> get, Action<AppSettings, string?> set, Func<string, bool> valid) =>
        new(id, section, s => get(s),
            e => e.ValueKind switch
            {
                JsonValueKind.Null => (true, null),
                JsonValueKind.String when !string.IsNullOrWhiteSpace(e.GetString()) && valid(e.GetString()!) => (true, e.GetString()),
                _ => Invalid,
            },
            (s, v) => set(s, (string?)v), v => v as string ?? "—");

    // Colores por aspecto: solo aspectos y huecos que existen, solo #RRGGBB válidos.
    private static (bool, object?) ReadAspectColors(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null) return (true, null);
        if (element.ValueKind != JsonValueKind.Object) return Invalid;

        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var aspect in element.EnumerateObject())
        {
            var definition = AspectCatalog.Retro.FirstOrDefault(a => a.Id == aspect.Name);
            if (definition is null || aspect.Value.ValueKind != JsonValueKind.Object) continue;
            var colors = new Dictionary<string, string>();
            foreach (var slot in aspect.Value.EnumerateObject())
            {
                if (definition.Slots.All(s => s.Id != slot.Name)) continue;
                if (slot.Value.ValueKind == JsonValueKind.String && NoteDisplayColor.IsActive(slot.Value.GetString()))
                    colors[slot.Name] = slot.Value.GetString()!.ToUpperInvariant();
            }
            if (colors.Count > 0) result[aspect.Name] = colors;
        }
        return (true, result.Count == 0 ? null : result);
    }

    private static string ShowAspectColors(object? value) =>
        value is Dictionary<string, Dictionary<string, string>> all && all.Count > 0
            ? string.Join("; ", all.OrderBy(a => a.Key).Select(a => $"{a.Key}: " + string.Join(", ", a.Value.OrderBy(c => c.Key).Select(c => $"{c.Key}={c.Value}"))))
            : "—";

    // Temas propios: los mismos del archivo pasados por NoteThemes.Sanitize (colores válidos, ids únicos).
    private static (bool, object?) ReadThemes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) return Invalid;
        try
        {
            var themes = JsonSerializer.Deserialize<List<NoteTheme>>(element.GetRawText());
            return (true, NoteThemes.Sanitize(themes));
        }
        catch (JsonException)
        {
            return Invalid;
        }
    }

    private static string ShowThemes(object? value) =>
        value is List<NoteTheme> themes && themes.Count > 0
            ? string.Join(", ", themes.Select(t => $"{t.Name} ({t.DarkColors.Count + t.LightColors.Count})"))
            : "—";
}
```

(Si `Enum<T>`/`Convert` chocan con nombres del espacio, calificarlos. Si `NoteTheme` no se deserializa con
las opciones por defecto —por ejemplo `init` o `required`—, usar las mismas que `SettingsService`.)

`src/Aldune.Core/ConfigExport.cs` (con BOM):

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aldune.Core;

/// <summary>Escribe el archivo de configuración (<c>*.aldune-config.json</c>). Solo salen los campos de
/// <see cref="ConfigFields.All"/> de las secciones marcadas.</summary>
public static class ConfigExport
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static string Build(AppSettings settings, ConfigSections sections, string appVersion)
    {
        var root = new JsonObject
        {
            ["format"] = ConfigFormat.Name,
            ["version"] = ConfigFormat.Version,
            ["app"] = appVersion,
        };
        AddSection(root, "appearance", ConfigSections.Appearance, settings, sections);
        AddSection(root, "settings", ConfigSections.Settings, settings, sections);
        return root.ToJsonString(Pretty);
    }

    private static void AddSection(JsonObject root, string name, ConfigSections section, AppSettings settings, ConfigSections wanted)
    {
        if (!wanted.HasFlag(section)) return;
        var node = new JsonObject();
        foreach (var field in ConfigFields.All.Where(f => f.Section == section))
            node[field.Id] = JsonSerializer.SerializeToNode(field.Get(settings));
        root[name] = node;
    }
}
```

`src/Aldune.Core/ConfigImport.cs` (con BOM):

```csharp
using System.Text.Json;

namespace Aldune.Core;

public sealed record ConfigChange(string FieldId, ConfigSections Section, string OldValue, string NewValue);

/// <summary>Lo que importar un archivo cambiaría, y cómo aplicarlo. Crear el plan no toca nada.</summary>
public sealed class ConfigImportPlan
{
    private readonly List<(ConfigField Field, object? Value)> _apply = [];
    private readonly List<ConfigChange> _changes = [];

    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public int FileVersion { get; init; }
    public string? FileApp { get; init; }
    public ConfigSections SectionsInFile { get; init; }
    public IReadOnlyList<ConfigChange> Changes => _changes;

    /// <summary>El idioma solo se aplica al reiniciar (como en Ajustes): la vista previa lo avisa.</summary>
    public bool ChangesLanguage => _changes.Any(c => c.FieldId == "language");

    internal void Add(ConfigField field, object? value, ConfigChange change)
    {
        _apply.Add((field, value));
        _changes.Add(change);
    }

    /// <summary>Aplica los cambios del plan. Con un plan inválido o sin cambios no hace nada.</summary>
    public void ApplyTo(AppSettings target)
    {
        foreach (var (field, value) in _apply) field.Set(target, value);
    }
}

/// <summary>
/// Lee un archivo de configuración con tolerancia (spec, sección 6): un campo o una sección que falta no
/// se toca; los campos desconocidos se ignoran; un color inválido o un tipo equivocado descartan ese
/// campo; un enum fuera de rango cae en su valor por defecto; una versión futura se lee con lo que se
/// entienda. Solo un archivo roto o que no es de Aldune es un error.
/// </summary>
public static class ConfigImport
{
    public static ConfigImportPlan Plan(string json, AppSettings current)
    {
        if (string.IsNullOrWhiteSpace(json)) return Fail("El archivo está vacío.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Fail("El archivo no es un JSON válido.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("format", out var format) || format.GetString() != ConfigFormat.Name)
                return Fail("No es un archivo de configuración de Aldune.");

            int version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 1;
            string? app = root.TryGetProperty("app", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;

            var sections = ConfigSections.None;
            var changes = new List<(ConfigField, object?, ConfigChange)>();
            foreach (var (name, section) in new[] { ("appearance", ConfigSections.Appearance), ("settings", ConfigSections.Settings) })
            {
                if (!root.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object) continue;
                sections |= section;
                foreach (var field in ConfigFields.All.Where(f => f.Section == section))
                {
                    if (!node.TryGetProperty(field.Id, out var element)) continue;
                    var (ok, value) = field.Read(element);
                    if (!ok) continue;

                    string before = field.Show(field.Get(current)), after = field.Show(value);
                    if (before == after) continue;
                    changes.Add((field, value, new ConfigChange(field.Id, section, before, after)));
                }
            }

            var plan = new ConfigImportPlan { IsValid = true, FileVersion = version, FileApp = app, SectionsInFile = sections };
            foreach (var (field, value, change) in changes) plan.Add(field, value, change);
            return plan;
        }
    }

    private static ConfigImportPlan Fail(string error) => new() { IsValid = false, Error = error };
}
```

(`Get` de un enum devuelve el propio enum, para que antes y después se muestren con la misma forma;
`SerializeToNode` de un enum con las opciones por defecto lo escribe como número, que es lo que comprueba
el test de forma con el `9`.)

`src/Aldune.Core/ConfigBackup.cs` (con BOM):

```csharp
namespace Aldune.Core;

/// <summary>Copia de <c>settings.json</c> antes de aplicar una importación, para poder volver atrás.</summary>
public static class ConfigBackup
{
    public static string? Create(string settingsPath, DateTimeOffset now)
    {
        if (!File.Exists(settingsPath)) return null;

        var baseName = $"{settingsPath}.antes-de-importar-{now:yyyyMMdd-HHmmss}";
        var target = baseName;
        // Dos importaciones en el mismo segundo no pisan la primera copia.
        for (int i = 2; File.Exists(target); i++) target = $"{baseName}-{i}";
        File.Copy(settingsPath, target);
        return target;
    }
}
```

- [ ] **Step 4: Tests en verde** — Run el filtro del Step 2 y la suite de Core. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core tests/Aldune.Core.Tests
git commit -m "Exportar e importar la configuracion en Core: campos con validacion, exportacion por secciones sin credenciales ni pantalla, plan de importacion con lista de cambios y copia previa"
```

---

### Task 2: Ajustes → Acerca de: exportar e importar

**Files:**
- Create: `src/Aldune/Windowing/ConfigExportWindow.xaml(.cs)`, `src/Aldune/Windowing/ConfigImportWindow.xaml(.cs)`
- Modify: `src/Aldune/Windowing/SettingsWindow.xaml`, `src/Aldune/Windowing/SettingsWindow.xaml.cs`, `src/Aldune/Resources/Strings.cs`, (si hace falta) `src/Aldune/Windowing/AppCoordinator.cs`

**Interfaces:**
- Consumes: `ConfigExport.Build`, `ConfigImport.Plan`, `ConfigImportPlan.ApplyTo/Changes/ChangesLanguage`, `ConfigBackup.Create`, `ConfigFields.Find`, `SettingsService.Save`, el camino de aplicar aspecto de Ajustes (`SaveAndApplyAppearance`, bloque 3) y las funciones que ya re-registran los atajos y recolocan el dock.
- Produces: `ConfigExportWindow.Show(Window owner) : ConfigSections?`; `ConfigImportWindow.Show(Window owner, ConfigImportPlan plan) : bool` (true = Aplicar).

- [ ] **Step 1: Textos** (en `Strings.cs`, junto a los de Acerca de), los cinco idiomas cada uno:

`ConfigExportButton` ("Export settings…" / "Exportar configuración…" / "Einstellungen exportieren…" / "Exporter la configuration…" / "Exportar configuração…"), `ConfigImportButton`, `ConfigExportTitle`, `ConfigExportHint` (qué se exporta y que **nunca** se exportan la sincronización, las claves, la pantalla elegida ni las posiciones), `ConfigSectionAppearance` ("Appearance"/"Aspecto"…), `ConfigSectionSettings` ("Settings"/"Ajustes"…) con una línea descriptiva cada una (`ConfigSectionAppearanceHint`: "Aspecto, colores, esquinas, señal, tema de notas y temas propios…"; `ConfigSectionSettingsHint`: "Idioma, atajos, dock, tareas, papelera…"), `ConfigExportSave` ("Export"), `ConfigImportTitle`, `ConfigImportNoChanges` ("The file has nothing different from your current settings."), `ConfigImportApply` ("Apply"), `ConfigImportRestartForLanguage` ("The language changes when you restart Aldune."), `ConfigImportBackupNote` ("A copy of your current settings is saved first."), `ConfigImportFromVersion(string app)` ("File from Aldune {app}"), `ConfigImportError(string detail)` ("Couldn't read the file: {detail}") — el detalle del error de Core va en español fijo en Core; para la interfaz usar un texto genérico traducido y no mostrar el de Core —, `ConfigImportDone` ("Settings imported."), y `ConfigFieldName(string id) : string` con **el nombre visible traducido de cada uno de los 31 campos** de `ConfigFields.All` (clave del JSON → nombre: `appearance` "Appearance", `aspectColors` "Aspect colors", `squareCorners` "Square corners", `syncSignal` "Sync signal", `uniformNoteColor` "Same color for every note", `noteTheme` "Note theme", `customThemes` "Custom themes", `newNoteTone` "New note tone", `colorAssignment` "Color for new notes", `fixedNoteColor` "Fixed note color", `language` "Language", `simplifiedMode` "Simple interface", `hotkeyEnabled`/`hotkeyModifiers`/`hotkeyKey` "New note shortcut"…, `recentHotkey*` "Last note shortcut", `dockEdge` "Dock edge", `dockView` "Dock view", `keepDockOpen`, `showNotePreview`, `hideOnFullscreen`, `trackpadGestures`, `moveCompletedTasksToEnd`, `autoHideCompletedTasks`, `autoHideDelayValue`/`autoHideDelayUnit`, `trashRetentionDays`, `rememberNotePositions`, `checkForUpdates`; un id desconocido devuelve el id). Un test de Core no puede ver `Strings`, así que lo comprueba una línea de la sonda: todos los ids de `ConfigFields.All` tienen nombre distinto del id en los cinco idiomas.

- [ ] **Step 2: `ConfigExportWindow`** — ventana pequeña en el estilo de `AppDialog` (mismos recursos, `WindowChrome`, `DynamicResource`): título, la `ConfigExportHint`, dos casillas (`AppCheckBoxStyle`) con su descripción debajo, las dos marcadas por defecto, botones "Exportar" (`PrimaryColorDialogButtonStyle`) y "Cancelar". `Show(owner)` devuelve las secciones marcadas o `null` si cancela; con ninguna marcada el botón Exportar está desactivado. Después de devolver las secciones, `SettingsWindow` abre `Microsoft.Win32.SaveFileDialog` (`Filter = "Aldune (*.aldune-config.json)|*.aldune-config.json"`, `FileName = "aldune.aldune-config.json"`, `AddExtension`, `DefaultExt = ".aldune-config.json"`) y escribe `ConfigExport.Build(_settings, sections, AppInfo.Version)` en UTF-8 sin BOM; si la escritura falla (`IOException`, `UnauthorizedAccessException`), `AppDialog` con el error traducido y nada más.

- [ ] **Step 3: `ConfigImportWindow`** — misma familia de ventana: título, `ConfigImportFromVersion(plan.FileApp)` si lo hay, una lista con scroll (una fila por cambio: `Strings.ConfigFieldName(id)` en negrita; debajo `Antes → Ahora` con los valores de `OldValue`/`NewValue`, en monoespaciada pequeña y recortados con elipsis a una línea con `ToolTip` del valor entero), `ConfigImportRestartForLanguage` si `plan.ChangesLanguage`, `ConfigImportBackupNote`, y "Aplicar" / "Cancelar". Sin cambios: solo el texto `ConfigImportNoChanges` y un botón "Cerrar" (no "Aplicar"). `Show(owner, plan)` devuelve `true` solo con "Aplicar".

- [ ] **Step 4: Botones y flujo en Ajustes → Acerca de** — en `AboutPage` (`SettingsWindow.xaml`), una sección "Configuración" con los dos botones (`RecordButtonStyle`). Importar: `OpenFileDialog` con el mismo filtro → leer el texto (`File.ReadAllText`; `IOException`/`UnauthorizedAccessException` → `AppDialog` con el error) → `ConfigImport.Plan(texto, _settings)` → si `!plan.IsValid`: `AppDialog` con `ConfigImportError` (texto genérico traducido) y nada más → `ConfigImportWindow.Show` → si cancela, **no se toca nada** → si aplica:
  1. `ConfigBackup.Create(<ruta de settings.json del SettingsService>, DateTimeOffset.Now)` (si falla, `AppDialog` y no aplica);
  2. `plan.ApplyTo(_settings)` y `_settingsService.Save(_settings)`;
  3. aplicar en vivo: el aspecto con el mismo camino que usa Ajustes al elegir uno (`SaveAndApplyAppearance` sin la pregunta del tema), el tema de notas y los colores de las notas abiertas (`RefreshNoteAppearance`), los atajos (volver a registrarlos con lo que ya use Ajustes al cambiarlos), el borde y la vista del dock, "mantener abierto", vista previa, pantalla completa, y las casillas y radios de la propia ventana de Ajustes puestos al día (volver a pintar la página con lo que ya haya: `PopulateAppearance`, `PopulateAspectColors`, y los demás `Populate*`/`Refresh*` que existan). El idioma queda para el reinicio (ya avisado en la vista previa).
  Buscar y reutilizar los métodos existentes en vez de duplicar lógica; si un ajuste no se puede aplicar en vivo con lo que hay, dejarlo para el reinicio y decirlo en el informe.
  4. `AppDialog` con `ConfigImportDone`.

- [ ] **Step 5: Compilar y suite** — Run: `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build`. Expected: verde, sin avisos nuevos.

- [ ] **Step 6: Commit**

```bash
git add -A src
git commit -m "Ajustes, Acerca de: exportar e importar la configuracion con vista previa, copia previa y aplicacion en vivo"
```

---

### Task 3: Verificación con sonda y cierre

**Files:**
- Modify: `docs/STATUS.md`
- Sonda desechable fuera del repo (`docs/WPF_PROBES.md`)

- [ ] **Step 1: Sonda** (sin teclas ni ratón; `settings.json` y base de datos temporales): con una `SettingsWindow` abierta sobre Acerca de, un `AppSettings` personalizado (bash, esquinas, tema propio, atajo, dock a la izquierda…) exportado a un archivo temporal; luego una instalación «limpia» (ajustes por defecto) que importa ese archivo llamando a los mismos métodos que el botón (sin los diálogos modales de archivo: separar el flujo en un método que reciba la ruta y el plan, como pide el paso 4, y llamarlo desde la sonda; el diálogo de la vista previa se captura abriéndolo sin esperar el resultado, con `Show` no modal en la sonda o leyendo su árbol visual). Capturas: Acerca de con los dos botones, el diálogo de exportar, el de importar con cambios, el de importar sin cambios, el de error; la app antes y después de aplicar (nota, dock, Ajustes en Aspecto). Comprobaciones automáticas: tras aplicar, `_settings` igual al personalizado en los campos de la lista y **intactos** los de sincronización y pantalla; existe la copia `settings.json.antes-de-importar-*` con el contenido anterior; cancelar deja `settings.json` sin cambios (hash igual); todos los ids de `ConfigFields.All` tienen nombre traducido distinto del id en los cinco idiomas.

- [ ] **Step 2: Smoke test** — avisar al usuario (mueve el ratón) y, con su "ok": `dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug`. Expected: verde.

- [ ] **Step 3: STATUS.md** — sección "## 2026-10-0X: bloque 4 de aspectos (exportar e importar)": qué se exporta y qué nunca (con la tabla de campos por sección), el formato, la tolerancia al importar, la copia previa, qué se aplica en vivo y qué al reiniciar, y el número de tests.

- [ ] **Step 4: Commit**

```bash
git add docs/STATUS.md
git commit -m "Docs: estado tras el bloque 4 de aspectos"
```

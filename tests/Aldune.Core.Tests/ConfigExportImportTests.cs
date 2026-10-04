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
        s.SyncTransport = SyncTransportKind.Server;
        s.SyncAutomatically = true;
        s.SyncIntervalMinutes = 5;
        s.LastSyncAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        s.WrappedPendingSyncKey = [4, 4, 4];
        s.SyncKeyRotationPending = true;
        s.SyncScope = SyncScopeKind.Tag;
        s.SyncTag = "etiqueta-sync";
        s.SyncNoteIds = [Guid.Parse("11111111-2222-3333-4444-555555555555")];
        s.DefaultNoteLayout = NoteLayoutTemplate.Grid;
        s.NotesManagerOrder = NoteListOrder.Title;
        s.SereneRecolored = true;
        s.ActiveSyncProfileId = "perfil-activo";
        s.SyncProfiles = [new SyncProfileSettings { Id = "perfil-activo", Name = "perfil-secreto", SyncFolderPath = @"Y:\otro\secreto" }];
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
                     "Wrapped", "perfil-", "etiqueta-sync", "11111111-2222", "SyncProfile", "LastSync", "SyncFolder", "SyncServer", "SyncWebDav", "SyncDevice", "SyncKey", "TargetMonitor", "DockFollowsMouse", "token", "password", "AQIDBA", "BQYH",
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
        Assert.Equal("perfil-activo", target.ActiveSyncProfileId);
        Assert.Equal("perfil-secreto", target.SyncProfiles.Single().Name);
        Assert.Equal("etiqueta-sync", target.SyncTag);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), target.LastSyncAt);
    }

    [Fact]
    public void Import_AFileThatCarriesSyncAndScreenKeys_IgnoresThem()
    {
        // Un archivo hecho a mano (o malicioso) con campos que esta vía no admite: ni se leen ni se aplican.
        var target = WithSecrets();
        var json = """
        {
          "format": "aldune-config", "version": 1,
          "appearance": { "syncEnabled": true, "wrappedSyncKey": "AAAA" },
          "settings": {
            "language": "fr", "syncEnabled": false, "syncFolderPath": "C:\\robado", "syncServerUrl": "https://malo.invalid",
            "wrappedSyncServerToken": "AAAA", "targetMonitorId": "OTRO", "targetMonitorIndex": 0, "dockFollowsMouse": false,
            "dockTagFilter": "otra", "syncDeviceId": "otro", "lastSyncAt": "2020-01-01T00:00:00Z"
          }
        }
        """;

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        Assert.Equal(["language"], plan.Changes.Select(c => c.FieldId).ToArray());
        Assert.True(target.SyncEnabled);
        Assert.Equal(@"Z:\secreto\sync", target.SyncFolderPath);
        Assert.Equal("https://sync.ejemplo.invalid", target.SyncServerUrl);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, target.WrappedSyncServerToken);
        Assert.Equal("MONITOR-ID-SECRETO", target.TargetMonitorId);
        Assert.Equal(2, target.TargetMonitorIndex);
        Assert.True(target.DockFollowsMouse);
        Assert.Equal("etiqueta-local", target.DockTagFilter);
        Assert.Equal("dispositivo-123", target.SyncDeviceId);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), target.LastSyncAt);
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
    public void Plan_DoesNotTouchTheSettingsUntilApplied()
    {
        var target = new AppSettings();
        var json = ConfigExport.Build(Customized(), ConfigSections.All, "1.5.0");

        var plan = ConfigImport.Plan(json, target);

        Assert.NotEmpty(plan.Changes);
        Assert.Equal(AppearanceMode.Dark, target.Appearance);
        Assert.Null(target.Language);
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
    public void Plan_ASectionThatIsNotAnObject_IsIgnored()
    {
        var target = Customized();
        var json = "{\"format\":\"aldune-config\",\"version\":1,\"settings\":[1,2],\"appearance\":\"x\"}";

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        Assert.True(plan.IsValid, plan.Error);
        Assert.Equal(ConfigSections.None, plan.SectionsInFile);
        Assert.Empty(plan.Changes);
        Assert.Equal("de", target.Language);
        Assert.Equal(AppearanceMode.Bash, target.Appearance);
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

    [Fact]
    public void Import_NeverTouchesLocalStateThatIsNotInTheFieldList()
    {
        // DefaultNoteLayout, NotesManagerOrder y SereneRecolored están rellenos en WithSecrets: si salieran
        // al exportar, al importar en una instalación limpia dejarían de tener su valor por defecto.
        var json = ConfigExport.Build(WithSecrets(), ConfigSections.All, "1.5.0");
        var fresh = new AppSettings();

        ConfigImport.Plan(json, fresh).ApplyTo(fresh);

        var defaults = new AppSettings();
        Assert.Equal(defaults.DefaultNoteLayout, fresh.DefaultNoteLayout);
        Assert.Equal(defaults.NotesManagerOrder, fresh.NotesManagerOrder);
        Assert.Equal(defaults.SereneRecolored, fresh.SereneRecolored);
        // Y los tres de verdad llevaban un valor distinto del de fábrica.
        Assert.NotEqual(defaults.DefaultNoteLayout, WithSecrets().DefaultNoteLayout);
        Assert.NotEqual(defaults.NotesManagerOrder, WithSecrets().NotesManagerOrder);
        Assert.NotEqual(defaults.SereneRecolored, WithSecrets().SereneRecolored);
    }

    [Fact]
    public void Fields_AreExactlyTheDocumentedList_InOrder()
    {
        // Golden: cambiar, quitar o añadir un campo obliga a tocar esta lista a conciencia.
        string[] expected =
        [
            "appearance", "aspectColors", "squareCorners", "syncSignal", "uniformNoteColor", "noteTheme",
            "customThemes", "newNoteTone", "colorAssignment", "fixedNoteColor",
            "language", "simplifiedMode", "hotkeyEnabled", "hotkeyModifiers", "hotkeyKey",
            "recentHotkeyEnabled", "recentHotkeyModifiers", "recentHotkeyKey", "dockEdge", "dockView",
            "keepDockOpen", "showNotePreview", "hideOnFullscreen", "trackpadGestures", "moveCompletedTasksToEnd",
            "autoHideCompletedTasks", "autoHideDelayValue", "autoHideDelayUnit", "trashRetentionDays",
            "rememberNotePositions", "checkForUpdates", "reminderIncludesTasks",
        ];

        Assert.Equal(32, expected.Length);
        Assert.Equal(expected, ConfigFields.All.Select(f => f.Id).ToArray());
    }

    // Propiedades de AppSettings que NUNCA viajan por el archivo de configuración, cada una con su razón.
    private static readonly Dictionary<string, string> NotExported = new()
    {
        ["WrappedDatabaseKey"] = "clave de la base de datos, protegida por DPAPI en este equipo",
        ["SyncEnabled"] = "sincronización: es de este equipo y de su perfil",
        ["SyncTransport"] = "sincronización: transporte del perfil",
        ["SyncFolderPath"] = "sincronización: ruta local del equipo",
        ["SyncServerUrl"] = "sincronización: dirección del servidor",
        ["WrappedSyncServerToken"] = "credencial del servidor",
        ["SyncWebDavUsername"] = "credencial WebDAV",
        ["WrappedSyncWebDavPassword"] = "credencial WebDAV",
        ["SyncAutomatically"] = "sincronización: del perfil",
        ["SyncIntervalMinutes"] = "sincronización: del perfil",
        ["LastSyncAt"] = "estado de sincronización de este equipo",
        ["SyncDeviceId"] = "identidad de esta instalación",
        ["WrappedSyncKey"] = "clave de cifrado de la sincronización",
        ["WrappedPendingSyncKey"] = "clave de cifrado pendiente de rotación",
        ["SyncKeyRotationPending"] = "estado de una rotación de clave en curso",
        ["SyncScope"] = "sincronización: del perfil",
        ["SyncNoteIds"] = "identificadores de notas de este equipo",
        ["SyncTag"] = "sincronización: del perfil",
        ["SyncProfiles"] = "perfiles de sincronización con sus credenciales",
        ["ActiveSyncProfileId"] = "perfil de sincronización activo en este equipo",
        ["TargetMonitorIndex"] = "pantalla elegida: depende del hardware de este equipo",
        ["TargetMonitorId"] = "pantalla elegida: depende del hardware de este equipo",
        ["DockFollowsMouse"] = "pantalla elegida: respaldo para los monitores de este equipo",
        ["DockTagFilter"] = "estado de la vista del dock, ligado a etiquetas locales",
        ["DefaultNoteLayout"] = "distribución de 'abrir todas': preferencia fuera de la lista documentada",
        ["NotesManagerOrder"] = "último orden elegido en Gestionar notas: estado de uso, no ajuste",
        ["SereneRecolored"] = "marca interna de una migración de datos de este equipo",
    };

    [Fact]
    public void EveryPublicSettableAppSettingsProperty_IsMappedByExactlyOneFieldOrExplicitlyExcluded()
    {
        var properties = typeof(AppSettings).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        foreach (var name in properties)
        {
            var mapped = ConfigFields.All.Count(f => f.Property == name);
            var excluded = NotExported.ContainsKey(name);
            Assert.True(mapped + (excluded ? 1 : 0) == 1,
                $"AppSettings.{name}: hay que mapearla con un ConfigField o añadirla, con su razón, a NotExported (mapeada {mapped} veces, excluida: {excluded}).");
        }

        // Y al revés: nada apunta a una propiedad que no existe, y cada campo tiene la suya.
        Assert.All(ConfigFields.All, f => Assert.Contains(f.Property, properties));
        Assert.All(NotExported.Keys, key => Assert.Contains(key, properties));
        Assert.Equal(ConfigFields.All.Count, ConfigFields.All.Select(f => f.Property).Distinct().Count());
    }

    [Theory]
    [InlineData("{\"format\":\"aldune-config\",\"settings\":{\"language\":\"\\ud800\"}}")] // sustituto suelto en un escape
    [InlineData("{\"format\":5}")]
    public void Plan_HostileJson_IsAnErrorAndNeverThrows(string json)
    {
        var plan = ConfigImport.Plan(json, new AppSettings());

        Assert.False(plan.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(plan.Error));
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void Plan_AFileWithAUtf8Bom_IsRead()
    {
        var json = "\uFEFF" + ConfigExport.Build(new AppSettings { Language = "fr" }, ConfigSections.Settings, "1.5.0");
        var target = new AppSettings();

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        Assert.True(plan.IsValid, plan.Error);
        Assert.Equal("fr", target.Language);
    }

    [Fact]
    public void Plan_ThemesWithTheSameNameAndCountButDifferentColors_AreAChange()
    {
        var target = new AppSettings { CustomThemes = [new NoteTheme { Id = "mio", Name = "Mio", DarkColors = ["#112233"] }] };
        var other = new AppSettings { CustomThemes = [new NoteTheme { Id = "mio", Name = "Mio", DarkColors = ["#445566"] }] };
        var json = ConfigExport.Build(other, ConfigSections.Appearance, "1.5.0");

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        var change = Assert.Single(plan.Changes);
        Assert.Equal("customThemes", change.FieldId);
        Assert.NotEqual(change.OldValue, change.NewValue);
        Assert.Equal("#445566", target.CustomThemes.Single().DarkColors.Single());
    }

    [Fact]
    public void Plan_ThemesWithTheSameColorsButADifferentId_AreAChange()
    {
        var target = new AppSettings { CustomThemes = [new NoteTheme { Id = "uno", Name = "Mio", LightColors = ["#112233"] }] };
        var other = new AppSettings { CustomThemes = [new NoteTheme { Id = "dos", Name = "Mio", LightColors = ["#112233"] }] };

        var plan = ConfigImport.Plan(ConfigExport.Build(other, ConfigSections.Appearance, "1.5.0"), target);

        Assert.Single(plan.Changes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(3650)]
    public void Plan_TrashRetentionDaysOutsideTheSettingsRange_FallsToTheDefault(int days)
    {
        var json = "{\"format\":\"aldune-config\",\"settings\":{\"trashRetentionDays\":" + days + "}}";
        var target = new AppSettings { TrashRetentionDays = 45 };

        ConfigImport.Plan(json, target).ApplyTo(target);

        Assert.Equal(NotesRepository.DefaultTrashRetentionDays, target.TrashRetentionDays);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void Plan_TrashRetentionDays_AcceptsTheWholeSettingsRange(int days)
    {
        var json = "{\"format\":\"aldune-config\",\"settings\":{\"trashRetentionDays\":" + days + "}}";
        var target = new AppSettings();

        ConfigImport.Plan(json, target).ApplyTo(target);

        Assert.Equal(days, target.TrashRetentionDays);
    }

    [Fact]
    public void Plan_AspectColorsWithEntriesButNoneValid_KeepTheUsersColors()
    {
        var target = new AppSettings { AspectColors = new() { ["bash"] = new() { ["accent"] = "#458588" } } };
        var json = "{\"format\":\"aldune-config\",\"appearance\":{\"aspectColors\":{\"bash\":{\"accent\":\"rojo\"},\"noExiste\":{\"accent\":\"#FFFFFF\"}}}}";

        var plan = ConfigImport.Plan(json, target);
        plan.ApplyTo(target);

        Assert.Empty(plan.Changes);
        Assert.Equal("#458588", target.ColorsFor(AppearanceMode.Bash)!["accent"]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public void Plan_AnEmptyOrNullAspectColors_StillClearsThem(string value)
    {
        var target = new AppSettings { AspectColors = new() { ["bash"] = new() { ["accent"] = "#458588" } } };
        var json = "{\"format\":\"aldune-config\",\"appearance\":{\"aspectColors\":" + value + "}}";

        ConfigImport.Plan(json, target).ApplyTo(target);

        Assert.Null(target.AspectColors);
    }

    [Fact]
    public void Plan_ALowercaseStoredColor_IsNotASpuriousChange()
    {
        var target = new AppSettings { UniformNoteColor = "#33363a" };
        var json = ConfigExport.Build(new AppSettings { UniformNoteColor = "#33363A" }, ConfigSections.Appearance, "1.5.0");

        var plan = ConfigImport.Plan(json, target);

        Assert.DoesNotContain(plan.Changes, c => c.FieldId == "uniformNoteColor");
    }

    [Fact]
    public void ApplyTo_TwoTargets_DoNotShareTheirCollectionsWithThePlanOrEachOther()
    {
        var json = ConfigExport.Build(Customized(), ConfigSections.Appearance, "1.5.0");
        var a = new AppSettings();
        var b = new AppSettings();
        var plan = ConfigImport.Plan(json, a);

        plan.ApplyTo(a);
        plan.ApplyTo(b);
        a.AspectColors!["bash"]["accent"] = "#000000";
        a.AspectColors["otro"] = new() { ["x"] = "#111111" };
        a.CustomThemes[0].LightColors.Add("#222222");
        a.CustomThemes.Add(new NoteTheme { Id = "extra" });

        Assert.Equal("#458588", b.AspectColors!["bash"]["accent"]);
        Assert.False(b.AspectColors.ContainsKey("otro"));
        Assert.Single(b.CustomThemes);
        Assert.Equal(2, b.CustomThemes.Single().LightColors.Count);
    }
}

using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"aldune-settings-test-{Guid.NewGuid()}");
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsFreshSettings()
    {
        var sut = new SettingsService(_settingsPath);
        var settings = sut.Load();
        Assert.Null(settings.WrappedDatabaseKey);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        var key = new byte[] { 1, 2, 3, 4, 5 };
        sut.Save(new AppSettings { WrappedDatabaseKey = key });

        var loaded = sut.Load();
        Assert.Equal(key, loaded.WrappedDatabaseKey);
    }

    [Fact]
    public void Load_OldFileWithoutMoveCompletedTasksToEnd_KeepsTasksWhereTheyAre()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{\"AutoHideCompletedTasks\":true}");

        var loaded = new SettingsService(_settingsPath).Load();

        Assert.False(loaded.MoveCompletedTasksToEnd);
        Assert.True(loaded.AutoHideCompletedTasks);
    }

    [Fact]
    public void SaveThenLoad_MoveCompletedTasksToEnd_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings { MoveCompletedTasksToEnd = true });

        Assert.True(sut.Load().MoveCompletedTasksToEnd);
    }

    [Fact]
    public void SaveThenLoad_MonitorSettings_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings { TargetMonitorIndex = 1, HideOnFullscreen = false });

        var loaded = sut.Load();
        Assert.Equal(1, loaded.TargetMonitorIndex);
        Assert.False(loaded.HideOnFullscreen);
    }

    [Fact]
    public void SaveThenLoad_AutoHideCompletedTasksSettings_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings
        {
            AutoHideCompletedTasks = true,
            AutoHideCompletedTasksDelayValue = 3,
            AutoHideCompletedTasksDelayUnit = TaskDelayUnit.Weeks
        });

        var loaded = sut.Load();
        Assert.True(loaded.AutoHideCompletedTasks);
        Assert.Equal(3, loaded.AutoHideCompletedTasksDelayValue);
        Assert.Equal(TaskDelayUnit.Weeks, loaded.AutoHideCompletedTasksDelayUnit);
        Assert.Equal(TimeSpan.FromDays(21), loaded.AutoHideCompletedTasksDelay);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_DefaultsAutoHideCompletedTasksToOffWithOneDay()
    {
        var sut = new SettingsService(_settingsPath);
        var settings = sut.Load();

        Assert.False(settings.AutoHideCompletedTasks);
        Assert.Equal(1, settings.AutoHideCompletedTasksDelayValue);
        Assert.Equal(TaskDelayUnit.Days, settings.AutoHideCompletedTasksDelayUnit);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_DefaultsTrashRetentionToThirtyDays()
    {
        var sut = new SettingsService(_settingsPath);
        Assert.Equal(NotesRepository.DefaultTrashRetentionDays, sut.Load().TrashRetentionDays);
    }

    [Fact]
    public void SaveThenLoad_TrashRetentionDays_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings { TrashRetentionDays = 7 });

        Assert.Equal(7, sut.Load().TrashRetentionDays);
    }

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

    [Fact]
    public void Load_EnumsWrittenAsText_AreReadInsteadOfFailingToStart()
    {
        // Un settings.json editado a mano con el nombre del valor en vez del número no puede impedir
        // que la app arranque: antes lanzaba JsonException y salía la pantalla de "no se puede iniciar".
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{\"NewNoteTone\":\"Dark\",\"DockEdge\":\"Left\"}");

        var settings = new SettingsService(_settingsPath).Load();

        Assert.Equal(NoteTone.Dark, settings.NewNoteTone);
        Assert.Equal(EdgePosition.Left, settings.DockEdge);
    }

    [Fact]
    public void Save_KeepsWritingEnumsAsNumbers()
    {
        // Una versión anterior de la app solo sabe leer números: si esta escribiera texto, volver a
        // ella dejaría un settings.json que no arranca.
        new SettingsService(_settingsPath).Save(new AppSettings { NewNoteTone = NoteTone.Dark });

        Assert.Contains("\"NewNoteTone\":1", File.ReadAllText(_settingsPath));
    }
}

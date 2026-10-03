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

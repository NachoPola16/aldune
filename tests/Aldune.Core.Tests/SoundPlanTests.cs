using Xunit;

namespace Aldune.Core.Tests;

public class SoundPlanTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-sound-{Guid.NewGuid():N}");

    public SoundPlanTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(string name, int bytes = 100)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public void Sounds_AreOffByDefault()
    {
        var settings = new AppSettings();
        foreach (var soundEvent in SoundPlan.All)
            Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, soundEvent).Kind);
    }

    [Fact]
    public void TurningThemOn_GivesOnlyTheAlertsASoundByDefault()
    {
        var settings = new AppSettings { SoundsEnabled = true };

        Assert.Equal(new SoundAction(SoundActionKind.System, SoundChoice.Exclamation), SoundPlan.Resolve(settings, SoundEvent.Reminder));
        Assert.Equal(new SoundAction(SoundActionKind.System, SoundChoice.Hand), SoundPlan.Resolve(settings, SoundEvent.SyncFailed));
        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.SyncDone).Kind);
        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.NoteCreated).Kind);
        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.NoteArchived).Kind);
    }

    [Fact]
    public void AChoicePerEvent_OverridesTheDefault()
    {
        var settings = new AppSettings { SoundsEnabled = true };
        settings.Sounds[SoundPlan.Id(SoundEvent.Reminder)] = new SoundSetting { Choice = SoundChoice.None };
        settings.Sounds[SoundPlan.Id(SoundEvent.NoteCreated)] = new SoundSetting { Choice = SoundChoice.Beep };

        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.Reminder).Kind);
        Assert.Equal(new SoundAction(SoundActionKind.System, SoundChoice.Beep), SoundPlan.Resolve(settings, SoundEvent.NoteCreated));
    }

    [Fact]
    public void WithTheSwitchOff_NoChoiceSounds()
    {
        var settings = new AppSettings { SoundsEnabled = false };
        settings.Sounds[SoundPlan.Id(SoundEvent.Reminder)] = new SoundSetting { Choice = SoundChoice.Beep };

        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.Reminder).Kind);
    }

    [Fact]
    public void ACustomWavFile_IsUsed()
    {
        var path = WriteFile("aviso.wav");
        var settings = new AppSettings { SoundsEnabled = true };
        settings.Sounds[SoundPlan.Id(SoundEvent.SyncDone)] = new SoundSetting { Choice = SoundChoice.File, FilePath = path };

        Assert.Equal(new SoundAction(SoundActionKind.File, Path: path), SoundPlan.Resolve(settings, SoundEvent.SyncDone));
    }

    [Theory]
    [InlineData("falta.wav")]       // no existe
    [InlineData("musica.mp3")]      // no es .wav
    public void ACustomFileThatCannotBePlayed_IsSilenceNeverAnError(string name)
    {
        var path = name == "falta.wav" ? Path.Combine(_dir, name) : WriteFile(name);
        var settings = new AppSettings { SoundsEnabled = true };
        settings.Sounds[SoundPlan.Id(SoundEvent.SyncDone)] = new SoundSetting { Choice = SoundChoice.File, FilePath = path };

        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.SyncDone).Kind);
    }

    [Fact]
    public void AHugeWav_IsSilence()
    {
        var path = WriteFile("enorme.wav", SoundPlan.MaxFileBytes + 1);
        Assert.False(SoundPlan.IsUsableFile(path));
    }

    [Fact]
    public void ACustomChoiceWithoutAPath_IsSilence()
    {
        var settings = new AppSettings { SoundsEnabled = true };
        settings.Sounds[SoundPlan.Id(SoundEvent.SyncDone)] = new SoundSetting { Choice = SoundChoice.File, FilePath = null };

        Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, SoundEvent.SyncDone).Kind);
    }

    [Fact]
    public void EventIds_AreStableAndUnique()
    {
        var ids = SoundPlan.All.Select(SoundPlan.Id).ToList();
        Assert.Equal(new[] { "reminder", "syncDone", "syncFailed", "noteCreated", "noteArchived" }, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}

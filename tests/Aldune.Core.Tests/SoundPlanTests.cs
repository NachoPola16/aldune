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
        Assert.Equal(new[] { "reminder", "syncDone", "syncFailed", "noteCreated", "noteArchived",
            "noteOpened", "noteClosed", "taskChecked", "noteDeleted", "typing" }, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void NewEvents_StartSilentEvenWithTheSwitchOn()
    {
        var settings = new AppSettings { SoundsEnabled = true };
        foreach (var e in new[] { SoundEvent.NoteOpened, SoundEvent.NoteClosed, SoundEvent.TaskChecked, SoundEvent.NoteDeleted, SoundEvent.Typing })
            Assert.Equal(SoundActionKind.None, SoundPlan.Resolve(settings, e).Kind);
    }

    [Fact]
    public void ExistingChoices_KeepTheirNumbers_AndNewOnesGoAtTheEnd()
    {
        Assert.Equal(6, (int)SoundChoice.File);
        Assert.Equal(7, (int)SoundChoice.PageTurn);
        Assert.Equal(8, (int)SoundChoice.PaperFold);
        Assert.Equal(9, (int)SoundChoice.PenTap);
        Assert.Equal(10, (int)SoundChoice.PaperCrumple);
        Assert.Equal(11, (int)SoundChoice.PenScratch);
    }

    [Fact]
    public void AppOwnSound_ResolvesToBundled()
    {
        var settings = new AppSettings { SoundsEnabled = true };
        settings.Sounds[SoundPlan.Id(SoundEvent.NoteOpened)] = new SoundSetting { Choice = SoundChoice.PageTurn };

        Assert.Equal(new SoundAction(SoundActionKind.Bundled, SoundChoice.PageTurn), SoundPlan.Resolve(settings, SoundEvent.NoteOpened));
    }

    [Fact]
    public void PaperAndPenForAll_AssignsAnOwnSoundToEveryNewEvent_AndLeavesTheOthers()
    {
        var settings = new AppSettings { SoundsEnabled = true };
        settings.Sounds[SoundPlan.Id(SoundEvent.SyncDone)] = new SoundSetting { Choice = SoundChoice.Beep };

        SoundPlan.ApplyPaperAndPen(settings);

        Assert.Equal(SoundChoice.PageTurn, SoundPlan.SettingFor(settings, SoundEvent.NoteOpened).Choice);
        Assert.Equal(SoundChoice.PaperFold, SoundPlan.SettingFor(settings, SoundEvent.NoteClosed).Choice);
        Assert.Equal(SoundChoice.PenTap, SoundPlan.SettingFor(settings, SoundEvent.TaskChecked).Choice);
        Assert.Equal(SoundChoice.PaperCrumple, SoundPlan.SettingFor(settings, SoundEvent.NoteDeleted).Choice);
        Assert.Equal(SoundChoice.PenScratch, SoundPlan.SettingFor(settings, SoundEvent.Typing).Choice);
        Assert.Equal(SoundChoice.Beep, SoundPlan.SettingFor(settings, SoundEvent.SyncDone).Choice);
        Assert.Equal(SoundChoice.Exclamation, SoundPlan.SettingFor(settings, SoundEvent.Reminder).Choice);
    }

    [Fact]
    public void Volume_DefaultsToFull_AndIsClamped()
    {
        Assert.Equal(1.0, SoundPlan.VolumeFactor(new AppSettings()));
        Assert.Equal(0.5, SoundPlan.VolumeFactor(new AppSettings { SoundVolume = 50 }));
        Assert.Equal(0.0, SoundPlan.VolumeFactor(new AppSettings { SoundVolume = -9 }));
        Assert.Equal(1.0, SoundPlan.VolumeFactor(new AppSettings { SoundVolume = 900 }));
    }

    [Fact]
    public void TypingThrottle_LetsThroughOneEvery70Ms()
    {
        var t = new TypingThrottle();
        Assert.True(t.TryAcquire(1000));
        Assert.False(t.TryAcquire(1069));
        Assert.True(t.TryAcquire(1070));
        Assert.False(t.TryAcquire(1100));
    }

    [Theory]
    [InlineData('a', true)]
    [InlineData(' ', true)]
    [InlineData('ñ', true)]
    [InlineData('\b', false)]
    [InlineData('\r', false)]
    [InlineData('\t', false)]
    [InlineData('\u001b', false)]
    [InlineData('\u007f', false)]
    public void TypingSounds_OnlyForPrintableCharacters(char c, bool expected) =>
        Assert.Equal(expected, TypingThrottle.IsPrintable(c));

    [Fact]
    public void ScalingWav_HalvesPcm16Samples_AndKeepsTheHeader()
    {
        var wav = new byte[44 + 4];
        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        BitConverter.GetBytes((short)1000).CopyTo(wav, 44);
        BitConverter.GetBytes((short)-2000).CopyTo(wav, 46);

        var scaled = SoundWav.Scale(wav, 0.5);

        Assert.Equal(wav.Length, scaled.Length);
        Assert.Equal(wav[..44], scaled[..44]);
        Assert.Equal(500, BitConverter.ToInt16(scaled, 44));
        Assert.Equal(-1000, BitConverter.ToInt16(scaled, 46));
        Assert.Equal(1000, BitConverter.ToInt16(wav, 44));   // no toca el original
    }

    [Fact]
    public void ScalingWav_AtFullVolume_ReturnsTheSameBytes()
    {
        var wav = new byte[48];
        Assert.Same(wav, SoundWav.Scale(wav, 1.0));
    }
}

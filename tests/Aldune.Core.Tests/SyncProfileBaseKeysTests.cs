namespace Aldune.Core.Tests;

/// <summary>Qué perfiles cuentan para la señal de sync de una nota: solo los activados cuyo alcance la incluye.</summary>
public class SyncProfileBaseKeysTests
{
    private static Note NoteWithTags(params string[] tags) => new()
    {
        Id = Guid.NewGuid(), Text = "x", Color = "#EBD38B", State = NoteState.Active, ScreenOrigin = "primary",
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Tags = tags.ToList(),
    };

    private static AppSettings TwoProfiles(out SyncProfileSettings second)
    {
        var settings = new AppSettings { SyncEnabled = true };
        SyncProfileStore.Ensure(settings);
        second = SyncProfileStore.Create(settings, "Trabajo");
        second.SyncEnabled = true;
        // Se deja elegido el primero: el segundo se lee de la lista.
        settings.ActiveSyncProfileId = settings.SyncProfiles[0].Id;
        SyncProfileStore.LoadActiveToLegacy(settings);
        return settings;
    }

    [Fact]
    public void Keys_IncludeEveryEnabledProfileThatCoversTheNote()
    {
        var settings = TwoProfiles(out var second);

        var keys = SyncProfileStore.BaseKeysCovering(settings, NoteWithTags(), null);

        Assert.Equal(new[] { "", second.BaseKey.Length > 0 ? second.BaseKey : second.Id }.OrderBy(k => k), keys.OrderBy(k => k));
    }

    [Fact]
    public void LegacySettingsWithoutAProfileList_FallBackToTheSingleEmptyKey()
    {
        var settings = new AppSettings { SyncEnabled = true, SyncProfiles = [] };

        Assert.Equal(new[] { "" }, SyncProfileStore.BaseKeysCovering(settings, NoteWithTags(), null));
        Assert.Empty(SyncProfileStore.BaseKeysCovering(new AppSettings { SyncEnabled = false, SyncProfiles = [] }, NoteWithTags(), null));
    }

    [Fact]
    public void ADisabledProfile_DoesNotCount()
    {
        var settings = TwoProfiles(out var second);
        second.SyncEnabled = false;

        Assert.Equal(new[] { "" }, SyncProfileStore.BaseKeysCovering(settings, NoteWithTags(), null));
    }

    [Fact]
    public void AProfileWhoseTagScopeLeftTheNoteOut_DoesNotCount()
    {
        var settings = TwoProfiles(out var second);
        second.SyncScope = SyncScopeKind.Tag;
        second.SyncTag = "trabajo";

        Assert.Equal(new[] { "" }, SyncProfileStore.BaseKeysCovering(settings, NoteWithTags("casa"), null));
        Assert.Equal(2, SyncProfileStore.BaseKeysCovering(settings, NoteWithTags("trabajo"), null).Count);
    }
}

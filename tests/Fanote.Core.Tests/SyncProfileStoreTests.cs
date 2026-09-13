using Fanote.Core;

namespace Fanote.Core.Tests;

public sealed class SyncProfileStoreTests
{
    [Fact]
    public void LegacySettingsAreMigratedWithoutLosingTheActiveConnection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fanote-settings-{Guid.NewGuid():N}.json");
        try
        {
            var original = new AppSettings
            {
                SyncEnabled = true,
                SyncTransport = SyncTransportKind.Server,
                SyncServerUrl = "http://server:8087",
                SyncFolderPath = "ignored",
                SyncScope = SyncScopeKind.SelectedNotes,
                SyncNoteIds = new List<Guid> { Guid.NewGuid() },
            };

            var service = new SettingsService(path);
            service.Save(original);
            var loaded = service.Load();

            var profile = Assert.Single(loaded.SyncProfiles);
            Assert.Equal("Mis dispositivos", profile.Name);
            Assert.Equal(profile.Id, loaded.ActiveSyncProfileId);
            Assert.True(loaded.SyncEnabled);
            Assert.Equal(SyncTransportKind.Server, loaded.SyncTransport);
            Assert.Equal("http://server:8087", loaded.SyncServerUrl);
            Assert.Equal(SyncScopeKind.SelectedNotes, loaded.SyncScope);
            Assert.Single(loaded.SyncNoteIds);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void ProfilesKeepIndependentTransportAndSelection()
    {
        var settings = new AppSettings { SyncFolderPath = "\\\\nas\\fanote" };
        SyncProfileStore.Ensure(settings);
        var devicesId = settings.ActiveSyncProfileId;

        SyncProfileStore.Create(settings, "Compartir con Ana");
        settings.SyncTransport = SyncTransportKind.Server;
        settings.SyncServerUrl = "https://share.example.test";
        settings.SyncScope = SyncScopeKind.SelectedNotes;
        settings.SyncNoteIds = new List<Guid> { Guid.NewGuid() };
        SyncProfileStore.SaveActiveFromLegacy(settings);

        settings.ActiveSyncProfileId = devicesId;
        SyncProfileStore.LoadActiveToLegacy(settings);
        Assert.Equal("\\\\nas\\fanote", settings.SyncFolderPath);
        Assert.Equal(SyncTransportKind.Folder, settings.SyncTransport);
        Assert.Equal(SyncScopeKind.AllNotes, settings.SyncScope);

        settings.ActiveSyncProfileId = settings.SyncProfiles.Single(profile => profile.Name == "Compartir con Ana").Id;
        SyncProfileStore.LoadActiveToLegacy(settings);
        Assert.Equal(SyncTransportKind.Server, settings.SyncTransport);
        Assert.Equal("https://share.example.test", settings.SyncServerUrl);
        Assert.Single(settings.SyncNoteIds);
    }
}

using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"fanote-settings-test-{Guid.NewGuid()}");
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
    public void SaveThenLoad_MonitorSettings_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings { TargetMonitorIndex = 1, HideOnFullscreen = false });

        var loaded = sut.Load();
        Assert.Equal(1, loaded.TargetMonitorIndex);
        Assert.False(loaded.HideOnFullscreen);
    }
}

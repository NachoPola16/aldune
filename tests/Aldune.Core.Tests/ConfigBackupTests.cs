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
    public void Create_TimestampIsCultureIndependent()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "x");
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // Calendario y dígitos distintos de los occidentales: el nombre no puede depender de ellos.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            var copy = ConfigBackup.Create(path, new DateTimeOffset(2026, 10, 4, 9, 30, 15, TimeSpan.Zero));

            Assert.Equal(Path.Combine(_dir, "settings.json.antes-de-importar-20261004-093015"), copy);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
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

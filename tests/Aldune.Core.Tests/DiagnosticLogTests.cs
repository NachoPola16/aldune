using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DiagnosticLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AlduneDiagTests", Guid.NewGuid().ToString("N"));
    private string LogPath => Path.Combine(_dir, "logs", "dock.log");
    private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(2));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Write_CreatesTheFolder_AndAppendsATimestampedLine()
    {
        var log = new DiagnosticLog(LogPath, clock: () => Noon);

        log.Write("dock", "oculto por pantalla completa");
        log.Write("pantallas", "2 pantallas");

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("2026-09-25 12:00:00.000 [dock] oculto por pantalla completa", lines[0]);
        Assert.Equal("2026-09-25 12:00:00.000 [pantallas] 2 pantallas", lines[1]);
    }

    [Fact]
    public void Write_RotatesToASingleOldFile_WhenTheLogIsFull()
    {
        var log = new DiagnosticLog(LogPath, maxBytes: 200, clock: () => Noon);

        for (int i = 0; i < 20; i++) log.Write("dock", $"linea {i}");

        Assert.True(new FileInfo(LogPath).Length <= 200 + 100, "el actual no crece sin límite");
        Assert.True(File.Exists(LogPath + ".old"));
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(LogPath)!).Length);
        // Lo último escrito siempre está en el actual.
        Assert.EndsWith("linea 19", File.ReadAllLines(LogPath)[^1]);
    }

    [Fact]
    public void Write_NeverThrows_WhenTheFileCannotBeWritten()
    {
        Directory.CreateDirectory(_dir);
        // Una carpeta con el nombre del fichero: cualquier escritura falla.
        var blocked = Path.Combine(_dir, "bloqueado.log");
        Directory.CreateDirectory(blocked);
        var log = new DiagnosticLog(blocked);

        var error = Record.Exception(() => log.Write("dock", "no importa"));

        Assert.Null(error);
    }

    [Fact]
    public void Write_KeepsMessagesOnOneLine()
    {
        var log = new DiagnosticLog(LogPath, clock: () => Noon);

        log.Write("dock", "clase\r\ncon salto");

        Assert.Single(File.ReadAllLines(LogPath));
    }
}

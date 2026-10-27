namespace Aldune.SyncServer.Tests;

public sealed class DataDirectoryCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-datadir-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void AWritableDirectory_PassesAndLeavesNothingBehind()
    {
        Directory.CreateDirectory(_dir);

        DataDirectoryCheck.EnsureWritable(_dir);

        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public void ADirectoryTheServerCannotWriteTo_FailsAtStartupWithTheFixInTheMessage()
    {
        // Sin carpeta es la forma portable de «no se puede escribir» (el caso real es un volumen de root con el servidor como usuario no root).
        var ex = Assert.Throws<InvalidOperationException>(() => DataDirectoryCheck.EnsureWritable(Path.Combine(_dir, "no-existe")));

        Assert.Contains("chown -R 1654:1654", ex.Message);
    }
}

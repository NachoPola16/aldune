namespace Aldune.Core.Tests;

public class SyncResultTests
{
    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(3, 0, 0, false)]   // solo subir no cambia nada aquí
    [InlineData(0, 1, 0, true)]
    [InlineData(0, 0, 1, true)]
    public void AppliedChanges_IsTrueWhenSomethingArrivedFromTheStore(int uploaded, int downloaded, int tombstones, bool expected) =>
        Assert.Equal(expected, new SyncResult(uploaded, downloaded, 0, tombstones, "otro perfil falló").AppliedChanges);
}

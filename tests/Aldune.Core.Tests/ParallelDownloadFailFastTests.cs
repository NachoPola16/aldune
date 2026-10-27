namespace Aldune.Core.Tests;

public class ParallelDownloadFailFastTests
{
    [Fact]
    public void AFailure_StopsTheDownloadsStillQueued()
    {
        int started = 0;
        var items = Enumerable.Range(0, 200).ToList();

        var ex = Assert.Throws<InvalidOperationException>(() => ParallelDownload.Run<int, string>(items, async item =>
        {
            Interlocked.Increment(ref started);
            await Task.Delay(20);
            if (item == 0) throw new InvalidOperationException("401");
            return item.ToString();
        }));

        Assert.Equal("401", ex.Message);
        // Las que ya estaban en vuelo terminan; las 190 y pico en cola no deben ni empezar.
        Assert.True(started <= ParallelDownload.MaxConcurrent * 3, $"empezaron {started}");
    }
}

using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class OpenNoteLiveSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithNoNoteOpen_NothingRuns()
        => Assert.False(OpenNoteLiveSync.ShouldRun(0, true, null, Now, false));

    [Fact]
    public void WithANoteOpenAndSyncConfigured_RunsWhenTheLastPassIsOldEnough()
        => Assert.True(OpenNoteLiveSync.ShouldRun(1, true, Now - TimeSpan.FromSeconds(60), Now, false));

    [Fact]
    public void ARecentPassOrOneInProgressHoldsItBack()
    {
        Assert.False(OpenNoteLiveSync.ShouldRun(1, true, Now - TimeSpan.FromSeconds(10), Now, false));
        Assert.False(OpenNoteLiveSync.ShouldRun(1, true, null, Now, true));
    }

    [Fact]
    public void WithoutAutomaticSync_NothingRuns()
        => Assert.False(OpenNoteLiveSync.ShouldRun(2, false, null, Now, false));
}

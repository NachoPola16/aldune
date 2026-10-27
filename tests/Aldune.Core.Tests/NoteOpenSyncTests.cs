namespace Aldune.Core.Tests;

/// <summary>Al abrir una nota se trae antes lo escrito en otro dispositivo, pero sin lanzar una pasada por cada apertura.</summary>
public sealed class NoteOpenSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SyncsWhenNeverAttempted() => Assert.True(NoteOpenSync.ShouldRun(syncConfigured: true, lastAttempt: null, Now, running: false));

    [Fact]
    public void DoesNothingWithoutSyncConfigured() => Assert.False(NoteOpenSync.ShouldRun(syncConfigured: false, lastAttempt: null, Now, running: false));

    [Fact]
    public void DoesNotStartAnotherPassWhileOneIsRunning() => Assert.False(NoteOpenSync.ShouldRun(true, null, Now, running: true));

    [Fact]
    public void WaitsMoreThanThirtySecondsSinceTheLastAttempt()
    {
        Assert.False(NoteOpenSync.ShouldRun(true, Now.AddSeconds(-29), Now, false));
        Assert.False(NoteOpenSync.ShouldRun(true, Now.AddSeconds(-30), Now, false), "a los 30 s justos todavía no");
        Assert.True(NoteOpenSync.ShouldRun(true, Now.AddSeconds(-31), Now, false));
    }
}

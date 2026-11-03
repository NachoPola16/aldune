using Xunit;

namespace Aldune.Core.Tests;

public sealed class SyncDiagnosticsTests
{
    private static SyncDiagnosticsInfo Info(params SyncAttempt[] attempts) => new()
    {
        AppVersion = "4.0.0", Platform = "Windows 11", Transport = "Server", Scope = "AllNotes", ServerHost = "sync.example.org",
        ProfilesTotal = 2, ProfilesEnabled = 1, ActiveNotes = 3000, ArchivedNotes = 12, TrashedNotes = 4, OpenConflicts = 1,
        LastSyncAt = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero), Attempts = attempts,
    };

    private static SyncAttempt Attempt(string? error, int up = 0, int down = 0, double seconds = 1.5) => new(
        new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromSeconds(seconds), Automatic: false,
        new SyncResult(up, down, 0, 0, error));

    [Fact]
    public void Report_HasTheFactsASupportPersonNeeds_AndNoNoteContentOrKeys()
    {
        string report = SyncDiagnostics.Render(Info(Attempt(null, up: 5, down: 2)));

        Assert.Contains("Aldune 4.0.0", report);
        Assert.Contains("Windows 11", report);
        Assert.Contains("Server", report);
        Assert.Contains("sync.example.org", report);
        Assert.Contains("3000", report);
        Assert.Contains("up 5", report);
        Assert.Contains("down 2", report);
        Assert.Contains("OK", report);
    }

    [Fact]
    public void Errors_AreShownButUrlsAreReducedToTheirHost_AndLongSecretsAreHidden()
    {
        string secret = new('A', 43);
        string report = SyncDiagnostics.Render(Info(Attempt(
            $"The server answered 401 at https://user:pw@sync.example.org/v1/notes?token={secret}&x=1 for key {secret}")));

        Assert.Contains("401", report);
        Assert.Contains("sync.example.org", report);
        Assert.DoesNotContain(secret, report);
        Assert.DoesNotContain("user:pw", report);
        Assert.DoesNotContain("/v1/notes", report);
        Assert.DoesNotContain("token=", report);
    }

    [Fact]
    public void LocalPathsEmailsAndInternalAddresses_AreHidden()
    {
        string report = SyncDiagnostics.Render(Info(Attempt(
            @"Could not read C:\Users\nacho\Sync\notes.json, \\nas\share\x, mail me@example.com, socket 192.168.1.77:8443 refused")));

        Assert.DoesNotContain("nacho", report);
        Assert.DoesNotContain("nas\\share", report);
        Assert.DoesNotContain("me@example.com", report);
        Assert.DoesNotContain("192.168", report);
        Assert.Contains("refused", report);
    }

    [Fact]
    public void NoAttemptsYet_SaysSo()
    {
        Assert.Contains("no sync attempts", SyncDiagnostics.Render(Info()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Attempts_AreListedNewestFirst()
    {
        var older = Attempt("boom", seconds: 1);
        var newer = new SyncAttempt(older.At.AddMinutes(5), TimeSpan.FromSeconds(2), true, new SyncResult(1, 0, 0, 0));

        string report = SyncDiagnostics.Render(Info(older, newer));

        Assert.True(report.IndexOf("10:05:00 UTC", StringComparison.Ordinal) < report.LastIndexOf("10:00:00 UTC", StringComparison.Ordinal), report);
    }
}

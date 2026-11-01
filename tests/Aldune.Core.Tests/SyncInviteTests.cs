using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class SyncInviteTests
{
    [Fact]
    public void FormatThenParseRoundTrips()
    {
        var text = SyncInvite.Format("https://aldune.example.com/", "abc_DEF-123");

        Assert.True(SyncInvite.TryParse(text, out var url, out var code));
        Assert.Equal("https://aldune.example.com/", url);
        Assert.Equal("abc_DEF-123", code);
    }

    [Fact]
    public void ParseToleratesWhatAChatAdds()
    {
        var text = SyncInvite.Format("https://aldune.example.com/", "the-code");
        var wrapped = "  " + text[..20] + "\r\n " + text[20..] + "  ";

        Assert.True(SyncInvite.TryParse(wrapped, out _, out var code));
        Assert.Equal("the-code", code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("aldune-invite:")]
    [InlineData("aldune-invite:!!!not-base64!!!")]
    [InlineData("aldune-invite:e30")] // {} : no url, no code
    public void ParseRefusesGarbage(string? text)
    {
        Assert.False(SyncInvite.TryParse(text, out _, out _));
    }

    [Theory]
    [InlineData("file:///c:/secret")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/")]
    public void ParseNeverAcceptsAnAddressThatIsNotHttp(string url)
    {
        Assert.False(SyncInvite.TryParse(SyncInvite.Format(url, "code"), out _, out _));
    }

    [Fact]
    public void ParseRefusesAnAbsurdlyLongText()
    {
        Assert.False(SyncInvite.TryParse(SyncInvite.Prefix + new string('A', 10_000), out _, out _));
    }
}

public sealed class SyncJoinWithInvitationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-join-{Guid.NewGuid():N}");

    public SyncJoinWithInvitationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private (SyncService Sync, AppSettings Settings) Create()
    {
        var settingsService = new SettingsService(Path.Combine(_root, "settings.json"));
        var settings = new AppSettings();
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(_root, "notes.db")), new ContentCipher(new byte[32]));
        return (new SyncService(repository, settings, settingsService), settings);
    }

    [Fact]
    public void JoiningCreatesANewServerProfileWithItsOwnLocalKey()
    {
        var (sync, settings) = Create();
        SyncProfileStore.Ensure(settings);
        var before = settings.SyncProfiles.Count;

        var profile = sync.JoinWithInvitation("Servidor de Nacho", "https://aldune.example.com/", "the-token-from-the-invitation-long");

        Assert.Equal(before + 1, settings.SyncProfiles.Count);
        Assert.Equal(profile.Id, settings.ActiveSyncProfileId);
        Assert.Equal(SyncTransportKind.Server, settings.SyncTransport);
        Assert.Equal("https://aldune.example.com/", settings.SyncServerUrl);
        Assert.Equal("the-token-from-the-invitation-long", sync.GetServerToken());
        Assert.NotNull(settings.WrappedSyncKey);
        Assert.True(settings.SyncEnabled);
    }

    [Fact]
    public void JoiningLeavesTheExistingProfileAlone()
    {
        var (sync, settings) = Create();
        SyncProfileStore.Ensure(settings);
        var first = settings.SyncProfiles[0];
        first.SyncServerUrl = "https://mine.example.com/";

        sync.JoinWithInvitation("Otro", "https://aldune.example.com/", "the-token-from-the-invitation-long");

        Assert.Equal("https://mine.example.com/", first.SyncServerUrl);
    }
}

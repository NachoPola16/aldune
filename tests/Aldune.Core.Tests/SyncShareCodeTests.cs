using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class SyncShareCodeTests
{
    [Fact]
    public void ProfileCodeRoundTripsKeyAndSelectedNotes()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var code = SyncShareCodeCodec.Encode(
            key,
            SyncScopeKind.SelectedNotes,
            new[] { first, first, second },
            "Compartir con Ana",
            SyncTransportKind.Server,
            "https://sync.example.test:8443/");
        var decoded = SyncShareCodeCodec.Decode(code);

        Assert.Equal(key, decoded.Key);
        Assert.Equal(SyncScopeKind.SelectedNotes, decoded.Scope);
        Assert.Equal(new[] { first, second }, decoded.NoteIds);
        Assert.Equal("Compartir con Ana", decoded.ProfileName);
        Assert.Equal(SyncTransportKind.Server, decoded.Transport);
        Assert.Equal("https://sync.example.test:8443/", decoded.ServerUrl);
        Assert.StartsWith("fanote-profile-v2:", code, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(key), code, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyProfileCodeStillImports()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var document = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            Key = SyncKeyFormat.Encode(key),
            Scope = SyncScopeKind.AllNotes,
            NoteIds = Array.Empty<Guid>()
        });
        var payload = Convert.ToBase64String(document).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var decoded = SyncShareCodeCodec.Decode("fanote-profile-v1:" + payload);

        Assert.Equal(key, decoded.Key);
        Assert.Null(decoded.Transport);
        Assert.Null(decoded.ServerUrl);
    }

    [Fact]
    public void WebDavProfileCodeCarriesTheConnectionUrlWithoutCredentials()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var code = SyncShareCodeCodec.Encode(
            key,
            SyncScopeKind.AllNotes,
            Array.Empty<Guid>(),
            "Nextcloud personal",
            SyncTransportKind.WebDav,
            "https://cloud.example.test/remote.php/dav/files/nacho/Fanote/");

        var decoded = SyncShareCodeCodec.Decode(code);

        Assert.Equal(SyncTransportKind.WebDav, decoded.Transport);
        Assert.Equal("https://cloud.example.test/remote.php/dav/files/nacho/Fanote/", decoded.ServerUrl);
        Assert.DoesNotContain("password", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeviceCodeRemainsAValidLegacyKeyOnlyCode()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var code = SyncKeyFormat.Encode(key);

        Assert.False(SyncShareCodeCodec.IsShareCode(code));
        Assert.Equal(key, SyncKeyFormat.Decode(code));
    }

    [Fact]
    public void InvalidProfileCodeIsRejected()
    {
        Assert.Throws<FormatException>(() => SyncShareCodeCodec.Decode("fanote-profile-v1:not-base64"));
    }
}

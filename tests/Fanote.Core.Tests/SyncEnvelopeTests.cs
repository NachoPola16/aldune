using System.Security.Cryptography;
using Fanote.Core;

namespace Fanote.Core.Tests;

public sealed class SyncEnvelopeTests
{
    [Fact]
    public void NoteRoundTrip_UsesPortableEncryptedPayload()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Text = "Título\r\n☐ tarea privada",
            Color = "mint",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTimeOffset.UtcNow,
            State = NoteState.Active,
            ScreenOrigin = "monitor-1",
            Tags = new[] { "trabajo", "Importante" }
        };

        var envelope = SyncEnvelopeCodec.CreateNote(note, key, "device-a");
        var bytes = SyncEnvelopeCodec.Serialize(envelope);
        var restored = SyncEnvelopeCodec.DecryptNote(SyncEnvelopeCodec.Deserialize(bytes), key);

        Assert.Equal(note.Id, restored.Id);
        Assert.Equal(note.Text, restored.Text);
        Assert.Equal(note.UpdatedAt, restored.UpdatedAt);
        Assert.Equal(note.Tags, restored.Tags);
        Assert.DoesNotContain(note.Text, System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void ProtectedNoteRoundTrip_StaysLockedAndCarriesOnlyProtectedContent()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Text = string.Empty,
            Color = "mint",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTimeOffset.UtcNow,
            State = NoteState.Active,
            ScreenOrigin = "monitor-1",
            IsProtected = true,
            ProtectedContent = ProtectedNoteContent.Protect("Título secreto\r\nContenido privado", "clave-segura")
        };

        var envelope = SyncEnvelopeCodec.CreateNote(note, key, "device-a");
        var restored = SyncEnvelopeCodec.DecryptNote(
            SyncEnvelopeCodec.Deserialize(SyncEnvelopeCodec.Serialize(envelope)), key);

        Assert.True(restored.IsProtected);
        Assert.Empty(restored.Text);
        Assert.NotNull(restored.ProtectedContent);
        Assert.True(restored.ProtectedContent!.TryUnprotect("clave-segura", out var text));
        Assert.Equal("Título secreto\r\nContenido privado", text);
        Assert.False(restored.ProtectedContent.TryUnprotect("incorrecta", out _));
    }

    [Fact]
    public void FolderTransport_ReplacesObjectsAtomicallyByNoteId()
    {
        var folder = Path.Combine(Path.GetTempPath(), "fanote-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Text = "uno",
                Color = "cyan",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTimeOffset.UtcNow,
                State = NoteState.Active,
                ScreenOrigin = "primary"
            };
            var key = RandomNumberGenerator.GetBytes(32);
            var transport = new FolderSyncTransport(folder);
            transport.Write(SyncEnvelopeCodec.CreateNote(note, key, "device-a"));

            var read = transport.ReadAll();
            Assert.Single(read);
            Assert.Equal(note.Id, read[0].Envelope.NoteId);
            Assert.False(read[0].Envelope.Tombstone);
            Assert.DoesNotContain(Directory.EnumerateFiles(Path.Combine(folder, "objects")),
                path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void KeyFormat_RoundTripsAndRejectsWrongLength()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        Assert.Equal(key, SyncKeyFormat.Decode(SyncKeyFormat.Encode(key)));
        Assert.Throws<FormatException>(() => SyncKeyFormat.Decode("too-short"));
    }

    [Fact]
    public void UnsupportedFutureFormat_IsRejectedWithoutDecrypting()
    {
        var envelope = new SyncEnvelope
        {
            Format = SyncCompatibility.CurrentFormat + 1,
            NoteId = Guid.NewGuid(),
            UpdatedAt = DateTimeOffset.UtcNow,
            DeviceId = "future-device",
            Tombstone = true
        };

        var error = Assert.Throws<FormatException>(() => SyncEnvelopeCodec.Deserialize(
            SyncEnvelopeCodec.Serialize(envelope)));

        Assert.Contains("newer Fanote version", error.Message, StringComparison.Ordinal);
    }
}

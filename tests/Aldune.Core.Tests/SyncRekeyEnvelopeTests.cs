namespace Aldune.Core.Tests;

/// <summary>
/// El sobre que publica la rotación de clave (<see cref="SyncService.RevokeSharedAccess"/>) para cada nota. Se prueba
/// suelto porque la rotación completa necesita DPAPI y aquí se quiere comprobar en cualquier sistema que el sobre
/// nuevo no pierde nada de la nota.
/// </summary>
public sealed class SyncRekeyEnvelopeTests
{
    [Fact]
    public void RekeyedNote_KeepsEveryFieldOfTheNoteAndOnlyChangesTheDate()
    {
        var key = new byte[32];
        key[0] = 7;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Text = "texto",
            Color = "#EBD38B",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            State = NoteState.Archived,
            ScreenOrigin = "primary",
            Tags = new[] { "Trabajo" },
            DockPosition = 1.5
        };
        var rekeyedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var envelope = SyncService.CreateRekeyedNoteEnvelope(note, key, "device", rekeyedAt);
        var copy = SyncEnvelopeCodec.DecryptNote(envelope, key);

        Assert.Equal(rekeyedAt, copy.UpdatedAt);
        Assert.Equal(note.Text, copy.Text);
        Assert.Equal(note.Color, copy.Color);
        Assert.Equal(note.CreatedAt, copy.CreatedAt);
        Assert.Equal(note.State, copy.State);
        Assert.Equal(note.Tags, copy.Tags);
        Assert.Equal(note.DockPosition, copy.DockPosition);
    }
}

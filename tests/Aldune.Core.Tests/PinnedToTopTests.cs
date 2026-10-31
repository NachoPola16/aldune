using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aldune.Core;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// «Fijada arriba»: la nota va la primera de la lista y el hecho viaja por la sync como un campo opcional del contenido cifrado (el formato
/// sigue siendo el 4). No es el «siempre encima» de Windows (NoteUnpinned), que es local. Ver docs/specs/2026-10-31-pinned-notes-design.md.
/// </summary>
public sealed class PinnedToTopTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-pinned-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public PinnedToTopTests() =>
        _sut = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static Note MakeNote(string text, bool pinned = false, DateTimeOffset? updatedAt = null) => new()
    {
        Id = Guid.NewGuid(), Text = text, Color = "#EBD38B", CreatedAt = Now, UpdatedAt = updatedAt ?? Now,
        State = NoteState.Active, ScreenOrigin = "primary", IsPinnedToTop = pinned
    };

    // ---- Repositorio

    [Fact]
    public void ANewNoteIsNotPinned() => Assert.False(_sut.Create("a", "#EBD38B", "primary").IsPinnedToTop);

    [Fact]
    public void SetPinnedToTop_IsKeptAndCountsAsAnEdit()
    {
        var created = _sut.Create("a", "#EBD38B", "primary");
        var before = _sut.GetById(created.Id)!.UpdatedAt;

        _sut.SetPinnedToTop(created.Id, true);

        var pinned = _sut.GetById(created.Id)!;
        Assert.True(pinned.IsPinnedToTop);
        // Sin refrescar UpdatedAt el sobre no se volvería a publicar y el otro equipo nunca vería el cambio.
        Assert.True(pinned.UpdatedAt > before);
        Assert.Equal("a", pinned.Text);

        _sut.SetPinnedToTop(created.Id, false);
        Assert.False(_sut.GetById(created.Id)!.IsPinnedToTop);
    }

    [Fact]
    public void TheDeckListsPinnedNotesFirstAndKeepsTheOrderInsideEachGroup()
    {
        var a = _sut.Create("a", "#EBD38B", "primary");
        var b = _sut.Create("b", "#EBD38B", "primary");
        var c = _sut.Create("c", "#EBD38B", "primary");
        _sut.SetPinnedToTop(c.Id, true);

        Assert.Equal(new[] { c.Id, a.Id, b.Id }, _sut.GetByState(NoteState.Active).Select(n => n.Id));

        // Arrastrar dentro del grupo (la lista que ve el dock es solo la del grupo) no mezcla los dos.
        _sut.MoveNote(b.Id, 0, new[] { a.Id, b.Id });
        Assert.Equal(new[] { c.Id, b.Id, a.Id }, _sut.GetByState(NoteState.Active).Select(n => n.Id));
    }

    [Fact]
    public void ThePinTravelsWithASyncedNote()
    {
        var note = MakeNote("remota", pinned: true);

        _sut.ApplySyncNote(note);
        Assert.True(_sut.GetById(note.Id)!.IsPinnedToTop);

        // La versión siguiente de la misma nota llega sin el pin (otro equipo la desfijó, o una versión antigua la reenvió): se quita.
        _sut.ApplySyncNote(new Note
        {
            Id = note.Id, Text = "remota", Color = note.Color, CreatedAt = note.CreatedAt, UpdatedAt = note.UpdatedAt.AddMinutes(1),
            State = NoteState.Active, ScreenOrigin = "primary", IsPinnedToTop = false
        });
        Assert.False(_sut.GetById(note.Id)!.IsPinnedToTop);
    }

    [Fact]
    public void ThePinIsKeptWhenTheDatabaseIsOpenedAgain()
    {
        var created = _sut.Create("a", "#EBD38B", "primary");
        _sut.SetPinnedToTop(created.Id, true);

        var reopened = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));

        Assert.True(reopened.GetById(created.Id)!.IsPinnedToTop);
    }

    // ---- Sobre de sync

    [Fact]
    public void ThePinSurvivesTheEnvelopeRoundTrip()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var note = MakeNote("x", pinned: true);

        var read = SyncEnvelopeCodec.DecryptNote(SyncEnvelopeCodec.CreateNote(note, key, "dev"), key);

        Assert.True(read.IsPinnedToTop);
        Assert.Equal(4, SyncEnvelopeCodec.CreateNote(note, key, "dev").Format);
    }

    [Fact]
    public void AnUnpinnedNoteWritesNoPinField_SoItsPayloadIsTheSameAsBefore()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var envelope = SyncEnvelopeCodec.CreateNote(MakeNote("x", pinned: false), key, "dev");

        using var cipher = new ContentCipher(key);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(cipher.Decrypt(new EncryptedContent(
            Convert.FromBase64String(envelope.CipherText!), Convert.FromBase64String(envelope.Nonce!), Convert.FromBase64String(envelope.Tag!)))));

        Assert.DoesNotContain("pinnedToTop", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("pinnedToTop", out _));
    }

    [Fact]
    public void APinnedNoteWritesTheFieldAsAnOptionalBoolean()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var envelope = SyncEnvelopeCodec.CreateNote(MakeNote("x", pinned: true), key, "dev");

        using var cipher = new ContentCipher(key);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(cipher.Decrypt(new EncryptedContent(
            Convert.FromBase64String(envelope.CipherText!), Convert.FromBase64String(envelope.Nonce!), Convert.FromBase64String(envelope.Tag!)))));

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("pinnedToTop").GetBoolean());
    }

    // ---- Equivalencia y orden

    [Fact]
    public void ANoteWithAndWithoutThePinAreDifferentVersions()
    {
        var a = MakeNote("same");
        var b = new Note
        {
            Id = a.Id, Text = a.Text, Color = a.Color, CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt, State = a.State,
            ScreenOrigin = a.ScreenOrigin, IsPinnedToTop = true
        };

        Assert.False(NoteEquivalence.SameContent(a, b));
    }

    [Theory]
    [InlineData(NoteListOrder.Dock)]
    [InlineData(NoteListOrder.Newest)]
    [InlineData(NoteListOrder.Oldest)]
    [InlineData(NoteListOrder.Title)]
    public void PinnedNotesComeFirstAndKeepTheOrderOfTheirGroup(NoteListOrder order)
    {
        var plainA = MakeNote("Alfa", updatedAt: Now.AddDays(-1));
        var pinnedB = MakeNote("Bravo", pinned: true, updatedAt: Now.AddDays(-5));
        var plainC = MakeNote("Charlie", updatedAt: Now.AddDays(-3));
        var pinnedD = MakeNote("Delta", pinned: true, updatedAt: Now.AddDays(-2));
        var notes = new[] { plainA, pinnedB, plainC, pinnedD };

        var sorted = NoteListing.Sort(notes, n => n, order).ToList();
        var unpinnedOrder = NoteListing.Sort(new[] { plainA, plainC }, n => n, order).ToList();
        var pinnedOrder = NoteListing.Sort(new[] { pinnedB, pinnedD }, n => n, order).ToList();

        Assert.Equal(pinnedOrder.Concat(unpinnedOrder), sorted);
    }
}

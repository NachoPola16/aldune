using System.Security.Cryptography;
using Aldune.Core;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>El «nombre visible» de una nota protegida: texto en claro, opcional, que se ve en el dock aunque el contenido siga cifrado.</summary>
public sealed class ProtectedTitleTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-ptitle-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public ProtectedTitleTests() =>
        _sut = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Theory]
    [InlineData("  Banco  ", "Banco")]
    [InlineData("Una\r\nlínea", "Una línea")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_TrimsAndCollapsesToOneLine(string? raw, string? expected) =>
        Assert.Equal(expected, ProtectedTitleRules.Normalize(raw));

    [Fact]
    public void Normalize_CutsAtTheMaximumLength()
    {
        var result = ProtectedTitleRules.Normalize(new string('a', 200));
        Assert.Equal(ProtectedTitleRules.MaxLength, result!.Length);
    }

    [Fact]
    public void SetProtectedTitle_IsKeptWhileLockedAndDoesNotTouchTheContent()
    {
        var created = _sut.Create("secreto\r\ncontenido", "#F5E3B3", "primary");
        _sut.Protect(created.Id, "clave");

        _sut.SetProtectedTitle(created.Id, "  Banco ");

        var locked = _sut.GetById(created.Id)!;
        Assert.Equal("Banco", locked.ProtectedTitle);
        Assert.Equal(string.Empty, locked.Text);
        Assert.True(_sut.TryUnlock(created.Id, "clave", out var unlocked));
        Assert.Equal("secreto\r\ncontenido", unlocked!.Text);
    }

    [Fact]
    public void SetProtectedTitle_OnAPlainNote_Throws()
    {
        var created = _sut.Create("hola", "#F5E3B3", "primary");
        Assert.Throws<InvalidOperationException>(() => _sut.SetProtectedTitle(created.Id, "x"));
    }

    [Fact]
    public void SetProtectedTitle_Empty_HidesTheNameAgain()
    {
        var created = _sut.Create("secreto", "#F5E3B3", "primary");
        _sut.Protect(created.Id, "clave");
        _sut.SetProtectedTitle(created.Id, "Banco");

        _sut.SetProtectedTitle(created.Id, "  ");

        Assert.Null(_sut.GetById(created.Id)!.ProtectedTitle);
    }

    [Fact]
    public void SetProtectedTitle_BumpsUpdatedAtSoItSyncs()
    {
        var created = _sut.Create("secreto", "#F5E3B3", "primary");
        _sut.Protect(created.Id, "clave");
        var before = _sut.GetById(created.Id)!.UpdatedAt;

        Thread.Sleep(5);
        _sut.SetProtectedTitle(created.Id, "Banco");

        Assert.True(_sut.GetById(created.Id)!.UpdatedAt > before);
    }

    [Fact]
    public void RemoveProtection_DropsTheName()
    {
        var created = _sut.Create("secreto", "#F5E3B3", "primary");
        _sut.Protect(created.Id, "clave");
        _sut.SetProtectedTitle(created.Id, "Banco");

        Assert.True(_sut.RemoveProtection(created.Id, "clave"));

        Assert.Null(_sut.GetById(created.Id)!.ProtectedTitle);
    }

    [Fact]
    public void ApplySyncNote_StoresTheNameAndReplacesIt()
    {
        var created = _sut.Create("secreto", "#F5E3B3", "primary");
        var content = _sut.Protect(created.Id, "clave");
        var remote = new Note
        {
            Id = created.Id, Text = string.Empty, Color = "#F5E3B3", CreatedAt = created.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1), State = NoteState.Active, ScreenOrigin = "primary",
            IsProtected = true, ProtectedContent = content, ProtectedTitle = "Banco"
        };

        _sut.ApplySyncNote(remote);
        Assert.Equal("Banco", _sut.GetById(created.Id)!.ProtectedTitle);

        remote.ProtectedTitle = null;
        _sut.ApplySyncNote(remote);
        Assert.Null(_sut.GetById(created.Id)!.ProtectedTitle);
    }

    [Fact]
    public void SyncEnvelope_CarriesTheNameAndOldEnvelopesWithoutItStillRead()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var note = new Note
        {
            Id = Guid.NewGuid(), Text = string.Empty, Color = "mint", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTimeOffset.UtcNow, State = NoteState.Active, ScreenOrigin = "monitor-1",
            IsProtected = true, ProtectedContent = ProtectedNoteContent.Protect("contenido", "clave"),
            ProtectedTitle = "Banco"
        };

        var restored = SyncEnvelopeCodec.DecryptNote(SyncEnvelopeCodec.CreateNote(note, key, "d"), key);
        Assert.Equal("Banco", restored.ProtectedTitle);

        note.ProtectedTitle = null;
        Assert.Null(SyncEnvelopeCodec.DecryptNote(SyncEnvelopeCodec.CreateNote(note, key, "d"), key).ProtectedTitle);
    }

    [Fact]
    public void SameContent_NoticesAChangeOfName()
    {
        var content = ProtectedNoteContent.Protect("contenido", "clave");
        Note Make(string? title) => new()
        {
            Id = Guid.Empty, Text = string.Empty, Color = "mint", CreatedAt = default, UpdatedAt = default,
            State = NoteState.Active, ScreenOrigin = "p", IsProtected = true, ProtectedContent = content, ProtectedTitle = title
        };

        Assert.True(NoteEquivalence.SameContent(Make("A"), Make("A")));
        Assert.False(NoteEquivalence.SameContent(Make("A"), Make("B")));
        Assert.False(NoteEquivalence.SameContent(Make("A"), Make(null)));
    }

    [Fact]
    public void DisplayTitle_OfAProtectedNote_IsTheNameOrNullWhenHidden()
    {
        Note Make(string? title) => new()
        {
            Id = Guid.NewGuid(), Text = string.Empty, Color = "mint", CreatedAt = default, UpdatedAt = default,
            State = NoteState.Active, ScreenOrigin = "p", IsProtected = true, ProtectedTitle = title
        };

        Assert.Equal("Banco", ProtectedTitleRules.Visible(Make("Banco")));
        Assert.Null(ProtectedTitleRules.Visible(Make(null)));
        // Una nota sin proteger nunca usa este campo, aunque lo arrastre por un fallo.
        var plain = Make("Banco"); plain.IsProtected = false;
        Assert.Null(ProtectedTitleRules.Visible(plain));
    }
}

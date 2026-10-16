using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// "Combinar" de la ventana de conflictos con notas protegidas. El texto de una nota protegida se lee vacío (va en
/// su propio cifrado, con la contraseña), así que no hay nada que combinar: la versión perdedora tiene que seguir en
/// la cola en vez de desaparecer. Va aparte de SyncConflictTests porque no necesita sincronizar (ni DPAPI).
/// </summary>
public sealed class SyncConflictMergeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-merge-{Guid.NewGuid():N}");
    private readonly string _databasePath;
    private readonly NotesRepository _repository;
    private readonly SyncService _sync;

    public SyncConflictMergeTests()
    {
        Directory.CreateDirectory(_root);
        _databasePath = Path.Combine(_root, "notes.db");
        _repository = new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(new byte[32]));
        _sync = new SyncService(_repository, new AppSettings(), new SettingsService(Path.Combine(_root, "settings.json")));
    }

    [Fact]
    public void MergingIntoAProtectedNote_IsRefusedAndKeepsTheConflict()
    {
        var note = _repository.Create("secreto", "#EBD38B", "primary");
        _repository.Protect(note.Id, "contraseña");
        var conflict = SaveConflict(note.Id, Plain(note.Id, "versión de otro equipo"));

        Assert.False(_sync.MergeConflict(conflict.Id));

        Assert.Single(_sync.GetConflicts());
        Assert.True(_repository.TryUnlock(note.Id, "contraseña", out var unlocked));
        Assert.Equal("secreto", unlocked!.Text);
    }

    [Fact]
    public void MergingAProtectedLosingVersion_IsRefusedAndKeepsTheConflict()
    {
        var note = _repository.Create("texto", "#EBD38B", "primary");
        var losing = Plain(note.Id, string.Empty);
        losing.IsProtected = true;
        losing.ProtectedContent = ProtectedNoteContent.Protect("secreto", "contraseña");
        var conflict = SaveConflict(note.Id, losing);

        Assert.False(_sync.MergeConflict(conflict.Id));

        Assert.Single(_sync.GetConflicts());
        Assert.Equal("texto", _repository.GetById(note.Id)!.Text);
    }

    [Fact]
    public void MergingTwoPlainVersions_StillCombinesThem()
    {
        var note = _repository.Create("compra\npan", "#EBD38B", "primary");
        var conflict = SaveConflict(note.Id, Plain(note.Id, "compra\nleche"));

        Assert.True(_sync.MergeConflict(conflict.Id));

        Assert.Empty(_sync.GetConflicts());
        var merged = _repository.GetById(note.Id)!.Text;
        Assert.Contains("pan", merged);
        Assert.Contains("leche", merged);
    }

    private static Note Plain(Guid id, string text) => new()
    {
        Id = id,
        Text = text,
        Color = "#EBD38B",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        State = NoteState.Active,
        ScreenOrigin = "primary"
    };

    private SyncConflict SaveConflict(Guid noteId, Note losing)
    {
        var conflict = new SyncConflict(Guid.NewGuid(), noteId, DateTimeOffset.UtcNow,
            new SyncConflictVersion(DateTimeOffset.UtcNow, "device-a", false, null),
            new SyncConflictVersion(losing.UpdatedAt, "device-b", false, losing));
        _repository.SaveSyncConflict(conflict);
        return conflict;
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}

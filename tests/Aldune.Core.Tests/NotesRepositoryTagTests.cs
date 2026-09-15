using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class NotesRepositoryTagTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-tags-{Guid.NewGuid()}.db");
    private readonly NotesRepository _repository;

    public NotesRepositoryTagTests()
    {
        _repository = new NotesRepository(
            new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
    }

    [Fact]
    public void SetTags_PersistsNormalizesAndFiltersByTag()
    {
        var matching = _repository.Create("matching", "#F5E3B3", "primary");
        _repository.Create("other", "#F5E3B3", "primary");

        _repository.SetTags(matching.Id, new[] { "Trabajo", " trabajo ", "importante" });

        var reloaded = Assert.Single(_repository.GetByState(NoteState.Active), n => n.Id == matching.Id);
        Assert.Equal(new[] { "Trabajo", "importante" }, reloaded.Tags);
        Assert.Equal(new[] { matching.Id }, _repository.GetByTag("TRABAJO", NoteState.Active).Select(n => n.Id));
        Assert.Equal(new[] { "importante", "Trabajo" }, _repository.GetAllTags());
    }

    [Fact]
    public void SetTags_EmptyRemovesUnusedTags()
    {
        var note = _repository.Create("note", "#F5E3B3", "primary");
        _repository.SetTags(note.Id, new[] { "temporary" });
        _repository.SetTags(note.Id, Array.Empty<string>());

        Assert.Empty(_repository.GetByState(NoteState.Active).Single().Tags);
        Assert.Empty(_repository.GetAllTags());
    }

    [Fact]
    public void CreateAndDeleteTag_ManagesGlobalTagAndAssignments()
    {
        var note = _repository.Create("note", "#F5E3B3", "primary");

        Assert.True(_repository.CreateTag("Trabajo"));
        Assert.False(_repository.CreateTag(" trabajo "));
        _repository.SetTags(note.Id, new[] { "Trabajo" });

        Assert.True(_repository.DeleteTag("TRABAJO"));
        Assert.Empty(_repository.GetAllTags());
        Assert.Empty(_repository.GetByState(NoteState.Active).Single().Tags);
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

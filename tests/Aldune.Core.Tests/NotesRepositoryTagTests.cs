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
    public void GetAllTags_FollowsTheCustomOrderAndKeepsNewTagsAlphabeticalAfterIt()
    {
        foreach (var name in new[] { "casa", "trabajo", "ideas" }) _repository.CreateTag(name);
        Assert.Equal(new[] { "casa", "ideas", "trabajo" }, _repository.GetAllTags());

        _repository.CustomTagOrder = new[] { "trabajo", "casa" };
        _repository.CreateTag("zeta");

        Assert.Equal(new[] { "trabajo", "casa", "ideas", "zeta" }, _repository.GetAllTags());
    }

    [Fact]
    public void SetTags_GivesTheNoteTheColorOfTheTagItGained()
    {
        _repository.TagColors = new Dictionary<string, string> { ["casa"] = "#A8E0B4", ["trabajo"] = "#9EC9F0" };
        var note = _repository.Create("n", "#F5E3B3", "primary");

        _repository.SetTags(note.Id, new[] { "Casa" });
        Assert.Equal("#A8E0B4", Reload(note.Id).Color);

        // Adding a second coloured tag: the one just added wins. Saving the same tags again changes nothing.
        _repository.SetTags(note.Id, new[] { "Casa", "Trabajo" });
        Assert.Equal("#9EC9F0", Reload(note.Id).Color);
        _repository.SetColor(note.Id, "#F5E3B3");
        _repository.SetTags(note.Id, new[] { "Casa", "Trabajo" });
        Assert.Equal("#F5E3B3", Reload(note.Id).Color);
    }

    [Fact]
    public void SetTags_WithATagThatHasNoColor_LeavesTheNoteColorAlone()
    {
        _repository.TagColors = new Dictionary<string, string> { ["casa"] = "#A8E0B4" };
        var note = _repository.Create("n", "#F5E3B3", "primary");

        _repository.SetTags(note.Id, new[] { "ideas" });

        Assert.Equal("#F5E3B3", Reload(note.Id).Color);
    }

    [Fact]
    public void ApplyTagColor_RecolorsActiveAndArchivedNotesButNotTrashed()
    {
        var active = _repository.Create("a", "#F5E3B3", "primary");
        var archived = _repository.Create("b", "#F5E3B3", "primary");
        var trashed = _repository.Create("c", "#F5E3B3", "primary");
        var untagged = _repository.Create("d", "#F5E3B3", "primary");
        foreach (var n in new[] { active, archived, trashed }) _repository.SetTags(n.Id, new[] { "casa" });
        _repository.SetState(archived.Id, NoteState.Archived);
        _repository.SetState(trashed.Id, NoteState.Trashed);

        _repository.ApplyTagColor("CASA", "#A8E0B4");

        Assert.Equal("#A8E0B4", Reload(active.Id).Color);
        Assert.Equal("#A8E0B4", Reload(archived.Id).Color);
        Assert.Equal("#F5E3B3", Reload(trashed.Id).Color);
        Assert.Equal("#F5E3B3", Reload(untagged.Id).Color);
    }

    private Note Reload(Guid id) => new[] { NoteState.Active, NoteState.Archived, NoteState.Trashed }.SelectMany(_repository.GetByState).First(n => n.Id == id);

    [Fact]
    public void RenameTag_ChangesTheNameOnEveryNoteAndTouchesThem()
    {
        var a = _repository.Create("a", "#F5E3B3", "primary");
        var b = _repository.Create("b", "#F5E3B3", "primary");
        _repository.SetTags(a.Id, new[] { "Casa", "otra" });
        _repository.SetTags(b.Id, new[] { "casa" });
        var before = Reload(a.Id).UpdatedAt;
        System.Threading.Thread.Sleep(5);

        Assert.True(_repository.RenameTag("CASA", "Hogar"));

        Assert.Equal(new[] { "Hogar", "otra" }, Reload(a.Id).Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(new[] { "Hogar" }, Reload(b.Id).Tags);
        Assert.DoesNotContain("Casa", _repository.GetAllTags(), StringComparer.OrdinalIgnoreCase);
        Assert.True(Reload(a.Id).UpdatedAt > before); // the sync must carry the change
    }

    [Fact]
    public void RenameTag_ToAnExistingTag_MergesThemAndDoesNotDuplicate()
    {
        var a = _repository.Create("a", "#F5E3B3", "primary");
        var b = _repository.Create("b", "#F5E3B3", "primary");
        _repository.SetTags(a.Id, new[] { "x", "y" });
        _repository.SetTags(b.Id, new[] { "x" });

        Assert.True(_repository.RenameTag("x", "y"));

        Assert.Equal(new[] { "y" }, Reload(a.Id).Tags);
        Assert.Equal(new[] { "y" }, Reload(b.Id).Tags);
        Assert.Equal(new[] { "y" }, _repository.GetAllTags());
    }

    [Fact]
    public void RenameTag_WithAnEmptyNameOrAnUnknownTag_DoesNothing()
    {
        var a = _repository.Create("a", "#F5E3B3", "primary");
        _repository.SetTags(a.Id, new[] { "x" });

        Assert.False(_repository.RenameTag("x", "   "));
        Assert.False(_repository.RenameTag("nope", "z"));
        Assert.Equal(new[] { "x" }, Reload(a.Id).Tags);
    }

    [Fact]
    public void SetTags_Empty_UnassignsButKeepsTheTag()
    {
        // Las etiquetas se crean y se borran en el gestor. Quitar la última nota de una etiqueta no
        // la borra: antes sí, de cuando las etiquetas se escribían a mano en cada nota.
        var note = _repository.Create("note", "#F5E3B3", "primary");
        _repository.SetTags(note.Id, new[] { "temporary" });
        _repository.SetTags(note.Id, Array.Empty<string>());

        Assert.Empty(_repository.GetByState(NoteState.Active).Single().Tags);
        Assert.Equal(new[] { "temporary" }, _repository.GetAllTags());
    }

    [Fact]
    public void SetTags_DoesNotDeleteTagsCreatedInTheManagerForLater()
    {
        var note = _repository.Create("note", "#F5E3B3", "primary");
        _repository.CreateTag("Viaje");

        _repository.SetTags(note.Id, new[] { "Trabajo" });

        Assert.Equal(new[] { "Trabajo", "Viaje" }, _repository.GetAllTags());
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

    [Fact]
    public void DeleteTagAndNotes_SendsActiveAndArchivedNotesWithTheTagToTheTrash_AndDropsTheTag()
    {
        var active = _repository.Create("active", "#F5E3B3", "primary");
        var archived = _repository.Create("archived", "#F5E3B3", "primary");
        var trashed = _repository.Create("already trashed", "#F5E3B3", "primary");
        var other = _repository.Create("other tag", "#F5E3B3", "primary");
        _repository.SetTags(active.Id, new[] { "Trabajo" });
        _repository.SetTags(archived.Id, new[] { "trabajo" });
        _repository.SetTags(trashed.Id, new[] { "Trabajo" });
        _repository.SetTags(other.Id, new[] { "Viaje" });
        _repository.SetState(archived.Id, NoteState.Archived);
        _repository.SetState(trashed.Id, NoteState.Trashed);

        Assert.Equal(2, _repository.DeleteTagAndNotes("TRABAJO"));

        Assert.Equal(new[] { "Viaje" }, _repository.GetAllTags());
        Assert.Equal(other.Id, _repository.GetByState(NoteState.Active).Single().Id);
        Assert.Empty(_repository.GetByState(NoteState.Archived));
        Assert.Equal(3, _repository.GetByState(NoteState.Trashed).Count);
    }

    [Fact]
    public void DeleteTagAndNotes_WithAnUnknownTag_ChangesNothing()
    {
        var note = _repository.Create("note", "#F5E3B3", "primary");

        Assert.Equal(0, _repository.DeleteTagAndNotes("nope"));
        Assert.Equal(NoteState.Active, _repository.GetByState(NoteState.Active).Single().State);
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

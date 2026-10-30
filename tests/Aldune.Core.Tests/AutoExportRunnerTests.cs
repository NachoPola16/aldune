using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class AutoExportRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-autoexport-{Guid.NewGuid():N}");
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-autoexport-{Guid.NewGuid():N}.db");

    public AutoExportRunnerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        Directory.Delete(_root, recursive: true);
    }

    private static ExportNote N(string text, Guid? id = null, NoteState state = NoteState.Active) => new(id ?? Guid.NewGuid(), text, state, false);

    [Fact]
    public void WritesTheFilesAndTheManifest()
    {
        var applied = AutoExportRunner.Run(_root, [N("Compra\n☐ pan")], includeArchived: true);

        Assert.Equal(1, applied);
        Assert.Contains("- [ ] pan", File.ReadAllText(Path.Combine(_root, "notas", "Compra.md")));
        Assert.True(File.Exists(Path.Combine(_root, ExportManifest.FileName)));
    }

    [Fact]
    public void TheFileHasNoByteOrderMark()
    {
        AutoExportRunner.Run(_root, [N("Hola")], includeArchived: true);

        var bytes = File.ReadAllBytes(Path.Combine(_root, "notas", "Hola.md"));
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public void RunningTwiceWithNoChanges_AppliesNothing()
    {
        var notes = new[] { N("Uno"), N("Dos") };
        AutoExportRunner.Run(_root, notes, true);

        Assert.Equal(0, AutoExportRunner.Run(_root, notes, true));
    }

    [Fact]
    public void ADeletedNote_ItsFileMovesToEliminadas_AndIsNeverDeleted()
    {
        var id = Guid.NewGuid();
        AutoExportRunner.Run(_root, [N("Adios", id)], true);

        AutoExportRunner.Run(_root, [], true);

        Assert.False(File.Exists(Path.Combine(_root, "notas", "Adios.md")));
        Assert.True(File.Exists(Path.Combine(_root, "_eliminadas", "Adios.md")));
    }

    [Fact]
    public void AFileEditedByHand_IsKept_AndTheNewCopyGoesBesideIt()
    {
        var id = Guid.NewGuid();
        AutoExportRunner.Run(_root, [N("Mia\nuno", id)], true);
        File.WriteAllText(Path.Combine(_root, "notas", "Mia.md"), "editado a mano");

        AutoExportRunner.Run(_root, [N("Mia\ndos", id)], true);

        Assert.Equal("editado a mano", File.ReadAllText(Path.Combine(_root, "notas", "Mia.md")));
        Assert.Contains("dos", File.ReadAllText(Path.Combine(_root, "notas", "Mia (Aldune).md")));
    }

    [Fact]
    public void AMissingRootFolder_IsNotRecreated()
    {
        var gone = Path.Combine(_root, "no-existe");

        Assert.Throws<DirectoryNotFoundException>(() => AutoExportRunner.Run(gone, [N("X")], true));
        Assert.False(Directory.Exists(gone));
    }

    [Fact]
    public void AForeignFileInTheFolder_IsLeftUntouched()
    {
        File.WriteAllText(Path.Combine(_root, "apuntes.txt"), "mio");
        AutoExportRunner.Run(_root, [N("Nota")], true);
        AutoExportRunner.Run(_root, [], true);

        Assert.Equal("mio", File.ReadAllText(Path.Combine(_root, "apuntes.txt")));
    }

    [Fact]
    public void ChangeStamp_ChangesWhenANoteIsCreatedEditedOrArchived()
    {
        var repo = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
        var empty = repo.ChangeStamp();

        var note = repo.Create("hola", "#F5E3B3", "primary");
        var created = repo.ChangeStamp();
        repo.SetState(note.Id, NoteState.Archived);
        var archived = repo.ChangeStamp();

        Assert.NotEqual(empty, created);
        Assert.NotEqual(created, archived);
        Assert.Equal(archived, repo.ChangeStamp());   // sin cambios, la misma huella
    }
}

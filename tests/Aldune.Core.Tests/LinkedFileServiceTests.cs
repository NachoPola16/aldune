using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class LinkedFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-linked-{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly NotesRepository _repository;
    private readonly LinkedFileService _sut;

    public LinkedFileServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "notes.db");
        _repository = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
        _sut = new LinkedFileService(_repository, () => "#EBD38B", () => "⚠ Conflicto: ", sleep: _ => { });
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        Directory.Delete(_dir, recursive: true);
    }

    private string WriteFile(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private Guid OpenLinked(string path)
    {
        var result = _sut.Open(path);
        Assert.Equal(LinkOutcome.Linked, result.Outcome);
        return result.NoteId!.Value;
    }

    [Fact]
    public void Open_CreatesATranslatedNoteWithoutSync()
    {
        var path = WriteFile("tema.md", "# Tema\n- [ ] repasar\n");

        var id = OpenLinked(path);

        Assert.Equal("# Tema\n☐ repasar\n", _repository.GetById(id)!.Text);
        var link = _repository.GetFileLink(id)!;
        Assert.Equal(Path.GetFullPath(path), link.Path);
        Assert.False(link.SyncEnabled);
    }

    [Fact]
    public void Open_TheSameFileTwice_ReturnsTheExistingNote()
    {
        var path = WriteFile("tema.md", "x");
        var id = OpenLinked(path);

        var again = _sut.Open(path.ToUpperInvariant());

        Assert.Equal(new LinkResult(LinkOutcome.AlreadyLinked, id), again);
        Assert.Single(_repository.GetByState(NoteState.Active));
    }

    [Fact]
    public void Open_RejectsWhatItCannotSafelyEdit()
    {
        var png = WriteFile("foto.png", "x");
        var ansi = Path.Combine(_dir, "ansi.txt");
        File.WriteAllBytes(ansi, [0x61, 0xF1, 0x6F]);
        var big = Path.Combine(_dir, "grande.md");
        File.WriteAllBytes(big, new byte[LinkedFileFormat.MaxBytes + 1]);

        Assert.Equal(LinkOutcome.UnsupportedExtension, _sut.Open(png).Outcome);
        Assert.Equal(LinkOutcome.UnsupportedEncoding, _sut.Open(ansi).Outcome);
        Assert.Equal(LinkOutcome.TooLarge, _sut.Open(big).Outcome);
        Assert.Equal(LinkOutcome.Unreadable, _sut.Open(Path.Combine(_dir, "no-existe.md")).Outcome);
        Assert.Empty(_repository.GetByState(NoteState.Active));
    }

    [Fact]
    public void AnEditInAldune_IsWrittenToTheFile()
    {
        var path = WriteFile("tema.md", "* [ ] uno\n* [ ] dos\n");
        var id = OpenLinked(path);

        _repository.UpdateText(id, "☒ uno\n☐ dos\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Written, result.Outcome);
        Assert.Equal("- [x] uno\n* [ ] dos\n", File.ReadAllText(path));
        Assert.Equal(ReconcileOutcome.Unchanged, _sut.Reconcile(id).Outcome);
        Assert.Empty(Directory.GetFiles(_dir, ".~aldune-*"));
    }

    [Fact]
    public void AnOutsideChange_IsReloadedIntoTheNote()
    {
        var path = WriteFile("tema.md", "- [ ] uno\n");
        var id = OpenLinked(path);

        File.WriteAllText(path, "- [ ] uno\n- [ ] dos\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Reloaded, result.Outcome);
        Assert.Equal("☐ uno\n☐ dos\n", _repository.GetById(id)!.Text);
    }

    [Fact]
    public void BothSidesChanged_KeepsTheFileAndMovesAldunesTextToANewNote()
    {
        var path = WriteFile("tema.md", "original\n");
        var id = OpenLinked(path);

        _repository.UpdateText(id, "lo de Aldune\n");
        File.WriteAllText(path, "lo del otro programa\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Conflict, result.Outcome);
        Assert.Equal("lo del otro programa\n", File.ReadAllText(path));
        Assert.Equal("lo del otro programa\n", _repository.GetById(id)!.Text);
        var copy = _repository.GetById(result.ConflictNoteId!.Value)!;
        Assert.Equal("⚠ Conflicto: lo de Aldune\n", copy.Text);
        Assert.Null(_repository.GetFileLink(copy.Id));
    }

    [Fact]
    public void AMissingFile_IsUnavailable_AndPendingEditsWaitForIt()
    {
        var path = WriteFile("tema.md", "original\n");
        var id = OpenLinked(path);
        var moved = path + ".fuera";
        File.Move(path, moved);

        _repository.UpdateText(id, "editado mientras no estaba\n");
        Assert.Equal(ReconcileOutcome.Unavailable, _sut.Reconcile(id).Outcome);
        Assert.False(File.Exists(path));

        File.Move(moved, path);
        Assert.Equal(ReconcileOutcome.Written, _sut.Reconcile(id).Outcome);
        Assert.Equal("editado mientras no estaba\n", File.ReadAllText(path));
    }

    [Fact]
    public void UnchangedBytes_AreNotRewritten()
    {
        // NoteText.Join pone CRLF entre título y cuerpo: el texto de la nota cambia, los bytes del archivo no.
        var path = WriteFile("tema.md", "# Tema\nlínea\n");
        var id = OpenLinked(path);
        var before = File.GetLastWriteTimeUtc(path);
        Thread.Sleep(20);

        _repository.UpdateText(id, "# Tema\r\nlínea\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Unchanged, result.Outcome);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.Equal(ReconcileOutcome.Unchanged, _sut.Reconcile(id).Outcome);
    }

    [Fact]
    public void Relink_ToAnotherFile_TreatsItsContentAsNew()
    {
        var first = WriteFile("a.md", "a\n");
        var id = OpenLinked(first);
        var second = WriteFile("b.md", "b\n");

        Assert.Equal(LinkOutcome.Linked, _sut.Relink(id, second).Outcome);
        Assert.Equal(ReconcileOutcome.Reloaded, _sut.Reconcile(id).Outcome);
        Assert.Equal("b\n", _repository.GetById(id)!.Text);
    }

    [Fact]
    public void OrphanTemporaryFiles_AreCleanedButNothingElse()
    {
        var path = WriteFile("tema.md", "x\n");
        var id = OpenLinked(path);
        var orphan = Path.Combine(_dir, $".~aldune-{Guid.NewGuid():N}.tmp");
        var lookalike = Path.Combine(_dir, ".~aldune-notas.tmp");
        File.WriteAllText(orphan, "");
        File.WriteAllText(lookalike, "");
        File.SetLastWriteTimeUtc(orphan, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(lookalike, DateTime.UtcNow.AddMinutes(-5));

        _repository.UpdateText(id, "y\n");
        _sut.Reconcile(id);

        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(lookalike));
    }

    [Fact]
    public void Unlink_KeepsTheNoteAndTheFile()
    {
        var path = WriteFile("tema.md", "x\n");
        var id = OpenLinked(path);

        _sut.Unlink(id);

        Assert.Null(_repository.GetFileLink(id));
        Assert.NotNull(_repository.GetById(id));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LooksChanged_DetectsAnOutsideWrite()
    {
        var path = WriteFile("tema.md", "x\n");
        var id = OpenLinked(path);
        Assert.False(LinkedFileService.LooksChanged(_repository.GetFileLink(id)!));

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.True(LinkedFileService.LooksChanged(_repository.GetFileLink(id)!));
    }

    [Fact]
    public void SaveAs_WritesTheFileAndLinksTheNote()
    {
        var note = _repository.Create("Título\r\n☐ a\r\n→ b", "#EBD38B", "primary");
        var path = Path.Combine(_dir, "nueva.md");

        var result = _sut.SaveAs(note.Id, path);

        Assert.Equal(new LinkResult(LinkOutcome.Linked, note.Id), result);
        Assert.Equal("Título\r\n- [ ] a\r\n- b", File.ReadAllText(path));
        Assert.Equal(ReconcileOutcome.Unchanged, _sut.Reconcile(note.Id).Outcome);
    }

    [Fact]
    public void SaveAs_NeverOverwritesAnExistingFile()
    {
        var note = _repository.Create("x", "#EBD38B", "primary");
        var path = WriteFile("existe.md", "del usuario");

        Assert.Equal(LinkOutcome.Unreadable, _sut.SaveAs(note.Id, path).Outcome);
        Assert.Equal("del usuario", File.ReadAllText(path));
    }

    // Revisión final, C1: lo que pasa en la ventana mientras Reconcile espera a que el archivo se asiente
    // (300 ms o más) cuenta como cambio pendiente. Antes se decidía con la nota leída antes de esa espera y
    // Reload pisaba lo tecleado sin avisar.
    [Fact]
    public void EditDuringTheStableRead_IsAConflictNotAnOverwrite()
    {
        var path = WriteFile("tema.md", "uno\n");
        var id = OpenLinked(path);
        var typing = new LinkedFileService(_repository, () => "#EBD38B", () => "⚠ Conflicto: ",
            sleep: _ => _repository.UpdateText(id, "uno tecleado\n"));
        File.WriteAllText(path, "uno desde fuera\n", new UTF8Encoding(false));

        var result = typing.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Conflict, result.Outcome);
        Assert.Contains("tecleado", _repository.GetById(result.ConflictNoteId!.Value)!.Text);
        Assert.Equal("uno desde fuera\n", File.ReadAllText(path));
    }

    // Revisión final, I2: Reconcile no debe pisar con una foto vieja lo que otro cambió mientras tanto.
    [Fact]
    public void SyncToggledDuringAWrite_IsKept()
    {
        var path = WriteFile("tema.md", "uno\n");
        var id = OpenLinked(path);
        _repository.UpdateText(id, "uno editado\n");
        var toggling = new LinkedFileService(_repository, () => "#EBD38B", () => "⚠ Conflicto: ",
            sleep: _ => _sut.SetSync(id, true));

        Assert.Equal(ReconcileOutcome.Written, toggling.Reconcile(id).Outcome);

        Assert.True(_repository.GetFileLink(id)!.SyncEnabled);
    }

    [Fact]
    public void UpdatingTheKnownStateOfARemovedLink_DoesNotResurrectIt()
    {
        var id = OpenLinked(WriteFile("tema.md", "x"));
        _sut.Unlink(id);

        _repository.UpdateFileLinkKnownState(id, "h", DateTimeOffset.UtcNow, "t");
        _repository.UpdateFileLinkPath(id, @"C:\otra.md");

        Assert.Null(_repository.GetFileLink(id));
    }

    [Fact]
    public void SetSync_TogglesTheFlag()
    {
        var id = OpenLinked(WriteFile("tema.md", "x"));
        _sut.SetSync(id, true);
        Assert.True(_repository.GetFileLink(id)!.SyncEnabled);
    }
}

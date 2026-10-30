using Xunit;

namespace Aldune.Core.Tests;

public class AutoExportPlannerTests
{
    /// <summary>Una carpeta en memoria: lo que el planificador ve del disco (solo huellas).</summary>
    private sealed class FakeFolder
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? HashOf(string path) => Files.TryGetValue(path, out var c) ? AutoExportPlanner.HashOf(c) : null;

        public void Apply(IEnumerable<ExportAction> actions)
        {
            foreach (var action in actions)
            {
                switch (action)
                {
                    case ExportAction.Write w: Files[w.Path] = w.Content; break;
                    case ExportAction.Move m: Files[m.To] = Files[m.From]; Files.Remove(m.From); break;
                }
            }
        }
    }

    private static ExportNote N(string text, NoteState state = NoteState.Active, bool isProtected = false, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), text, state, isProtected);

    private static AutoExportPlan Run(FakeFolder folder, ExportManifest manifest, IEnumerable<ExportNote> notes, bool archived = true)
    {
        var plan = AutoExportPlanner.Plan(notes.ToList(), manifest, archived, folder.HashOf);
        folder.Apply(plan.Actions);
        return plan;
    }

    [Fact]
    public void ANewNote_IsWrittenAsMarkdownUnderNotas()
    {
        var folder = new FakeFolder();
        var note = N("Lista\n☐ pan");

        var plan = Run(folder, new ExportManifest(), [note]);

        var write = Assert.IsType<ExportAction.Write>(Assert.Single(plan.Actions));
        Assert.Equal("notas/Lista.md", write.Path);
        Assert.Equal(MarkdownExport.ToMarkdown(note.Text), write.Content);
        Assert.Equal("notas/Lista.md", plan.Manifest.Files[note.Id.ToString()].Path);
    }

    [Fact]
    public void WithNothingChanged_NothingIsWrittenTheSecondTime()
    {
        var folder = new FakeFolder();
        var notes = new[] { N("Uno"), N("Dos") };
        var first = Run(folder, new ExportManifest(), notes);

        var second = Run(folder, first.Manifest, notes);

        Assert.Empty(second.Actions);
    }

    [Fact]
    public void AnEditedNote_RewritesItsOwnFileUnderTheSameName()
    {
        var folder = new FakeFolder();
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Titulo\nuno", id: id)]);

        var second = Run(folder, first.Manifest, [N("Titulo nuevo\nuno", id: id)]);

        var write = Assert.IsType<ExportAction.Write>(Assert.Single(second.Actions));
        Assert.Equal("notas/Titulo.md", write.Path);   // el nombre es estable aunque cambie el título
        Assert.Contains("Titulo nuevo", folder.Files["notas/Titulo.md"]);
    }

    [Fact]
    public void TwoNotesWithTheSameTitle_GetDifferentNames()
    {
        var folder = new FakeFolder();

        var plan = Run(folder, new ExportManifest(), [N("Igual"), N("igual")]);

        Assert.Equal(2, plan.Actions.OfType<ExportAction.Write>().Select(w => w.Path.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public void ProtectedAndTrashedNotes_AreNeverWritten()
    {
        var folder = new FakeFolder();

        var plan = Run(folder, new ExportManifest(), [N("Secreto", isProtected: true), N("Basura", NoteState.Trashed), N("Normal")]);

        Assert.Equal(["notas/Normal.md"], plan.Actions.OfType<ExportAction.Write>().Select(w => w.Path));
    }

    [Fact]
    public void ArchivedNotes_GoToArchivadas_OnlyIfAsked()
    {
        var note = N("Vieja", NoteState.Archived);

        var with = Run(new FakeFolder(), new ExportManifest(), [note], archived: true);
        var without = Run(new FakeFolder(), new ExportManifest(), [note], archived: false);

        Assert.Equal("archivadas/Vieja.md", Assert.IsType<ExportAction.Write>(Assert.Single(with.Actions)).Path);
        Assert.Empty(without.Actions);
    }

    [Fact]
    public void ArchivingANote_MovesItsFileAcrossFoldersInsteadOfDuplicatingIt()
    {
        var folder = new FakeFolder();
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Nota", id: id)]);

        var second = Run(folder, first.Manifest, [N("Nota", NoteState.Archived, id: id)]);

        Assert.Contains(second.Actions, a => a is ExportAction.Move { From: "notas/Nota.md", To: "archivadas/Nota.md" });
        Assert.False(folder.Files.ContainsKey("notas/Nota.md"));
        Assert.True(folder.Files.ContainsKey("archivadas/Nota.md"));
    }

    [Fact]
    public void ADeletedOrTrashedNote_IsMovedToEliminadas_NeverDeleted()
    {
        var folder = new FakeFolder();
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Adios", id: id)]);

        var second = Run(folder, first.Manifest, []);

        Assert.Contains(second.Actions, a => a is ExportAction.Move { From: "notas/Adios.md", To: "_eliminadas/Adios.md" });
        Assert.DoesNotContain(second.Actions, a => a.GetType().Name.Contains("Delete"));
        Assert.Empty(second.Manifest.Files);
        Assert.True(folder.Files.ContainsKey("_eliminadas/Adios.md"));
    }

    [Fact]
    public void ANoteThatBecameProtected_LeavesTheNotesFolder()
    {
        var folder = new FakeFolder();
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Antes", id: id)]);

        var second = Run(folder, first.Manifest, [N("Antes", isProtected: true, id: id)]);

        Assert.False(folder.Files.ContainsKey("notas/Antes.md"));
        Assert.True(folder.Files.ContainsKey("_eliminadas/Antes.md"));
    }

    [Fact]
    public void AFileTheUserEditedByHand_IsNotOverwritten_TheCopyGoesBesideIt()
    {
        var folder = new FakeFolder();
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Mia\nuno", id: id)]);
        folder.Files["notas/Mia.md"] = "editado a mano";

        var second = Run(folder, first.Manifest, [N("Mia\ndos", id: id)]);

        Assert.Equal("editado a mano", folder.Files["notas/Mia.md"]);
        var side = Assert.IsType<ExportAction.Write>(Assert.Single(second.Actions));
        Assert.Equal("notas/Mia (Aldune).md", side.Path);
        Assert.Contains("dos", side.Content);
    }

    [Fact]
    public void TheBesideCopy_IsNotRewrittenWhileNothingChanges()
    {
        var folder = new FakeFolder();
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Mia\nuno", id: id)]);
        folder.Files["notas/Mia.md"] = "editado a mano";
        var notes = new[] { N("Mia\ndos", id: id) };
        var second = Run(folder, first.Manifest, notes);

        var third = Run(folder, second.Manifest, notes);

        Assert.Empty(third.Actions);
    }

    [Fact]
    public void AFileThatDisappeared_IsWrittenAgain()
    {
        var folder = new FakeFolder();
        var notes = new[] { N("Vuelve") };
        var first = Run(folder, new ExportManifest(), notes);
        folder.Files.Clear();

        var second = Run(folder, first.Manifest, notes);

        Assert.Equal("notas/Vuelve.md", Assert.IsType<ExportAction.Write>(Assert.Single(second.Actions)).Path);
    }

    [Fact]
    public void AForeignFileWithTheSameName_IsNeverOverwritten()
    {
        var folder = new FakeFolder();
        folder.Files["notas/Lista.md"] = "de otra herramienta";

        var plan = Run(folder, new ExportManifest(), [N("Lista")]);

        Assert.Equal("de otra herramienta", folder.Files["notas/Lista.md"]);
        Assert.Equal("notas/Lista (Aldune).md", Assert.IsType<ExportAction.Write>(Assert.Single(plan.Actions)).Path);
    }

    [Fact]
    public void MovingToEliminadas_NeverClobbersAnEarlierOne()
    {
        var folder = new FakeFolder();
        folder.Files["_eliminadas/Adios.md"] = "anterior";
        var id = Guid.NewGuid();
        var first = Run(folder, new ExportManifest(), [N("Adios", id: id)]);

        Run(folder, first.Manifest, []);

        Assert.Equal("anterior", folder.Files["_eliminadas/Adios.md"]);
        Assert.True(folder.Files.ContainsKey("_eliminadas/Adios (2).md"));
    }

    [Fact]
    public void TheManifest_RoundTripsThroughJson_AndABrokenOneIsEmpty()
    {
        var manifest = new ExportManifest();
        manifest.Files["abc"] = new ExportEntry("notas/A.md", "123");

        var back = ExportManifest.FromJson(manifest.ToJson());

        Assert.Equal("notas/A.md", back.Files["abc"].Path);
        Assert.Equal("123", back.Files["abc"].Hash);
        Assert.Empty(ExportManifest.FromJson("{ esto no es json").Files);
        Assert.Empty(ExportManifest.FromJson(null).Files);
    }

    [Fact]
    public void ManifestPathsThatEscapeTheFolder_AreIgnored()
    {
        var manifest = new ExportManifest();
        manifest.Files["x"] = new ExportEntry("../fuera.md", "1");
        manifest.Files["y"] = new ExportEntry("C:/windows/x.md", "1");
        var folder = new FakeFolder();

        var plan = Run(folder, manifest, []);

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Manifest.Files);
    }
}

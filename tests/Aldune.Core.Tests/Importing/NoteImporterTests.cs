using Aldune.Core.Importing;

namespace Aldune.Core.Tests.Importing;

public sealed class NoteImporterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-importer-{Guid.NewGuid():N}");
    private readonly NotesRepository _repo;
    private readonly ContentCipher _cipher;
    private static readonly ImportColorPolicy Policy = new(NoteThemes.BuiltIn[0], NoteTone.Light, NoteColorAssignment.RotateAvoidNeighbors, null);

    public NoteImporterTests()
    {
        Directory.CreateDirectory(_dir);
        _cipher = new ContentCipher(new byte[32]);
        _repo = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "t.db")), _cipher);
    }

    public void Dispose()
    {
        _cipher.Dispose();
        TestPools.ReleaseDirectory(_dir);
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static ImportedNote N(string text, ImportHue? hue = null, NoteState state = NoteState.Active, bool pinned = false,
        DateTimeOffset? created = null, DateTimeOffset? updated = null, params string[] tags) =>
        new(text, hue, tags, state, pinned, created, updated);

    private static ImportPreview P(params ImportedNote[] notes) => new() { Notes = notes };

    private IReadOnlyList<Note> All() =>
        [.. _repo.GetByState(NoteState.Active), .. _repo.GetByState(NoteState.Archived), .. _repo.GetByState(NoteState.Trashed)];

    [Fact]
    public void Run_CreatesNotes_WithOriginalDatesTagsStateAndPin()
    {
        var created = new DateTimeOffset(2022, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var updated = created.AddDays(3);
        var plan = NoteImporter.Plan(P(N("a\r\nbody", ImportHue.Green, NoteState.Archived, true, created, updated, "x", "y")), All());

        var result = NoteImporter.Run(plan, _repo, Policy);

        Assert.Equal(new ImportResult(1, 0), result);
        var note = Assert.Single(_repo.GetByState(NoteState.Archived));
        Assert.Equal("a\r\nbody", note.Text);
        Assert.Equal(created, note.CreatedAt);
        Assert.Equal(updated, note.UpdatedAt);
        Assert.True(note.IsPinnedToTop);
        Assert.Equal(new[] { "x", "y" }, note.Tags.OrderBy(t => t));
    }

    [Fact]
    public void Run_Twice_DoesNotDuplicate()
    {
        var created = new DateTimeOffset(2022, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var preview = P(N("same", null, created: created), N("other", null, created: created));
        NoteImporter.Run(NoteImporter.Plan(preview, All()), _repo, Policy);

        var plan2 = NoteImporter.Plan(preview, All());

        Assert.Empty(plan2.ToCreate);
        Assert.Equal(2, plan2.Duplicates);
        Assert.Equal(2, All().Count);
    }

    [Fact]
    public void SameTextWithAnotherOriginalDate_IsADifferentNote_ButWithoutDatesTheTextDecides()
    {
        var d1 = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        NoteImporter.Run(NoteImporter.Plan(P(N("t", created: d1)), All()), _repo, Policy);

        Assert.Single(NoteImporter.Plan(P(N("t", created: d1.AddDays(1))), All()).ToCreate);
        Assert.Empty(NoteImporter.Plan(P(N("t")), All()).ToCreate);
    }

    [Fact]
    public void DuplicatesInsideTheSameBatch_AreCollapsed_AndExistingNotesAreNeverTouched()
    {
        var mine = _repo.Create("mine", "#EBD38B", "primary");

        var plan = NoteImporter.Plan(P(N("mine"), N("dup"), N("dup")), All());

        Assert.Equal(new[] { "dup" }, plan.ToCreate.Select(n => n.Text));
        Assert.Equal(2, plan.Duplicates);
        NoteImporter.Run(plan, _repo, Policy);
        Assert.Equal("mine", _repo.GetById(mine.Id)!.Text);
        Assert.Equal(2, All().Count);
    }

    [Fact]
    public void TrashedNotes_GetAFreshBinClock_SoThePurgeDoesNotEatThemAtOnce()
    {
        var old = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

        NoteImporter.Run(NoteImporter.Plan(P(N("bin", state: NoteState.Trashed, created: old, updated: old)), All()), _repo, Policy, now);

        var note = Assert.Single(_repo.GetByState(NoteState.Trashed));
        Assert.Equal(old, note.CreatedAt);
        Assert.Equal(now, note.UpdatedAt);
    }

    [Fact]
    public void FutureAndMissingDates_AreClampedToNow()
    {
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

        NoteImporter.Run(NoteImporter.Plan(P(N("f", created: now.AddYears(5), updated: now.AddYears(6)), N("m")), All()), _repo, Policy, now);

        Assert.All(All(), n => { Assert.Equal(now, n.CreatedAt); Assert.Equal(now, n.UpdatedAt); });
    }

    [Fact]
    public void Colours_ComeFromTheActiveTheme_ByHue()
    {
        var plan = NoteImporter.Plan(P(N("y", ImportHue.Yellow), N("b", ImportHue.Blue), N("p", ImportHue.Pink)), All());

        NoteImporter.Run(plan, _repo, Policy);

        var byText = All().ToDictionary(n => n.Text, n => n.Color);
        var theme = Policy.Theme;
        Assert.All(byText.Values, c => Assert.Contains(c, theme.LightColors.Concat(theme.DarkColors)));
        Assert.Equal(3, byText.Values.Distinct().Count());
        Assert.Equal(ImportHue.Yellow, ImportHues.FromHex(byText["y"]));
        Assert.Equal(ImportHue.Blue, ImportHues.FromHex(byText["b"]));
    }

    [Fact]
    public void DarkTone_PicksDarkColours_AndNoHueStillGetsAThemeColour()
    {
        var dark = Policy with { Tone = NoteTone.Dark, Theme = NoteThemes.BuiltIn.First(t => t.Id == NoteThemes.SereneId) };

        NoteImporter.Run(NoteImporter.Plan(P(N("g", ImportHue.Green), N("none")), All()), _repo, dark);

        Assert.All(All(), n => Assert.True(NoteColorDerivation.IsDark(n.Color)));
    }

    [Fact]
    public void AnAchromaticTheme_FallsBackToTheNormalAssignment()
    {
        var gray = NoteThemes.BuiltIn.First(t => t.Id == NoteThemes.GrayId);

        NoteImporter.Run(NoteImporter.Plan(P(N("a", ImportHue.Pink)), All()), _repo, Policy with { Theme = gray });

        Assert.Contains(Assert.Single(All()).Color, gray.LightColors);
    }

    [Fact]
    public void ImportedNotes_AreReadyToSync()
    {
        NoteImporter.Run(NoteImporter.Plan(P(N("s")), All()), _repo, Policy);

        Assert.Single(_repo.GetAllForSync());
    }
}

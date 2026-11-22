namespace Aldune.Core.Tests;

public sealed class NoteMergeTests
{
    private static readonly Guid Id = Guid.Parse("00000001-0000-4000-8000-000000000000");
    private static readonly DateTimeOffset T0 = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private static Note Make(string text, int minutes = 0, string color = "#EBD38B", NoteState state = NoteState.Active,
        string[]? tags = null, bool pinned = false, CompletedTasksPolicy policy = CompletedTasksPolicy.Default,
        double? dock = null, bool isProtected = false) => new()
    {
        Id = Id, Text = text, Color = color, CreatedAt = T0, UpdatedAt = T0.AddMinutes(minutes), State = state,
        ScreenOrigin = "primary", Tags = tags ?? Array.Empty<string>(), IsPinnedToTop = pinned,
        CompletedTasks = policy, DockPosition = dock, IsProtected = isProtected
    };

    [Fact]
    public void TextMergesAndColourComesFromTheOnlySideThatChangedIt()
    {
        var baseNote = Make("compra\npan");
        var local = Make("compra\npan\nleche", minutes: 1);
        var remote = Make("compra\npan", minutes: 2, color: "#C2D4FF");

        var result = NoteMerge.Merge(baseNote, local, "a", remote, "b");

        Assert.False(result.Conflict);
        Assert.Equal("compra\npan\nleche", result.Merged!.Text);
        Assert.Equal("#C2D4FF", result.Merged.Color);
    }

    [Fact]
    public void BothChangedColourTakesTheNewerWithoutConflict()
    {
        var baseNote = Make("x");
        var result = NoteMerge.Merge(baseNote, Make("x", 1, color: "#111111"), "a", Make("x", 2, color: "#222222"), "b");
        Assert.False(result.Conflict);
        Assert.Equal("#222222", result.Merged!.Color);
    }

    [Fact]
    public void TagsAreSetMergedAgainstTheBase()
    {
        var baseNote = Make("x", tags: new[] { "a", "b" });
        var local = Make("x", 1, tags: new[] { "a", "b", "c" });
        var remote = Make("x", 2, tags: new[] { "a" });

        var result = NoteMerge.Merge(baseNote, local, "a", remote, "b");

        Assert.False(result.Conflict);
        Assert.Equal(new[] { "a", "c" }, result.Merged!.Tags.OrderBy(t => t, StringComparer.Ordinal));
    }

    [Fact]
    public void TrashedOnOneSideAndTextEditedOnTheOtherIsAConflict()
    {
        var baseNote = Make("a");
        var result = NoteMerge.Merge(baseNote, Make("a\nb", 1), "a", Make("a", 2, state: NoteState.Trashed), "b");
        Assert.True(result.Conflict);
        Assert.Null(result.Merged);
    }

    [Fact]
    public void ArchivedOnOneSideAndTextEditedOnTheOtherMerges()
    {
        var baseNote = Make("a");
        var result = NoteMerge.Merge(baseNote, Make("a\nb", 1), "a", Make("a", 2, state: NoteState.Archived), "b");
        Assert.False(result.Conflict);
        Assert.Equal(NoteState.Archived, result.Merged!.State);
        Assert.Equal("a\nb", result.Merged.Text);
    }

    [Fact]
    public void ProtectedNotesAreNeverMerged()
    {
        var baseNote = Make("a");
        Assert.True(NoteMerge.Merge(baseNote, Make("a\nb", 1, isProtected: true), "a", Make("a\nc", 2), "b").Conflict);
        Assert.True(NoteMerge.Merge(baseNote, Make("a\nb", 1), "a", Make("a\nc", 2, isProtected: true), "b").Conflict);
    }

    [Fact]
    public void SameLineChangedDifferentlyIsAConflict()
    {
        var baseNote = Make("a\nb");
        Assert.True(NoteMerge.Merge(baseNote, Make("a\nB1", 1), "a", Make("a\nB2", 2), "b").Conflict);
    }

    [Fact]
    public void SymmetricWhenSidesAreSwapped()
    {
        var baseNote = Make("a\nb", tags: new[] { "t" });
        var local = Make("a\nb\nx", 1, color: "#111111", tags: new[] { "t", "u" });
        var remote = Make("y\na\nb", 2, color: "#222222", pinned: true);

        var ab = NoteMerge.Merge(baseNote, local, "dev-a", remote, "dev-b");
        var ba = NoteMerge.Merge(baseNote, remote, "dev-b", local, "dev-a");

        Assert.False(ab.Conflict);
        Assert.True(NoteEquivalence.SameContent(ab.Merged, ba.Merged));
    }

    [Fact]
    public void PinnedPolicyAndDockPositionFollowTheSingleChangingSide()
    {
        var baseNote = Make("x", dock: 1.0);
        var local = Make("x", 1, pinned: true, policy: CompletedTasksPolicy.KeepInPlace);
        var remote = Make("x", 2, dock: 2.5);

        var result = NoteMerge.Merge(baseNote, local, "a", remote, "b");

        Assert.False(result.Conflict);
        Assert.True(result.Merged!.IsPinnedToTop);
        Assert.Equal(CompletedTasksPolicy.KeepInPlace, result.Merged.CompletedTasks);
        Assert.Equal(2.5, result.Merged.DockPosition);
    }
}

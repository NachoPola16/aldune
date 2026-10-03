using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteSyncSignalTests
{
    private static readonly DateTimeOffset Edited = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static Note Make(params string[] tags) => new()
    {
        Id = Guid.NewGuid(), Text = "t", Color = "#EBD38B", CreatedAt = Edited, UpdatedAt = Edited,
        State = NoteState.Active, ScreenOrigin = "primary", Tags = tags,
    };

    private static AppSettings Syncing() => new() { SyncEnabled = true };

    [Fact]
    public void WithoutSync_IsHidden()
    {
        Assert.Equal(SyncSignalState.Hidden, NoteSyncSignal.For(Make(), new AppSettings(), null, false));
    }

    [Fact]
    public void BaseMatchesTheNoteDate_IsSynced()
    {
        Assert.Equal(SyncSignalState.Synced, NoteSyncSignal.For(Make(), Syncing(), new SyncBaseVersion(Edited, "a"), false));
    }

    [Fact]
    public void NoBaseOrAnOlderBase_IsPending()
    {
        Assert.Equal(SyncSignalState.Pending, NoteSyncSignal.For(Make(), Syncing(), null, false));
        Assert.Equal(SyncSignalState.Pending, NoteSyncSignal.For(Make(), Syncing(), new SyncBaseVersion(Edited.AddMinutes(-1), "a"), false));
    }

    [Fact]
    public void InTheConflictQueue_IsConflict_AndGoesBackWhenItLeaves()
    {
        var note = Make();
        var synced = new SyncBaseVersion(Edited, "a");
        Assert.Equal(SyncSignalState.Conflict, NoteSyncSignal.For(note, Syncing(), synced, true));
        Assert.Equal(SyncSignalState.Synced, NoteSyncSignal.For(note, Syncing(), synced, false));
    }

    [Fact]
    public void OutsideTheSelectiveScope_IsExcluded()
    {
        var bySelection = new AppSettings { SyncEnabled = true, SyncScope = SyncScopeKind.SelectedNotes };
        var byTag = new AppSettings { SyncEnabled = true, SyncScope = SyncScopeKind.Tag, SyncTag = "uni" };

        Assert.Equal(SyncSignalState.Excluded, NoteSyncSignal.For(Make(), bySelection, null, false));
        Assert.Equal(SyncSignalState.Excluded, NoteSyncSignal.For(Make("casa"), byTag, null, false));
        Assert.Equal(SyncSignalState.Pending, NoteSyncSignal.For(Make("UNI"), byTag, null, false));
    }

    [Fact]
    public void Glyph_IsEmptyOnlyWhenHidden()
    {
        Assert.Equal("", NoteSyncSignal.Glyph(SyncSignalState.Hidden));
        Assert.Equal("▂▄▆█", NoteSyncSignal.Glyph(SyncSignalState.Synced));
        Assert.All(new[] { SyncSignalState.Pending, SyncSignalState.Conflict, SyncSignalState.Excluded },
            state => Assert.NotEqual("", NoteSyncSignal.Glyph(state)));
    }
}

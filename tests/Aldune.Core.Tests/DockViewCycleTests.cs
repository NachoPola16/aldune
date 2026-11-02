namespace Aldune.Core.Tests;

/// <summary>Cambiar de vista del dock con la rueda: Activas → cada etiqueta con notas → Archivadas → Papelera → Todas, saltando las vacías y las que el usuario quita de la rueda.</summary>
public sealed class DockViewCycleTests
{
    private static DockViewCycle.Counts Counts(int active = 3, int archived = 2, int trashed = 1, params (string Tag, int N)[] tags) =>
        new(active, archived, trashed, tags.ToDictionary(t => t.Tag, t => t.N, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void ForwardWalksActiveTagsArchivedTrashAllAndWrapsAround()
    {
        var counts = Counts(3, 2, 1, ("casa", 2), ("trabajo", 1));
        var order = new List<(DockViewKind, string?)>();
        (DockViewKind view, string? tag) = (DockViewKind.Active, null);
        for (int i = 0; i < 7; i++)
        {
            (view, tag) = DockViewCycle.Next(view, tag, counts, +1);
            order.Add((view, tag));
        }

        Assert.Equal(new (DockViewKind, string?)[]
        {
            (DockViewKind.Tag, "casa"), (DockViewKind.Tag, "trabajo"), (DockViewKind.Archived, null),
            (DockViewKind.Trashed, null), (DockViewKind.All, null), (DockViewKind.Active, null), (DockViewKind.Tag, "casa")
        }, order);
    }

    [Fact]
    public void ATagTheUserLeftOutIsNeverAStop_ButTheOthersAre()
    {
        var counts = Counts(3, 0, 0, ("casa", 2), ("secreta", 1), ("trabajo", 1));
        var stops = new DockViewCycle.Stops(SkipTags: ["Secreta"]);
        var seen = new List<string?>();
        (DockViewKind view, string? tag) = (DockViewKind.Active, null);
        for (int i = 0; i < 4; i++)
        {
            (view, tag) = DockViewCycle.Next(view, tag, counts, +1, stops);
            seen.Add(view == DockViewKind.Tag ? tag : view.ToString());
        }
        Assert.Equal(["casa", "trabajo", "Active", "casa"], seen);
    }

    [Fact]
    public void ALeftOutTagStillWorksFromWhereItWasChosenByHand()
    {
        var counts = Counts(3, 0, 0, ("casa", 2), ("secreta", 1), ("trabajo", 1));
        var stops = new DockViewCycle.Stops(SkipTags: ["secreta"]);
        Assert.Equal((DockViewKind.Tag, "trabajo"), DockViewCycle.Next(DockViewKind.Tag, "secreta", counts, +1, stops));
        Assert.Equal((DockViewKind.Tag, "casa"), DockViewCycle.Next(DockViewKind.Tag, "secreta", counts, -1, stops));
    }

    [Fact]
    public void ForwardFollowsTheUsersOwnTagOrder()
    {
        var counts = new DockViewCycle.Counts(3, 0, 0,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["casa"] = 2, ["trabajo"] = 1, ["ideas"] = 1 }, new[] { "trabajo", "ideas" });
        var (view, tag) = (DockViewKind.Active, (string?)null);
        var seen = new List<string?>();
        for (int i = 0; i < 3; i++) { (view, tag) = DockViewCycle.Next(view, tag, counts, +1); seen.Add(tag); }
        Assert.Equal(new string?[] { "trabajo", "ideas", "casa" }, seen);
    }

    [Fact]
    public void BackwardIsTheExactReverse()
    {
        var counts = Counts(3, 2, 1, ("casa", 2), ("trabajo", 1));

        Assert.Equal((DockViewKind.All, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, -1));
        Assert.Equal((DockViewKind.Tag, (string?)"trabajo"), DockViewCycle.Next(DockViewKind.Archived, null, counts, -1));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Tag, "casa", counts, -1));
    }

    [Fact]
    public void EmptyTagsArchivedAndTrashAreSkipped()
    {
        var counts = Counts(active: 4, archived: 0, trashed: 0, ("casa", 0), ("trabajo", 2));

        Assert.Equal((DockViewKind.Tag, (string?)"trabajo"), DockViewCycle.Next(DockViewKind.Active, null, counts, +1));
        // With nothing archived or trashed those two views, and «All» (which would equal Active), are not stops.
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Tag, "trabajo", counts, +1));
        Assert.Equal((DockViewKind.Tag, (string?)"trabajo"), DockViewCycle.Next(DockViewKind.Active, null, counts, -1));
    }

    [Fact]
    public void OnlyTheNonEmptyOnesAreStops()
    {
        var counts = Counts(active: 4, archived: 2, trashed: 0);

        Assert.Equal((DockViewKind.Archived, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, +1));
        // Trash is empty: after Archived comes «All» (it differs from Active because something is archived).
        Assert.Equal((DockViewKind.All, (string?)null), DockViewCycle.Next(DockViewKind.Archived, null, counts, +1));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.All, null, counts, +1));
    }

    [Fact]
    public void ViewsTheUserLeftOutOfTheWheelAreSkippedEvenWhenTheyHaveNotes()
    {
        var counts = Counts(active: 3, archived: 2, trashed: 5);
        var noTrash = new DockViewCycle.Stops(Trash: false);

        Assert.Equal((DockViewKind.Archived, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, +1, noTrash));
        Assert.Equal((DockViewKind.All, (string?)null), DockViewCycle.Next(DockViewKind.Archived, null, counts, +1, noTrash));
        Assert.Equal((DockViewKind.Archived, (string?)null), DockViewCycle.Next(DockViewKind.All, null, counts, -1, noTrash));
    }

    [Fact]
    public void TagsLeftOutOfTheWheelAreSkippedAndOnlyTheViewsRemain()
    {
        var counts = Counts(active: 3, archived: 2, trashed: 5, ("casa", 2), ("trabajo", 1));
        var noTags = new DockViewCycle.Stops(Tags: false);

        Assert.Equal((DockViewKind.Archived, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, +1, noTags));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.All, null, counts, +1, noTags));
        Assert.Equal((DockViewKind.All, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, -1, noTags));
    }

    [Fact]
    public void WithOnlyTagsInTheWheelItGoesRoundTheTagsAndBackToActive()
    {
        var counts = Counts(active: 3, archived: 2, trashed: 5, ("casa", 2), ("trabajo", 1));
        var onlyTags = new DockViewCycle.Stops(Archived: false, Trash: false, All: false);

        Assert.Equal((DockViewKind.Tag, (string?)"casa"), DockViewCycle.Next(DockViewKind.Active, null, counts, +1, onlyTags));
        Assert.Equal((DockViewKind.Tag, (string?)"trabajo"), DockViewCycle.Next(DockViewKind.Tag, "casa", counts, +1, onlyTags));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Tag, "trabajo", counts, +1, onlyTags));
    }

    [Fact]
    public void FromAViewOutsideTheWheelTheWheelContinuesInTheNormalOrder()
    {
        // The user clicked «Trash» on purpose, but it is not in the wheel: turning forward goes on to «All», backward to «Archived».
        var counts = Counts(active: 3, archived: 2, trashed: 5);
        var noTrash = new DockViewCycle.Stops(Trash: false);

        Assert.Equal((DockViewKind.All, (string?)null), DockViewCycle.Next(DockViewKind.Trashed, null, counts, +1, noTrash));
        Assert.Equal((DockViewKind.Archived, (string?)null), DockViewCycle.Next(DockViewKind.Trashed, null, counts, -1, noTrash));
    }

    [Fact]
    public void FromAnEmptyViewTheWheelStillMovesOn()
    {
        // Looking at an empty Trash (clicked on purpose): the wheel goes to something that has notes.
        var counts = Counts(active: 3, archived: 0, trashed: 0, ("casa", 1));

        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Trashed, null, counts, +1));
        Assert.Equal((DockViewKind.Tag, (string?)"casa"), DockViewCycle.Next(DockViewKind.Trashed, null, counts, -1));
    }

    [Fact]
    public void LeavingEverythingOutStillLeavesActive()
    {
        var counts = Counts(active: 3, archived: 2, trashed: 5);
        var none = new DockViewCycle.Stops(Archived: false, Trash: false, All: false);

        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Archived, null, counts, +1, none));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, +1, none));
    }

    [Fact]
    public void ActiveIsAlwaysReachableEvenWhenThereAreNoActiveNotes()
    {
        var counts = Counts(active: 0, archived: 1, trashed: 0);

        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.All, null, counts, +1));
    }

    [Fact]
    public void ATagThatNoLongerExistsContinuesFromWhereTheTagsWould()
    {
        var counts = Counts(3, 2, 1, ("casa", 1));

        // La etiqueta activa ya no tiene notas: avanzar sigue con lo que viene después de las etiquetas.
        Assert.Equal((DockViewKind.Archived, (string?)null), DockViewCycle.Next(DockViewKind.Tag, "borrada", counts, +1));
    }

    [Fact]
    public void ViewLabelNamesWhereYouLanded()
    {
        Assert.Equal("#casa", DockViewCycle.Label(DockViewKind.Tag, "casa", "Active", "Archived", "Trash", "All"));
        Assert.Equal("Trash", DockViewCycle.Label(DockViewKind.Trashed, null, "Active", "Archived", "Trash", "All"));
        Assert.Equal("All", DockViewCycle.Label(DockViewKind.All, null, "Active", "Archived", "Trash", "All"));
    }
}

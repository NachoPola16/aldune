namespace Aldune.Core.Tests;

/// <summary>Cambiar de vista del dock con la rueda: Activas → cada etiqueta con notas → Archivadas → Papelera → Todas, saltando las vacías.</summary>
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
    public void BackwardIsTheExactReverse()
    {
        var counts = Counts(3, 2, 1, ("casa", 2), ("trabajo", 1));

        Assert.Equal((DockViewKind.All, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, -1));
        Assert.Equal((DockViewKind.Tag, (string?)"trabajo"), DockViewCycle.Next(DockViewKind.Archived, null, counts, -1));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Tag, "casa", counts, -1));
    }

    [Fact]
    public void EmptyViewsAreSkipped()
    {
        var counts = Counts(active: 4, archived: 0, trashed: 0, ("casa", 0), ("trabajo", 2));

        Assert.Equal((DockViewKind.Tag, (string?)"trabajo"), DockViewCycle.Next(DockViewKind.Active, null, counts, +1));
        // Sin archivadas ni papelera, «Todas» no aporta nada y se salta.
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Tag, "trabajo", counts, +1));
    }

    [Fact]
    public void ActiveIsAlwaysReachableEvenWhenThereAreNoActiveNotes()
    {
        var counts = Counts(active: 0, archived: 1, trashed: 0);

        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.All, null, counts, +1));
    }

    [Fact]
    public void WithNothingElseTheViewStaysOnActive()
    {
        var counts = Counts(active: 0, archived: 0, trashed: 0);

        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, +1));
        Assert.Equal((DockViewKind.Active, (string?)null), DockViewCycle.Next(DockViewKind.Active, null, counts, -1));
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

namespace Aldune.Core.Tests;

public class NoteListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 15, 0, 0, TimeSpan.FromHours(2));

    private static Note MakeNote(string text, DateTimeOffset updatedAt, DateTimeOffset? createdAt = null, bool isProtected = false) => new()
    {
        Id = Guid.NewGuid(),
        Text = text,
        Color = "#EBD38B",
        CreatedAt = createdAt ?? updatedAt,
        UpdatedAt = updatedAt,
        State = NoteState.Active,
        ScreenOrigin = "primary",
        IsProtected = isProtected,
    };

    [Fact]
    public void DockOrder_KeepsTheOrderItCameIn()
    {
        var notes = new[] { MakeNote("b", Now), MakeNote("a", Now.AddDays(-1)), MakeNote("c", Now.AddDays(-3)) };

        Assert.Equal(notes, NoteListing.Sort(notes, n => n, NoteListOrder.Dock));
    }

    [Fact]
    public void Newest_AndOldest_SortByLastChange()
    {
        var old = MakeNote("old", Now.AddDays(-10));
        var middle = MakeNote("middle", Now.AddDays(-2));
        var recent = MakeNote("recent", Now.AddMinutes(-5));
        var notes = new[] { middle, old, recent };

        Assert.Equal(new[] { recent, middle, old }, NoteListing.Sort(notes, n => n, NoteListOrder.Newest));
        Assert.Equal(new[] { old, middle, recent }, NoteListing.Sort(notes, n => n, NoteListOrder.Oldest));
    }

    // Por el título que se ve (la primera línea), sin distinguir mayúsculas ni tildes de orden; las
    // protegidas no enseñan su título, así que van al final en vez de ordenarse por un texto oculto.
    [Fact]
    public void Title_SortsByTheVisibleTitleAndLeavesProtectedNotesLast()
    {
        var zebra = MakeNote("zebra\nbody", Now);
        var apple = MakeNote("Apple", Now);
        var secret = MakeNote(string.Empty, Now, isProtected: true);
        var banana = MakeNote("banana", Now);

        Assert.Equal(new[] { apple, banana, zebra, secret },
            NoteListing.Sort(new[] { zebra, secret, apple, banana }, n => n, NoteListOrder.Title));
    }

    [Fact]
    public void Sort_WorksOverWrappers()
    {
        var old = MakeNote("old", Now.AddDays(-10));
        var recent = MakeNote("recent", Now);
        var rows = new[] { (Note: old, Tag: 1), (Note: recent, Tag: 2) };

        Assert.Equal(new[] { 2, 1 }, NoteListing.Sort(rows, r => r.Note, NoteListOrder.Newest).Select(r => r.Tag));
    }

    [Theory]
    [InlineData(0, 0, NoteDateKind.Today)]         // ahora mismo
    [InlineData(-14, 30, NoteDateKind.Today)]      // 01:30 de hoy (hora local)
    [InlineData(-16, 30, NoteDateKind.ThisYear)]   // 23:30 de ayer
    [InlineData(-24 * 200, 0, NoteDateKind.ThisYear)]
    [InlineData(-24 * 300, 0, NoteDateKind.Older)] // diciembre del año pasado
    public void DateKind_BucketsByLocalCalendarDay(int hoursAgo, int extraMinutes, NoteDateKind expected)
    {
        var updated = Now.AddHours(hoursAgo).AddMinutes(extraMinutes);

        Assert.Equal(expected, NoteListing.DateKind(updated, Now));
    }

    // La fecha guardada va en UTC: el día que cuenta es el de quien mira, no el de Greenwich.
    [Fact]
    public void DateKind_ConvertsUtcToTheViewersOffset()
    {
        var updatedUtc = new DateTimeOffset(2026, 9, 26, 23, 30, 0, TimeSpan.Zero); // 01:30 del 27 en +2

        Assert.Equal(NoteDateKind.Today, NoteListing.DateKind(updatedUtc, Now));
    }
}

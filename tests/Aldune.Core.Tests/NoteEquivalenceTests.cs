using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteEquivalenceTests
{
    private static Note Make(string text = "texto", string color = "#EBD38B", NoteState state = NoteState.Active,
        string[]? tags = null, double? dock = null, DateTimeOffset? updatedAt = null) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Text = text,
        Color = color,
        CreatedAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        UpdatedAt = updatedAt ?? new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
        State = state,
        ScreenOrigin = "primary",
        Tags = tags ?? [],
        DockPosition = dock,
    };

    [Fact]
    public void SameContent_DifferentDatesAndColorCase_AreEquivalent()
    {
        var a = Make(color: "#ebd38b");
        var b = Make(color: "#EBD38B", updatedAt: new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

        Assert.True(NoteEquivalence.SameContent(a, b));
    }

    [Fact]
    public void SameContent_TagsInAnotherOrder_AreEquivalent()
    {
        Assert.True(NoteEquivalence.SameContent(Make(tags: ["uni", "casa"]), Make(tags: ["casa", "uni"])));
    }

    [Theory]
    [InlineData("otro texto", "#EBD38B", NoteState.Active)]
    [InlineData("texto", "#C2D4FF", NoteState.Active)]
    [InlineData("texto", "#EBD38B", NoteState.Archived)]
    public void SameContent_AnyRealDifference_IsNotEquivalent(string text, string color, NoteState state)
    {
        Assert.False(NoteEquivalence.SameContent(Make(), Make(text, color, state)));
    }

    [Fact]
    public void SameContent_DifferentTagsOrDockPosition_IsNotEquivalent()
    {
        Assert.False(NoteEquivalence.SameContent(Make(tags: ["uni"]), Make(tags: ["casa"])));
        Assert.False(NoteEquivalence.SameContent(Make(dock: 1), Make(dock: 2)));
    }

    [Fact]
    public void SameContent_WithAMissingSide_IsNotEquivalent()
    {
        // Un borrado (sin nota) frente a una edición nunca es "lo mismo".
        Assert.False(NoteEquivalence.SameContent(Make(), null));
        Assert.False(NoteEquivalence.SameContent(null, null));
    }
}

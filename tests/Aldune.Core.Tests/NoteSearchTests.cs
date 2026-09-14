using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteSearchTests
{
    [Fact]
    public void Matches_EmptyQuery_ReturnsTrue()
    {
        Assert.True(NoteSearch.Matches("Groceries", ""));
    }

    [Fact]
    public void Matches_WhitespaceOnlyQuery_ReturnsTrue()
    {
        Assert.True(NoteSearch.Matches("Groceries", "   "));
    }

    [Fact]
    public void Matches_QueryFoundAtStart_ReturnsTrue()
    {
        Assert.True(NoteSearch.Matches("Groceries\nmilk, eggs", "Groceries"));
    }

    [Fact]
    public void Matches_QueryFoundInMiddle_ReturnsTrue()
    {
        Assert.True(NoteSearch.Matches("Office\n- understand all the apis\n- create tickets", "apis"));
    }

    [Fact]
    public void Matches_QueryNotFound_ReturnsFalse()
    {
        Assert.False(NoteSearch.Matches("Groceries\nmilk, eggs", "invoice"));
    }

    [Fact]
    public void Matches_DifferentCase_StillMatches()
    {
        Assert.True(NoteSearch.Matches("Groceries", "groceries"));
    }

    [Fact]
    public void Matches_QueryWithLeadingTrailingWhitespace_IsTrimmedBeforeMatching()
    {
        Assert.True(NoteSearch.Matches("Groceries", "  Groceries  "));
    }

    [Fact]
    public void Matches_AccentedText_CaseInsensitiveAccentSensitive()
    {
        Assert.True(NoteSearch.Matches("Café con leche", "CAFÉ"));
        Assert.False(NoteSearch.Matches("Café con leche", "cafe"));
    }
}

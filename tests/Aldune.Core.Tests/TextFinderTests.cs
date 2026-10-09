using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class TextFinderTests
{
    [Fact]
    public void FindAll_IgnoresCase_AndReturnsEveryStart()
    {
        Assert.Equal([0, 10], TextFinder.FindAll("Pan y mas PAN", "pan"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("zzz")]
    public void FindAll_NoMatchOrEmptyQuery_IsEmpty(string query)
    {
        Assert.Empty(TextFinder.FindAll("Pan y mas pan", query));
    }

    [Fact]
    public void FindAll_Matches_DoNotOverlap()
    {
        Assert.Equal([0, 2], TextFinder.FindAll("aaaa", "aa"));
    }

    [Fact]
    public void FindAll_KeepsSpacesInTheQuery()
    {
        Assert.Equal([4], TextFinder.FindAll("a b  c", " c"));
    }

    [Theory]
    [InlineData(0, true, 0)]   // el cursor está justo en un resultado: ese
    [InlineData(1, true, 1)]   // entre dos: el siguiente
    [InlineData(11, true, 0)]  // pasado el último: vuelve al primero
    [InlineData(10, false, 1)] // hacia atrás: el anterior al cursor (el de 5)
    [InlineData(0, false, 2)]  // antes del primero: da la vuelta al último
    public void NextIndex_WrapsAround(int position, bool forward, int expected)
    {
        int[] matches = [0, 5, 10];
        Assert.Equal(expected, TextFinder.NextIndex(matches, position, forward));
    }

    [Fact]
    public void NextIndex_NoMatches_IsMinusOne()
    {
        Assert.Equal(-1, TextFinder.NextIndex([], 3, true));
    }
}

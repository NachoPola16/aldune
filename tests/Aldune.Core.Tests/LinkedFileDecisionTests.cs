using Xunit;

namespace Aldune.Core.Tests;

public class LinkedFileDecisionTests
{
    [Theory]
    [InlineData("A", "A", true, LinkedFileAction.Write)]
    [InlineData("A", "A", false, LinkedFileAction.None)]
    [InlineData("A", "B", false, LinkedFileAction.Reload)]
    [InlineData("A", "B", true, LinkedFileAction.Conflict)]
    [InlineData("A", null, false, LinkedFileAction.Unavailable)]
    [InlineData("A", null, true, LinkedFileAction.Unavailable)]
    // Sin huella conocida (recién vuelto a vincular con "Buscar…"): el disco cuenta como distinto.
    [InlineData(null, "B", false, LinkedFileAction.Reload)]
    [InlineData(null, "B", true, LinkedFileAction.Conflict)]
    public void Decide(string? known, string? current, bool pending, LinkedFileAction expected) =>
        Assert.Equal(expected, LinkedFileDecision.Decide(known, current, pending));
}

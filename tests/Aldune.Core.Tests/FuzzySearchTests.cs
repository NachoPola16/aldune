namespace Aldune.Core.Tests;

public class FuzzySearchTests
{
    [Theory]
    [InlineData("sincronizacion", "Sincronización")]
    [InlineData("SINCRONIZACIÓN", "sincronizacion")]
    [InlineData("tamano", "Tamaño del texto")]
    [InlineData("dock", "Posición del dock")]
    public void Matches_IgnoreCaseAndAccents(string query, string text) =>
        Assert.True(FuzzySearch.Score(query, text) >= FuzzySearch.MinimumScore, $"{query} / {text}");

    [Theory]
    [InlineData("sincornizacion", "Sincronización")]   // two letters swapped
    [InlineData("sincronizcion", "Sincronización")]    // a letter missing
    [InlineData("sincronizzacion", "Sincronización")]  // an extra letter
    [InlineData("notificasiones", "Notificaciones")]   // a different spelling of a sound
    [InlineData("shadow", "Shadows on the dock")]      // the singular of a word in the text
    [InlineData("colour", "Color theme")]              // another spelling of the word (1 letter)
    public void Matches_ToleratesTypos(string query, string text) =>
        Assert.True(FuzzySearch.Score(query, text) >= FuzzySearch.MinimumScore, $"{query} / {text}");

    [Theory]
    [InlineData("sinc", "Sincronización")]
    [InlineData("notif", "Notificaciones y sonidos")]
    [InlineData("dock pos", "Posición del dock")]
    [InlineData("sonidos notificaciones", "Notificaciones y sonidos")] // order does not matter
    public void Matches_PartialWordsAndAnyOrder(string query, string text) =>
        Assert.True(FuzzySearch.Score(query, text) >= FuzzySearch.MinimumScore, $"{query} / {text}");

    [Theory]
    [InlineData("zzz", "Sincronización")]
    [InlineData("sombra", "Posición del dock")]
    [InlineData("dock sonido", "Posición del dock")]   // every word of the query has to match something
    [InlineData("ab", "Posición del dock")]            // too short to be fuzzy: only real prefixes count
    public void DoesNotMatch_UnrelatedText(string query, string text) =>
        Assert.True(FuzzySearch.Score(query, text) < FuzzySearch.MinimumScore, $"{query} / {text}");

    [Fact]
    public void Score_RanksExactAboveTypoAbovePartial()
    {
        double exact = FuzzySearch.Score("sincronizacion", "Sincronización");
        double typo = FuzzySearch.Score("sincornizacion", "Sincronización");
        double partial = FuzzySearch.Score("sinc", "Sincronización");
        Assert.True(exact > typo, $"{exact} > {typo}");
        Assert.True(exact > partial, $"{exact} > {partial}");
    }

    [Fact]
    public void Rank_FiltersAndOrdersByScore_KeepingTheOriginalOrderOnTies()
    {
        var items = new[] { "Sincronización", "Sonidos", "Sincronizar ahora", "Aspecto" };

        var ranked = FuzzySearch.Rank(items, item => item, "sincroniz");

        Assert.Equal(new[] { "Sincronización", "Sincronizar ahora" }, ranked);
    }

    [Fact]
    public void Rank_WithAnEmptyQuery_ReturnsNothing() =>
        Assert.Empty(FuzzySearch.Rank(new[] { "a", "b" }, item => item, "  "));
}

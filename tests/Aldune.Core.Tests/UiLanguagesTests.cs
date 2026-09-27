namespace Aldune.Core.Tests;

public class UiLanguagesTests
{
    [Theory]
    [InlineData("es", "en", "es")]
    [InlineData("de", "es", "de")]
    [InlineData("fr", "en", "fr")]
    [InlineData("pt", "en", "pt")]
    [InlineData("en", "de", "en")]
    public void AnExplicitChoice_WinsOverWindows(string chosen, string windows, string expected) =>
        Assert.Equal(expected, UiLanguages.Resolve(chosen, windows));

    // Sin elección (o con una de una versión futura que esta no conoce), el de Windows si lo hay.
    [Theory]
    [InlineData(null, "de", "de")]
    [InlineData(null, "fr", "fr")]
    [InlineData(null, "pt", "pt")]
    [InlineData(null, "es", "es")]
    [InlineData(null, "it", "en")]
    [InlineData("xx", "fr", "fr")]
    public void WithoutAValidChoice_FollowsWindowsOrFallsBackToEnglish(string? chosen, string windows, string expected) =>
        Assert.Equal(expected, UiLanguages.Resolve(chosen, windows));

    // Cada idioma con su propio nombre y en orden alfabético de ese nombre: "Español" está siempre en
    // la E, sea cual sea el idioma de la interfaz. Es la convención de Windows, macOS y Android.
    [Fact]
    public void EveryLanguage_NamesItselfInItsOwnLanguage_SortedByThatName()
    {
        Assert.Equal(new[] { "de", "en", "es", "fr", "pt" }, UiLanguages.All.Select(l => l.Code));
        Assert.Equal(UiLanguages.All.Select(l => l.Name).Order(StringComparer.InvariantCultureIgnoreCase),
            UiLanguages.All.Select(l => l.Name));
        Assert.Equal("Português (Brasil)", UiLanguages.All.Single(l => l.Code == "pt").Name);
        Assert.Equal("pt-BR", UiLanguages.All.Single(l => l.Code == "pt").Culture);
    }
}

using System.Text.Json;
using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>«Tamaño del texto» de Ajustes: unos pocos pasos fijos, 100 % de fábrica, y el zoom de una nota (Ctrl + rueda).</summary>
public class TextScaleTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(115, 115)]
    [InlineData(130, 130)]
    [InlineData(150, 150)]
    [InlineData(0, 100)]       // dato roto: el de fábrica
    [InlineData(-30, 100)]
    [InlineData(9999, 100)]
    [InlineData(120, 115)]     // a mano en el fichero: el paso más cercano
    [InlineData(140, 130)]
    public void Normalize_KeepsAKnownStepOrFallsBackToTheNearest(int value, int expected) =>
        Assert.Equal(expected, TextScale.Normalize(value));

    [Fact]
    public void Factor_IsThePercentageAsAMultiplier() => Assert.Equal(1.3, TextScale.Factor(130), 3);

    [Fact]
    public void AnOldSettingsFile_LoadsAtOneHundredPercent()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{ \"dockEdge\": 1 }")!;
        Assert.Equal(100, settings.TextScalePercent);
    }

    [Theory]
    [InlineData(100, 1, 115)]
    [InlineData(150, 1, 150)]      // tope
    [InlineData(100, -1, 100)]     // suelo: la nota no se hace más pequeña que el 100 % del ajuste
    [InlineData(130, -1, 115)]
    public void Step_MovesOneNotchAndStopsAtTheEnds(int current, int direction, int expected) =>
        Assert.Equal(expected, TextScale.Step(current, direction));
}

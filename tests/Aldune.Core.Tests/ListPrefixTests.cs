using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ListPrefixTests
{
    [Theory]
    [InlineData("☐ comprar", 2, "comprar", 0)]
    [InlineData("☒ hecho", 2, "hecho", 0)]
    [InlineData("→ idea", 2, "idea", 0)]
    [InlineData("☐ ", 2, "", 0)]                      // tarea recién creada, aún vacía
    [InlineData("    ☐ con sangría", 6, "    con sangría", 4)] // la sangría se queda
    [InlineData("☐ comprar", 1, "comprar", 0)]         // entre el glifo y el espacio: también entero
    public void RemoveOnBackspace_RightAfterThePrefix_RemovesItWhole(string text, int caret, string expected, int expectedCaret)
    {
        var result = ListPrefix.RemoveOnBackspace(text, caret);

        Assert.Equal(expected, result!.Value.Text);
        Assert.Equal(expectedCaret, result.Value.Caret);
    }

    [Fact]
    public void RemoveOnBackspace_OnASecondLine_OnlyTouchesThatLine()
    {
        var text = "uno\r\n☐ dos";
        var result = ListPrefix.RemoveOnBackspace(text, 7);

        Assert.Equal("uno\r\ndos", result!.Value.Text);
        Assert.Equal(5, result.Value.Caret);
    }

    [Theory]
    [InlineData("☐ comprar", 5)]   // en mitad del texto: retroceso normal
    [InlineData("☐ comprar", 0)]   // antes del glifo
    [InlineData("comprar", 2)]     // no es lista
    [InlineData("☐comprar", 1)]    // pegado: ya no es tarea, retroceso normal
    [InlineData("", 0)]
    public void RemoveOnBackspace_AnywhereElse_ReturnsNull(string text, int caret)
    {
        Assert.Null(ListPrefix.RemoveOnBackspace(text, caret));
    }
}

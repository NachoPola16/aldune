using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ListIndentTests
{
    // --- Indent ------------------------------------------------------------------------------------

    [Fact]
    public void Indent_ATaskLine_AddsOneLevel()
    {
        var result = ListIndent.Indent("☐ comprar pan", caret: 9);

        Assert.Equal("    ☐ comprar pan", result.Text);
        Assert.Equal(13, result.Caret);
    }

    [Fact]
    public void Indent_ABulletLine_AddsOneLevel()
    {
        var result = ListIndent.Indent("→ comprar pan", caret: 0);

        Assert.Equal("    → comprar pan", result.Text);
        Assert.Equal(4, result.Caret);
    }

    [Fact]
    public void Indent_AnAlreadyIndentedLine_AddsAnotherLevelOnTop()
    {
        var result = ListIndent.Indent("    ☐ sub-tarea", caret: 0);

        Assert.Equal("        ☐ sub-tarea", result.Text);
    }

    [Fact]
    public void Indent_APlainLine_InsertsOneLevelOfSpacesAtTheCaret()
    {
        // En texto libre, Tab no mueve la línea entera: escribe la misma sangría que usan las listas
        // donde está el cursor. Antes era una tabulación literal, que se dibujaba más ancha y dejaba
        // el texto libre en otra columna que las tareas.
        var result = ListIndent.Indent("texto normal", 5);

        Assert.Equal("texto     normal", result.Text);
        Assert.Equal(9, result.Caret);
    }

    [Fact]
    public void Indent_AnEmptyText_InsertsOneLevel()
    {
        var result = ListIndent.Indent("", 0);

        Assert.Equal("    ", result.Text);
        Assert.Equal(4, result.Caret);
    }

    [Fact]
    public void Indent_OnlyTouchesItsOwnLine()
    {
        var text = "primera\r\n☐ segunda\r\ntercera";
        int caretInSecond = text.IndexOf("segunda", StringComparison.Ordinal);

        var result = ListIndent.Indent(text, caretInSecond);

        Assert.Equal("primera\r\n    ☐ segunda\r\ntercera", result.Text);
    }

    // --- Outdent -----------------------------------------------------------------------------------

    [Fact]
    public void Outdent_AnIndentedTaskLine_RemovesOneLevel()
    {
        var result = ListIndent.Outdent("    ☐ sub-tarea", caret: 8);

        Assert.Equal("☐ sub-tarea", result.Text);
        Assert.Equal(4, result.Caret);
    }

    [Fact]
    public void Outdent_AnIndentedBulletLine_RemovesOneLevel()
    {
        var result = ListIndent.Outdent("    → sub-punto", caret: 0);

        Assert.Equal("→ sub-punto", result.Text);
        Assert.Equal(0, result.Caret);
    }

    [Fact]
    public void Outdent_ATaskLineAlreadyAtRootLevel_DoesNothingButIsStillHandled()
    {
        // No hay sangria que quitar: se queda igual, pero el gesto se consume.
        var result = ListIndent.Outdent("☐ comprar pan", caret: 5);

        Assert.Equal("☐ comprar pan", result.Text);
        Assert.Equal(5, result.Caret);
    }

    [Fact]
    public void Outdent_WithLessThanAFullLevelOfIndent_RemovesWhateverThereIs()
    {
        var result = ListIndent.Outdent("  ☐ dos espacios", caret: 10);

        Assert.Equal("☐ dos espacios", result.Text);
        Assert.Equal(8, result.Caret);
    }

    [Fact]
    public void Outdent_APlainLineWithoutIndent_DoesNothingButIsStillHandled()
    {
        // Mayús+Tab dentro de una nota ya no salta a otro control: igual que en una lista.
        var result = ListIndent.Outdent("texto normal", 5);

        Assert.Equal("texto normal", result.Text);
        Assert.Equal(5, result.Caret);
    }

    [Fact]
    public void Outdent_APlainLineWithLeadingSpaces_RemovesOneLevel()
    {
        var result = ListIndent.Outdent("        texto con sangria", 10);

        Assert.Equal("    texto con sangria", result.Text);
        Assert.Equal(6, result.Caret);
    }

    [Fact]
    public void Outdent_ALegacyLeadingTab_CountsAsOneLevel()
    {
        // Las notas escritas antes de unificar la sangría pueden empezar por una tabulación literal.
        var result = ListIndent.Outdent("\ttexto viejo", 3);

        Assert.Equal("texto viejo", result.Text);
        Assert.Equal(2, result.Caret);
    }

    [Fact]
    public void Outdent_OnlyTouchesItsOwnLine()
    {
        var text = "primera\r\n    ☐ segunda\r\ntercera";
        int caretInSecond = text.IndexOf("segunda", StringComparison.Ordinal);

        var result = ListIndent.Outdent(text, caretInSecond);

        Assert.Equal("primera\r\n☐ segunda\r\ntercera", result.Text);
    }

    // --- Varias líneas seleccionadas -----------------------------------------------------------------

    [Fact]
    public void IndentLines_IndentsEveryLineTheSelectionTouchesAndKeepsItSelected()
    {
        var text = "uno\n☐ dos\ntres";
        int start = 1;                      // dentro de "uno"
        int length = text.IndexOf("tres", StringComparison.Ordinal) + 2 - start;

        var result = ListIndent.IndentLines(text, start, length);

        Assert.Equal("    uno\n    ☐ dos\n    tres", result.Text);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(result.Text.Length, result.SelectionLength);
    }

    [Fact]
    public void IndentLines_ASelectionEndingAtTheStartOfALine_DoesNotTouchThatLine()
    {
        // Seleccionar dos líneas enteras con Mayús+Abajo deja el final al principio de la tercera.
        var text = "uno\ndos\ntres";
        int length = text.IndexOf("tres", StringComparison.Ordinal);

        var result = ListIndent.IndentLines(text, 0, length);

        Assert.Equal("    uno\n    dos\ntres", result.Text);
    }

    [Fact]
    public void OutdentLines_RemovesOneLevelFromEachLine()
    {
        var text = "    uno\n        dos\ntres\n\tcuatro";

        var result = ListIndent.OutdentLines(text, 0, text.Length);

        Assert.Equal("uno\n    dos\ntres\ncuatro", result.Text);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(result.Text.Length, result.SelectionLength);
    }

    [Fact]
    public void IndentLines_WorksWithWindowsLineEndings()
    {
        var text = "uno\r\ndos";

        var result = ListIndent.IndentLines(text, 0, text.Length);

        Assert.Equal("    uno\r\n    dos", result.Text);
    }

    [Fact]
    public void IndentLines_LeavesBlankLinesEmpty()
    {
        // Como en cualquier editor: sangrar un bloque no llena de espacios las líneas en blanco.
        var text = "uno\n\ndos";

        var result = ListIndent.IndentLines(text, 0, text.Length);

        Assert.Equal("    uno\n\n    dos", result.Text);
    }
}

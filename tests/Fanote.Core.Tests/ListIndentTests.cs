using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class ListIndentTests
{
    // --- Indent ------------------------------------------------------------------------------------

    [Fact]
    public void Indent_ATaskLine_AddsOneLevel()
    {
        var result = ListIndent.Indent("☐ comprar pan", caret: 9);

        Assert.NotNull(result);
        Assert.Equal("    ☐ comprar pan", result!.Value.Text);
        Assert.Equal(13, result.Value.Caret);
    }

    [Fact]
    public void Indent_ABulletLine_AddsOneLevel()
    {
        var result = ListIndent.Indent("→ comprar pan", caret: 0);

        Assert.NotNull(result);
        Assert.Equal("    → comprar pan", result!.Value.Text);
        Assert.Equal(4, result.Value.Caret);
    }

    [Fact]
    public void Indent_AnAlreadyIndentedLine_AddsAnotherLevelOnTop()
    {
        var result = ListIndent.Indent("    ☐ sub-tarea", caret: 0);

        Assert.NotNull(result);
        Assert.Equal("        ☐ sub-tarea", result!.Value.Text);
    }

    [Fact]
    public void Indent_APlainLine_ReturnsNull()
    {
        // El llamante usa null para dejar pasar el Tab normal (inserta una tabulación literal).
        Assert.Null(ListIndent.Indent("texto normal", 5));
    }

    [Fact]
    public void Indent_OnlyTouchesItsOwnLine()
    {
        var text = "primera\r\n☐ segunda\r\ntercera";
        int caretInSecond = text.IndexOf("segunda", StringComparison.Ordinal);

        var result = ListIndent.Indent(text, caretInSecond);

        Assert.Equal("primera\r\n    ☐ segunda\r\ntercera", result!.Value.Text);
    }

    // --- Outdent -----------------------------------------------------------------------------------

    [Fact]
    public void Outdent_AnIndentedTaskLine_RemovesOneLevel()
    {
        var result = ListIndent.Outdent("    ☐ sub-tarea", caret: 8);

        Assert.NotNull(result);
        Assert.Equal("☐ sub-tarea", result!.Value.Text);
        Assert.Equal(4, result.Value.Caret);
    }

    [Fact]
    public void Outdent_AnIndentedBulletLine_RemovesOneLevel()
    {
        var result = ListIndent.Outdent("    → sub-punto", caret: 0);

        Assert.NotNull(result);
        Assert.Equal("→ sub-punto", result!.Value.Text);
        Assert.Equal(0, result.Value.Caret);
    }

    [Fact]
    public void Outdent_ATaskLineAlreadyAtRootLevel_DoesNothingButIsStillHandled()
    {
        // Sigue siendo una linea de lista (no null), pero no hay sangria que quitar: se queda igual.
        var result = ListIndent.Outdent("☐ comprar pan", caret: 5);

        Assert.NotNull(result);
        Assert.Equal("☐ comprar pan", result!.Value.Text);
        Assert.Equal(5, result.Value.Caret);
    }

    [Fact]
    public void Outdent_WithLessThanAFullLevelOfIndent_RemovesWhateverThereIs()
    {
        var result = ListIndent.Outdent("  ☐ dos espacios", caret: 10);

        Assert.NotNull(result);
        Assert.Equal("☐ dos espacios", result!.Value.Text);
        Assert.Equal(8, result.Value.Caret);
    }

    [Fact]
    public void Outdent_APlainLine_ReturnsNull()
    {
        Assert.Null(ListIndent.Outdent("texto normal", 5));
    }

    [Fact]
    public void Outdent_ARootLevelPlainLineWithLeadingSpaces_ReturnsNull()
    {
        // Sin glifo de tarea ni de viñeta, no es asunto de ListIndent aunque tenga sangría.
        Assert.Null(ListIndent.Outdent("    texto con sangria", 10));
    }

    [Fact]
    public void Outdent_OnlyTouchesItsOwnLine()
    {
        var text = "primera\r\n    ☐ segunda\r\ntercera";
        int caretInSecond = text.IndexOf("segunda", StringComparison.Ordinal);

        var result = ListIndent.Outdent(text, caretInSecond);

        Assert.Equal("primera\r\n☐ segunda\r\ntercera", result!.Value.Text);
    }
}

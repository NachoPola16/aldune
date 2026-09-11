using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class MarkdownExportTests
{
    // --- ToMarkdown: título ----------------------------------------------------------------------

    [Fact]
    public void ToMarkdown_TitleOnly_BecomesHeading()
    {
        Assert.Equal("# Comprar pan", MarkdownExport.ToMarkdown("Comprar pan"));
    }

    [Fact]
    public void ToMarkdown_EmptyText_UsesPlaceholderAsHeading()
    {
        Assert.Equal($"# {NoteTitleHelper.PlaceholderTitle}", MarkdownExport.ToMarkdown(""));
    }

    // --- ToMarkdown: cuerpo normal -----------------------------------------------------------------

    [Fact]
    public void ToMarkdown_PlainBody_KeptAsIs()
    {
        var result = MarkdownExport.ToMarkdown("Título\r\nlínea 1\r\nlínea 2");
        Assert.Equal("# Título\r\n\r\nlínea 1\r\nlínea 2", result);
    }

    // --- ToMarkdown: casillas de tarea --------------------------------------------------------------

    [Fact]
    public void ToMarkdown_UncheckedTask_BecomesMarkdownCheckbox()
    {
        var result = MarkdownExport.ToMarkdown("Título\r\n☐ comprar pan");
        Assert.Equal("# Título\r\n\r\n- [ ] comprar pan", result);
    }

    [Fact]
    public void ToMarkdown_CheckedTask_BecomesCheckedMarkdownCheckbox()
    {
        var result = MarkdownExport.ToMarkdown("Título\r\n☒ hecho");
        Assert.Equal("# Título\r\n\r\n- [x] hecho", result);
    }

    [Fact]
    public void ToMarkdown_CheckedAlternateGlyph_AlsoBecomesCheckedCheckbox()
    {
        var result = MarkdownExport.ToMarkdown("Título\r\n☑ hecho");
        Assert.Equal("# Título\r\n\r\n- [x] hecho", result);
    }

    [Fact]
    public void ToMarkdown_IndentedTask_KeepsIndentBeforeTheDash()
    {
        var result = MarkdownExport.ToMarkdown("Título\r\n    ☐ anidada");
        Assert.Equal("# Título\r\n\r\n    - [ ] anidada", result);
    }

    [Fact]
    public void ToMarkdown_TaskGlyphGluedToText_StillConverts()
    {
        // TaskLines.PrefixLength reconoce esto como tarea sin espacio detrás del glifo.
        var result = MarkdownExport.ToMarkdown("Título\r\n☐pegado");
        Assert.Equal("# Título\r\n\r\n- [ ] pegado", result);
    }

    [Fact]
    public void ToMarkdown_MultipleTasksAmongPlainLines_OnlyTasksConvert()
    {
        var result = MarkdownExport.ToMarkdown("Título\r\nNota suelta\r\n☐ tarea uno\r\n☒ tarea dos\r\nMás texto");
        Assert.Equal(
            "# Título\r\n\r\nNota suelta\r\n- [ ] tarea uno\r\n- [x] tarea dos\r\nMás texto",
            result);
    }

    // --- SuggestedFileName ---------------------------------------------------------------------------

    [Fact]
    public void SuggestedFileName_UsesTitleWithMdExtension()
    {
        Assert.Equal("Comprar pan.md", MarkdownExport.SuggestedFileName("Comprar pan\r\nmás texto"));
    }

    [Fact]
    public void SuggestedFileName_EmptyText_UsesPlaceholder()
    {
        Assert.Equal($"{NoteTitleHelper.PlaceholderTitle}.md", MarkdownExport.SuggestedFileName(""));
    }

    [Theory]
    [InlineData("Notas: viaje/2026")]
    [InlineData("Precio < 10 * \"oferta\" | fin?")]
    public void SuggestedFileName_RemovesInvalidFileNameCharacters(string title)
    {
        var fileName = MarkdownExport.SuggestedFileName(title);
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        Assert.All(fileName, c => Assert.DoesNotContain(c, invalidChars));
    }

    [Fact]
    public void SuggestedFileName_VeryLongTitle_IsTruncated()
    {
        var longTitle = new string('a', 200);
        var fileName = MarkdownExport.SuggestedFileName(longTitle);
        Assert.True(fileName.Length <= 84, $"Esperaba un nombre corto, salió de {fileName.Length} caracteres.");
        Assert.EndsWith(".md", fileName);
    }
}

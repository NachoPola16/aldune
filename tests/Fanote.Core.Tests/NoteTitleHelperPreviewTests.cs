using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class NoteTitleHelperPreviewTests
{
    [Fact]
    public void Preview_IsEmpty_WhenThereIsOnlyATitle()
    {
        // La pestana centra su titulo en ese caso, en vez de dejar un renglon en blanco.
        Assert.Equal(string.Empty, NoteTitleHelper.GetPreview("Lista de la compra"));
    }

    [Fact]
    public void Preview_IsEmpty_ForAnEmptyNote()
    {
        Assert.Equal(string.Empty, NoteTitleHelper.GetPreview(string.Empty));
    }

    [Fact]
    public void Preview_TakesEverythingAfterTheFirstLine()
    {
        Assert.Equal("pan leche", NoteTitleHelper.GetPreview("Compra\npan\nleche"));
    }

    [Fact]
    public void Preview_CollapsesRunsOfWhitespaceIntoOneSpace()
    {
        // En una linea de ~26 caracteres, respetar los saltos y sangrias originales gastaria el
        // hueco en huecos.
        Assert.Equal("a b", NoteTitleHelper.GetPreview("t\n\n  a \t\n   b  \n\n"));
    }

    [Fact]
    public void Preview_HasNoLeadingOrTrailingSpace()
    {
        var preview = NoteTitleHelper.GetPreview("t\n   hola   ");
        Assert.Equal("hola", preview);
    }

    [Fact]
    public void Preview_HandlesWindowsLineEndings()
    {
        Assert.Equal("cuerpo", NoteTitleHelper.GetPreview("titulo\r\ncuerpo"));
    }

    [Fact]
    public void Preview_IsEmpty_WhenTheBodyIsOnlyWhitespace()
    {
        Assert.Equal(string.Empty, NoteTitleHelper.GetPreview("titulo\n   \n\t\n"));
    }
}

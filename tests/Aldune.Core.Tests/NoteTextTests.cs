using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteTextTests
{
    [Fact]
    public void Split_TakesTheFirstLineAsTitle()
    {
        var (title, body) = NoteText.Split("Compra semana\r\n☐ Pan\r\n☐ Leche");

        Assert.Equal("Compra semana", title);
        Assert.Equal("☐ Pan\r\n☐ Leche", body);
    }

    [Fact]
    public void Split_WithASingleLine_HasNoBody()
    {
        var (title, body) = NoteText.Split("Solo una linea");

        Assert.Equal("Solo una linea", title);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public void Split_OfEmptyText_IsTwoEmptyStrings()
    {
        Assert.Equal((string.Empty, string.Empty), NoteText.Split(string.Empty));
    }

    [Fact]
    public void Split_HandlesUnixNewlines()
    {
        var (title, body) = NoteText.Split("titulo\ncuerpo");

        Assert.Equal("titulo", title);
        Assert.Equal("cuerpo", body);
    }

    [Fact]
    public void Split_KeepsTheBlankLinesOfTheBody()
    {
        // El cuerpo se respeta tal cual: una linea en blanco entre parrafos es del usuario.
        var (_, body) = NoteText.Split("t\r\nuno\r\n\r\ndos");
        Assert.Equal("uno\r\n\r\ndos", body);
    }

    [Fact]
    public void Join_PutsThemBackTogether()
    {
        Assert.Equal("titulo\r\ncuerpo", NoteText.Join("titulo", "cuerpo"));
    }

    [Fact]
    public void Join_WithAnEmptyBody_AddsNoTrailingNewline()
    {
        // Si no, una nota de una sola linea acumularia un salto al final cada vez que se abre.
        Assert.Equal("titulo", NoteText.Join("titulo", string.Empty));
    }

    [Fact]
    public void Join_WithAnEmptyTitle_KeepsTheBodyWhereItWas()
    {
        // La primera linea vacia es informacion: el cuerpo no puede subir a ocupar su sitio.
        Assert.Equal("\r\ncuerpo", NoteText.Join(string.Empty, "cuerpo"));
    }

    [Fact]
    public void Join_OfTwoEmpties_IsEmpty()
    {
        Assert.Equal(string.Empty, NoteText.Join(string.Empty, string.Empty));
    }

    [Theory]
    [InlineData("titulo\r\ncuerpo")]
    [InlineData("solo titulo")]
    [InlineData("")]
    [InlineData("t\r\nuno\r\ndos\r\ntres")]
    [InlineData("\r\ncuerpo sin titulo")]
    public void SplitThenJoin_GivesBackTheSameText(string original)
    {
        var (title, body) = NoteText.Split(original);
        Assert.Equal(original, NoteText.Join(title, body));
    }

    [Fact]
    public void SplitThenJoin_DropsATrailingNewlineAfterTheTitle()
    {
        // Unico caso en que no es identico, y es el correcto: "titulo\r\n" y "titulo" son la misma
        // nota, y guardar el salto haria crecer el texto en cada apertura.
        var (title, body) = NoteText.Split("titulo\r\n");
        Assert.Equal("titulo", NoteText.Join(title, body));
    }

    [Fact]
    public void Split_AgreesWithWhatTheTabShows()
    {
        // El titulo de la cabecera y el que sale en la pestana del dock tienen que ser el mismo, o
        // la nota se llamaria de dos maneras a la vez.
        const string text = "Compra semana\r\n☐ Pan";
        var (title, _) = NoteText.Split(text);

        Assert.Equal(NoteTitleHelper.GetTitle(text), title);
    }
}

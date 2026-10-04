using Xunit;

namespace Aldune.Core.Tests;

public class MarkdownLinkTests
{
    // Archivos difíciles: cada uno tiene que volver idéntico tras traducir y reconstruir sin editar.
    public static TheoryData<string> Corpus => new()
    {
        "",
        "\n",
        "\r\n\r\n",
        "solo una línea sin salto",
        "# Tema 3\r\n\r\n- [ ] repasar\r\n- [x] hecho\r\n- viñeta\r\n",
        "# Tema\n- [ ] lf\n* [X] mayúscula\n+ más\n",
        "mezcla\r\n- [ ] crlf\n- lf\r\nfin",
        "  - [ ] sangría con espacios\n\t- [ ] sangría con tabulador\n    * anidada\n",
        "1. numerada\n2. [ ] numerada con casilla\n",
        "---\ntitle: cabecera YAML\ntags: [a, b]\n---\n- [ ] tarea\n",
        "```\n- [ ] dentro de código\n```\n- [ ] fuera\n",
        "~~~md\n* viñeta en código\n~~~\n",
        "espacios al final   \n- [ ] tarea con espacios   \n",
        "- [ ]\n- [x]\n- \n",
        "**negrita** y *cursiva*\n---\n- [x]sin espacio\n",
        "☐ glifo de Aldune escrito a mano\n→ flecha\n",
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void UneditedRoundTrip_IsByteForByte(string file) =>
        Assert.Equal(file, MarkdownLink.ToFileText(MarkdownLink.ToNoteText(file), file));

    [Fact]
    public void ToNoteText_TranslatesTasksAndBulletsKeepingIndentAndNewLines()
    {
        var note = MarkdownLink.ToNoteText("- [ ] a\r\n  * [X] b\n+ c\n1. d");
        Assert.Equal("☐ a\r\n  ☒ b\n→ c\n1. d", note);
    }

    [Fact]
    public void ToNoteText_LeavesCodeBlocksAlone()
    {
        var file = "```\n- [ ] código\n```\n- [ ] fuera\n";
        Assert.Equal("```\n- [ ] código\n```\n☐ fuera\n", MarkdownLink.ToNoteText(file));
    }

    [Fact]
    public void CheckingATask_ChangesOnlyThatLine()
    {
        var file = "# Lista\n* [ ] uno\n* [ ] dos   \n";
        var edited = MarkdownLink.ToNoteText(file).Replace("☐ uno", "☒ uno");

        Assert.Equal("# Lista\n- [x] uno\n* [ ] dos   \n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void NewLines_UseTheDominantNewLineAndBulletMarker()
    {
        var file = "* a\n* b\n";
        // El TextBox mete CRLF al pulsar Enter, aunque el archivo sea LF.
        var edited = MarkdownLink.ToNoteText(file).Replace("→ b\n", "→ b\r\n→ nueva\r\n☐ tarea\n");

        Assert.Equal("* a\n* b\n* nueva\n- [ ] tarea\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void JoinedTitleWithCrLf_KeepsTheOriginalLf()
    {
        // NoteText.Join une título y cuerpo con CRLF: la primera línea no ha cambiado, y su salto tampoco.
        var file = "# Tema\nlínea\n";
        var note = MarkdownLink.ToNoteText(file).Replace("# Tema\n", "# Tema\r\n");

        Assert.Equal(file, MarkdownLink.ToFileText(note, file));
    }

    [Fact]
    public void DeletingALine_RemovesOnlyThatLine()
    {
        var file = "- [ ] a\r\n- [ ] b\r\n- [ ] c\r\n";
        var edited = MarkdownLink.ToNoteText(file).Replace("☐ b\r\n", "");

        Assert.Equal("- [ ] a\r\n- [ ] c\r\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void MovingALine_KeepsItsOriginalBytes()
    {
        var file = "* [X] hecha\n- [ ] pendiente\n";
        var edited = "☐ pendiente\n☒ hecha\n";

        Assert.Equal("- [ ] pendiente\n* [X] hecha\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void NewTaskInsideACodeBlock_IsWrittenLiterally()
    {
        var file = "```\nx\n```\n";
        var edited = "```\nx\n☐ literal\n```\n";

        Assert.Equal("```\nx\n☐ literal\n```\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void NewFile_UsesCrLfAndDashes()
    {
        Assert.Equal("Título\r\n- [ ] a\r\n- b", MarkdownLink.ToFileText("Título\r\n☐ a\r\n→ b", ""));
    }

    [Fact]
    public void AddingATrailingNewLine_KeepsTheRestIntact()
    {
        var file = "a\nb";
        Assert.Equal("a\nb\n", MarkdownLink.ToFileText("a\nb\r\n", file));
    }
}

using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class TaskLinesTests
{
    // --- Reconocer una línea de tarea -----------------------------------------------------------

    [Theory]
    [InlineData("☐ comprar pan", true)]
    [InlineData("☒ comprar pan", true)]
    [InlineData("    ☐ con sangría", true)]
    [InlineData("\t☐ con tabulador", true)]
    [InlineData("comprar pan", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsTaskLine_RecognisesThePrefix(string line, bool expected)
    {
        Assert.Equal(expected, TaskLines.IsTaskLine(line));
    }

    [Fact]
    public void IsTaskLine_GlyphWithTextGluedRightAfterIt_IsStillATask()
    {
        // Decisión revertida a propósito: el usuario pidió poder marcar una tarea aunque se le
        // haya pegado el texto al glifo sin espacio (p. ej. al borrar el espacio sin querer
        // mientras se edita) -- antes esto no contaba como tarea, ahora sí. Ver TaskLines.PrefixLength
        // para cómo se sigue quitando el prefijo correctamente en ambos casos.
        Assert.True(TaskLines.IsTaskLine("☐comprar"));
    }

    [Fact]
    public void IsTaskLine_GlyphAlone_IsATask()
    {
        Assert.True(TaskLines.IsTaskLine("☐"));
    }

    [Fact]
    public void IsTaskLine_GlyphInTheMiddleOfTheLine_IsNotATask()
    {
        Assert.False(TaskLines.IsTaskLine("mira este simbolo ☐ que raro"));
    }

    [Fact]
    public void IsChecked_OnlyForTheCheckedGlyph()
    {
        Assert.True(TaskLines.IsChecked("☒ hecho"));
        Assert.False(TaskLines.IsChecked("☐ pendiente"));
        Assert.False(TaskLines.IsChecked("texto normal"));
    }

    [Fact]
    public void IsChecked_AlsoAcceptsTheTickedBoxPastedFromElsewhere()
    {
        // Nunca se escribe ☑ (ver TaskLines.Checked para el porque), pero llega pegado desde otras
        // apps de tareas y tiene que contar como marcada.
        Assert.True(TaskLines.IsChecked("☑ hecho"));
        Assert.True(TaskLines.IsTaskLine("☑ hecho"));
    }

    [Fact]
    public void ToggleCheckboxAt_APastedTickedBox_BecomesUnchecked()
    {
        Assert.Equal("☐ hecho", TaskLines.ToggleCheckboxAt("☑ hecho", 0));
    }

    // --- Marcar y desmarcar con un clic ---------------------------------------------------------

    [Fact]
    public void ToggleCheckboxAt_UncheckedBecomesChecked()
    {
        var text = "☐ comprar pan";
        Assert.Equal("☒ comprar pan", TaskLines.ToggleCheckboxAt(text, 0));
    }

    [Fact]
    public void ToggleCheckboxAt_CheckedBecomesUnchecked()
    {
        var text = "☒ comprar pan";
        Assert.Equal("☐ comprar pan", TaskLines.ToggleCheckboxAt(text, 0));
    }

    [Fact]
    public void ToggleCheckboxAt_TheRightLineOfSeveral()
    {
        var text = "☐ uno\r\n☐ dos\r\n☐ tres";
        int second = text.IndexOf("☐ dos", StringComparison.Ordinal);

        Assert.Equal("☐ uno\r\n☒ dos\r\n☐ tres", TaskLines.ToggleCheckboxAt(text, second));
    }

    [Fact]
    public void ToggleCheckboxAt_SomewhereWithoutACheckbox_ReturnsNull()
    {
        // El llamante usa este null para dejar que el clic coloque el cursor como siempre.
        Assert.Null(TaskLines.ToggleCheckboxAt("☐ comprar pan", 5));
        Assert.Null(TaskLines.ToggleCheckboxAt("texto normal", 0));
    }

    [Fact]
    public void ToggleCheckboxAt_GlyphThatIsNotItsLinePrefix_ReturnsNull()
    {
        var text = "hola ☐ mundo";
        Assert.Null(TaskLines.ToggleCheckboxAt(text, text.IndexOf('☐')));
    }

    [Fact]
    public void ToggleCheckboxAt_OutOfRange_ReturnsNull()
    {
        Assert.Null(TaskLines.ToggleCheckboxAt("☐ x", -1));
        Assert.Null(TaskLines.ToggleCheckboxAt("☐ x", 99));
    }

    [Fact]
    public void ToggleCheckboxAt_GlyphWithTextGluedRightAfterIt_StillToggles()
    {
        // ToggleCheckboxAt solo voltea un caracter, asi que ya funcionaba sin cambios en cuanto
        // GlyphIndex reconoce la linea -- lo que confirma este test es justo eso.
        Assert.Equal("☒comprar", TaskLines.ToggleCheckboxAt("☐comprar", 0));
    }

    // --- PrefixLength (cuánto ocupa el prefijo de casilla) --------------------------------------

    [Fact]
    public void PrefixLength_WithASpaceAfterTheGlyph_IsTwo()
    {
        Assert.Equal(2, TaskLines.PrefixLength("☐ comprar", 0));
    }

    [Fact]
    public void PrefixLength_WithTextGluedRightAfterTheGlyph_IsOne()
    {
        Assert.Equal(1, TaskLines.PrefixLength("☐comprar", 0));
    }

    [Fact]
    public void PrefixLength_GlyphAloneAtTheEndOfTheLine_IsOne()
    {
        Assert.Equal(1, TaskLines.PrefixLength("☐", 0));
    }

    // --- Convertir una línea en tarea (atajo de teclado) ----------------------------------------

    [Fact]
    public void ToggleTaskLineAt_PlainLineGetsThePrefix_AndTheCaretFollowsTheText()
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt("comprar pan", caret: 7);

        Assert.Equal("☐ comprar pan", text);
        Assert.Equal(9, caret); // el cursor sigue justo antes de "pan"
    }

    [Fact]
    public void ToggleTaskLineAt_TaskLineLosesThePrefix()
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt("☐ comprar pan", caret: 9);

        Assert.Equal("comprar pan", text);
        Assert.Equal(7, caret);
    }

    [Fact]
    public void ToggleTaskLineAt_KeepsTheIndent()
    {
        var (text, _) = TaskLines.ToggleTaskLineAt("    comprar pan", caret: 4);
        Assert.Equal("    ☐ comprar pan", text);
    }

    [Fact]
    public void ToggleTaskLineAt_OnlyTouchesItsOwnLine()
    {
        var text = "primera\r\nsegunda\r\ntercera";
        int caretInSecond = text.IndexOf("segunda", StringComparison.Ordinal) + 2;

        var (result, _) = TaskLines.ToggleTaskLineAt(text, caretInSecond);

        Assert.Equal("primera\r\n☐ segunda\r\ntercera", result);
    }

    [Fact]
    public void ToggleTaskLineAt_OnAnEmptyLine_StillCreatesATask()
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt("", caret: 0);
        Assert.Equal("☐ ", text);
        Assert.Equal(2, caret);
    }

    [Fact]
    public void ToggleTaskLineAt_ABulletLine_ConvertsItToATaskInstead()
    {
        // Pulsar el atajo de tarea sobre una viñea ya existente la convierte, no la apila -- mismo
        // criterio simétrico que BulletLines.ToggleBulletLineAt con una tarea.
        var (text, caret) = TaskLines.ToggleTaskLineAt("→ comprar pan", caret: 9);

        Assert.Equal("☐ comprar pan", text);
        Assert.Equal(9, caret);
    }

    [Fact]
    public void ToggleTaskLineAt_ABulletLineWithIndent_KeepsTheIndentAfterConverting()
    {
        var (text, _) = TaskLines.ToggleTaskLineAt("    → sub-punto", caret: 0);
        Assert.Equal("    ☐ sub-punto", text);
    }

    [Fact]
    public void ToggleTaskLineAt_GlyphWithTextGluedRightAfterIt_StripsOnlyTheGlyph()
    {
        // Sin PrefixLength esto quitaria 2 caracteres a ciegas (glifo + "espacio" asumido) y se
        // comeria la "c" de "comprar", dejando "omprar" -- justo el bug que motiva PrefixLength.
        var (text, caret) = TaskLines.ToggleTaskLineAt("☐comprar pan", caret: 5);

        Assert.Equal("comprar pan", text);
        Assert.Equal(4, caret);
    }

    // --- Continuar la lista con Enter -----------------------------------------------------------

    [Fact]
    public void EnterContinuation_AtTheEndOfATask_StartsTheNextOne()
    {
        var text = "☐ comprar pan";
        var result = TaskLines.EnterContinuation(text, text.Length);

        Assert.NotNull(result);
        Assert.Equal("☐ comprar pan\r\n☐ ", result!.Value.Text);
        Assert.Equal(result.Value.Text.Length, result.Value.Caret);
    }

    [Fact]
    public void EnterContinuation_KeepsTheIndentOfTheLineItContinues()
    {
        var text = "    ☐ sub-tarea";
        var result = TaskLines.EnterContinuation(text, text.Length);

        Assert.Equal("    ☐ sub-tarea\r\n    ☐ ", result!.Value.Text);
    }

    [Fact]
    public void EnterContinuation_OnAnEmptyTask_EndsTheList()
    {
        // Salir de la lista con dos Enter, como en cualquier editor: la casilla vacia desaparece en
        // vez de encadenar casillas vacias para siempre.
        var text = "☐ comprar pan\r\n☐ ";
        var result = TaskLines.EnterContinuation(text, text.Length);

        Assert.NotNull(result);
        Assert.Equal("☐ comprar pan\r\n", result!.Value.Text);
        Assert.Equal(result.Value.Text.Length, result.Value.Caret);
    }

    [Fact]
    public void EnterContinuation_OnAPlainLine_ReturnsNull()
    {
        Assert.Null(TaskLines.EnterContinuation("texto normal", 12));
    }

    [Fact]
    public void EnterContinuation_InTheMiddleOfATask_ReturnsNull()
    {
        // Enter en mitad de una tarea la parte en dos, como haria un editor normal.
        Assert.Null(TaskLines.EnterContinuation("☐ comprar pan", 6));
    }

    [Fact]
    public void EnterContinuation_ContinuesFromACheckedTask_WithAnEmptyBox()
    {
        var text = "☒ hecho";
        var result = TaskLines.EnterContinuation(text, text.Length);

        Assert.Equal("☒ hecho\r\n☐ ", result!.Value.Text);
    }

    [Fact]
    public void EnterContinuation_GlyphWithTextGluedRightAfterIt_StillContinuesTheList()
    {
        // Sin PrefixLength, "rest" se calcularia como line[2..] y se comeria la "o" de "hecho".
        var text = "☒hecho";
        var result = TaskLines.EnterContinuation(text, text.Length);

        Assert.Equal("☒hecho\r\n☐ ", result!.Value.Text);
    }

    [Fact]
    public void EnterContinuation_GlyphAloneWithTextGluedAfterItRemoved_EndsTheList()
    {
        // Una tarea sin espacio y sin contenido de verdad (solo el glifo) tambien tiene que poder
        // terminar la lista con Enter, igual que la version bien formada.
        var text = "☐";
        var result = TaskLines.EnterContinuation(text, text.Length);

        Assert.Equal("", result!.Value.Text);
    }

    // --- Recuento -------------------------------------------------------------------------------

    [Fact]
    public void Count_CountsDoneAndTotal_IgnoringPlainLines()
    {
        var text = "Compra\r\n☒ pan\r\n☐ leche\r\n☐ huevos\r\nnota suelta";

        var (done, total) = TaskLines.Count(text);

        Assert.Equal(1, done);
        Assert.Equal(3, total);
    }

    [Fact]
    public void Count_WithNoTasks_IsZero()
    {
        Assert.Equal((0, 0), TaskLines.Count("solo texto\r\nsin tareas"));
    }

    [Fact]
    public void Count_WorksWithUnixNewlines()
    {
        Assert.Equal((1, 2), TaskLines.Count("☒ uno\n☐ dos"));
    }

    // --- LineContaining ---------------------------------------------------------------------------

    [Fact]
    public void LineContaining_ReturnsTheFullLineAtTheGivenIndex()
    {
        var text = "primera\r\n☒ segunda linea\r\ntercera";
        int indexInsideSecondLine = text.IndexOf("segunda", StringComparison.Ordinal);

        Assert.Equal("☒ segunda linea", TaskLines.LineContaining(text, indexInsideSecondLine));
    }

    [Fact]
    public void LineContaining_OnASingleLineText_ReturnsTheWholeText()
    {
        Assert.Equal("☒ unica linea", TaskLines.LineContaining("☒ unica linea", 3));
    }

    // --- IsEmptyTaskLine --------------------------------------------------------------------------

    [Theory]
    [InlineData("☐ ", true)]
    [InlineData("☐", true)]
    [InlineData("☐   ", true)] // solo espacios detrás, sigue sin contenido real
    [InlineData("☐ comprar pan", false)]
    [InlineData("☐comprar", false)]
    [InlineData("comprar pan", false)]
    [InlineData("", false)]
    public void IsEmptyTaskLine_RecognisesATaskWithNoRealContent(string line, bool expected)
    {
        Assert.Equal(expected, TaskLines.IsEmptyTaskLine(line));
    }

    [Fact]
    public void IsEmptyTaskLine_CheckedEmptyTaskIsAlsoEmpty()
    {
        Assert.True(TaskLines.IsEmptyTaskLine("☒ "));
    }
}

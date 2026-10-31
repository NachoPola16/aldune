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
    public void IsTaskLine_GlyphWithTextGluedRightAfterIt_IsNotATask()
    {
        // Revertido otra vez (2026-10-03), a petición del usuario: la tarea exige el espacio. El
        // motivo para aceptarla pegada era borrar el espacio sin querer, y eso ya no pasa: el
        // retroceso quita el prefijo entero (ListPrefix.RemoveOnBackspace).
        Assert.False(TaskLines.IsTaskLine("☐comprar"));
    }

    [Theory]
    [InlineData("☐-")]
    [InlineData("☐- comprar")]
    [InlineData("☒.")]
    [InlineData("    ☐—")]
    [InlineData("→ ☐-")]
    public void IsTaskLine_GlyphWithPunctuationGluedRightAfterIt_IsATask(string line)
    {
        // Un guion (o cualquier signo) pegado a la casilla no la convierte en texto: lo que deja de ser tarea es una PALABRA pegada.
        // Sin esto, escribir "-" justo detrás de una casilla vacía la dejaba sin poder marcar.
        Assert.True(TaskLines.IsTaskLine(line));
    }

    [Fact]
    public void ToggleCheckboxAt_GlyphWithADashGluedRightAfterIt_TicksIt()
    {
        Assert.Equal("☒-", TaskLines.ToggleCheckboxAt("☐-", 0));
    }

    [Fact]
    public void IsTaskLine_GlyphGluedToAnotherGlyph_IsNotATask()
    {
        Assert.False(TaskLines.IsTaskLine("☐☐"));
    }

    [Theory]
    [InlineData("- ☐ comprar", 2)]
    [InlineData("• ☒ hecho", 2)]
    [InlineData("  * ☐ x", 4)]
    [InlineData("1. ☐ x", 3)]
    [InlineData("12) ☐ x", 4)]
    [InlineData("- ☐-", 2)]
    public void GlyphIndex_ListMarkerBeforeTheGlyph_StillFindsIt(string line, int expected)
    {
        Assert.Equal(expected, TaskLines.GlyphIndex(line));
    }

    [Theory]
    [InlineData("-☐ x")]
    [InlineData("- - ☐ x")]
    [InlineData("-x ☐ x")]
    [InlineData("hola ☐ x")]
    [InlineData("1234. ☐ x")]
    [InlineData("1.☐ x")]
    public void IsTaskLine_SomethingElseBeforeTheGlyph_IsNotATask(string line)
    {
        Assert.False(TaskLines.IsTaskLine(line));
    }

    // --- Espacio protegido detrás de la casilla ---------------------------------------------------

    [Theory]
    [InlineData("☐ x", 1, 2)]
    [InlineData("- ☐ x", 3, 4)]
    [InlineData("→ ☐ x", 3, 4)]
    [InlineData("  ☒ x", 3, 4)]
    [InlineData("☐ ", 1, 2)]
    public void ClampCaret_BetweenTheGlyphAndItsSpace_MovesAfterTheSpace(string line, int caret, int expected)
    {
        Assert.Equal(expected, TaskLines.ClampCaret(line, caret));
    }

    [Theory]
    [InlineData("☐ x", 0)]
    [InlineData("☐ x", 2)]
    [InlineData("☐ x", 3)]
    [InlineData("- ☐ x", 2)]
    [InlineData("- ☐ x", 1)]
    [InlineData("☐x", 1)]
    [InlineData("texto", 3)]
    public void ClampCaret_AnyOtherPosition_IsLeftAlone(string line, int caret)
    {
        Assert.Equal(caret, TaskLines.ClampCaret(line, caret));
    }

    [Theory]
    [InlineData("☐ x", 1, 0)]
    [InlineData("- ☐ x", 3, 2)]
    public void ClampCaret_MovingLeft_JumpsToTheFrontOfTheGlyph(string line, int caret, int expected)
    {
        Assert.Equal(expected, TaskLines.ClampCaret(line, caret, movingLeft: true));
    }

    [Fact]
    public void ClampCaret_OnlyLooksAtTheLineTheCaretIsIn()
    {
        const string text = "☐ a\n☐ b";
        Assert.Equal(6, TaskLines.ClampCaret(text, 5));   // entre la casilla y el espacio de la segunda línea
        Assert.Equal(1, TaskLines.ClampCaret("x\n☐ b", 1));
    }

    [Fact]
    public void EnsureGlyphSpace_SignGluedToTheGlyph_GetsItsSpace()
    {
        var (text, caret) = TaskLines.EnsureGlyphSpace("☐-", 2);
        Assert.Equal("☐ -", text);
        Assert.Equal(3, caret);
    }

    [Fact]
    public void EnsureGlyphSpace_CaretBeforeTheGlyph_StaysPut()
    {
        var (text, caret) = TaskLines.EnsureGlyphSpace("- ☐-x", 0);
        Assert.Equal("- ☐ -x", text);
        Assert.Equal(0, caret);
    }

    [Theory]
    [InlineData("☐ x")]
    [InlineData("☐comprar")]
    [InlineData("☐")]
    [InlineData("hola")]
    public void EnsureGlyphSpace_NothingToFix_ReturnsTheSameText(string line)
    {
        var (text, caret) = TaskLines.EnsureGlyphSpace(line, 1);
        Assert.Equal(line, text);
        Assert.Equal(1, caret);
    }

    [Fact]
    public void IsTaskLine_GlyphAlone_IsNotATask()
    {
        Assert.False(TaskLines.IsTaskLine("☐"));
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
    public void ToggleCheckboxAt_GlyphWithTextGluedRightAfterIt_IsNotACheckbox()
    {
        // Sin espacio no es tarea, así que el clic sobre ese glifo es un clic normal.
        Assert.Null(TaskLines.ToggleCheckboxAt("☐comprar", 0));
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
    public void ToggleTaskLineAt_ABulletLine_AddsTheBoxAfterTheArrow()
    {
        // Flecha y casilla son dos interruptores independientes: el atajo de tarea sobre una viñeta añade la
        // casilla detrás de la flecha ("→ ☐ x"), no la sustituye.
        var (text, caret) = TaskLines.ToggleTaskLineAt("→ comprar pan", caret: 9);

        Assert.Equal("→ ☐ comprar pan", text);
        Assert.Equal(11, caret);
    }

    [Fact]
    public void ToggleTaskLineAt_ArrowAndBox_RemovesOnlyTheBox()
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt("→ ☐ comprar pan", caret: 11);

        Assert.Equal("→ comprar pan", text);
        Assert.Equal(9, caret);
    }

    [Theory]
    // Cursor en la sangría o delante de la flecha: quitar la casilla no lo mueve (antes saltaba hasta el glifo).
    [InlineData("    ☐ x", 1, "    x", 1)]
    [InlineData("→ ☐ x", 0, "→ x", 0)]
    [InlineData("→ ☐ x", 1, "→ x", 1)]
    // Dentro del prefijo: va a donde empezaba la casilla.
    [InlineData("☐ x", 1, "x", 0)]
    public void ToggleTaskLineAt_RemovingTheBox_DoesNotPushACaretThatWasBeforeIt(
        string line, int caret, string expectedText, int expectedCaret)
    {
        var (text, newCaret) = TaskLines.ToggleTaskLineAt(line, caret);

        Assert.Equal(expectedText, text);
        Assert.Equal(expectedCaret, newCaret);
    }

    [Fact]
    public void ToggleTaskLineAt_PutThenRemove_LeavesACaretInTheIndentWhereItWas()
    {
        var (added, caretAfterAdding) = TaskLines.ToggleTaskLineAt("    x", caret: 2);
        var (removed, caretAfterRemoving) = TaskLines.ToggleTaskLineAt(added, caretAfterAdding);

        Assert.Equal("    x", removed);
        Assert.Equal(2, caretAfterRemoving);
    }

    [Fact]
    public void ArrowFollowedByABox_IsATask()
    {
        Assert.True(TaskLines.IsTaskLine("→ ☐ a"));
        Assert.False(TaskLines.IsChecked("→ ☐ a"));
        Assert.True(TaskLines.IsChecked("  → ☒ a"));
        Assert.Equal((1, 2), TaskLines.Count("→ ☒ a\n→ ☐ b\n→ c"));
    }

    [Fact]
    public void ToggleCheckboxAt_ArrowAndBox_FlipsTheBox()
    {
        Assert.Equal("→ ☒ a", TaskLines.ToggleCheckboxAt("→ ☐ a", 2));
        Assert.Null(TaskLines.ToggleCheckboxAt("→ ☐ a", 0));
    }

    [Fact]
    public void EnterContinuation_ArrowAndBox_ContinuesWithBoth()
    {
        var (text, _) = TaskLines.EnterContinuation("→ ☐ a", caret: 5, newLine: "\n")!.Value;
        Assert.Equal("→ ☐ a\n→ ☐ ", text);
    }

    [Fact]
    public void EnterContinuation_AnEmptyArrowAndBox_EndsTheList()
    {
        var (text, caret) = TaskLines.EnterContinuation("x\n→ ☐ ", caret: 6, newLine: "\n")!.Value;
        Assert.Equal("x\n", text);
        Assert.Equal(2, caret);
    }

    [Fact]
    public void ToggleTaskLineAt_ABulletLineWithIndent_KeepsTheIndentAndTheArrow()
    {
        var (text, _) = TaskLines.ToggleTaskLineAt("    → sub-punto", caret: 0);
        Assert.Equal("    → ☐ sub-punto", text);
    }

    [Fact]
    public void ToggleTaskLineAt_GlyphWithTextGluedRightAfterIt_RepairsItIntoATask()
    {
        // Una tarea antigua escrita sin espacio ya no cuenta como tarea; Ctrl+L la repara metiendo
        // el espacio, en vez de apilar una casilla nueva delante ("☐ ☐comprar").
        var (text, caret) = TaskLines.ToggleTaskLineAt("☐comprar pan", caret: 5);

        Assert.Equal("☐ comprar pan", text);
        Assert.Equal(6, caret);
    }

    [Fact]
    public void ToggleTaskLineAt_IndentedGluedGlyph_RepairsItKeepingTheIndent()
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt("    ☒hecho", caret: 0);

        Assert.Equal("    ☒ hecho", text);
        Assert.Equal(0, caret);
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
    public void EnterContinuation_GlyphWithTextGluedRightAfterIt_IsAPlainEnter()
    {
        Assert.Null(TaskLines.EnterContinuation("☒hecho", "☒hecho".Length));
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
    [InlineData("☐", false)]
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

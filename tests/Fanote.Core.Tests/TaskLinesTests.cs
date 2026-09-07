using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

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
    public void IsTaskLine_GlyphWithoutASpaceAfterIt_IsNotATask()
    {
        // Un ☐ suelto escrito a mano no debe convertir la linea en tarea sin querer.
        Assert.False(TaskLines.IsTaskLine("☐comprar"));
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
}

using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class TaskListsTests
{
    // --- CheckboxOnLine (Ctrl+Enter) --------------------------------------------------------------

    [Fact]
    public void CheckboxOnLine_CaretAnywhereOnATask_ReturnsItsGlyph()
    {
        var text = "intro\n  ☐ pan\nfin";
        int caret = text.IndexOf("an", StringComparison.Ordinal);

        Assert.Equal(text.IndexOf('☐'), TaskLists.CheckboxOnLine(text, caret));
    }

    [Fact]
    public void CheckboxOnLine_NotATask_ReturnsNull()
    {
        Assert.Null(TaskLists.CheckboxOnLine("solo texto\n☐ tarea", 3));
    }

    [Fact]
    public void CheckboxOnLine_CaretAtEndOfTheLastTask_StillFindsIt()
    {
        var text = "☐ uno\r\n☒ dos";
        Assert.Equal(text.IndexOf('☒'), TaskLists.CheckboxOnLine(text, text.Length));
    }

    // --- UncheckAll / RemoveChecked ---------------------------------------------------------------

    [Fact]
    public void UncheckAll_UnchecksEveryCheckedTask_IncludingTheAlternateGlyph()
    {
        var edit = TaskLists.UncheckAll("Compra\r\n☒ pan\r\n  ☑ leche\r\n☐ huevos\r\nnota ☒ suelta");

        Assert.True(edit.Changed);
        Assert.Equal("Compra\r\n☐ pan\r\n  ☐ leche\r\n☐ huevos\r\nnota ☒ suelta", edit.Text);
    }

    [Fact]
    public void UncheckAll_NothingChecked_IsUnchanged()
    {
        var edit = TaskLists.UncheckAll("☐ pan\n☐ leche");
        Assert.False(edit.Changed);
        Assert.Equal("☐ pan\n☐ leche", edit.Text);
    }

    [Fact]
    public void RemoveChecked_RemovesOnlyCheckedTaskLines()
    {
        var edit = TaskLists.RemoveChecked("☒ pan\r\n☐ leche\r\n☒ huevos\r\nfin");

        Assert.True(edit.Changed);
        Assert.Equal("☐ leche\r\nfin", edit.Text);
    }

    [Fact]
    public void RemoveChecked_LastLine_DoesNotLeaveADanglingCarriageReturn()
    {
        Assert.Equal("algo", TaskLists.RemoveChecked("algo\r\n☒ hecho").Text);
    }

    [Fact]
    public void RemoveChecked_MapsTheCaretOntoTheSameCharacter()
    {
        var text = "☒ hecha\r\nfinal texto";
        var edit = TaskLists.RemoveChecked(text);
        int caret = text.IndexOf("texto", StringComparison.Ordinal);

        int mapped = edit.MapIndex(caret);

        Assert.Equal("texto", edit.Text.Substring(mapped, 5));
    }

    [Fact]
    public void HasChecked_And_IsAllDone()
    {
        Assert.True(TaskLists.HasChecked("☐ a\n☒ b"));
        Assert.False(TaskLists.HasChecked("☐ a\nb ☒"));
        Assert.True(TaskLists.IsAllDone("Compra\n☒ a\n☑ b\nnota"));
        Assert.False(TaskLists.IsAllDone("☒ a\n☐ b"));
        Assert.False(TaskLists.IsAllDone("sin tareas"));
    }

    // --- SettleToggled (hechas al final) -----------------------------------------------------------

    [Fact]
    public void SettleToggled_JustChecked_MovesToTheEndOfItsList()
    {
        var text = "Compra\r\n☒ pan\r\n☐ leche\r\n☐ huevos\r\nnotas";
        var edit = TaskLists.SettleToggled(text, text.IndexOf('☒'));

        Assert.Equal("Compra\r\n☐ leche\r\n☐ huevos\r\n☒ pan\r\nnotas", edit.Text);
    }

    [Fact]
    public void SettleToggled_CheckedAfterOthersAlreadyDone_GoesBelowThem()
    {
        // El último marcado queda el último: el orden de lo hecho es el orden en que se hizo.
        var text = "☒ leche\n☐ huevos\n☒ pan";
        var edit = TaskLists.SettleToggled(text, 0);

        Assert.Equal("☐ huevos\n☒ pan\n☒ leche", edit.Text);
    }

    [Fact]
    public void SettleToggled_JustUnchecked_GoesBackAboveTheCheckedOnes()
    {
        var text = "☐ leche\n☒ pan\n☐ huevos\n☒ sal";
        int glyph = text.IndexOf("☐ huevos", StringComparison.Ordinal);
        // "huevos" acaba de desmarcarse y está por debajo de una hecha: sube justo encima de ella.
        var edit = TaskLists.SettleToggled(text, glyph);

        Assert.Equal("☐ leche\n☐ huevos\n☒ pan\n☒ sal", edit.Text);
    }

    [Fact]
    public void SettleToggled_AlreadyInPlace_IsUnchanged()
    {
        var text = "☐ leche\n☒ pan";
        Assert.False(TaskLists.SettleToggled(text, text.IndexOf('☒')).Changed);
    }

    [Fact]
    public void SettleToggled_LastLineWithoutTerminator_KeepsLineEndingsInPlace()
    {
        var text = "☒ pan\r\n☐ leche";
        var edit = TaskLists.SettleToggled(text, 0);

        Assert.Equal("☐ leche\r\n☒ pan", edit.Text);
    }

    [Fact]
    public void SettleToggled_OnlyWithinTheSameIndentAndContiguousTasks()
    {
        var text = "☒ pan\n☐ leche\n\n☐ otra lista";
        var edit = TaskLists.SettleToggled(text, 0);

        Assert.Equal("☐ leche\n☒ pan\n\n☐ otra lista", edit.Text);
    }

    [Fact]
    public void SettleToggled_ListWithSubtasks_IsLeftAlone()
    {
        // Mover una tarea con hijas (o saltar por encima de una) las separaría de su madre.
        var text = "☒ cena\n  ☐ pan\n☐ postre";
        Assert.False(TaskLists.SettleToggled(text, 0).Changed);

        var sibling = "☒ bebida\n☐ cena\n  ☐ pan";
        Assert.False(TaskLists.SettleToggled(sibling, 0).Changed);
    }

    [Fact]
    public void SettleToggled_MapsTheCaretWithTheMovedLineAndTheOthers()
    {
        var text = "☒ pan\n☐ leche\nfin";
        var edit = TaskLists.SettleToggled(text, 0);

        Assert.Equal("pan", edit.Text.Substring(edit.MapIndex(text.IndexOf("pan", StringComparison.Ordinal)), 3));
        Assert.Equal("leche", edit.Text.Substring(edit.MapIndex(text.IndexOf("leche", StringComparison.Ordinal)), 5));
        Assert.Equal("fin", edit.Text.Substring(edit.MapIndex(text.IndexOf("fin", StringComparison.Ordinal)), 3));
    }

    // --- Pegar desde Markdown ----------------------------------------------------------------------

    [Theory]
    [InlineData("- [ ] pan", "☐ pan")]
    [InlineData("- [x] leche", "☒ leche")]
    [InlineData("* [X] sal", "☒ sal")]
    [InlineData("  + [ ] indentada", "  ☐ indentada")]
    [InlineData("[ ] sin guion", "☐ sin guion")]
    [InlineData("- pan", "- pan")]
    [InlineData("texto [x] en medio", "texto [x] en medio")]
    public void ConvertMarkdownTasks_ConvertsOnlyTaskSyntaxAtLineStart(string input, string expected)
    {
        Assert.Equal(expected, TaskLists.ConvertMarkdownTasks(input));
    }

    [Fact]
    public void ConvertMarkdownTasks_KeepsLineEndings()
    {
        Assert.Equal("☐ a\r\n☒ b\nc", TaskLists.ConvertMarkdownTasks("- [ ] a\r\n- [x] b\nc"));
    }

    // --- Pendientes para el aviso del recordatorio -------------------------------------------------

    [Fact]
    public void PendingTasks_ReturnsTheFirstUncheckedContents()
    {
        var pending = TaskLists.PendingTasks("Compra\n☒ pan\n☐ leche\n☐\n  ☐ huevos\n☐ sal", 2);

        Assert.Equal(new[] { "leche", "huevos" }, pending);
    }
}

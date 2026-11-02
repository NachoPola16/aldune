using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class LineMovementTests
{
    [Theory]
    [InlineData("a\nb\nc\nd", 0, 2, "b\nc\na\nd")]
    [InlineData("a\nb\nc\nd", 3, 1, "a\nd\nb\nc")]
    [InlineData("a\r\nb\r\nc", 0, 2, "b\r\nc\r\na")]
    public void MoveTo_PutsTheLineAtTheNewIndexKeepingTheTerminators(string text, int from, int to, string expected)
    {
        Assert.Equal(expected, LineMovement.MoveTo(text, from, to)!.Value.Text);
    }

    [Fact]
    public void MoveTo_LeavesTheCaretAtTheEndOfTheMovedLine()
    {
        var result = LineMovement.MoveTo("uno\ndos\ntres", 0, 2)!.Value;
        Assert.Equal("dos\ntres\nuno", result.Text);
        Assert.Equal(result.Text.Length, result.Caret);
    }

    [Theory]
    [InlineData("a\nb", 1, 1)]
    [InlineData("a\nb", 0, 5)]
    [InlineData("a\nb", -1, 0)]
    public void MoveTo_IsNullWhenNothingChangesOrALineDoesNotExist(string text, int from, int to)
    {
        Assert.Null(LineMovement.MoveTo(text, from, to));
    }

    [Fact]
    public void Move_Up_SwapsWithThePreviousLine()
    {
        var text = "uno\r\ndos\r\ntres";
        int caretInDos = text.IndexOf("dos", StringComparison.Ordinal) + 1;

        var result = LineMovement.Move(text, caretInDos, LineDirection.Up);

        Assert.Equal("dos\r\nuno\r\ntres", result!.Value.Text);
    }

    [Fact]
    public void Move_Down_SwapsWithTheNextLine()
    {
        var text = "uno\r\ndos\r\ntres";
        int caretInDos = text.IndexOf("dos", StringComparison.Ordinal) + 1;

        var result = LineMovement.Move(text, caretInDos, LineDirection.Down);

        Assert.Equal("uno\r\ntres\r\ndos", result!.Value.Text);
    }

    [Fact]
    public void Move_Up_OnTheFirstLine_ReturnsNull()
    {
        var text = "uno\r\ndos";
        Assert.Null(LineMovement.Move(text, caret: 1, LineDirection.Up));
    }

    [Fact]
    public void Move_Down_OnTheLastLine_ReturnsNull()
    {
        var text = "uno\r\ndos";
        int caretInDos = text.IndexOf("dos", StringComparison.Ordinal) + 1;
        Assert.Null(LineMovement.Move(text, caretInDos, LineDirection.Down));
    }

    [Fact]
    public void Move_OnASingleLineText_ReturnsNullEitherDirection()
    {
        Assert.Null(LineMovement.Move("solo una linea", caret: 3, LineDirection.Up));
        Assert.Null(LineMovement.Move("solo una linea", caret: 3, LineDirection.Down));
    }

    [Fact]
    public void Move_Up_KeepsTheCaretsOffsetWithinTheLine()
    {
        var text = "uno\r\ndos\r\ntres";
        int caretInDos = text.IndexOf("dos", StringComparison.Ordinal) + 2; // dentro de "dos", tras "do"

        var result = LineMovement.Move(text, caretInDos, LineDirection.Up);

        // "dos" ahora vive donde estaba "uno" (índice 0); el cursor sigue 2 caracteres dentro de ella.
        Assert.Equal(2, result!.Value.Caret);
    }

    [Fact]
    public void Move_WorksOnTaskLines_ButIsNotLimitedToThem()
    {
        // La petición original era para tareas con casilla, pero el atajo no distingue: cualquier
        // línea se puede subir o bajar igual.
        var text = "☐ uno\r\n☐ dos\r\n☐ tres";
        int caretInSegunda = text.IndexOf("☐ dos", StringComparison.Ordinal);

        var result = LineMovement.Move(text, caretInSegunda, LineDirection.Down);

        Assert.Equal("☐ uno\r\n☐ tres\r\n☐ dos", result!.Value.Text);
    }

    [Fact]
    public void Move_WorksWithUnixNewlines()
    {
        var text = "uno\ndos\ntres";
        int caretInDos = text.IndexOf("dos", StringComparison.Ordinal);

        var result = LineMovement.Move(text, caretInDos, LineDirection.Up);

        Assert.Equal("dos\nuno\ntres", result!.Value.Text);
    }

    [Fact]
    public void Move_MultipleTimes_KeepsMovingTheSameLogicalLine()
    {
        var text = "a\r\nb\r\nc\r\nd";
        int caret = text.IndexOf('b');

        var first = LineMovement.Move(text, caret, LineDirection.Down)!.Value;
        Assert.Equal("a\r\nc\r\nb\r\nd", first.Text);

        var second = LineMovement.Move(first.Text, first.Caret, LineDirection.Down)!.Value;
        Assert.Equal("a\r\nc\r\nd\r\nb", second.Text);
    }
}

package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals

/** The caret never rests in front of a task's box (the cursor handle would sit on top of the box and swallow the tap that unticks it). */
class CaretOffBoxTest {
    @Test
    fun aCaretBeforeTheBoxMovesBehindItsSpace() {
        assertEquals(2, TaskLines.keepCaretOffBox("☐ x", 0))
        assertEquals(2, TaskLines.keepCaretOffBox("☐ x", 1))
        assertEquals(4, TaskLines.keepCaretOffBox("  ☒ x", 0))
        assertEquals(4, TaskLines.keepCaretOffBox("  ☒ x", 2))
        assertEquals(4, TaskLines.keepCaretOffBox("a\n☐ x", 2))
    }

    @Test
    fun movingLeftFromTheTextJumpsToTheEndOfThePreviousLine() {
        assertEquals(1, TaskLines.keepCaretOffBox("a\n☐ x", 3, movingLeft = true))
    }

    @Test
    fun movingLeftOnTheFirstLineStaysBehindTheBox() {
        assertEquals(2, TaskLines.keepCaretOffBox("☐ x", 1, movingLeft = true))
    }

    @Test
    fun otherPositionsAreLeftAlone() {
        assertEquals(3, TaskLines.keepCaretOffBox("☐ xy", 3))
        assertEquals(1, TaskLines.keepCaretOffBox("plain", 1))
        assertEquals(0, TaskLines.keepCaretOffBox("plain\n☐ x", 0))
    }
}

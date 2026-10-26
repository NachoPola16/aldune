package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull

class LineMovementTest {
    @Test fun `llevar una linea hacia abajo desplaza las intermedias hacia arriba`() {
        val moved = LineMovement.moveTo("a\nb\nc", fromLine = 0, toLine = 2)!!
        assertEquals("b\nc\na", moved.text)
        assertEquals(moved.text.length, moved.caret)
    }

    @Test fun `llevar una linea hacia arriba desplaza las intermedias hacia abajo`() {
        val moved = LineMovement.moveTo("a\nb\nc", fromLine = 2, toLine = 0)!!
        assertEquals("c\na\nb", moved.text)
        assertEquals(1, moved.caret)
    }

    @Test fun `a una linea de distancia es lo mismo que mover con la flecha`() {
        val byArrow = LineMovement.move("a\nb\nc", caret = 0, direction = LineDirection.Down)!!
        assertEquals(byArrow.text, LineMovement.moveTo("a\nb\nc", 0, 1)!!.text)
    }

    @Test fun `los terminadores se quedan en su sitio y la ultima linea sigue sin terminador`() {
        assertEquals("b\r\nc\r\na", LineMovement.moveTo("a\r\nb\r\nc", 0, 2)!!.text)
        assertEquals("c\na\nb", LineMovement.moveTo("a\nb\nc", 2, 0)!!.text)
    }

    @Test fun `la sangria y la casilla viajan con la linea`() {
        val text = "☐ uno\n  ☐ dos\n☒ tres"
        assertEquals("  ☐ dos\n☐ uno\n☒ tres", LineMovement.moveTo(text, 0, 1)!!.text)
    }

    @Test fun `misma linea o fuera de rango no hace nada`() {
        assertNull(LineMovement.moveTo("a\nb", 1, 1))
        assertNull(LineMovement.moveTo("a\nb", -1, 1))
        assertNull(LineMovement.moveTo("a\nb", 0, 2))
        assertNull(LineMovement.moveTo("", 0, 0))
    }
}

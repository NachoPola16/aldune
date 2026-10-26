package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class AppendTaskTest {
    @Test fun `se anade tras la ultima tarea`() {
        assertEquals("Compra\n☐ pan\n☒ leche\n☐ sal", TaskLists.appendTask("Compra\n☐ pan\n☒ leche", "sal"))
    }

    @Test fun `con texto despues de la lista el articulo va junto a la lista, no al final de la nota`() {
        assertEquals("Compra\n☐ pan\n☐ sal\n\nnotas varias", TaskLists.appendTask("Compra\n☐ pan\n\nnotas varias", "sal"))
    }

    @Test fun `sin tareas va al final de la nota`() {
        assertEquals("Compra\n☐ sal", TaskLists.appendTask("Compra", "sal"))
        assertEquals("Compra\n☐ sal", TaskLists.appendTask("Compra\n", "sal"))
    }

    @Test fun `una nota vacia se queda solo con la tarea`() {
        assertEquals("☐ sal", TaskLists.appendTask("", "sal"))
    }

    @Test fun `respeta el salto de linea de la nota`() {
        assertEquals("Compra\r\n☐ pan\r\n☐ sal", TaskLists.appendTask("Compra\r\n☐ pan", "sal"))
    }

    @Test fun `limpia espacios y saltos del articulo`() {
        assertEquals("☐ pan\n☐ leche entera", TaskLists.appendTask("☐ pan", "  leche\n entera \t"))
    }

    @Test fun `un articulo vacio no cambia nada`() {
        assertEquals("☐ pan", TaskLists.appendTask("☐ pan", "   "))
        assertEquals("☐ pan", TaskLists.appendTask("☐ pan", ""))
    }

    @Test fun `la ultima tarea con sangria no cambia donde va el articulo`() {
        assertEquals("☐ a\n  ☐ a1\n☐ sal", TaskLists.appendTask("☐ a\n  ☐ a1", "sal"))
    }

    @Test fun `una nota que acaba en salto CRLF sin tareas no deja linea en blanco de mas`() {
        assertEquals("Compra\r\n☐ sal", TaskLists.appendTask("Compra\r\n", "sal"))
    }

    @Test fun `con CRLF y texto despues de la ultima tarea el resto queda intacto`() {
        assertEquals("☐ pan\r\n☐ sal\r\nfin\r\n", TaskLists.appendTask("☐ pan\r\nfin\r\n", "sal"))
    }

    @Test fun `un articulo solo con tabuladores y saltos es vacio`() {
        assertEquals("☐ pan", TaskLists.appendTask("☐ pan", "\t\n \r\n"))
    }

    @Test fun `un articulo con emojis se conserva`() {
        assertEquals("☐ pan\n☐ 🥛 leche", TaskLists.appendTask("☐ pan", "🥛  leche"))
    }
}

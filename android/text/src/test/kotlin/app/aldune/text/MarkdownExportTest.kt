package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** Los mismos casos que `MarkdownExportTests` de Core: el fichero exportado en Android es idéntico al de Windows. */
class MarkdownExportTest {
    private fun md(text: String) = MarkdownExport.toMarkdown(text, "Nota nueva")

    @Test fun `solo titulo es un encabezado`() = assertEquals("# Comprar pan", md("Comprar pan"))

    @Test fun `texto vacio usa el titulo de reserva`() = assertEquals("# Nota nueva", md(""))

    @Test fun `cuerpo normal se deja tal cual`() =
        assertEquals("# Título\r\n\r\nlínea 1\r\nlínea 2", md("Título\r\nlínea 1\r\nlínea 2"))

    @Test fun `casillas de tarea`() {
        assertEquals("# Título\r\n\r\n- [ ] comprar pan", md("Título\r\n☐ comprar pan"))
        assertEquals("# Título\r\n\r\n- [x] hecho", md("Título\r\n☒ hecho"))
        assertEquals("# Título\r\n\r\n- [x] hecho", md("Título\r\n☑ hecho"))
        assertEquals("# Título\r\n\r\n    - [ ] anidada", md("Título\r\n    ☐ anidada"))
    }

    @Test fun `un glifo pegado al texto sigue siendo texto`() {
        assertEquals("# Título\r\n\r\n☐pegado", md("Título\r\n☐pegado"))
        assertEquals("# Título\r\n\r\n→pegado", md("Título\r\n→pegado"))
    }

    @Test fun `solo las tareas se convierten entre lineas normales`() = assertEquals(
        "# Título\r\n\r\nNota suelta\r\n- [ ] tarea uno\r\n- [x] tarea dos\r\nMás texto",
        md("Título\r\nNota suelta\r\n☐ tarea uno\r\n☒ tarea dos\r\nMás texto"),
    )

    @Test fun `viñetas`() {
        assertEquals("# Título\r\n\r\n- comprar pan", md("Título\r\n→ comprar pan"))
        assertEquals("# Título\r\n\r\n    - anidada", md("Título\r\n    → anidada"))
        assertEquals("# Título\r\n\r\n- [ ] tarea\r\n- punto\r\nMás texto", md("Título\r\n☐ tarea\r\n→ punto\r\nMás texto"))
    }

    @Test fun `flecha y casilla es una tarea sin la flecha`() = assertEquals(
        "# Título\r\n\r\n- [ ] comprar pan\r\n  - [x] hecho",
        md("Título\r\n→ ☐ comprar pan\r\n  → ☒ hecho"),
    )

    @Test fun `el nombre de fichero sale del titulo`() {
        assertEquals("Comprar pan.md", MarkdownExport.suggestedFileName("Comprar pan\r\nmás texto", "Nota nueva"))
        assertEquals("Nota nueva.md", MarkdownExport.suggestedFileName("", "Nota nueva"))
    }

    @Test fun `el nombre de fichero no lleva caracteres invalidos`() {
        for (title in listOf("Notas: viaje/2026", "Precio < 10 * \"oferta\" | fin?")) {
            val name = MarkdownExport.suggestedFileName(title, "Nota nueva")
            assertTrue(name.none { it in "<>:\"/\\|?*" || it.code < 32 }, name)
        }
    }

    @Test fun `un titulo muy largo se recorta`() {
        val name = MarkdownExport.suggestedFileName("a".repeat(200), "Nota nueva")
        assertTrue(name.length <= 84, "salió de ${name.length} caracteres")
        assertTrue(name.endsWith(".md"))
    }
}

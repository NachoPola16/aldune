package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertTrue

class SharedNoteTextTest {
    private fun compose(text: String?, subject: String?) = SharedNoteText.compose(text, subject)

    private fun text(text: String?, subject: String? = null): String = assertIs<SharedNote.Text>(compose(text, subject)).value

    // Regla 1
    @Test
    fun `los saltos de linea CRLF y CR pasan a LF`() {
        assertEquals("a\nb\nc", text("a\r\nb\rc"))
    }

    // Regla 2
    @Test
    fun `se quitan las lineas en blanco del principio y del final y los espacios del final`() {
        assertEquals("hola\nmundo", text("\n  \n\t\nhola\nmundo  \n\n   \n"))
    }

    @Test
    fun `la sangria de la primera linea se conserva`() {
        assertEquals("   - uno\n   - dos", text("\n\n   - uno\n   - dos\n"))
    }

    @Test
    fun `el espacio en blanco es el de DotNet`() {
        // U+0085 es espacio en .NET y no en Kotlin; U+001F lo es en Kotlin y no en .NET.
        assertEquals("a", text("a\u0085  "))
        assertEquals("\u001Fa", text("\u001Fa"))
    }

    // Regla 3
    @Test
    fun `el asunto colapsa todo espacio en blanco, saltos incluidos, a un espacio`() {
        assertEquals("Mi titulo largo\ncuerpo", text("cuerpo", "  Mi\ntitulo \r\n\t largo  "))
    }

    // Regla 4
    @Test
    fun `sin asunto es el texto y sin texto es el asunto`() {
        assertEquals("solo texto", text("solo texto", null))
        assertEquals("solo texto", text("solo texto", "   "))
        assertEquals("solo asunto", text(null, "solo asunto"))
        assertEquals("solo asunto", text("  \n ", "solo asunto"))
    }

    @Test
    fun `sin ninguno de los dos es Empty`() {
        assertEquals(SharedNote.Empty, compose(null, null))
        assertEquals(SharedNote.Empty, compose("", ""))
        assertEquals(SharedNote.Empty, compose(" \r\n\t ", " \n "))
        assertEquals(SharedNote.Empty, compose("\u0085", null))
    }

    // Regla 5
    @Test
    fun `si la primera linea ya empieza por el asunto no se repite`() {
        assertEquals("https://x.org/a", text("https://x.org/a", "https://x.org/a"))
        assertEquals("Titulo\nhttps://x.org", text("Titulo\nhttps://x.org", "Titulo"))
        assertEquals("Titulo largo de la pagina\nURL", text("Titulo largo de la pagina\nURL", "Titulo"))
    }

    @Test
    fun `la comparacion no distingue mayusculas y recorta la primera linea`() {
        assertEquals("  TITULO  \nurl", text("  TITULO  \nurl", "titulo"))
    }

    @Test
    fun `solo cuenta la primera linea del texto`() {
        assertEquals("Titulo\nuna\nTitulo", text("una\nTitulo", "Titulo"))
    }

    // Regla 6
    @Test
    fun `si no, el asunto va en la primera linea y el texto en la segunda`() {
        assertEquals("Pagina\nhttps://x.org", text("https://x.org", "Pagina"))
    }

    @Test
    fun `un asunto mas largo que la primera linea no cuenta como prefijo`() {
        assertEquals("Titulo largo\nTitulo", text("Titulo", "Titulo largo"))
    }

    // Regla 7
    @Test
    fun `las casillas de Markdown pasan a casillas de Aldune`() {
        assertEquals("☐ a\n☒ b", text("- [ ] a\n- [x] b"))
        assertEquals("Lista\n☐ a", text("- [ ] a", "Lista"))
    }

    // Regla 8
    @Test
    fun `hasta un millon de caracteres cabe y mas es TooLong`() {
        val limit = SharedNoteText.MAX_LENGTH
        assertEquals(1_000_000, limit)
        assertEquals(limit, text("x".repeat(limit)).length)
        assertEquals(SharedNote.TooLong, compose("x".repeat(limit + 1), null))
    }

    @Test
    fun `el tope cuenta el asunto y el salto que se anaden`() {
        // asunto (1) + "\n" (1) + texto: con 999.998 de texto son justo 1.000.000.
        assertEquals(limitedLength(), text("x".repeat(SharedNoteText.MAX_LENGTH - 2), "A").length)
        assertEquals(SharedNote.TooLong, compose("x".repeat(SharedNoteText.MAX_LENGTH - 1), "A"))
    }

    private fun limitedLength() = SharedNoteText.MAX_LENGTH

    @Test
    fun `lo que sobra en blanco no cuenta para el tope`() {
        val padded = "\n\n" + "x".repeat(SharedNoteText.MAX_LENGTH) + "   \n\n"
        assertEquals(SharedNoteText.MAX_LENGTH, text(padded).length)
    }

    // Regla 9
    @Test
    fun `asunto que es la URL misma`() {
        assertEquals("https://example.com/a?b=c", text("https://example.com/a?b=c", "https://example.com/a?b=c"))
    }

    @Test
    fun `una almohadilla al principio del texto se respeta`() {
        assertEquals("Asunto\n# Encabezado\ncuerpo", text("# Encabezado\ncuerpo", "Asunto"))
        assertEquals("#etiqueta", text("#etiqueta"))
    }

    @Test
    fun `texto solo de espacios con asunto valido da el asunto`() {
        assertEquals("Asunto", text("   \n\t  ", "Asunto"))
    }

    @Test
    fun `null en cualquiera de los dos`() {
        assertEquals("a", text("a", null))
        assertEquals("b", text(null, "b"))
        assertEquals(SharedNote.Empty, compose(null, null))
    }

    @Test
    fun `nunca se corta, o cabe entero o es TooLong`() {
        val result = compose("a".repeat(SharedNoteText.MAX_LENGTH + 5), "Asunto")
        assertEquals(SharedNote.TooLong, result)
    }

    // Lo que sale tiene que aceptarlo NoteService.create
    @Test
    fun `todo Text que sale lo acepta NoteService create`() {
        val cases = listOf(
            "a\r\nb" to "s", "\r\n\r\n x \r\n" to null, "- [ ] a\r- [X] b" to "Lista", null to "solo asunto",
            "https://x.org" to "https://x.org", "Titulo\r\nurl" to " titulo ", "  " to "tab\there",
        )
        for ((t, s) in cases) {
            val value = text(t, s)
            assertTrue(value.isNotEmpty())
            assertFalse(value.contains("\r"))
            assertIs<NoteResult.Ok<Note>>(Fixture().service.create(value), "texto=$t asunto=$s")
        }
    }
}

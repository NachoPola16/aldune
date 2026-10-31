package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.ValueSource
import org.junit.jupiter.api.Test

/** Twin of `LinkTextTests` in `Aldune.Core.Tests`: both platforms must decide the same. */
class LinkTextTest {
    private fun linkAt(text: String, index: Int): String? = LinkText.findAt(text, index)?.let { text.substring(it.start, it.end) }

    @Test fun `devuelve la direccion bajo el indice`() {
        assertEquals("https://example.com/a?b=1", linkAt("see https://example.com/a?b=1 now", 8))
        assertEquals("http://example.com", linkAt("http://example.com", 0))
        assertEquals("https://example.com", linkAt("see https://example.com.", 10))
        assertEquals("https://example.com/x", linkAt("(https://example.com/x)", 5))
        assertEquals("https://en.wikipedia.org/wiki/A_(b)", linkAt("https://en.wikipedia.org/wiki/A_(b)", 3))
        assertEquals("https://example.com", linkAt("go to https://example.com, then", 10))
        assertEquals("HTTPS://EXAMPLE.COM/Path", linkAt("HTTPS://EXAMPLE.COM/Path", 0))
    }

    @Test fun `fuera de una direccion no hay enlace`() {
        assertNull(LinkText.findAt("see https://example.com now", 2))
        assertNull(LinkText.findAt("see https://example.com now", 24))
        assertNull(LinkText.findAt("no links here", 3))
        assertNull(LinkText.findAt("", 0))
    }

    @ParameterizedTest
    @ValueSource(strings = [
        "file:///C:/Windows/System32/calc.exe", "javascript:alert(1)", "ftp://example.com/x", "data:text/html,hi",
        "content://contacts/people", "intent://scan/#Intent;scheme=zxing;end", "mailto:a@b.com",
    ])
    fun `solo se aceptan http y https`(text: String) = assertNull(LinkText.findAt(text, 3))

    @Test fun `una direccion con usuario que disfraza el servidor se ignora`() {
        assertNull(LinkText.findAt("https://good-host.com@attacker.tld/", 10))
    }

    @Test fun `el esquema solo no es un enlace`() = assertNull(LinkText.findAt("https://", 3))

    @Test fun `se para en espacios, saltos de linea y casillas`() {
        assertEquals("https://example.com/a", linkAt("\u2610 https://example.com/a\nnext line", 6))
    }

    @Test fun `una racha absurdamente larga se descarta`() {
        assertNull(LinkText.findAt("https://example.com/" + "a".repeat(5000), 5))
    }

    @Test fun `findAll devuelve todos en orden y recortados`() {
        val text = "uno https://a.com/x, dos (http://b.org/y) tres"
        val found = LinkText.findAll(text).map { text.substring(it.start, it.end) }
        assertEquals(listOf("https://a.com/x", "http://b.org/y"), found)
    }

    @Test fun `hostOf enseña el destino y rechaza lo no web`() {
        assertEquals("example.com", LinkText.hostOf("https://example.com/a"))
        assertNull(LinkText.hostOf("file:///etc/passwd"))
        assertNotNull(LinkText.toUri("https://example.com/a"))
    }
}

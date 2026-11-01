package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals

class TagColorsTest {
    @Test fun `lo guardado se lee igual y lo mal formado se ignora`() {
        val colors = mapOf("Casa" to "#A8E0B4", "dos palabras" to "#9EC9F0")
        assertEquals(colors, TagColors.decode(TagColors.encode(colors)))
        assertEquals(emptyMap(), TagColors.decode(""))
        assertEquals(mapOf("a" to "#111111"), TagColors.decode("sin tabulador\na\t#111111\n\t#222222"))
    }

    @Test fun `poner y quitar el color de una etiqueta no distingue mayusculas`() {
        val colors = mapOf("casa" to "#111111")
        assertEquals(mapOf("Casa" to "#222222"), TagColors.with(colors, "Casa", "#222222"))
        assertEquals(emptyMap(), TagColors.with(colors, "CASA", null))
    }
}

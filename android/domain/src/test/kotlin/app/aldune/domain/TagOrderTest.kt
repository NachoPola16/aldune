package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals

class TagOrderTest {
    @Test fun `sin orden propio es alfabetico sin distinguir mayusculas`() {
        assertEquals(listOf("Casa", "ideas", "Zeta"), TagOrder.apply(listOf("Zeta", "ideas", "Casa"), emptyList()))
    }

    @Test fun `el orden propio va primero y lo demas sigue alfabetico`() {
        assertEquals(listOf("zeta", "ideas", "casa", "otra"), TagOrder.apply(listOf("casa", "otra", "zeta", "ideas"), listOf("zeta", "ideas")))
    }

    @Test fun `una etiqueta del orden propio que ya no existe se ignora y las mayusculas no importan`() {
        assertEquals(listOf("Ideas", "casa"), TagOrder.apply(listOf("casa", "Ideas"), listOf("borrada", "IDEAS")))
    }

    @Test fun `mover una etiqueta parte del orden visible y fija el orden completo`() {
        val visible = listOf("a", "b", "c")
        assertEquals(listOf("a", "c", "b"), TagOrder.move(visible, "b", +1))
        assertEquals(listOf("b", "a", "c"), TagOrder.move(visible, "b", -1))
        assertEquals(visible, TagOrder.move(visible, "a", -1))
        assertEquals(visible, TagOrder.move(visible, "c", +1))
    }

    @Test fun `llevar una etiqueta al sitio de otra la deja en esa posicion`() {
        val visible = listOf("a", "b", "c", "d")
        assertEquals(listOf("b", "c", "a", "d"), TagOrder.moveTo(visible, "a", "c"))
        assertEquals(listOf("d", "a", "b", "c"), TagOrder.moveTo(visible, "D", "a"))
        assertEquals(visible, TagOrder.moveTo(visible, "b", "b"))
        assertEquals(visible, TagOrder.moveTo(visible, "x", "a"))
    }

    @Test fun `la lista guardada se codifica con saltos de linea y se lee igual`() {
        val tags = listOf("uno", "dos palabras", "tres")
        assertEquals(tags, TagOrder.decode(TagOrder.encode(tags)))
        assertEquals(emptyList(), TagOrder.decode(""))
    }

    @Test fun `el filtro de la hoja busca dentro del nombre sin distinguir mayusculas`() {
        assertEquals(listOf("Compras", "decompras"), TagOrder.filter(listOf("Compras", "casa", "decompras"), "OMPR"))
        assertEquals(listOf("a", "b"), TagOrder.filter(listOf("a", "b"), "  "))
    }

    @Test fun `el sitio de las notas sin etiqueta se inserta y se conserva al reordenar etiquetas`() {
        val u = TagGrouping.UNTAGGED
        assertEquals(listOf("a", "b", u), TagOrder.groupOrder(listOf("a", "b"), emptyList()))
        assertEquals(listOf("a", u, "b"), TagOrder.groupOrder(listOf("a", "b"), listOf("a", u, "b")))
        assertEquals(listOf("a", "b", u), TagOrder.groupOrder(listOf("a", "b"), listOf("a", "b", "c", u)))
        // Moving tags from the dialog must not forget where the untagged group was.
        assertEquals(listOf("b", u, "a"), TagOrder.keepUntagged(listOf("b", "a"), listOf("a", u, "b")))
        assertEquals(listOf("b", "a"), TagOrder.keepUntagged(listOf("b", "a"), listOf("a", "b")))
    }
}

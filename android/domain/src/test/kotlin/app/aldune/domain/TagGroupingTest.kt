package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals

class TagGroupingTest {
    private data class N(val id: String, val tags: List<String>)

    private fun group(items: List<N>, order: List<String>) = TagGrouping.group(items, order) { it.tags }.map { it.tag to it.items.map(N::id) }

    @Test
    fun `una nota con dos etiquetas sale en las dos`() {
        val r = group(listOf(N("a", listOf("casa", "trabajo")), N("b", listOf("casa"))), listOf("casa", "trabajo"))
        assertEquals(listOf("casa" to listOf("a", "b"), "trabajo" to listOf("a")), r)
    }

    @Test
    fun `las secciones siguen el orden de las etiquetas dado, no el de las notas`() {
        val r = group(listOf(N("a", listOf("b")), N("c", listOf("a"))), listOf("a", "b"))
        assertEquals(listOf("a", "b"), r.map { it.first })
    }

    @Test
    fun `las notas sin etiqueta van al final con tag nulo`() {
        val r = TagGrouping.group(listOf(N("x", emptyList()), N("y", listOf("a"))), listOf("a")) { it.tags }
        assertEquals(listOf("a", null), r.map { it.tag })
        assertEquals(listOf("x"), r.last().items.map { it.id })
    }

    @Test
    fun `no se crean secciones vacias y las mayusculas no duplican`() {
        val r = group(listOf(N("a", listOf("Casa"))), listOf("casa", "otra"))
        assertEquals(listOf("casa" to listOf("a")), r)
    }

    @Test
    fun `una etiqueta que no esta en el orden dado va detras en orden alfabetico`() {
        val r = group(listOf(N("a", listOf("zeta")), N("b", listOf("beta"))), listOf("alfa"))
        assertEquals(listOf("beta", "zeta"), r.map { it.first })
    }

    @Test
    fun `una nota no se repite dentro de la misma seccion`() {
        val r = group(listOf(N("a", listOf("x", "X"))), listOf("x"))
        assertEquals(listOf("x" to listOf("a")), r)
    }

    @Test
    fun `las notas sin etiqueta van donde el orden pone su marca`() {
        val items = listOf(N("x", emptyList()), N("y", listOf("a")), N("z", listOf("b")))
        val r = TagGrouping.group(items, listOf("a", TagGrouping.UNTAGGED, "b")) { it.tags }
        assertEquals(listOf("a", null, "b"), r.map { it.tag })
        val first = TagGrouping.group(items, listOf(TagGrouping.UNTAGGED, "a", "b")) { it.tags }
        assertEquals(listOf(null, "a", "b"), first.map { it.tag })
    }

    @Test
    fun `an empty tag goes after every group that has notes`() {
        val keys = TagGrouping.sectionKeys(sequence = listOf("a", "empty", "b", TagGrouping.UNTAGGED), present = setOf("a", "b", TagGrouping.UNTAGGED), dragging = false)
        assertEquals(listOf("a", "b", TagGrouping.UNTAGGED, "empty"), keys)
    }

    @Test
    fun `while tags are dragged they keep their own order`() {
        val keys = TagGrouping.sectionKeys(sequence = listOf("a", "empty", "b"), present = setOf("a", "b"), dragging = true)
        assertEquals(listOf("a", "empty", "b"), keys)
    }

    @Test
    fun `a group the order does not know yet goes last and nothing is hidden`() {
        val keys = TagGrouping.sectionKeys(sequence = listOf("a"), present = setOf("a", "new"), dragging = false)
        assertEquals(listOf("a", "new"), keys)
    }

    @Test
    fun `section keys are lower case and not repeated`() {
        val keys = TagGrouping.sectionKeys(sequence = listOf("Casa", "casa"), present = setOf("casa"), dragging = false)
        assertEquals(listOf("casa"), keys)
    }
}

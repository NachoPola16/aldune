package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class WidgetContentTest {
    private fun note(
        id: String,
        text: String = id,
        color: String = "#EBD38B",
        state: NoteState = NoteState.ACTIVE,
        position: Double? = null,
        tags: List<String> = emptyList(),
        isProtected: Boolean = false,
        protectedJson: String? = null,
    ) = Note(
        id = id, text = text, lineEnding = LineEnding.LF, color = color, createdAt = Instant.EPOCH, updatedAt = Instant.EPOCH,
        state = state, tags = tags, dockPosition = position, isProtected = isProtected, protectedContentJson = protectedJson,
    )

    private val options = WidgetOptions(placeholder = "Nota nueva")

    private fun build(notes: Collection<Note>, o: WidgetOptions = options) = WidgetContentBuilder.build(notes, o)

    private fun open(strip: WidgetStrip) = assertIs<WidgetStrip.Open>(strip)

    // Regla 1
    @Test
    fun `solo salen las notas activas`() {
        val content = build(listOf(
            note("a"), note("b", state = NoteState.ARCHIVED), note("c", state = NoteState.TRASHED), note("d"),
        ))
        assertEquals(listOf("a", "d"), content.strips.map { it.id })
        assertEquals(0, content.hiddenCount)
    }

    // Regla 2
    @Test
    fun `salen en el orden del mazo`() {
        val content = build(listOf(note("a", position = 3.0), note("b", position = 1.0), note("c", position = 2.0)))
        assertEquals(listOf("b", "c", "a"), content.strips.map { it.id })
    }

    // Regla 3
    @Test
    fun `una protegida con nombre visible lo lleva en Locked, cortado al maximo`() {
        val named = note("p", isProtected = true, protectedJson = "{}").copy(protectedTitle = "Banco")
        assertEquals(WidgetStrip.Locked("p", "#EBD38B", "Banco"), build(listOf(named)).strips.single())
        // Sin proteger, el campo no cuenta aunque venga relleno.
        val plain = note("q").copy(protectedTitle = "Banco")
        open(build(listOf(plain)).strips.single())
    }

    @Test
    fun `una protegida es Locked con su color, con o sin vista previa, aunque tenga texto`() {
        val n = note("p", text = "texto en memoria\nsegunda", color = "#112233", isProtected = true)
        for (preview in listOf(false, true)) {
            val strip = build(listOf(n), options.copy(showPreview = preview)).strips.single()
            assertEquals(WidgetStrip.Locked("p", "#112233"), strip)
        }
    }

    @Test
    fun `ningun rastro de una protegida llega al resultado`() {
        val marker = "SECRETO-9f3a"
        val notes = listOf(
            note("p1", text = "$marker texto\n$marker segunda", tags = listOf("etiqueta-$marker"), isProtected = true, protectedJson = """{"x":"$marker"}"""),
            note("p2", text = "", tags = listOf("t-$marker"), isProtected = true, protectedJson = """{"Text":"$marker"}"""),
            note("o", text = "abierta"),
        )
        for (preview in listOf(false, true)) {
            val content = build(notes, options.copy(showPreview = preview))
            assertFalse(content.toString().contains(marker))
            content.strips.forEach { assertFalse(it.toString().contains(marker)) }
            assertEquals(2, content.strips.count { it is WidgetStrip.Locked })
        }
    }

    // Regla 4
    @Test
    fun `el titulo usa NoteTitleHelper con el relleno`() {
        val content = build(listOf(note("a", text = "# Compra\nleche"), note("b", text = "  ")))
        assertEquals("Compra", open(content.strips[0]).title)
        assertEquals("Nota nueva", open(content.strips[1]).title)
    }

    @Test
    fun `un titulo largo se corta a 120 con puntos suspensivos`() {
        val limit = WidgetContentBuilder.MAX_TITLE_LENGTH
        assertEquals(120, limit)
        val exact = "x".repeat(limit)
        assertEquals(exact, open(build(listOf(note("a", text = exact))).strips.single()).title)

        val cut = open(build(listOf(note("a", text = "x".repeat(limit + 1)))).strips.single()).title
        assertEquals(limit, cut.length)
        assertTrue(cut.endsWith("…"))
    }

    @Test
    fun `al cortar no se parte un par sustituto`() {
        val emoji = "😀" // dos unidades UTF-16
        // 118 x + emoji: la unidad 119 sería la mitad del emoji; hay que quitarlo entero.
        val text = "x".repeat(118) + emoji + "yyy"
        val title = open(build(listOf(note("a", text = text))).strips.single()).title
        assertEquals("x".repeat(118) + "…", title)
        assertTrue(title.none { it.isSurrogate() })
    }

    // Regla 5
    @Test
    fun `sin showPreview no hay vista previa`() {
        assertNull(open(build(listOf(note("a", text = "t\nresto"))).strips.single()).preview)
    }

    @Test
    fun `con showPreview la vista previa es la de NoteTitleHelper`() {
        val strip = open(build(listOf(note("a", text = "t\n☐ uno\ndos")), options.copy(showPreview = true)).strips.single())
        assertEquals("uno dos", strip.preview)
    }

    @Test
    fun `si la vista previa sale vacia es null`() {
        val o = options.copy(showPreview = true)
        assertNull(open(build(listOf(note("a", text = "solo titulo")), o).strips.single()).preview)
        assertNull(open(build(listOf(note("a", text = "t\n☐ \n  ")), o).strips.single()).preview)
    }

    @Test
    fun `una vista previa larga se corta a 200 con puntos suspensivos`() {
        val limit = WidgetContentBuilder.MAX_PREVIEW_LENGTH
        assertEquals(200, limit)
        val o = options.copy(showPreview = true)
        val exact = open(build(listOf(note("a", text = "t\n" + "y".repeat(limit))), o).strips.single()).preview
        assertEquals("y".repeat(limit), exact)
        val cut = open(build(listOf(note("a", text = "t\n" + "y".repeat(limit + 1))), o).strips.single()).preview!!
        assertEquals(limit, cut.length)
        assertTrue(cut.endsWith("…"))
    }

    // Regla 6
    @Test
    fun `las etiquetas no salen`() {
        val strip = open(build(listOf(note("a", text = "t\nr", tags = listOf("secreta-etiqueta"))), options.copy(showPreview = true)).strips.single())
        assertFalse(strip.toString().contains("secreta-etiqueta"))
    }

    // Regla 7
    @Test
    fun `el limite cuenta las tiras y lo que no cabe va a hiddenCount`() {
        val notes = (1..5).map { note("n$it", position = it.toDouble()) } + note("p", position = 6.0, isProtected = true) +
            note("arch", state = NoteState.ARCHIVED)
        val content = build(notes, options.copy(limit = 4))
        assertEquals(listOf("n1", "n2", "n3", "n4"), content.strips.map { it.id })
        assertEquals(2, content.hiddenCount)

        val withLocked = build(notes, options.copy(limit = 6))
        assertIs<WidgetStrip.Locked>(withLocked.strips.last())
        assertEquals(0, withLocked.hiddenCount)
    }

    @Test
    fun `limite cero o negativo oculta todo`() {
        val notes = listOf(note("a"), note("b"))
        for (limit in listOf(0, -3)) {
            val content = build(notes, options.copy(limit = limit))
            assertTrue(content.strips.isEmpty())
            assertEquals(2, content.hiddenCount)
        }
    }

    @Test
    fun `el limite por defecto es 50`() {
        assertEquals(50, WidgetOptions.DEFAULT_LIMIT)
        assertEquals(50, options.limit)
        val content = build((1..53).map { note("n$it", position = it.toDouble()) })
        assertEquals(50, content.strips.size)
        assertEquals(3, content.hiddenCount)
    }

    // Regla 8
    @Test
    fun `sin notas activas, vacio y sin ocultas`() {
        assertEquals(WidgetContent(emptyList(), 0), build(emptyList()))
        assertEquals(WidgetContent(emptyList(), 0), build(listOf(note("a", state = NoteState.TRASHED))))
    }

    // Regla 9
    @Test
    fun `el color va tal cual, sin normalizar`() {
        assertEquals("#ebd38b", build(listOf(note("a", color = "#ebd38b"))).strips.single().color)
    }
}

package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

class ConflictDiffTest {
    @Test
    fun `summarize devuelve null con el mismo texto`() {
        assertNull(ConflictDiff.summarize("a\nb", "a\nb"))
        assertNull(ConflictDiff.summarize("", ""))
    }

    @Test
    fun `CRLF y LF cuentan igual`() {
        assertNull(ConflictDiff.summarize("a\r\nb", "a\nb"))
        assertNull(ConflictDiff.summarize("a\nb", "a\r\nb"))
    }

    @Test
    fun `summarize da la primera linea distinta y cuantas hay`() {
        val summary = ConflictDiff.summarize("uno\ndos\ntres\ncuatro", "uno\nDOS\ntres\nCUATRO")!!
        assertEquals(ConflictDiffSummary(line = 2, winnerLine = "dos", losingLine = "DOS", differingLines = 2), summary)
    }

    @Test
    fun `una linea que falta cuenta como vacia`() {
        val longerWinner = ConflictDiff.summarize("a\nb\nc", "a")!!
        assertEquals(ConflictDiffSummary(2, "b", "", 2), longerWinner)
        val longerLosing = ConflictDiff.summarize("a", "a\nb")!!
        assertEquals(ConflictDiffSummary(2, "", "b", 1), longerLosing)
    }

    @Test
    fun `una linea vacia de mas no es una diferencia si falta en el otro lado`() {
        // "a\n" son dos líneas ("a" y ""), y la que falta cuenta como vacía: no hay nada distinto.
        assertNull(ConflictDiff.summarize("a\n", "a"))
    }

    @Test
    fun `clip recorta los extremos y pone puntos suspensivos si se pasa`() {
        assertEquals("hola", ConflictDiff.clip("  hola \t", 10))
        assertEquals("hola", ConflictDiff.clip("hola", 4))
        assertEquals("hol…", ConflictDiff.clip("hola mundo", 3))
        assertEquals("", ConflictDiff.clip("   ", 3))
    }

    @Test
    fun `lineDiff marca las lineas de la otra version que difieren por posicion`() {
        val lines = ConflictDiff.lineDiff("uno\ndos\ntres", "uno\nDOS\ntres")
        assertEquals(listOf(DiffLine(1, "uno", false), DiffLine(2, "DOS", true), DiffLine(3, "tres", false)), lines)
    }

    @Test
    fun `lineDiff con textos iguales no marca nada`() {
        val lines = ConflictDiff.lineDiff("a\nb", "a\nb")
        assertEquals(listOf(DiffLine(1, "a", false), DiffLine(2, "b", false)), lines)
    }

    @Test
    fun `lineDiff con CRLF compara y devuelve las lineas sin el retorno`() {
        val lines = ConflictDiff.lineDiff("a\nb", "a\r\nX")
        assertEquals(listOf(DiffLine(1, "a", false), DiffLine(2, "X", true)), lines)
    }

    @Test
    fun `lineDiff con la otra version vacia es una linea vacia`() {
        assertEquals(listOf(DiffLine(1, "", true)), ConflictDiff.lineDiff("algo", ""))
        assertEquals(listOf(DiffLine(1, "", false)), ConflictDiff.lineDiff("", ""))
    }

    @Test
    fun `lineDiff con la otra version mas larga marca las lineas extra no vacias`() {
        val lines = ConflictDiff.lineDiff("a", "a\nb\n")
        // La ganadora no tiene línea 2 ni 3: cuentan como vacías, así que "b" difiere y la última línea vacía no.
        assertEquals(listOf(DiffLine(1, "a", false), DiffLine(2, "b", true), DiffLine(3, "", false)), lines)
    }

    @Test
    fun `lineDiff con la otra version mas corta solo enseña sus lineas`() {
        // Lo que sobra en la ganadora no es texto de la otra versión: no se inventan líneas vacías al pintarla.
        val lines = ConflictDiff.lineDiff("a\nb\nc", "a")
        assertEquals(listOf(DiffLine(1, "a", false)), lines)
    }

    @Test
    fun `lineDiff y summarize cuentan las mismas diferencias cuando la otra version no es mas corta`() {
        val winner = "a\nb\nc"
        val losing = "a\nX\nc\nd"
        assertEquals(ConflictDiff.summarize(winner, losing)!!.differingLines, ConflictDiff.lineDiff(winner, losing).count { it.differs })
    }
}

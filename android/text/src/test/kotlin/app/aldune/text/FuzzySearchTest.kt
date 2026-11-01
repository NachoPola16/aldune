package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue

/** The same cases as `FuzzySearchTests` in C#. */
class FuzzySearchTest {
    private fun matches(query: String, text: String) = assertTrue(FuzzySearch.score(query, text) >= FuzzySearch.MINIMUM_SCORE) { "$query / $text" }
    private fun misses(query: String, text: String) = assertTrue(FuzzySearch.score(query, text) < FuzzySearch.MINIMUM_SCORE) { "$query / $text" }

    @Test fun `ignora mayusculas y acentos`() {
        matches("sincronizacion", "Sincronización")
        matches("SINCRONIZACIÓN", "sincronizacion")
        matches("tamano", "Tamaño del texto")
        matches("dock", "Posición del dock")
    }

    @Test fun `tolera erratas`() {
        matches("sincornizacion", "Sincronización")
        matches("sincronizcion", "Sincronización")
        matches("sincronizzacion", "Sincronización")
        matches("notificasiones", "Notificaciones")
        matches("shadow", "Shadows on the dock")
        matches("colour", "Color theme")
        matches("sond volme", "Volume Sounds")
    }

    @Test fun `palabras a medias y en cualquier orden`() {
        matches("sinc", "Sincronización")
        matches("notif", "Notificaciones y sonidos")
        matches("dock pos", "Posición del dock")
        matches("sonidos notificaciones", "Notificaciones y sonidos")
    }

    @Test fun `no encuentra lo que no tiene que ver`() {
        misses("zzz", "Sincronización")
        misses("sombra", "Posición del dock")
        misses("dock sonido", "Posición del dock")
        misses("ab", "Posición del dock")
    }

    @Test fun `exacto antes que errata antes que parcial`() {
        val exact = FuzzySearch.score("sincronizacion", "Sincronización")
        val typo = FuzzySearch.score("sincornizacion", "Sincronización")
        val partial = FuzzySearch.score("sinc", "Sincronización")
        assertTrue(exact > typo)
        assertTrue(exact > partial)
    }

    @Test fun `rank filtra y ordena sin alterar los empates`() {
        val items = listOf("Sincronización", "Sonidos", "Sincronizar ahora", "Aspecto")
        assertEquals(listOf("Sincronización", "Sincronizar ahora"), FuzzySearch.rank(items, { it }, "sincroniz"))
        assertTrue(FuzzySearch.rank(listOf("a", "b"), { it }, "  ").isEmpty())
    }
}

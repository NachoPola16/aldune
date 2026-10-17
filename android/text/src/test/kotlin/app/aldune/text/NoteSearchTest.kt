package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import java.util.Locale

class NoteSearchTest {
    @Test fun ignoraMayusculas() {
        assertTrue(NoteSearch.matches("Comprar LECHE", "leche"))
        assertTrue(NoteSearch.matches("comprar leche", "LECHE"))
    }

    @Test fun ignoraAcentosEnLaNotaYEnLaConsulta() {
        assertTrue(NoteSearch.matches("Café con leche", "cafe"))
        assertTrue(NoteSearch.matches("cafe con leche", "CAFÉ"))
        assertTrue(NoteSearch.matches("El niño juega", "nino"))
        assertTrue(NoteSearch.matches("El nino juega", "niño"))
    }

    @Test fun laConsultaYaDescompuestaTambienCoincide() {
        // "e" + acento combinante (NFD) frente a "é" precompuesta (NFC).
        assertTrue(NoteSearch.matches("café", "café"))
        assertTrue(NoteSearch.matches("café", "café"))
    }

    @Test fun consultaVaciaOSoloEspaciosCoincideConTodo() {
        assertTrue(NoteSearch.matches("lo que sea", ""))
        assertTrue(NoteSearch.matches("lo que sea", "   "))
        assertTrue(NoteSearch.matches("lo que sea", " \t\r\n "))
        assertTrue(NoteSearch.matches("", ""))
        assertTrue(NoteSearch.matchesNote("x", emptyList(), "  "))
    }

    @Test fun recortaLaConsulta() {
        assertTrue(NoteSearch.matches("comprar leche", "  leche \n"))
        // Solo los extremos: un espacio interior sigue siendo parte de la consulta.
        assertTrue(NoteSearch.matches("comprar leche", " comprar leche "))
        assertFalse(NoteSearch.matches("comprarleche", "comprar leche"))
    }

    @Test fun textoVacioSoloEncuentraConsultaVacia() {
        assertFalse(NoteSearch.matches("", "a"))
    }

    @Test fun noEncuentraLoQueNoEsta() {
        assertFalse(NoteSearch.matches("Café con leche", "te"))
        assertFalse(NoteSearch.matches("hola", "hola mundo"))
    }

    @Test fun buscaTambienEnLasEtiquetas() {
        assertTrue(NoteSearch.matchesNote("sin relación", listOf("Trabajo", "Urgente"), "TRABAJO"))
        assertTrue(NoteSearch.matchesNote("sin relación", listOf("Ocupación"), "ocupacion"))
        assertFalse(NoteSearch.matchesNote("sin relación", listOf("Trabajo"), "casa"))
        assertTrue(NoteSearch.matchesNote("tiene casa", emptyList(), "casa"))
        assertFalse(NoteSearch.matchesNote("nada", emptyList(), "casa"))
    }

    @Test fun nYEnyeSoloSePleganPorElAcento() {
        // La tilde de la ñ es una marca combinante, así que «n» y «ñ» se igualan; es la misma regla que «e» y «é».
        assertTrue(NoteSearch.matches("año", "ano"))
        assertTrue(NoteSearch.matches("ano", "año"))
        // Letras distintas que no se descomponen siguen siendo distintas.
        assertFalse(NoteSearch.matches("straße", "strasse"))
        assertFalse(NoteSearch.matches("Ørsted", "orsted"))
        assertFalse(NoteSearch.matches("l", "ł"))
        // Y dos letras sin relación, por supuesto.
        assertFalse(NoteSearch.matches("pino", "pimo"))
    }

    @Test fun manejaEmojiYParesSustitutos() {
        assertTrue(NoteSearch.matches("Fiesta 🎉 en casa", "🎉"))
        assertTrue(NoteSearch.matches("Fiesta 🎉 en casa", "🎉 en"))
        assertTrue(NoteSearch.matches("Fiesta 🎉 en casa", "fiesta 🎉"))
        assertFalse(NoteSearch.matches("Fiesta 🎉 en casa", "🎂"))
        // Emoji con selector de variación (marca Mn): se quita igual de ambos lados, así que coincide con o sin él.
        assertTrue(NoteSearch.matches("corazón ❤️ rojo", "❤"))
        assertTrue(NoteSearch.matches("corazón ❤ rojo", "❤️"))
        // Un par sustituto suelto no rompe el plegado ni lanza.
        assertFalse(NoteSearch.matches("\uD83D", "a"))
    }

    @Test fun alemanFrancesYPortugues() {
        assertTrue(NoteSearch.matches("Über die Brücke", "uber"))
        assertTrue(NoteSearch.matches("Über die Brücke", "brucke"))
        assertTrue(NoteSearch.matches("Schöne Grüße", "schone gru"))
        // La «ß» no se descompone: «grusse» no encuentra «Grüße» (ver nYEnyeSoloSePleganPorElAcento).
        assertFalse(NoteSearch.matches("Schöne Grüße", "grusse"))
        assertTrue(NoteSearch.matches("Où est la crème brûlée ?", "creme brulee"))
        assertTrue(NoteSearch.matches("Où est la crème brûlée ?", "ou est"))
        assertTrue(NoteSearch.matches("Ação de coração", "acao"))
        assertTrue(NoteSearch.matches("Ação de coração", "coracao"))
        assertTrue(NoteSearch.matches("São Paulo é grande", "sao paulo e"))
    }

    @Test fun noDependeDelIdiomaDelMovil() {
        val previous = Locale.getDefault()
        try {
            // Con configuración regional turca, «I».lowercase() daría «ı» (i sin punto) y «INFO» no encontraría «info».
            Locale.setDefault(Locale.forLanguageTag("tr-TR"))
            assertTrue(NoteSearch.matches("INFO", "info"))
            assertTrue(NoteSearch.matches("info", "INFO"))
            assertTrue(NoteSearch.matches("İstanbul", "istanbul"))
        } finally {
            Locale.setDefault(previous)
        }
    }

    @Test fun saltosDeLineaYTextoLargo() {
        assertTrue(NoteSearch.matches("línea uno\r\nlínea dos\nlínea tres", "dos"))
        assertTrue(NoteSearch.matches("x".repeat(100_000) + "Ñandú", "nandu"))
    }
}

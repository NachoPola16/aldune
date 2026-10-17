package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

class ListKeysTest {
    private fun at(text: String, caret: Int) = TextAndCaret(text, caret)

    @Test fun enterAlFinalDeUnaTareaContinuaLaLista() {
        // Antes: "☐ a" con el cursor al final; el teclado inserta "\n".
        val result = ListKeys.resolve(at("☐ a", 3), at("☐ a\n", 4))
        assertEquals(at("☐ a\n☐ ", 6), result)
    }

    @Test fun enterAlFinalDeUnaVinietaContinuaLaLista() {
        assertEquals(at("→ a\n→ ", 6), ListKeys.resolve(at("→ a", 3), at("→ a\n", 4)))
    }

    @Test fun enterEnUnaTareaVaciaLaTermina() {
        assertEquals(at("", 0), ListKeys.resolve(at("☐ ", 2), at("☐ \n", 3)))
    }

    @Test fun enterEnMitadDeLineaSeDejaPasar() {
        assertNull(ListKeys.resolve(at("☐ ab", 3), at("☐ a\nb", 4)))
    }

    @Test fun enterEnUnaLineaNormalSeDejaPasar() {
        assertNull(ListKeys.resolve(at("hola", 4), at("hola\n", 5)))
    }

    @Test fun enterConSangriaConservaLaSangria() {
        assertEquals(at("    ☐ a\n    ☐ ", 14), ListKeys.resolve(at("    ☐ a", 7), at("    ☐ a\n", 8)))
    }

    @Test fun retrocesoTrasLaCasillaQuitaElPrefijoEntero() {
        // "☐ x" con el cursor tras el espacio (2); el teclado borra el espacio y deja "☐x" con el cursor en 1.
        assertEquals(at("x", 0), ListKeys.resolve(at("☐ x", 2), at("☐x", 1)))
    }

    @Test fun retrocesoTrasLaVinietaQuitaElPrefijoEntero() {
        assertEquals(at("", 0), ListKeys.resolve(at("→ ", 2), at("→", 1)))
    }

    @Test fun retrocesoEnTextoNormalSeDejaPasar() {
        assertNull(ListKeys.resolve(at("☐ ab", 4), at("☐ a", 3)))
    }

    @Test fun cualquierOtraEdicionSeDejaPasar() {
        // Escribir una letra, pegar varios caracteres o cambiar solo la selección no son ni Enter ni Retroceso.
        assertNull(ListKeys.resolve(at("☐ a", 3), at("☐ ab", 4)))
        assertNull(ListKeys.resolve(at("☐ a", 3), at("☐ a\n\n", 5)))
        assertNull(ListKeys.resolve(at("☐ a", 3), at("☐ a", 1)))
    }

    @Test fun conSeleccionNoSeInterpreta() {
        // Reemplazar una selección por "\n" es Enter sobre un rango: el editor normal lo resuelve.
        assertNull(ListKeys.resolve(TextSelection("☐ abc", 2, 5), at("☐ \n", 3)))
    }
}

package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull

/** Las mismas reglas que `ProtectedTitleRules` de Windows (Core): una línea, recortado, con tope, vacío = sin nombre. */
class ProtectedTitleRulesTest {
    @Test fun `recorta los espacios`() = assertEquals("Banco", ProtectedTitleRules.normalize("  Banco  "))

    @Test fun `junta las lineas en una`() = assertEquals("Una línea", ProtectedTitleRules.normalize("Una\r\nlínea"))

    @Test fun `vacio o solo espacios es sin nombre`() {
        assertNull(ProtectedTitleRules.normalize("   "))
        assertNull(ProtectedTitleRules.normalize(null))
        assertNull(ProtectedTitleRules.normalize("\n\t\r"))
    }

    @Test fun `corta en el maximo`() =
        assertEquals(ProtectedTitleRules.MAX_LENGTH, ProtectedTitleRules.normalize("a".repeat(200))!!.length)

    @Test fun `no deja a medias un par sustituto al cortar`() {
        val title = "a".repeat(ProtectedTitleRules.MAX_LENGTH - 1) + "🛒"
        val cut = ProtectedTitleRules.normalize(title)!!
        assertEquals("a".repeat(ProtectedTitleRules.MAX_LENGTH - 1), cut)
    }

    @Test fun `visible solo cuenta en una nota protegida`() {
        assertEquals("Banco", ProtectedTitleRules.visible(true, " Banco "))
        assertNull(ProtectedTitleRules.visible(true, null))
        assertNull(ProtectedTitleRules.visible(false, "Banco"))
    }
}

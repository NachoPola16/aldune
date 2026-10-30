package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

class ColorNamesTest {
    @Test
    fun `los colores de fabrica de las notas tienen su nombre`() {
        assertEquals(ColorName.YELLOW, ColorNames.of("#EBD38B"))
        assertEquals(ColorName.GREEN, ColorNames.of("#AAE6B1"))
        assertEquals(ColorName.TURQUOISE, ColorNames.of("#83E7F2"))
        assertEquals(ColorName.BLUE, ColorNames.of("#C2D4FF"))
        assertEquals(ColorName.PINK, ColorNames.of("#F9BEF2"))
        assertEquals(ColorName.ORANGE, ColorNames.of("#FFC5AF"))
    }

    @Test
    fun `un rojo palido es rosa y uno intenso es rojo`() {
        assertEquals(ColorName.PINK, ColorNames.of("#FFB3B3"))
        assertEquals(ColorName.RED, ColorNames.of("#D32F2F"))
    }

    @Test
    fun `un naranja oscuro es marron`() {
        assertEquals(ColorName.ORANGE, ColorNames.of("#F57C00"))
        assertEquals(ColorName.BROWN, ColorNames.of("#6D4C2F"))
    }

    @Test
    fun `los grises se reparten en blanco, gris y negro`() {
        assertEquals(ColorName.WHITE, ColorNames.of("#FAFAFA"))
        assertEquals(ColorName.GRAY, ColorNames.of("#808080"))
        assertEquals(ColorName.BLACK, ColorNames.of("#111111"))
    }

    @Test
    fun `los tonos fuera de las bandas caen en el nombre vecino`() {
        assertEquals(ColorName.PURPLE, ColorNames.of("#7E57C2"))
        assertEquals(ColorName.GREEN, ColorNames.of("#2E7D32"))
        assertEquals(ColorName.BLUE, ColorNames.of("#1976D2"))
    }

    @Test
    fun `un texto que no es un color no tiene nombre`() {
        assertNull(ColorNames.of("rojo"))
        assertNull(ColorNames.of(""))
    }
}

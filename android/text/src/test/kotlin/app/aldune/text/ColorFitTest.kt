package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class ColorFitTest {
    @Test fun `ratio de blanco y negro es 21 y un color invalido da 1`() {
        assertEquals(21.0, ColorFit.ratio("#FFFFFF", "#000000"), 0.0001)
        assertEquals(1.0, ColorFit.ratio("rojo", "#000000"), 0.0)
    }

    @Test fun `un color que ya se lee se devuelve en mayusculas sin tocarlo`() =
        assertEquals("#1E1A14", ColorFit.foreground("#1e1a14", "#FFFFFF", 4.5))

    @Test fun `un fondo demasiado claro para texto blanco se oscurece hasta el minimo`() {
        val fitted = ColorFit.background("#FFFF00", "#FFFFFF", 4.5)
        assertTrue(ColorFit.ratio(fitted, "#FFFFFF") >= 4.5)
    }

    @Test fun `un texto oscuro sobre fondo oscuro se aclara`() {
        val fitted = ColorFit.foreground("#101010", "#202020", 4.5)
        assertTrue(ColorFit.ratio(fitted, "#202020") >= 4.5)
    }

    @Test fun `lighter y darker mueven la claridad y se quedan en 0-1`() {
        assertEquals("#FFFFFF", ColorFit.lighter("#FFFFFF", 0.5))
        assertEquals("#000000", ColorFit.darker("#000000", 0.5))
        assertEquals("nope", ColorFit.lighter("nope", 0.1))
    }

    @Test fun `toward mezcla en sRGB y redondea hacia arriba la mitad`() {
        assertEquals("#000000", ColorMix.toward("#000000", "#FFFFFF", 0.0))
        assertEquals("#FFFFFF", ColorMix.toward("#000000", "#FFFFFF", 1.0))
        assertEquals("#808080", ColorMix.toward("#000000", "#FFFFFF", 0.5)) // 127.5 → 128
        assertEquals("#000000", ColorMix.toward("#000000", "#FFFFFF", -3.0))
        assertEquals("rojo", ColorMix.toward("rojo", "#FFFFFF", 0.5))
    }
}

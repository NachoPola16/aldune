package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull

class CustomColorTest {
    @Test fun `normalize acepta con o sin almohadilla y devuelve mayusculas`() {
        assertEquals("#F5E3B3", CustomColor.normalize("f5e3b3"))
        assertEquals("#F5E3B3", CustomColor.normalize("  #f5E3b3 "))
    }

    @Test fun `normalize rechaza vacio, longitud distinta de seis y caracteres que no son hexadecimales`() {
        assertNull(CustomColor.normalize(null))
        assertNull(CustomColor.normalize("   "))
        assertNull(CustomColor.normalize("#FFF"))
        assertNull(CustomColor.normalize("#F5E3B3FF"))
        assertNull(CustomColor.normalize("#GGHHII"))
    }

    @Test fun `toHex y channels son inversos`() {
        assertEquals("#0A10FF", CustomColor.toHex(10, 16, 255))
        assertEquals(Triple(10, 16, 255), CustomColor.channels("#0A10FF"))
        assertNull(CustomColor.channels("rojo"))
    }

    @Test fun `toHex recorta cada canal a 0-255`() {
        assertEquals("#00FF00", CustomColor.toHex(-5, 300, 0))
    }
}

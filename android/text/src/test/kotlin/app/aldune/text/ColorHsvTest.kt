package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue

class ColorHsvTest {
    @Test fun `los primarios tienen su tono`() {
        assertEquals(0f, ColorHsv.fromHex("#FF0000")!!.hue)
        assertEquals(120f, ColorHsv.fromHex("#00FF00")!!.hue)
        assertEquals(240f, ColorHsv.fromHex("#0000FF")!!.hue)
    }

    @Test fun `un gris no tiene saturacion y el negro no tiene valor`() {
        assertEquals(0f, ColorHsv.fromHex("#808080")!!.saturation)
        assertEquals(0f, ColorHsv.fromHex("#000000")!!.value)
    }

    @Test fun `ida y vuelta conserva el color`() {
        for (hex in listOf("#EBD38B", "#3FD0E0", "#7CFC7C", "#102030", "#FFFFFF", "#9C1F6E")) {
            val hsv = ColorHsv.fromHex(hex)!!
            assertEquals(hex, ColorHsv.toHex(hsv.hue, hsv.saturation, hsv.value))
        }
    }

    @Test fun `un valor que no es color da null y los fuera de rango se recortan`() {
        assertNull(ColorHsv.fromHex("rojo"))
        assertEquals("#FFFFFF", ColorHsv.toHex(0f, -1f, 2f))
        assertEquals(ColorHsv.toHex(0f, 1f, 1f), ColorHsv.toHex(360f, 1f, 1f))
    }

    @Test fun `la rejilla son colores validos y sin repetir`() {
        assertTrue(ColorHsv.swatches.all { CustomColor.normalize(it) != null })
        assertEquals(ColorHsv.swatches.size, ColorHsv.swatches.toSet().size)
        assertNotNull(ColorHsv.swatches.firstOrNull())
    }
}

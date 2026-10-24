package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class AspectColorsJsonTest {
    @Test fun `ida y vuelta conserva lo elegido`() {
        val colors = mapOf("bash" to mapOf("accent" to "#D79921"), "xp-light" to mapOf("titleBar" to "#A8452A"))
        assertEquals(colors, AspectColorsJson.parse(AspectColorsJson.write(colors)))
    }

    @Test fun `la forma es la de AspectColors de settings json de Windows`() =
        assertEquals("""{"bash":{"accent":"#D79921"}}""", AspectColorsJson.write(mapOf("bash" to mapOf("accent" to "#D79921"))))

    // Review Focus 3: un valor roto cae en el de fábrica (aquí, se descarta) y no rompe nada.
    @Test fun `un json ilegible, vacio o nulo da un mapa vacio`() {
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse(null))
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse(""))
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse("{no es json"))
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse("[1,2]"))
    }

    @Test fun `descarta lo que no es un color y conserva el resto`() {
        val json = """{"bash":{"accent":"rojo","user":"#8ae234","path":5},"xp-light":"x"}"""
        assertEquals(mapOf("bash" to mapOf("user" to "#8AE234")), AspectColorsJson.parse(json))
    }
}

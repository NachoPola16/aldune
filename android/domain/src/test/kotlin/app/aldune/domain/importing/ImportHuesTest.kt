package app.aldune.domain.importing

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class ImportHuesTest {
    @Test fun `names map to the closest family`() {
        val expected = mapOf(
            "YELLOW" to ImportHue.Yellow, "Green" to ImportHue.Green, "TEAL" to ImportHue.Cyan, "CERULEAN" to ImportHue.Cyan,
            "blue" to ImportHue.Blue, "PURPLE" to ImportHue.Blue, "PINK" to ImportHue.Pink, "RED" to ImportHue.Orange,
            "ORANGE" to ImportHue.Orange, "BROWN" to ImportHue.Orange,
        )
        for ((name, hue) in expected) assertEquals(hue, ImportHues.fromName(name), name)
    }

    @Test fun `neutrals and unknowns have no hue`() {
        for (name in listOf("DEFAULT", "Gray", "Charcoal", "white", "", "nonsense")) assertNull(ImportHues.fromName(name), name)
        assertNull(ImportHues.fromName(null))
    }

    @Test fun `hex uses the hue unless it is grey`() {
        assertEquals(ImportHue.Yellow, ImportHues.fromHex("#FFF740"))
        assertEquals(ImportHue.Blue, ImportHues.fromHex("#3A7BD5"))
        assertNull(ImportHues.fromHex("#808080"))
        assertNull(ImportHues.fromHex("not a colour"))
    }
}

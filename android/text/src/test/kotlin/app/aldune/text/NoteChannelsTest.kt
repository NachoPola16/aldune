package app.aldune.text

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotEquals

class NoteChannelsTest {
    @Test fun `los canales dan la vuelta cada cuatro notas`() {
        assertEquals("CH1", NoteChannels.number(0))
        assertEquals("CH4", NoteChannels.number(3))
        assertEquals("CH1", NoteChannels.number(4))
        assertEquals("CH4", NoteChannels.number(-1))
    }

    @Test fun `cada canal tiene su onda y su clave de paleta`() {
        assertEquals(4, (0 until 4).map { NoteChannels.wavePath(it) }.toSet().size)
        assertEquals("Channel2", NoteChannels.paletteKey(1))
    }

    @Test fun `la cara tintada sale del color del canal y no es el canal`() {
        val palette = AppPalette.forMode(AppPalette.TELECOM_DARK, systemUsesLight = false)
        val tint = NoteChannels.tint(1, palette, null)
        assertEquals(palette.getValue("Channel2"), tint.accent)
        assertNotEquals(tint.accent, tint.face)
    }

    @Test fun `con un color unico todos los canales llevan el mismo acento`() {
        val palette = AppPalette.forMode(AppPalette.TELECOM_DARK, systemUsesLight = false)
        assertEquals(NoteChannels.tint(0, palette, "#EBD38B").accent, NoteChannels.tint(2, palette, "#EBD38B").accent)
    }
}

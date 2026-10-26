package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class NoteColorChoicesTest {
    private val theme = listOf("#EBD38B", "#AAE6B1", "#83E7F2")

    @Test fun `sin colores guardados la paleta es la del tema`() {
        assertEquals(theme, NoteColorChoices.palette(theme, emptyList()))
    }

    @Test fun `un color fuera del tema va delante, como el negro de un aspecto retro`() {
        assertEquals(listOf("#000000") + theme, NoteColorChoices.palette(theme, listOf("#000000")))
    }

    @Test fun `un color que ya esta en el tema no se repite, tampoco si cambia la capitalizacion`() {
        assertEquals(theme, NoteColorChoices.palette(theme, listOf("#ebd38b")))
    }

    @Test fun `los colores de la sesion se conservan y el original no desaparece al elegir otro`() {
        // El negro abre seleccionado; el usuario elige un verde del tema y luego uno propio: el negro tiene que seguir ahí para volver a él.
        assertEquals(listOf("#000000", "#123456") + theme, NoteColorChoices.palette(theme, listOf("#000000", "#AAE6B1", "#123456")))
    }

    @Test fun `un color guardado repetido sale una sola vez`() {
        assertEquals(listOf("#000000") + theme, NoteColorChoices.palette(theme, listOf("#000000", "#000000")))
    }
}

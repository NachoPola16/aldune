package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue

class SoundPlanTest {
    @Test
    fun `las claves de los eventos son las de Windows y no se repiten`() {
        val ids = SoundEvent.entries.map { it.id }
        assertEquals(
            listOf("reminder", "syncDone", "syncFailed", "noteCreated", "noteArchived", "noteOpened", "noteClosed", "taskChecked", "noteDeleted", "typing", "textDeleted"),
            ids,
        )
        assertEquals(ids.size, ids.toSet().size)
    }

    @Test
    fun `las opciones conservan los numeros de Windows`() {
        assertEquals(0, SoundChoice.NONE.code)
        assertEquals(7, SoundChoice.PAGE_TURN.code)
        assertEquals(8, SoundChoice.PAPER_FOLD.code)
        assertEquals(9, SoundChoice.PEN_TAP.code)
        assertEquals(10, SoundChoice.PAPER_CRUMPLE.code)
        assertEquals(11, SoundChoice.PEN_SCRATCH.code)
        assertEquals(12, SoundChoice.ERASER.code)
    }

    @Test
    fun `apagado de fabrica no suena nada`() {
        for (event in SoundEvent.entries) assertNull(SoundPlan.resolve(SoundSettings(), event))
    }

    @Test
    fun `encendido sin elegir nada tampoco suena`() {
        // En Android los avisos del sistema ya traen su sonido; los de la app solo suenan si se eligen.
        for (event in SoundEvent.entries) assertNull(SoundPlan.resolve(SoundSettings(enabled = true), event))
    }

    @Test
    fun `una eleccion por evento suena y el interruptor general la silencia`() {
        val choices = mapOf("noteOpened" to SoundChoice.PAGE_TURN.code)
        assertEquals(SoundChoice.PAGE_TURN, SoundPlan.resolve(SoundSettings(enabled = true, choices = choices), SoundEvent.NOTE_OPENED))
        assertNull(SoundPlan.resolve(SoundSettings(enabled = false, choices = choices), SoundEvent.NOTE_OPENED))
    }

    @Test
    fun `una opcion que Android no tiene, como un sonido de Windows, se queda en silencio`() {
        val choices = mapOf("noteOpened" to 2, "noteClosed" to 99, "typing" to 6)
        val settings = SoundSettings(enabled = true, choices = choices)
        assertNull(SoundPlan.resolve(settings, SoundEvent.NOTE_OPENED))
        assertNull(SoundPlan.resolve(settings, SoundEvent.NOTE_CLOSED))
        assertNull(SoundPlan.resolve(settings, SoundEvent.TYPING))
    }

    @Test
    fun `los sonidos sugeridos asignan los cuatro eventos de las notas y respetan lo demas`() {
        val before = mapOf("syncDone" to 3)
        val after = SoundPlan.paperAndPen(before)
        assertEquals(SoundChoice.PAGE_TURN.code, after["noteOpened"])
        assertEquals(SoundChoice.PAPER_FOLD.code, after["noteClosed"])
        assertEquals(SoundChoice.PEN_TAP.code, after["taskChecked"])
        assertEquals(SoundChoice.PAPER_CRUMPLE.code, after["noteDeleted"])
        // Escribir y borrar texto suenan en cada pulsación: se activan a mano.
        assertNull(after["typing"])
        assertNull(after["textDeleted"])
        assertEquals(3, after["syncDone"])
        assertEquals(1, before.size) // no toca el original
    }

    @Test
    fun `al encender los sonidos por primera vez se aplican los sugeridos`() {
        val applied = SoundPlan.paperAndPenIfUnset(emptyMap())
        assertEquals(SoundChoice.PAGE_TURN.code, applied?.get("noteOpened"))
    }

    @Test
    fun `si ya se eligio algo para las notas no se pisa nada`() {
        assertNull(SoundPlan.paperAndPenIfUnset(mapOf("noteClosed" to 0)))
    }

    @Test
    fun `borrar texto es un cambio que acorta el texto`() {
        assertTrue(TypingThrottle.isDeleting("hola", "hol"))
        assertTrue(TypingThrottle.isDeleting("hola", ""))
        assertFalse(TypingThrottle.isDeleting("hola", "holas"))
        assertFalse(TypingThrottle.isDeleting("hola", "hola"))
    }

    @Test
    fun `el volumen va de 0 a 1 y 100 es el de fabrica`() {
        assertEquals(1.0f, SoundSettings().volumeFactor)
        assertEquals(0.5f, SoundSettings(volume = 50).volumeFactor)
        assertEquals(0f, SoundSettings(volume = -4).volumeFactor)
        assertEquals(1f, SoundSettings(volume = 400).volumeFactor)
    }

    @Test
    fun `escribir suena como mucho cada 70 ms`() {
        val t = TypingThrottle()
        assertTrue(t.tryAcquire(1000))
        assertFalse(t.tryAcquire(1069))
        assertTrue(t.tryAcquire(1070))
        assertFalse(t.tryAcquire(1100))
    }

    @Test
    fun `solo suenan los caracteres imprimibles`() {
        assertTrue(TypingThrottle.isPrintable('a'))
        assertTrue(TypingThrottle.isPrintable(' '))
        assertTrue(TypingThrottle.isPrintable('ñ'))
        assertFalse(TypingThrottle.isPrintable('\n'))
        assertFalse(TypingThrottle.isPrintable('\t'))
        assertFalse(TypingThrottle.isPrintable('\u007f'))
    }

    @Test
    fun `el texto que crece con caracteres imprimibles cuenta como escribir y borrar no`() {
        assertTrue(TypingThrottle.isTyping("hola", "hola m"))
        assertTrue(TypingThrottle.isTyping("", "a"))
        assertFalse(TypingThrottle.isTyping("hola m", "hola "))   // borrar
        assertFalse(TypingThrottle.isTyping("hola", "hola\n"))    // intro
        assertFalse(TypingThrottle.isTyping("hola", "hola"))      // sin cambio
        assertTrue(TypingThrottle.isTyping("ab", "aXb"))          // en medio
    }
}

package app.aldune.syncclient

import app.aldune.sync.Invitations
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class QrEncoderTest {
    /** Dibuja los módulos como lo vería la cámara (con la zona de silencio de 4 módulos) para pasárselos al lector. */
    private fun lumaOf(modules: Array<BooleanArray>, scale: Int = 6): Triple<ByteArray, Int, Int> {
        val side = (modules.size + 8) * scale
        val out = ByteArray(side * side) { 255.toByte() }
        for (y in modules.indices) for (x in modules.indices) {
            if (!modules[y][x]) continue
            for (dy in 0 until scale) for (dx in 0 until scale) out[((y + 4) * scale + dy) * side + (x + 4) * scale + dx] = 0
        }
        return Triple(out, side, side)
    }

    @Test fun `lo que dibuja lo lee el lector de la app`() {
        val text = Invitations.format("https://aldune.example.com/", "0123456789abcdef0123456789abcdef")
        val modules = QrEncoder.modules(text)
        val (luma, w, h) = lumaOf(modules)

        assertEquals(text, QrDecoder.decodeText(luma, w, w, h))
    }

    @Test fun `la matriz es cuadrada y no incluye margen`() {
        val modules = QrEncoder.modules("hola")

        assertTrue(modules.size >= 21)
        assertTrue(modules.all { it.size == modules.size })
        // Las tres esquinas con marca de posición empiezan con un módulo oscuro.
        assertTrue(modules[0][0] && modules[0][modules.size - 1] && modules[modules.size - 1][0])
    }
}

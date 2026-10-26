package app.aldune.syncclient

import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.qrcode.QRCodeWriter
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class QrDecoderTest {
    private val code = "aldune-profile-v2:" + "eyJWZXJzaW9uIjoyfQ".repeat(14)

    /** Un QR dibujado como lo vería la cámara: plano de luminancia con `stride` bytes por fila (negro 0, blanco 255). */
    private fun lumaOf(text: String, size: Int = 480, stride: Int = size, inverted: Boolean = false): ByteArray {
        val hints = mapOf(EncodeHintType.MARGIN to 4, EncodeHintType.ERROR_CORRECTION to ErrorCorrectionLevel.M)
        val matrix = QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints)
        val white = (if (inverted) 0 else 255).toByte()
        val black = (if (inverted) 255 else 0).toByte()
        val out = ByteArray(stride * size) { 127 }
        for (y in 0 until size) for (x in 0 until size) out[y * stride + x] = if (matrix[x, y]) black else white
        return out
    }

    @Test fun `lee un codigo de perfil de tamano normal`() {
        assertEquals(code, QrDecoder.decodeText(lumaOf(code), 480, 480, 480))
    }

    @Test fun `lee un codigo de perfil con muchas notas`() {
        val big = "aldune-profile-v2:" + "abcdefghijklmnopqrstuvwxyz0123456789-_".repeat(20)
        assertEquals(big, QrDecoder.decodeText(lumaOf(big, size = 900), 900, 900, 900))
    }

    @Test fun `lee con el stride de fila mayor que el ancho, como entrega CameraX`() {
        val luma = lumaOf(code, size = 480, stride = 512)
        assertEquals(code, QrDecoder.decodeText(luma, rowStride = 512, width = 480, height = 480))
    }

    @Test fun `lee un QR con los colores invertidos`() {
        assertEquals(code, QrDecoder.decodeText(lumaOf(code, inverted = true), 480, 480, 480))
    }

    @Test fun `devuelve el texto de cualquier QR, sin filtrar por contenido`() {
        assertEquals("https://example.com", QrDecoder.decodeText(lumaOf("https://example.com"), 480, 480, 480))
    }

    @Test fun `una imagen sin QR da null`() {
        assertNull(QrDecoder.decodeText(ByteArray(480 * 480) { 255.toByte() }, 480, 480, 480))
    }

    @Test fun `medidas imposibles dan null en vez de fallar`() {
        assertNull(QrDecoder.decodeText(ByteArray(10), 480, 480, 480)) // buffer demasiado corto
        assertNull(QrDecoder.decodeText(ByteArray(100), 5, 10, 10)) // stride menor que el ancho
        assertNull(QrDecoder.decodeText(ByteArray(0), 0, 0, 0))
    }
}

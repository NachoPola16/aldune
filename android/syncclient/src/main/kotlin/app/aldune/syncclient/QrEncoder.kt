package app.aldune.syncclient

import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.qrcode.QRCodeWriter
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel

/**
 * Dibuja un texto como QR (la invitación de «Invitar a alguien»: quien la recibe la lee con el «Escanear QR» del formulario de unirse).
 * Devuelve solo los módulos, sin zona de silencio: la interfaz la añade al pintar, y así esto no depende de Android.
 */
object QrEncoder {
    fun modules(text: String): Array<BooleanArray> {
        val hints = mapOf(EncodeHintType.MARGIN to 0, EncodeHintType.ERROR_CORRECTION to ErrorCorrectionLevel.M)
        // Con el tamaño pedido en 0 el escritor devuelve exactamente un píxel por módulo.
        val matrix = QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 0, 0, hints)
        return Array(matrix.height) { y -> BooleanArray(matrix.width) { x -> matrix[x, y] } }
    }
}

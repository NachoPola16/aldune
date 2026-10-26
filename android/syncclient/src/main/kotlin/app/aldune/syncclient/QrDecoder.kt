package app.aldune.syncclient

import com.google.zxing.BarcodeFormat
import com.google.zxing.BinaryBitmap
import com.google.zxing.ChecksumException
import com.google.zxing.DecodeHintType
import com.google.zxing.FormatException
import com.google.zxing.LuminanceSource
import com.google.zxing.MultiFormatReader
import com.google.zxing.NotFoundException
import com.google.zxing.PlanarYUVLuminanceSource
import com.google.zxing.common.HybridBinarizer

/**
 * Lee un QR de un fotograma de la cámara: el plano Y (luminancia) de una imagen YUV_420_888, que CameraX entrega con `rowStride` bytes por
 * fila (casi siempre mayor que el ancho). Solo QR, y con un segundo intento invertido: algunos monitores en modo oscuro lo enseñan claro
 * sobre oscuro. No decide si el texto es de Aldune; eso lo hace quien lo usa (`ProfileCodes.findShareCode`).
 */
object QrDecoder {
    private val hints = mapOf(
        DecodeHintType.POSSIBLE_FORMATS to listOf(BarcodeFormat.QR_CODE),
        DecodeHintType.TRY_HARDER to true,
    )

    fun decodeText(luma: ByteArray, rowStride: Int, width: Int, height: Int): String? {
        if (width <= 0 || height <= 0 || rowStride < width || luma.size < rowStride * (height - 1) + width) return null
        val source = PlanarYUVLuminanceSource(luma, rowStride, height, 0, 0, width, height, false)
        return read(source) ?: read(source.invert())
    }

    // Un lector por llamada: MultiFormatReader guarda estado y el análisis de CameraX puede ir en otro hilo.
    private fun read(source: LuminanceSource): String? = try {
        MultiFormatReader().decode(BinaryBitmap(HybridBinarizer(source)), hints).text
    } catch (e: NotFoundException) {
        null
    } catch (e: ChecksumException) {
        null
    } catch (e: FormatException) {
        null
    }
}

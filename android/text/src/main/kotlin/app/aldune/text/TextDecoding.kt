package app.aldune.text

import java.nio.ByteBuffer
import java.nio.charset.CharacterCodingException
import java.nio.charset.Charset
import java.nio.charset.CodingErrorAction

/**
 * How the bytes of a picked text file become text (port of `LinkedFileFormat.Decode` in Aldune.Core). Anything that is not UTF-8 or UTF-16
 * with a BOM is refused instead of guessed: reading an ANSI file as UTF-8 would import garbage in place of the accents.
 */
object TextDecoding {
    /** Same cap as the note editor. */
    const val MAX_BYTES = 2 * 1024 * 1024

    enum class Rejection { TOO_LARGE, NOT_TEXT }

    class Decoded(val text: String)

    fun rejection(bytes: ByteArray): Rejection? = when {
        bytes.size > MAX_BYTES -> Rejection.TOO_LARGE
        decode(bytes) == null -> Rejection.NOT_TEXT
        else -> null
    }

    fun decode(bytes: ByteArray): Decoded? {
        if (bytes.size > MAX_BYTES) return null
        fun at(i: Int) = bytes.getOrElse(i) { 0 }.toInt() and 0xFF
        val text = try {
            when {
                at(0) == 0xEF && at(1) == 0xBB && at(2) == 0xBF && bytes.size >= 3 -> strict(bytes, 3, Charsets.UTF_8)
                bytes.size >= 2 && at(0) == 0xFF && at(1) == 0xFE -> strict(bytes, 2, Charsets.UTF_16LE)
                bytes.size >= 2 && at(0) == 0xFE && at(1) == 0xFF -> strict(bytes, 2, Charsets.UTF_16BE)
                else -> strict(bytes, 0, Charsets.UTF_8)
            }
        } catch (_: CharacterCodingException) {
            return null
        }
        // A NUL never appears in text: it is a binary that happens to be valid UTF-8.
        return if ('\u0000' in text) null else Decoded(text)
    }

    private fun strict(bytes: ByteArray, offset: Int, charset: Charset): String =
        charset.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT)
            .decode(ByteBuffer.wrap(bytes, offset, bytes.size - offset)).toString()
}

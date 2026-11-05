package app.aldune.text

/**
 * The font of the note text (a local setting: it never travels with the sync). Same numbers as `NoteFontChoice` on Windows; Android has no
 * "installed font" choice (4) because the system offers no public list of fonts. Spec: docs/specs/2026-11-04-note-font-choice-design.md.
 */
object NoteFonts {
    const val SYSTEM = 0
    const val SERIF = 1
    const val EASY_TO_READ = 2
    const val MONOSPACE = 3
    const val IMPORTED = 5

    /** An unknown value (from a future version) or the Windows-only "installed" one loads as the system font. */
    fun normalize(value: Int): Int = if (value == SERIF || value == EASY_TO_READ || value == MONOSPACE || value == IMPORTED) value else SYSTEM

    /** The looks that define their own text style (terminal and notebooks) keep it: the setting only applies where the look uses the system font. */
    fun applies(monospaceLook: Boolean, serifLook: Boolean): Boolean = !monospaceLook && !serifLook
}

enum class FontFileProblem { NONE, EXTENSION, TOO_LARGE, NOT_A_FONT, NO_NAME }

data class FontFileInfo(val isValid: Boolean, val family: String?, val problem: FontFileProblem)

/**
 * The check of an imported font file before it is copied into the app's folder: extension, size, a real sfnt header and a readable family name.
 * Port of `FontFileInspector` (Windows): same rules, same tests. It is not a full parser (`Typeface.createFromFile` does the rest and a file that
 * fails to load falls back to the system font); its job is to refuse what is plainly not a font. Every offset is bounds-checked.
 */
object FontFileInspector {
    const val MAX_BYTES = 10 * 1024 * 1024
    private const val MAX_NAME_LENGTH = 100

    fun inspect(data: ByteArray, fileName: String): FontFileInfo {
        val lower = fileName.lowercase()
        if (!lower.endsWith(".ttf") && !lower.endsWith(".otf")) return fail(FontFileProblem.EXTENSION)
        if (data.size > MAX_BYTES) return fail(FontFileProblem.TOO_LARGE)
        if (data.size < 12) return fail(FontFileProblem.NOT_A_FONT)

        // 00 01 00 00 (TrueType), 'OTTO' (CFF) and 'true' (old Apple). A collection ('ttcf') holds many fonts and is not accepted.
        val version = u32(data, 0)
        if (version != 0x00010000L && version != 0x4F54544FL && version != 0x74727565L) return fail(FontFileProblem.NOT_A_FONT)

        val tables = u16(data, 4)
        if (tables == 0 || tables > 256 || 12L + 16L * tables > data.size) return fail(FontFileProblem.NOT_A_FONT)

        var nameOffset = -1L
        var nameLength = 0L
        for (i in 0 until tables) {
            val record = 12 + 16 * i
            if (data[record] == 'n'.code.toByte() && data[record + 1] == 'a'.code.toByte() && data[record + 2] == 'm'.code.toByte() && data[record + 3] == 'e'.code.toByte()) {
                nameOffset = u32(data, record + 8)
                nameLength = u32(data, record + 12)
                break
            }
        }
        if (nameOffset < 0) return fail(FontFileProblem.NO_NAME)
        if (nameOffset + nameLength > data.size || nameLength < 6) return fail(FontFileProblem.NOT_A_FONT)

        val table = nameOffset.toInt()
        val count = u16(data, table + 2)
        val stringBase = table.toLong() + u16(data, table + 4)
        if (6L + 12L * count > nameLength) return fail(FontFileProblem.NOT_A_FONT)

        // The typographic family (16) is the one people know; the legacy family (1) carries the weight in some files ("Roboto Light").
        for ((platform, nameId) in listOf(3 to 16, 3 to 1, 1 to 16, 1 to 1)) {
            for (i in 0 until count) {
                val record = table + 6 + 12 * i
                if (u16(data, record) != platform || u16(data, record + 6) != nameId) continue
                val length = u16(data, record + 8)
                val start = stringBase + u16(data, record + 10)
                if (start + length > data.size || start + length > nameOffset + nameLength) continue
                val text = String(data, start.toInt(), length, if (platform == 3) Charsets.UTF_16BE else Charsets.ISO_8859_1).trim()
                if (text.isEmpty() || text.length > MAX_NAME_LENGTH || text.any { it.isISOControl() }) continue
                return FontFileInfo(true, text, FontFileProblem.NONE)
            }
        }
        return fail(FontFileProblem.NO_NAME)
    }

    private fun fail(problem: FontFileProblem) = FontFileInfo(false, null, problem)
    private fun u16(d: ByteArray, i: Int): Int = ((d[i].toInt() and 0xFF) shl 8) or (d[i + 1].toInt() and 0xFF)
    private fun u32(d: ByteArray, i: Int): Long = (u16(d, i).toLong() shl 16) or u16(d, i + 2).toLong()
}

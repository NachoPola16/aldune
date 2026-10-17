package app.aldune.text

/**
 * `char.IsWhiteSpace`, `Trim` y `TrimEnd` de .NET. Kotlin trae los suyos, pero con otro conjunto (cuenta U+001C..U+001F y
 * no cuenta U+0085): para que el port recorte exactamente lo mismo que Core se copia el criterio de .NET.
 */
internal fun Char.isDotNetWhiteSpace(): Boolean = when (this) {
    '\t', '\n', '\u000B', '\u000C', '\r', ' ', '\u0085', ' ' -> true
    else -> when (Character.getType(this)) {
        Character.SPACE_SEPARATOR.toInt(), Character.LINE_SEPARATOR.toInt(), Character.PARAGRAPH_SEPARATOR.toInt() -> true
        else -> false
    }
}

internal fun String.trimDotNet(): String {
    var start = 0
    var end = length
    while (start < end && this[start].isDotNetWhiteSpace()) start++
    while (end > start && this[end - 1].isDotNetWhiteSpace()) end--
    return substring(start, end)
}

internal fun String.isNullOrWhiteSpaceDotNet(): Boolean = all { it.isDotNetWhiteSpace() }

/** `TrimEnd('\r')`. */
internal fun String.trimEndCr(): String {
    var end = length
    while (end > 0 && this[end - 1] == '\r') end--
    return substring(0, end)
}

internal fun String.insertAt(index: Int, value: String): String = substring(0, index) + value + substring(index)

internal fun String.trimEndDotNet(): String {
    var end = length
    while (end > 0 && this[end - 1].isDotNetWhiteSpace()) end--
    return substring(0, end)
}

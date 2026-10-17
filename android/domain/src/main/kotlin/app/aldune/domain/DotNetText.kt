package app.aldune.domain

/**
 * `String.Trim()` de .NET: el conjunto de espacios de Kotlin difiere (cuenta U+001C..U+001F y no U+0085). Copia mínima de
 * `DotNetText.kt` de `:text`, donde es `internal` y no se puede usar desde aquí; las etiquetas deben recortarse exactamente como en Core.
 */
private fun Char.isDotNetWhiteSpace(): Boolean = when (this) {
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

package app.aldune.text

/**
 * El «nombre visible» de una nota protegida: texto en claro y opcional que sustituye a «Nota protegida» en la lista, el widget y el
 * aviso. Port de `ProtectedTitleRules` de Core. Es un campo aparte del contenido cifrado, a propósito: sacarlo del texto filtraría
 * sin querer lo que se cree oculto.
 */
object ProtectedTitleRules {
    const val MAX_LENGTH = 60

    /** Una sola línea, sin espacios sobrantes y con tope. Vacío (o solo espacios) es «sin nombre». */
    fun normalize(raw: String?): String? {
        if (raw.isNullOrBlank()) return null
        var oneLine = raw.split('\r', '\n', '\t').map { it.trim() }.filter { it.isNotEmpty() }.joinToString(" ")
        if (oneLine.length > MAX_LENGTH) {
            var end = MAX_LENGTH
            // Sin partir un par sustituto (emoji) por la mitad.
            if (oneLine[end - 1].isHighSurrogate()) end--
            oneLine = oneLine.substring(0, end).trimEnd()
        }
        return oneLine.ifEmpty { null }
    }

    /** El nombre que se puede enseñar, o `null` si hay que enseñar «Nota protegida». Solo cuenta en notas protegidas. */
    fun visible(isProtected: Boolean, title: String?): String? = if (isProtected) normalize(title) else null
}

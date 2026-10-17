package app.aldune.text

import java.text.Normalizer
import java.util.Locale

/**
 * Búsqueda en las notas. A diferencia de `NoteSearch` de Core (que solo ignora mayúsculas), esta también ignora los
 * acentos: en el móvil se escribe sin ellos con mucha frecuencia y «cafe» debe encontrar «Café» (spec de Android, §2.1).
 * Por eso NO tiene vectores compartidos con C#: es una decisión del cliente Android.
 *
 * Se pliega descomponiendo en NFD y quitando las marcas combinantes (`\p{Mn}`), así que «n» y «ñ» solo se igualan porque
 * la tilde es una marca; las letras que no se descomponen (ß, ø, æ, đ, ł…) siguen siendo letras distintas.
 */
object NoteSearch {
    /** `true` si el texto contiene la consulta (recortada) sin distinguir mayúsculas ni acentos; una consulta vacía lo encuentra todo. */
    fun matches(noteText: String, query: String): Boolean {
        val folded = foldedQuery(query) ?: return true
        return fold(noteText).contains(folded)
    }

    /** Como [matches], pero también busca en las etiquetas de la nota. */
    fun matchesNote(noteText: String, labels: Collection<String>, query: String): Boolean {
        val folded = foldedQuery(query) ?: return true
        return fold(noteText).contains(folded) || labels.any { fold(it).contains(folded) }
    }

    /** La consulta recortada y plegada; `null` si no queda nada (coincide con todo). */
    private fun foldedQuery(query: String): String? {
        val trimmed = query.trimDotNet()
        return if (trimmed.isEmpty()) null else fold(trimmed)
    }

    /** Sin acentos y en minúsculas, con `Locale.ROOT` para que el resultado no dependa del idioma del móvil (la «I» turca). */
    internal fun fold(text: String): String {
        val decomposed = Normalizer.normalize(text, Normalizer.Form.NFD)
        val builder = StringBuilder(decomposed.length)
        var i = 0
        while (i < decomposed.length) {
            val codePoint = decomposed.codePointAt(i)
            if (Character.getType(codePoint) != Character.NON_SPACING_MARK.toInt()) builder.appendCodePoint(codePoint)
            i += Character.charCount(codePoint)
        }
        return builder.toString().lowercase(Locale.ROOT)
    }
}

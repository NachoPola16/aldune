package app.aldune.domain

/**
 * El orden de las etiquetas en la fila y en la hoja de «todas». Por defecto alfabético; el usuario puede fijar uno propio (ajuste local del
 * dispositivo, no viaja por la sync). Las etiquetas de ese orden van primero y las demás, alfabéticas detrás: una etiqueta nueva nunca se pierde.
 */
object TagOrder {
    fun apply(tags: List<String>, custom: List<String>): List<String> {
        val rank = HashMap<String, Int>()
        custom.forEachIndexed { i, tag -> rank.putIfAbsent(tag.lowercase(), i) }
        return tags.sortedWith(compareBy<String> { rank[it.lowercase()] ?: Int.MAX_VALUE }.thenBy { it.lowercase() })
    }

    /** Mueve [tag] una posición ([delta] = -1 arriba, +1 abajo) dentro del orden visible y devuelve el orden completo para guardar. */
    fun move(visible: List<String>, tag: String, delta: Int): List<String> {
        val from = visible.indexOfFirst { it.equals(tag, ignoreCase = true) }
        val to = from + delta
        if (from < 0 || to !in visible.indices) return visible
        return visible.toMutableList().also { val t = it.removeAt(from); it.add(to, t) }
    }

    // Un nombre de etiqueta no lleva saltos de línea (una sola línea en la nota), así que sirven de separador.
    fun encode(tags: List<String>): String = tags.joinToString("\n")

    fun decode(raw: String): List<String> = raw.split('\n').filter { it.isNotBlank() }

    fun filter(tags: List<String>, query: String): List<String> =
        query.trim().let { q -> if (q.isEmpty()) tags else tags.filter { it.contains(q, ignoreCase = true) } }
}

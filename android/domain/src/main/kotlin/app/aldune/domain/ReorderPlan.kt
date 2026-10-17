package app.aldune.domain

/** De un arrastre en la lista a los argumentos de [NoteService.move]. Pura: lo que importa es qué notas quedan a cada lado. */
object ReorderPlan {
    /** Las vecinas de la nota movida en su sitio nuevo: [before] queda justo encima y [after] justo debajo (`null` en el borde). */
    data class Neighbours(val before: String?, val after: String?)

    /**
     * La nota de la posición [from] de [ids] se suelta en la posición [to] (índice dentro de la lista ya reordenada). `null` si no hay
     * movimiento: mismo sitio, o un índice fuera de rango.
     */
    fun neighbours(ids: List<String>, from: Int, to: Int): Neighbours? {
        if (from !in ids.indices || to !in ids.indices || from == to) return null
        val result = ids.toMutableList().apply { add(to, removeAt(from)) }
        return Neighbours(before = result.getOrNull(to - 1), after = result.getOrNull(to + 1))
    }
}

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

    enum class Direction { UP, DOWN }

    /** Un paso de una posición: las vecinas del sitio nuevo y la posición resultante (1 = la primera), para anunciarla. */
    data class Step(val neighbours: Neighbours, val position: Int)

    /** Sube ([delta] = -1) o baja (+1) la nota [id] una posición. `null` si no está en la lista o ya está en el borde. */
    fun step(ids: List<String>, id: String, delta: Int): Step? {
        val from = ids.indexOf(id)
        val to = from + delta
        val neighbours = neighbours(ids, from, to) ?: return null
        return Step(neighbours, position = to + 1)
    }

    /**
     * Qué flechas se ofrecen para [id]: ninguna si la lista no es reordenable (con búsqueda, etiqueta u otro orden, las vecinas que se ven
     * no son las vecinas reales), y solo las que tienen hueco.
     */
    fun available(ids: List<String>, id: String, reorderable: Boolean): Set<Direction> {
        if (!reorderable) return emptySet()
        val index = ids.indexOf(id)
        if (index < 0) return emptySet()
        return buildSet {
            if (index > 0) add(Direction.UP)
            if (index < ids.lastIndex) add(Direction.DOWN)
        }
    }

    /**
     * [available] for every id of one group in a single pass. The list asks per visible row, and answering each with an `indexOf` over thousands
     * of notes (plus copying the group) made scrolling a big notebook janky; the screen builds this once per list instead.
     */
    fun availableAll(ids: List<String>): Map<String, Set<Direction>> {
        val up = setOf(Direction.UP)
        val down = setOf(Direction.DOWN)
        val both = setOf(Direction.UP, Direction.DOWN)
        val result = HashMap<String, Set<Direction>>(ids.size * 2)
        ids.forEachIndexed { index, id ->
            result[id] = when {
                ids.size < 2 -> emptySet()
                index == 0 -> down
                index == ids.lastIndex -> up
                else -> both
            }
        }
        return result
    }
}

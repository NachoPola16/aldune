package app.aldune.domain

/**
 * Reparte las notas por etiqueta para la vista agrupada de «Todas las notas». Una nota con varias etiquetas sale en cada una (es una vista,
 * no mueve nada) y las que no tienen ninguna van en una sección final con etiqueta nula. Las secciones siguen el orden de etiquetas del
 * usuario ([order]); una que ese orden no conozca va detrás, alfabética, para que nunca se pierda una nota.
 */
object TagGrouping {
    class Group<T>(val tag: String?, val items: List<T>)

    fun <T> group(items: List<T>, order: List<String>, tagsOf: (T) -> List<String>): List<Group<T>> {
        val byTag = LinkedHashMap<String, MutableList<T>>()
        val names = HashMap<String, String>()
        val untagged = ArrayList<T>()
        for (item in items) {
            val tags = tagsOf(item)
            if (tags.isEmpty()) { untagged.add(item); continue }
            for (tag in tags.distinctBy { it.lowercase() }) {
                val key = tag.lowercase()
                names.putIfAbsent(key, tag)
                byTag.getOrPut(key) { ArrayList() }.add(item)
            }
        }
        val rank = HashMap<String, Int>()
        order.forEachIndexed { i, tag -> rank.putIfAbsent(tag.lowercase(), i) }
        val sorted = byTag.keys.sortedWith(compareBy<String> { rank[it] ?: Int.MAX_VALUE }.thenBy { it })
        val groups = sorted.map { Group(names.getValue(it).let { n -> order.firstOrNull { o -> o.equals(n, ignoreCase = true) } ?: n }, byTag.getValue(it)) }
        return if (untagged.isEmpty()) groups else groups + Group(null, untagged)
    }
}

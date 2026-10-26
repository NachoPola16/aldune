package app.aldune.text

import java.security.MessageDigest

/**
 * «Ocultar tareas hechas» (solo Android). A diferencia de Windows, que borra la línea pasado el plazo, aquí el texto no cambia nunca:
 * las líneas se esconden al dibujar el editor y un interruptor las vuelve a enseñar. Por eso la sync no se entera.
 *
 * Cuándo se marcó cada tarea se guarda aparte y solo en este dispositivo (clave → milisegundos). La clave es un hash del contenido de la
 * tarea, sin casilla ni sangría, como en Windows: aguanta que cambie la posición de la línea, y desmarcar y volver a marcar reinicia el reloj.
 */
object HiddenTasks {
    /** Tramo del texto a esconder: `[start, end)`. Incluye el salto de línea, para que no quede una línea en blanco. */
    data class Span(val start: Int, val end: Int) {
        val length: Int get() = end - start
    }

    fun key(line: String): String {
        val clean = line.trimEndCr()
        val glyph = TaskLines.glyphIndex(clean)
        val content = if (glyph >= 0) clean.substring(glyph + TaskLines.prefixLength(clean, glyph)) else clean
        val digest = MessageDigest.getInstance("SHA-256").digest(content.toByteArray(Charsets.UTF_8))
        return digest.take(8).joinToString("") { "%02x".format(it) }
    }

    /** Los registros al día para [text]: las tareas hechas nuevas empiezan a contar en [now], las que ya no están hechas se olvidan. */
    fun track(text: String, completedAt: Map<String, Long>, now: Long): Map<String, Long> {
        val out = LinkedHashMap<String, Long>()
        for (line in text.split('\n')) {
            val clean = line.trimEndCr()
            if (!TaskLines.isTaskLine(clean) || !TaskLines.isChecked(clean)) continue
            val k = key(clean)
            out[k] = completedAt[k] ?: now
        }
        return out
    }

    /** Los tramos de [text] que ocultar: tareas hechas con registro cuyo plazo [delayMs] ya venció. Ordenados y sin solaparse. */
    fun hiddenSpans(text: String, completedAt: Map<String, Long>, now: Long, delayMs: Long): List<Span> =
        merge(hiddenLines(text, completedAt, now, delayMs))

    /** Cuántas tareas se ocultan (las líneas, antes de juntar los tramos seguidos). */
    fun hiddenCount(text: String, completedAt: Map<String, Long>, now: Long, delayMs: Long): Int =
        hiddenLines(text, completedAt, now, delayMs).size

    private fun hiddenLines(text: String, completedAt: Map<String, Long>, now: Long, delayMs: Long): List<Span> {
        val spans = ArrayList<Span>()
        var start = 0
        while (start <= text.length) {
            val lf = text.indexOf('\n', start)
            val lineEnd = if (lf < 0) text.length else lf
            val clean = text.substring(start, lineEnd).trimEndCr()
            val doneAt = if (TaskLines.isTaskLine(clean) && TaskLines.isChecked(clean)) completedAt[key(clean)] else null
            if (doneAt != null && now - doneAt >= delayMs) {
                if (lf >= 0) {
                    spans.add(Span(start, lf + 1))
                } else if (start > 0) {
                    // Última línea, sin salto detrás: se lleva el de delante.
                    val from = if (start >= 2 && text[start - 2] == '\r') start - 2 else start - 1
                    spans.add(Span(from, text.length))
                } else {
                    spans.add(Span(0, text.length))
                }
            }
            if (lf < 0) break
            start = lf + 1
        }
        return spans
    }

    private fun merge(spans: List<Span>): List<Span> {
        val out = ArrayList<Span>()
        for (s in spans.sortedBy { it.start }) {
            val last = out.lastOrNull()
            if (last != null && s.start <= last.end) out[out.lastIndex] = Span(last.start, maxOf(last.end, s.end)) else out.add(s)
        }
        return out
    }

    /** El índice en el texto real que corresponde a [displayed], un índice del texto con [spans] ya quitados. */
    fun toRaw(displayed: Int, spans: List<Span>): Int {
        var raw = displayed
        for (s in spans) if (raw >= s.start) raw += s.length
        return raw
    }
}

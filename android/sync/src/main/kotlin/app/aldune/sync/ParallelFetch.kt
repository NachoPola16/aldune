package app.aldune.sync

import java.util.concurrent.ExecutionException
import java.util.concurrent.Executors

/** Peticiones a la vez al almacén: bastan para tapar la latencia sin saturar a un servidor doméstico ni a un WebDAV compartido. */
internal const val PARALLEL_FETCH_LIMIT = 8

/**
 * Cada pasada de sync lee el almacén entero, y bajar las notas de una en una multiplicaba la latencia por su número aunque nada hubiera
 * cambiado (en red móvil, segundos por pasada). Devuelve los resultados en el orden de [items]; un resultado null se descarta (un archivo
 * ilegible no impide el resto). El primer fallo sale con su propio tipo, no como `ExecutionException`: el motor distingue por él los
 * fallos normales de sync.
 */
internal fun <T, R : Any> parallelMap(items: List<T>, fetch: (T) -> R?): List<R> {
    if (items.isEmpty()) return emptyList()
    val pool = Executors.newFixedThreadPool(minOf(PARALLEL_FETCH_LIMIT, items.size))
    try {
        val futures = items.map { item -> pool.submit<R?> { fetch(item) } }
        return futures.mapNotNull { future ->
            try {
                future.get()
            } catch (e: ExecutionException) {
                throw e.cause ?: e
            }
        }
    } finally {
        pool.shutdownNow()
    }
}

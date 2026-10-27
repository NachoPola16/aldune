package app.aldune.sync

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.io.IOException
import java.util.concurrent.atomic.AtomicInteger

class ParallelFetchTest {
    @Test
    fun `conserva el orden de entrada y descarta los null`() {
        val result = parallelMap((1..30).toList()) { n -> if (n % 5 == 0) null else n * 2 }
        assertEquals((1..30).filter { it % 5 != 0 }.map { it * 2 }, result)
    }

    @Test
    fun `va de varios en varios sin pasar del tope`() {
        val inFlight = AtomicInteger()
        val peak = AtomicInteger()
        parallelMap((1..40).toList()) {
            val now = inFlight.incrementAndGet()
            peak.updateAndGet { maxOf(it, now) }
            Thread.sleep(20)
            inFlight.decrementAndGet()
            it
        }
        assertTrue(peak.get() > 1, "iba de uno en uno")
        assertTrue(peak.get() <= PARALLEL_FETCH_LIMIT, "pasó del tope: ${peak.get()}")
    }

    @Test
    fun `un fallo sale con su tipo, sin envolver`() {
        // El motor distingue los fallos normales de sync (IOException) por su tipo: no pueden llegar como ExecutionException.
        assertThrows(IOException::class.java) {
            parallelMap((1..10).toList()) { if (it == 7) throw IOException("sin red") else it }
        }
    }

    @Test
    fun `una lista vacia no arranca nada`() {
        assertEquals(emptyList<Int>(), parallelMap(emptyList<Int>()) { it })
    }
}

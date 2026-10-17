package app.aldune.domain

import java.time.Duration
import java.time.Instant

/** Reloj de prueba: no avanza solo; cada test decide cuándo pasa el tiempo. */
class FakeClock(var current: Instant = Instant.parse("2026-03-01T10:00:00Z")) : Clock {
    override fun now(): Instant = current
    fun advance(duration: Duration) { current = current.plus(duration) }
}

/** Ids predecibles: n1, n2, n3… */
class SequentialIds : IdFactory {
    private var next = 1
    override fun newId(): String = "n${next++}"
}

class Fixture(val deviceId: String = "android-1") {
    val clock = FakeClock()
    val store = InMemoryNoteStore()
    val service = NoteService(store, clock, deviceId, SequentialIds())

    /** Crea una nota ya validada (falla el test si el servicio la rechaza). */
    fun create(text: String = "hola", color: String = "#FFDDD4"): Note = service.create(text, color).getOrThrow()

    fun stored(id: String): Note = store.get(id)!!
}

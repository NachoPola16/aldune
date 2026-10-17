package app.aldune.domain

import java.time.Instant
import java.util.UUID

/** El reloj se inyecta: nada del dominio llama a `Instant.now()` por su cuenta, para poder probar «cuándo cambia UpdatedAt». */
fun interface Clock {
    fun now(): Instant
}

/** El único sitio donde se mira el reloj real. */
object SystemClock : Clock {
    override fun now(): Instant = Instant.now()
}

fun interface IdFactory {
    fun newId(): String
}

/** UUID aleatorio en minúsculas con guiones, el formato de `Guid.ToString()` en Core. */
object RandomIdFactory : IdFactory {
    override fun newId(): String = UUID.randomUUID().toString()
}

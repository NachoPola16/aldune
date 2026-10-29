package app.aldune.syncclient

import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteState
import java.time.Instant

const val DEVICE = "0123456789abcdef0123456789abcdef"
const val OTHER_DEVICE = "fedcba9876543210fedcba9876543210"
const val ID1 = "11111111-1111-1111-1111-111111111111"
const val ID2 = "22222222-2222-2222-2222-222222222222"
const val ID3 = "33333333-3333-3333-3333-333333333333"
val T0: Instant = Instant.parse("2026-03-01T10:00:00Z")

/** Contenido protegido tal como lo escribe core (claves en camelCase, en este orden). */
const val PROTECTED_JSON = """{"version":1,"salt":"c2FsdHNhbHRzYWx0c2FsdA==","cipherText":"Y2lwaGVy","nonce":"bm9uY2Vub25jZW5v","tag":"dGFndGFndGFndGFndGFn"}"""

fun note(
    id: String = ID1,
    text: String = "hola",
    ending: LineEnding = LineEnding.LF,
    color: String = "#FFDDD4",
    createdAt: Instant = T0,
    updatedAt: Instant = T0,
    state: NoteState = NoteState.ACTIVE,
    tags: List<String> = emptyList(),
    position: Double? = null,
    origin: String = "",
    author: String? = null,
    isProtected: Boolean = false,
    protectedJson: String? = null,
    protectedTitle: String? = null,
) = Note(id, text, ending, color, createdAt, updatedAt, state, tags, position, origin, author, isProtected, protectedJson, protectedTitle)

/** Reloj de prueba: no avanza solo. */
class TestClock(var current: Instant = T0) : app.aldune.domain.Clock {
    override fun now(): Instant = current
    fun advanceSeconds(seconds: Long) { current = current.plusSeconds(seconds) }
}

val TEST_KEY = ByteArray(32) { (it + 1).toByte() }

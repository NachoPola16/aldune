package app.aldune.core

import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory
import java.time.OffsetDateTime
import java.util.UUID

class VersionOrderTest {
    private fun version(o: kotlinx.serialization.json.JsonObject) =
        SyncVersion(OffsetDateTime.parse(o.str("updatedAt")), o.str("deviceId"))

    @TestFactory
    fun `el orden de los vectores`() = Vectors.objects("versionOrder").mapIndexed { i, v ->
        DynamicTest.dynamicTest("caso ${i + 1}") {
            val a = version(v.getValue("a").jsonObject)
            val b = version(v.getValue("b").jsonObject)
            val expected = v.getValue("expectedSign").jsonPrimitive.content.toInt()
            assertEquals(expected, SyncVersion.compare(a, b))
            assertEquals(-expected, SyncVersion.compare(b, a)) // antisimetría
        }
    }

    private fun at(device: String) = SyncVersion(OffsetDateTime.parse("2026-10-17T10:00:00Z"), device)

    @Test
    fun `el desempate es ordinal por unidades UTF-16 y no por punto de codigo`() {
        // U+1F600 son los sustitutos D83D DE00; U+FF5E es una sola unidad FF5E. Por unidades, D83D < FF5E;
        // por punto de código sería al revés. Core usa String.CompareOrdinal (unidades).
        assertEquals(-1, SyncVersion.compare(at("😀"), at("～")))
    }

    @Test
    fun `el desempate distingue mayusculas y no es cultural`() {
        assertEquals(-1, SyncVersion.compare(at("B"), at("a")))
        assertEquals(1, SyncVersion.compare(at("a"), at("A")))
        assertEquals(-1, SyncVersion.compare(at("a"), at("á")))
        assertEquals(-1, SyncVersion.compare(at("a"), at("ab")))
    }

    @Test
    fun `solo se devuelve -1, 0 o 1`() {
        assertEquals(-1, SyncVersion.compare(at("a"), at("zzzz")))
        assertEquals(1, SyncVersion.compare(at("zzzz"), at("a")))
    }

    @Test
    fun `se puede construir desde un sobre`() {
        val envelope = SyncEnvelope(noteId = UUID.randomUUID(), tombstone = false,
            updatedAt = OffsetDateTime.parse("2026-10-17T12:00:00+02:00"), deviceId = "a", cipherText = null, nonce = null, tag = null)
        assertEquals(0, SyncVersion.compare(SyncVersion.of(envelope), at("a")))
    }
}

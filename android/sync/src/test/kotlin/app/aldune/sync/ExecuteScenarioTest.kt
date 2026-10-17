package app.aldune.sync

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.DynamicTest.dynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory
import java.time.OffsetDateTime
import java.time.ZoneOffset

/** Todos los escenarios de punta a punta: plan + `execute` con los dobles, y se compara lo que queda en cada lado. */
class ExecuteScenarioTest {
    @TestFactory
    fun `cada escenario deja local y almacen como Core`() = Scenario.all.map { s ->
        dynamicTest(s.name) {
            val now = OffsetDateTime.now(ZoneOffset.UTC).toTicks()
            val local = InMemoryLocalStore(s.localNotes, s.localTombstones, s.bases)
            val transport = InMemorySyncTransport(s.remote, s.unreadable)
            val result = execute(planSync(s.input(now), s.key), local, transport)
            assertExecutionMatches(s, result, local, transport, now)
        }
    }

    @Test
    fun `los 34 escenarios estan cubiertos`() = assertEquals(34, Scenario.all.size)
}

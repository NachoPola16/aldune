package app.aldune.sync

import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.DynamicTest.dynamicTest
import org.junit.jupiter.api.TestFactory
import java.time.OffsetDateTime
import java.time.ZoneOffset

/**
 * `planSync` contra los escenarios de Core, agrupados como las tareas del encargo. Aquí solo se mira lo que
 * dice el plan (contadores, error, conflictos, bases); lo que queda en cada lado lo mira ExecuteScenarioTest.
 */
class PlanScenarioTest {
    private fun cases(vararg codes: String): List<DynamicTest> = Scenario.named(*codes).map { s ->
        dynamicTest(s.name) {
            val now = OffsetDateTime.now(ZoneOffset.UTC).toTicks()
            assertPlanMatches(s, planSync(s.input(now), s.key), now)
        }
    }

    // Tarea 2: sin borrados ni alcance.
    @TestFactory
    fun `subir, aplicar, iguales, bases, conflicto real, migracion, empate y protegidas`() =
        cases("E01", "E02", "E03", "E04", "E05", "E06", "E07", "E08", "E09", "E10", "E16", "E22", "E25", "E28")

    // Tarea 3: borrados.
    @TestFactory
    fun `borrados firmados, sin firma y locales`() = cases("E11", "E12", "E13", "E15", "E27")

    // Tarea 4: alcance.
    @TestFactory
    fun `alcance por etiqueta, notas elegidas y asimetria de borrados`() = cases("E17", "E18", "E19", "E20", "E26")

    // Tarea 5: errores.
    @TestFactory
    fun `ninguna nota descifra y formato desconocido`() = cases("E23", "E24")
}

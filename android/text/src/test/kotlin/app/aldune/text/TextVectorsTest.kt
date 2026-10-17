package app.aldune.text

import kotlinx.serialization.json.JsonElement
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.DynamicTest.dynamicTest
import org.junit.jupiter.api.TestFactory

/**
 * Los vectores de texto, color y orden son el contrato con `Aldune.Core`: por cada caso se ejecuta la operación del port con
 * sus argumentos y se compara con lo que devolvió Core. Un test dinámico por operación; si falla, imprime op, args,
 * esperado y obtenido de los primeros casos rotos.
 */
class TextVectorsTest {
    @TestFactory fun lines(): List<DynamicTest> = run("text-lines.json")
    @TestFactory fun color(): List<DynamicTest> = run("text-color.json")
    @TestFactory fun order(): List<DynamicTest> = run("text-order.json")

    private fun run(file: String): List<DynamicTest> =
        Vectors.load(file).groupBy { it.op }.map { (op, cases) ->
            dynamicTest("$op (${cases.size})") {
                val failures = cases.mapNotNull { c ->
                    val actual = try {
                        Operations.execute(c.op, c.args)
                    } catch (e: Throwable) {
                        return@mapNotNull "${c.op} ${c.args}\n    esperado: ${c.expected}\n    lanzó: $e"
                    }
                    if (jsonEquals(c.expected, actual)) null
                    else "${c.op} ${c.args}\n    esperado: ${c.expected}\n    obtenido: $actual"
                }
                if (failures.isNotEmpty()) {
                    throw AssertionError("${failures.size} de ${cases.size} casos fallan; los primeros:\n" + failures.take(5).joinToString("\n"))
                }
            }
        }
}

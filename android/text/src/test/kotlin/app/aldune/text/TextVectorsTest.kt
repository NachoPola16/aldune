package app.aldune.text

import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.double
import kotlinx.serialization.json.jsonPrimitive
import kotlin.math.abs
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
                    if (matches(c, actual)) null
                    else "${c.op} ${c.args}\n    esperado: ${c.expected}\n    obtenido: $actual"
                }
                if (failures.isNotEmpty()) {
                    throw AssertionError("${failures.size} de ${cases.size} casos fallan; los primeros:\n" + failures.take(5).joinToString("\n"))
                }
            }
        }

    /**
     * Comparación estricta (1e-9 relativa) salvo un caso, apuntado en el informe: en un gris puro (#010101, #7F7F7F, #FEFEFE) el croma
     * real es ~1e-8, ruido de la resta de a y b, y su matiz no significa nada; `Math.cbrt`/`Math.pow` de la JVM y de .NET difieren en 1
     * ulp y la resta lo amplifica (croma con diferencia relativa ~3e-11 y matiz con ~1e-8). Ahí se comprueba la claridad estricta, el
     * croma con tolerancia absoluta de 1e-12 y el matiz no se compara. Ningún `#RRGGBB` depende de esto.
     */
    private fun matches(c: VectorCase, actual: JsonElement): Boolean {
        if (jsonEquals(c.expected, actual)) return true
        if (c.op != "OklchColor.FromHex") return false
        val e = c.expected as? JsonObject ?: return false
        val a = actual as? JsonObject ?: return false
        val chroma = e.getValue("c").jsonPrimitive.double
        if (chroma >= 1e-6) return false
        return jsonEquals(e.getValue("l"), a.getValue("l")) &&
            abs(chroma - a.getValue("c").jsonPrimitive.double) < 1e-12
    }
}

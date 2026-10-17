package app.aldune.text

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import java.io.File
import kotlin.math.abs
import kotlin.math.max

/** Un caso de `tests/vectors/text-*.json`: operación + argumentos por nombre → lo que devolvió Core. */
class VectorCase(val op: String, val args: JsonObject, val expected: JsonElement) {
    override fun toString() = "$op $args"
}

object Vectors {
    private fun dir(): File = File(System.getProperty("aldune.vectors.dir") ?: error("falta aldune.vectors.dir"))

    fun load(file: String): List<VectorCase> {
        val root = Json.parseToJsonElement(File(dir(), file).readText(Charsets.UTF_8)).jsonObject
        return root.getValue("cases").jsonArray.map {
            val o = it.jsonObject
            VectorCase(o.getValue("op").str(), o.getValue("args").jsonObject, o.getValue("expected"))
        }
    }
}

private fun JsonElement.str() = (this as JsonPrimitive).content

/** Tolerancia relativa para los reales (README de los vectores); los textos, los booleanos y los `#RRGGBB` van exactos. */
const val REAL_TOLERANCE = 1e-9

fun jsonEquals(expected: JsonElement, actual: JsonElement): Boolean = when {
    expected is JsonNull || actual is JsonNull -> expected is JsonNull && actual is JsonNull
    expected is JsonObject -> actual is JsonObject && expected.keys == actual.keys &&
        expected.all { (k, v) -> jsonEquals(v, actual.getValue(k)) }
    expected is JsonArray -> actual is JsonArray && expected.size == actual.size &&
        expected.indices.all { jsonEquals(expected[it], actual[it]) }
    expected is JsonPrimitive && actual is JsonPrimitive -> primitiveEquals(expected, actual)
    else -> false
}

private fun primitiveEquals(e: JsonPrimitive, a: JsonPrimitive): Boolean {
    if (e.isString || a.isString) return e.isString && a.isString && e.content == a.content
    if (e.booleanOrNull != null || a.booleanOrNull != null) return e.booleanOrNull == a.booleanOrNull
    val x = e.doubleOrNull ?: return false
    val y = a.doubleOrNull ?: return false
    if (x == y) return true
    return abs(x - y) <= REAL_TOLERANCE * max(abs(x), abs(y))
}

// Argumentos por nombre, como el `Execute` de TextVectorsTests.cs.
fun JsonObject.s(key: String): String = getValue(key).str()
fun JsonObject.i(key: String): Int = getValue(key).str().toInt()
fun JsonObject.d(key: String): Double = getValue(key).str().toDouble()
fun JsonObject.ns(key: String): String? = (this[key] as? JsonPrimitive)?.takeIf { it !is JsonNull }?.contentOrNull
fun JsonObject.nd(key: String): Double? = ns(key)?.toDouble()
fun JsonObject.strings(key: String): List<String> = getValue(key).jsonArray.map { it.str() }

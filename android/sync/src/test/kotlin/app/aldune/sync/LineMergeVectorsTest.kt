package app.aldune.sync

import java.io.File
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** `tests/vectors/merge-vectors.json`: el contrato de [LineMerge], compartido con `MergeVectorsTests` de Core. */
class LineMergeVectorsTest {
    private val dir = File(System.getProperty("aldune.vectors.dir") ?: error("falta aldune.vectors.dir"))

    @Test fun `todos los vectores de fusion dan lo mismo que Core`() {
        val cases = Json.parseToJsonElement(File(dir, "merge-vectors.json").readText(Charsets.UTF_8)).jsonObject.getValue("cases").jsonArray
        assertTrue(cases.size >= 16, "Faltan casos en merge-vectors.json: ${cases.size}")
        for (case in cases) {
            val c = case.jsonObject
            val name = (c.getValue("name") as JsonPrimitive).content
            fun text(key: String) = (c.getValue(key) as JsonPrimitive).content
            val result = LineMerge.merge(text("base"), text("local"), text("remote"))
            assertEquals((c.getValue("conflict") as JsonPrimitive).boolean, result.conflict, "$name: conflicto")
            val expected = c.getValue("expectedText")
            if (expected !is JsonNull) assertEquals((expected as JsonPrimitive).content, result.text, "$name: texto")
        }
    }
}

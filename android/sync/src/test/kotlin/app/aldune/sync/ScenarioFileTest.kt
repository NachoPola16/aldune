package app.aldune.sync

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import java.io.File
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Assertions.assertEquals

class ScenarioFileTest {
    @Test
    fun `el archivo de escenarios trae los 34 casos de F0`() {
        val dir = File(System.getProperty("aldune.vectors.dir") ?: error("falta aldune.vectors.dir"))
        val root = Json.parseToJsonElement(File(dir, "sync-scenarios.json").readText(Charsets.UTF_8)).jsonObject
        assertEquals(34, root.getValue("scenarios").jsonArray.size)
    }
}

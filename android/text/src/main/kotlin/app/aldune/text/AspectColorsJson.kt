package app.aldune.text

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import java.util.Locale

/**
 * `aspectColors` en `SharedPreferences`: la misma forma que `AspectColors` de `settings.json` en Windows (`{"bash":{"accent":"#D79921"}}`:
 * id de aspecto → id de hueco → `#RRGGBB`). Lo ilegible o lo que no es un color se descarta: un valor roto cae en el de fábrica.
 */
object AspectColorsJson {
    fun parse(json: String?): Map<String, Map<String, String>> {
        if (json.isNullOrBlank()) return emptyMap()
        val root = runCatching { Json.parseToJsonElement(json) }.getOrNull() as? JsonObject ?: return emptyMap()
        val result = linkedMapOf<String, Map<String, String>>()
        for ((aspect, slots) in root) {
            val chosen = linkedMapOf<String, String>()
            for ((slot, value) in (slots as? JsonObject).orEmpty()) {
                val hex = (value as? JsonPrimitive)?.takeIf { it.isString }?.content
                if (hex != null && NoteDisplayColor.isActive(hex)) chosen[slot] = hex.uppercase(Locale.ROOT)
            }
            if (chosen.isNotEmpty()) result[aspect] = chosen
        }
        return result
    }

    fun write(colors: Map<String, Map<String, String>>): String = buildJsonObject {
        for ((aspect, slots) in colors) put(aspect, buildJsonObject { for ((slot, hex) in slots) put(slot, hex) })
    }.toString()
}

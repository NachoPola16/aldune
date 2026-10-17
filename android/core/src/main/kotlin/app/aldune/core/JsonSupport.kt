package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.time.OffsetDateTime
import java.time.format.DateTimeParseException

/** Utilidades de lectura tolerante del JSON de Core (System.Text.Json con perfil Web: claves sin distinguir mayúsculas). */
internal object JsonSupport {
    fun parseObject(text: String, what: String): JsonObject {
        val element = try {
            Json.parseToJsonElement(text)
        } catch (e: Exception) {
            throw SyncFormatException("The $what is not valid JSON.", e)
        }
        return element as? JsonObject ?: throw SyncFormatException("The $what is empty.")
    }

    /** Valor de la clave sin distinguir mayúsculas; `null` si falta (un JSON `null` también cuenta como ausente). */
    fun JsonObject.field(name: String): JsonElement? {
        val value = this[name] ?: entries.firstOrNull { it.key.equals(name, ignoreCase = true) }?.value
        return if (value == null || value is JsonNull) null else value
    }

    fun JsonObject.string(name: String): String? = (field(name) as? JsonPrimitive)?.takeIf { it.isString }?.content

    fun JsonObject.int(name: String): Int? =
        (field(name) as? JsonPrimitive)?.content?.trim()?.toIntOrNull()

    fun JsonObject.double(name: String): Double? =
        (field(name) as? JsonPrimitive)?.content?.trim()?.toDoubleOrNull()

    fun JsonObject.boolean(name: String): Boolean? = when ((field(name) as? JsonPrimitive)?.content) {
        "true" -> true
        "false" -> false
        else -> null
    }

    fun parseDate(value: String): OffsetDateTime = try {
        OffsetDateTime.parse(value.trim())
    } catch (e: DateTimeParseException) {
        throw SyncFormatException("The date \"$value\" is not a valid ISO 8601 date with offset.", e)
    }
}

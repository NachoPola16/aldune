package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import java.time.format.DateTimeFormatterBuilder
import java.time.format.DateTimeParseException
import java.time.temporal.ChronoUnit

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

    // Como System.Text.Json: "2026-10-17T10:30:15.5+00:00" (desfase con dos puntos, nunca "Z",
    // fracción sin ceros finales y con un máximo de 7 dígitos, que es la precisión de .NET).
    private val WRITE_FORMAT: DateTimeFormatter = DateTimeFormatterBuilder()
        .append(DateTimeFormatter.ISO_LOCAL_DATE_TIME)
        .appendOffset("+HH:MM", "+00:00")
        .toFormatter()

    fun formatDate(value: OffsetDateTime): String = value.truncatedTo(ChronoUnit.NANOS).let {
        it.withNano(it.nano / 100 * 100).format(WRITE_FORMAT)
    }
}

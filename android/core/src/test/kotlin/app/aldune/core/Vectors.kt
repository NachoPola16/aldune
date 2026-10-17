package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import java.io.File

/** Acceso a tests/vectors/sync-format4.json, el contrato del formato (generado por Aldune.Core). */
object Vectors {
    val dir: File = File(System.getProperty("aldune.vectors.dir") ?: error("falta aldune.vectors.dir"))

    val root: JsonObject by lazy {
        Json.parseToJsonElement(File(dir, "sync-format4.json").readText(Charsets.UTF_8)).jsonObject
    }

    val keyBytes: ByteArray by lazy {
        val hex = root.getValue("syncKeyHex").jsonPrimitive.content
        ByteArray(hex.length / 2) { hex.substring(it * 2, it * 2 + 2).toInt(16).toByte() }
    }

    val keyEncoded: String get() = root.getValue("syncKeyEncoded").jsonPrimitive.content

    fun array(name: String): JsonArray = root.getValue(name).jsonArray
    fun objects(name: String): List<JsonObject> = array(name).map { it.jsonObject }
}

fun JsonObject.str(name: String): String = getValue(name).jsonPrimitive.content

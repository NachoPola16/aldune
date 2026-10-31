package app.aldune.text

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull

/**
 * La versión de Android más reciente publicada en GitHub, para avisar de que hay una nueva (lo que `ReleaseUpdateChecker` hace en Windows).
 * Las releases de Android llevan el tag `android-vX.Y.Z` y comparten repositorio con las de Windows (`vX.Y.Z`), así que `releases/latest`
 * no sirve: se lee la lista y se filtra. Solo cuentan las estables (ni borradores ni pre-releases, ni sufijos `-rcN`).
 */
object AndroidReleaseUpdate {
    const val PAGE = "https://github.com/NachoPola16/aldune/releases/tag/"
    const val API = "https://api.github.com/repos/NachoPola16/aldune/releases?per_page=100"
    private const val PREFIX = "android-v"

    /** [version] es la de `versionName` y [tag] el tag completo: la página se abre con [PAGE] + tag, nunca con una URL del JSON. */
    data class Update(val version: String, val tag: String)

    /** `1.0.1` → (1,0,1,0); cualquier cosa que no sea 2–4 números separados por puntos (`1.0.1-rc2`, `abc`) → `null`. */
    fun parse(text: String?): List<Int>? {
        if (text.isNullOrEmpty() || text.length > 32) return null
        val parts = text.split('.')
        if (parts.size !in 2..4) return null
        val numbers = parts.map { p -> if (p.isNotEmpty() && p.length <= 6 && p.all { it in '0'..'9' }) p.toInt() else return null }
        return numbers + List(4 - numbers.size) { 0 }
    }

    private fun isNewer(candidate: List<Int>, current: List<Int>): Boolean {
        for (i in 0 until 4) if (candidate[i] != current[i]) return candidate[i] > current[i]
        return false
    }

    /** La actualización que dice un [tag] guardado de una consulta anterior, si sigue siendo más nueva que [currentVersion] (p. ej. tras instalarla ya no). */
    fun fromTag(tag: String?, currentVersion: String): Update? {
        if (tag == null || !tag.startsWith(PREFIX)) return null
        val current = parse(currentVersion.substringBefore('-')) ?: return null
        val version = tag.removePrefix(PREFIX)
        val numbers = parse(version) ?: return null
        return if (isNewer(numbers, current)) Update(version, tag) else null
    }

    /** La release de Android más alta que supera a [currentVersion], o `null` si no hay (o el JSON no es el esperado). */
    fun latest(json: String, currentVersion: String): Update? {
        val current = parse(currentVersion.substringBefore('-')) ?: return null
        val releases = runCatching { Json.parseToJsonElement(json) }.getOrNull() as? JsonArray ?: return null
        var best: Pair<List<Int>, Update>? = null
        for (item in releases) {
            val release = item as? JsonObject ?: continue
            if (flag(release, "draft") != false || flag(release, "prerelease") != false) continue
            val tag = (release["tag_name"] as? JsonPrimitive)?.takeIf { it.isString }?.content ?: continue
            if (!tag.startsWith(PREFIX)) continue
            val version = tag.removePrefix(PREFIX)
            val numbers = parse(version) ?: continue
            if (!isNewer(numbers, current)) continue
            if (best == null || isNewer(numbers, best.first)) best = numbers to Update(version, tag)
        }
        return best?.second
    }

    // `null` si falta o no es un booleano: se descarta la entrada en vez de fiarse de ella.
    private fun flag(release: JsonObject, name: String): Boolean? = (release[name] as? JsonPrimitive)?.booleanOrNull
}

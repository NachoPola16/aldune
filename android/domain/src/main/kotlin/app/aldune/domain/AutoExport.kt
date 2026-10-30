package app.aldune.domain

import app.aldune.text.MarkdownExport
import java.security.MessageDigest
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

/** Una nota tal como la ve la copia automática: solo lo que hace falta para decidir qué escribir. */
data class ExportNote(val id: String, val text: String, val state: NoteState, val isProtected: Boolean)

/** Lo que hay que hacer en la carpeta de copia. Nunca hay un «borrar»: solo crear, reescribir lo propio y mover. */
sealed interface ExportAction {
    data class Write(val path: String, val content: String) : ExportAction
    data class Move(val from: String, val to: String) : ExportAction
}

/** Un fichero que la app escribió: su ruta relativa, la huella de lo que escribió y la copia que va a su lado si el usuario lo tocó a mano. */
data class ExportEntry(val path: String, val hash: String, val sidePath: String? = null, val sideHash: String? = null)

/**
 * El manifiesto `.aldune-export.json` de la carpeta (el mismo formato que en Windows): qué ficheros son de Aldune, por id de nota, y con qué
 * contenido. Un manifiesto roto o ausente vale como vacío: todo lo que ya hay se trata como ajeno y no se pisa.
 */
data class ExportManifest(val files: Map<String, ExportEntry> = emptyMap()) {
    fun toJson(): String = buildJsonObject {
        put("Version", 1)
        put("Files", buildJsonObject {
            for ((id, e) in files) put(id, buildJsonObject {
                put("Path", e.path)
                put("Hash", e.hash)
                e.sidePath?.let { put("SidePath", it) }
                e.sideHash?.let { put("SideHash", it) }
            })
        })
    }.toString()

    companion object {
        const val FILE_NAME = ".aldune-export.json"   // en la raíz de la carpeta de copia

        fun fromJson(json: String?): ExportManifest {
            if (json.isNullOrBlank()) return ExportManifest()
            val root = runCatching { Json.parseToJsonElement(json) }.getOrNull() as? JsonObject ?: return ExportManifest()
            val files = root["Files"] as? JsonObject ?: return ExportManifest()
            val result = linkedMapOf<String, ExportEntry>()
            for ((id, value) in files) {
                val o = value as? JsonObject ?: continue
                fun str(key: String) = (o[key] as? JsonPrimitive)?.takeIf { it.isString }?.content
                val path = str("Path") ?: continue
                val hash = str("Hash") ?: continue
                result[id] = ExportEntry(path, hash, str("SidePath"), str("SideHash"))
            }
            return ExportManifest(result)
        }
    }
}

data class AutoExportPlan(val actions: List<ExportAction>, val manifest: ExportManifest)

/**
 * Decide qué escribir en la carpeta de la copia automática: el port de `AutoExportPlanner` de Core, con las mismas reglas. Pura: ve la carpeta
 * solo a través de `readHash` (huella SHA-256 de un fichero por su ruta relativa, o `null` si no existe).
 *
 * Un `.md` por nota activa (y archivada si se pide), con un nombre que se fija la primera vez; las protegidas y la papelera quedan fuera; una nota
 * que sale mueve su fichero a `_eliminadas/` si nadie lo tocó; nunca se borra nada ni se pisa un fichero que el usuario cambió o que no es nuestro
 * (la copia va al lado como `nombre (Aldune).md`).
 */
object AutoExportPlanner {
    const val ACTIVE_DIR = "notas"
    const val ARCHIVED_DIR = "archivadas"
    const val REMOVED_DIR = "_eliminadas"
    private const val SIDE_SUFFIX = "Aldune"

    fun hashOf(content: String): String =
        MessageDigest.getInstance("SHA-256").digest(content.toByteArray(Charsets.UTF_8)).joinToString("") { "%02X".format(it) }

    fun plan(notes: List<ExportNote>, manifest: ExportManifest, includeArchived: Boolean, placeholder: String, readHash: (String) -> String?): AutoExportPlan {
        val actions = mutableListOf<ExportAction>()
        val files = manifest.files.filterValues { isSafe(it.path) && (it.sidePath == null || isSafe(it.sidePath)) }.toMutableMap()
        val used = files.values.flatMap { listOfNotNull(it.path, it.sidePath) }.map { it.lowercase() }.toMutableSet()

        val wanted = notes.filter { !it.isProtected && (it.state == NoteState.ACTIVE || (includeArchived && it.state == NoteState.ARCHIVED)) }.associateBy { it.id }

        // Lo que ya no se exporta: su fichero se aparta si sigue como lo dejamos; si el usuario lo cambió, se deja donde está.
        for ((id, entry) in files.entries.filter { it.key !in wanted }.sortedBy { it.key }) {
            files.remove(id)
            if (readHash(entry.path) != entry.hash) continue
            actions += ExportAction.Move(entry.path, uniquePath(REMOVED_DIR, fileNameOf(entry.path), used, readHash, checkDisk = true))
        }

        for (note in notes.filter { it.id in wanted }) {
            val dir = if (note.state == NoteState.ARCHIVED) ARCHIVED_DIR else ACTIVE_DIR
            val content = MarkdownExport.toMarkdown(note.text, placeholder)
            val hash = hashOf(content)
            var entry = files[note.id]

            if (entry != null && dirOf(entry.path) != dir) {
                // Archivada o desarchivada: su fichero cambia de carpeta si es nuestro y sigue intacto.
                if (readHash(entry.path) == entry.hash) {
                    val moved = uniquePath(dir, fileNameOf(entry.path), used, readHash, checkDisk = false)
                    actions += ExportAction.Move(entry.path, moved)
                    entry = entry.copy(path = moved, sidePath = null, sideHash = null)
                    if (entry.hash != hash) {
                        actions += ExportAction.Write(moved, content)
                        entry = entry.copy(hash = hash)
                    }
                    files[note.id] = entry
                    continue
                }
                entry = null // el usuario lo cambió: se queda donde está y la nota empieza un fichero nuevo
            }

            if (entry == null) {
                var path = uniquePath(dir, titleBase(note.text, placeholder), used, readHash, checkDisk = false)
                val disk = readHash(path)
                if (disk == null) {
                    actions += ExportAction.Write(path, content)
                } else if (disk != hash) {
                    val side = sidePathFor(path, hash, null, used, readHash)
                    path = side.first
                    if (side.second) actions += ExportAction.Write(path, content)
                }
                files[note.id] = ExportEntry(path, hash)
                continue
            }

            val current = readHash(entry.path)
            if (current == null || current == entry.hash) {
                if (current == null || entry.hash != hash) actions += ExportAction.Write(entry.path, content)
                files[note.id] = entry.copy(hash = hash)
            } else if (current == hash) {
                files[note.id] = entry.copy(hash = hash)
            } else if (entry.hash != hash) {
                // El usuario cambió el fichero a mano y la nota también cambió: la copia nueva va al lado.
                var side = entry.sidePath ?: sidePathFor(entry.path, hash, null, used, readHash).first
                val sideDisk = readHash(side)
                if (entry.sidePath != null && sideDisk != null && sideDisk != hash && sideDisk != entry.sideHash) {
                    side = sidePathFor(entry.path, hash, null, used, readHash).first // la de al lado también se tocó: otra distinta
                }
                if (readHash(side) != hash) actions += ExportAction.Write(side, content)
                files[note.id] = entry.copy(sidePath = side, sideHash = hash)
            }
        }
        return AutoExportPlan(actions, ExportManifest(files))
    }

    /** Ruta de la copia que va junto a [mainPath] (`nombre (Aldune).md`, `(Aldune 2)`…): la primera libre o que ya tenga este contenido. Devuelve la ruta y si hay que escribirla. */
    private fun sidePathFor(mainPath: String, hash: String, ownSideHash: String?, used: MutableSet<String>, readHash: (String) -> String?): Pair<String, Boolean> {
        val dir = dirOf(mainPath)
        val name = fileNameOf(mainPath).dropLast(3)
        var n = 1
        while (true) {
            val candidate = "$dir/$name ($SIDE_SUFFIX${if (n > 1) " $n" else ""}).md"
            val disk = readHash(candidate)
            if (disk == null || disk == ownSideHash) { used += candidate.lowercase(); return candidate to true }
            if (disk == hash) { used += candidate.lowercase(); return candidate to false }
            n++
        }
    }

    private fun titleBase(text: String, placeholder: String): String = MarkdownExport.suggestedFileName(text, placeholder).removeSuffix(".md")

    /** Una ruta libre en [dir]: ni usada por otra nota ni (con [checkDisk]) ya existente. */
    private fun uniquePath(dir: String, fileName: String, used: MutableSet<String>, readHash: (String) -> String?, checkDisk: Boolean): String {
        val base = fileName.removeSuffix(".md")
        var n = 1
        while (true) {
            val candidate = "$dir/$base${if (n > 1) " ($n)" else ""}.md"
            if (candidate.lowercase() !in used && (!checkDisk || readHash(candidate) == null)) { used += candidate.lowercase(); return candidate }
            n++
        }
    }

    private fun dirOf(path: String) = path.substring(0, path.lastIndexOf('/'))

    private fun fileNameOf(path: String) = path.substring(path.lastIndexOf('/') + 1)

    /** Solo rutas relativas de dos niveles (`carpeta/fichero.md`) sin salir de la carpeta de copia: un manifiesto manipulado no puede tocar nada fuera. */
    private fun isSafe(path: String?): Boolean {
        if (path.isNullOrEmpty() || '\\' in path || ':' in path || path.startsWith("/")) return false
        val parts = path.split('/')
        return parts.size == 2 && parts.all { it.isNotEmpty() && it != "." && it != ".." } && parts[1].endsWith(".md", ignoreCase = true)
    }
}

package app.aldune.domain.importing

import java.io.ByteArrayOutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

fun file(name: String, text: String, modified: Long? = null) = ImportInput.ofBytes(name, text.toByteArray(Charsets.UTF_8), modified)

fun zip(name: String, vararg entries: Pair<String, ByteArray>): ImportInput {
    val out = ByteArrayOutputStream()
    ZipOutputStream(out).use { z -> for ((n, b) in entries) { z.putNextEntry(ZipEntry(n)); z.write(b); z.closeEntry() } }
    return ImportInput.ofBytes(name, out.toByteArray())
}

val ImportPreview.texts get() = notes.map { it.text }

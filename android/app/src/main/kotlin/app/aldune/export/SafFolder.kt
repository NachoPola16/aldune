package app.aldune.export

import android.content.Context
import android.net.Uri
import android.provider.DocumentsContract
import java.io.FileNotFoundException
import java.io.IOException
import java.security.MessageDigest

/** La carpeta elegida ya no existe o el sistema nos quitó el permiso: la copia automática se apaga y se avisa una vez. */
class FolderLostException(message: String, cause: Throwable? = null) : IOException(message, cause)

/**
 * Una carpeta elegida con el selector del sistema (`ACTION_OPEN_DOCUMENT_TREE`), con solo lo que necesita la copia automática: leer la huella de
 * un fichero, escribirlo y moverlo. Las rutas son relativas de dos niveles (`notas/Lista.md`). **No hay borrar**: ni existe la operación aquí.
 * Usa `DocumentsContract` directamente (sin librerías extra); una consulta por nivel, que para unas cuantas carpetas es de sobra.
 */
class SafFolder(context: Context, private val tree: Uri) {
    private val resolver = context.applicationContext.contentResolver
    private val rootId = DocumentsContract.getTreeDocumentId(tree)

    private fun uriOf(docId: String): Uri = DocumentsContract.buildDocumentUriUsingTree(tree, docId)

    /** Existe y se puede consultar la carpeta raíz. Un permiso perdido o una carpeta borrada acaban aquí en `false`. */
    fun rootExists(): Boolean = try {
        resolver.query(uriOf(rootId), arrayOf(DocumentsContract.Document.COLUMN_DOCUMENT_ID), null, null, null)?.use { it.moveToFirst() } == true
    } catch (_: SecurityException) {
        false
    } catch (_: IllegalArgumentException) {
        false
    }

    private fun child(parentId: String, name: String): Pair<String, String>? {
        val children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, parentId)
        val cursor = resolver.query(
            children,
            arrayOf(DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME, DocumentsContract.Document.COLUMN_MIME_TYPE),
            null, null, null,
        ) ?: throw IOException("sin acceso a la carpeta")
        cursor.use {
            while (it.moveToNext()) {
                if (it.getString(1).equals(name, ignoreCase = true)) return it.getString(0) to it.getString(2)
            }
        }
        return null
    }

    private fun ensureDir(name: String): String {
        child(rootId, name)?.let { return it.first }
        val created = DocumentsContract.createDocument(resolver, uriOf(rootId), DocumentsContract.Document.MIME_TYPE_DIR, name)
            ?: throw IOException("no se pudo crear la carpeta $name")
        return DocumentsContract.getDocumentId(created)
    }

    /** Carpeta y nombre de una ruta relativa; una ruta sin carpeta (el manifiesto) está en la raíz. */
    private fun split(path: String) = if ('/' in path) path.substringBefore('/') to path.substringAfter('/') else "" to path

    private fun dirIdOf(dir: String): String? = if (dir.isEmpty()) rootId else child(rootId, dir)?.first

    private fun find(path: String): String? {
        val (dir, name) = split(path)
        val dirId = dirIdOf(dir) ?: return null
        return child(dirId, name)?.first
    }

    /** La huella SHA-256 (hexadecimal en mayúsculas, como `AutoExportPlanner.hashOf`) del fichero, o `null` si no existe. */
    fun hashOf(path: String): String? {
        val id = find(path) ?: return null
        val bytes = try {
            resolver.openInputStream(uriOf(id))?.use { it.readBytes() }
        } catch (_: FileNotFoundException) {
            null
        } ?: return null
        return MessageDigest.getInstance("SHA-256").digest(bytes).joinToString("") { "%02X".format(it) }
    }

    fun readText(path: String): String? {
        val id = find(path) ?: return null
        return resolver.openInputStream(uriOf(id))?.use { String(it.readBytes(), Charsets.UTF_8) }
    }

    /** Crea o reescribe [path] con [content] (UTF-8 sin BOM, como Windows). */
    fun write(path: String, content: String) {
        val (dir, name) = split(path)
        val dirId = if (dir.isEmpty()) rootId else ensureDir(dir)
        val existing = child(dirId, name)?.first
        val target = if (existing != null) uriOf(existing)
        else DocumentsContract.createDocument(resolver, uriOf(dirId), mimeOf(name), name) ?: throw IOException("no se pudo crear $path")
        // «wt»: sobrescribir de verdad; con «w» algunos proveedores dejan la cola del contenido anterior.
        resolver.openOutputStream(target, "wt")?.use { it.write(content.toByteArray(Charsets.UTF_8)) } ?: throw IOException("no se pudo escribir $path")
    }

    /** Mueve [from] a [to] sin borrar nada. Si el proveedor no sabe mover, copia y deja el original donde está: preferible a perder un fichero. */
    fun move(from: String, to: String) {
        val (fromDir, fromName) = split(from)
        val (toDir, toName) = split(to)
        val fromDirId = dirIdOf(fromDir) ?: return
        val srcId = child(fromDirId, fromName)?.first ?: return
        val toDirId = ensureDir(toDir)

        val moved = try {
            DocumentsContract.moveDocument(resolver, uriOf(srcId), uriOf(fromDirId), uriOf(toDirId))
        } catch (_: UnsupportedOperationException) {
            null
        } catch (_: IllegalStateException) {
            null
        }
        if (moved == null) {
            write(to, readText(from) ?: return)
            return
        }
        val nowName = child(toDirId, fromName)?.takeIf { fromName != toName }
        if (nowName != null) DocumentsContract.renameDocument(resolver, moved, toName)
    }

    // El tipo tiene que casar con la extensión: con otro, el proveedor del sistema añade la suya al nombre (un `.json` con «text/markdown» salía como `.json.md`).
    private fun mimeOf(name: String) = if (name.endsWith(".json", ignoreCase = true)) "application/json" else "text/markdown"
}

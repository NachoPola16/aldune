package app.aldune.domain

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.util.zip.ZipInputStream
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class NotesExportTest {
    private val fx = Fixture()

    private fun note(text: String, state: NoteState = NoteState.ACTIVE, protectedNote: Boolean = false, title: String? = null): Note {
        val created = fx.create(text.ifEmpty { "x" })
        val base = fx.stored(created.id).copy(state = state)
        return if (protectedNote) base.copy(text = "", isProtected = true, protectedContentJson = "{}", protectedTitle = title) else base
    }

    private fun unzip(bytes: ByteArray): Map<String, String> {
        val result = linkedMapOf<String, String>()
        ZipInputStream(ByteArrayInputStream(bytes), Charsets.UTF_8).use { zip ->
            generateSequence { zip.nextEntry }.forEach { result[it.name] = zip.readBytes().toString(Charsets.UTF_8) }
        }
        return result
    }

    private fun export(notes: List<Note>): Pair<NotesExport.Result, Map<String, String>> {
        val out = ByteArrayOutputStream()
        val result = NotesExport.write(out, notes, placeholder = "Sin título") { count -> "$count protegidas sin exportar" }
        return result to unzip(out.toByteArray())
    }

    @Test fun cadaNotaActivaYArchivadaVaEnSuFicheroMarkdown() {
        val (result, files) = export(listOf(note("Compra\n☐ pan"), note("Idea", state = NoteState.ARCHIVED)))
        assertEquals(NotesExport.Result(exported = 2, omittedProtected = 0), result)
        assertEquals(setOf("notas/Compra.md", "archivadas/Idea.md"), files.keys)
        assertTrue(files.getValue("notas/Compra.md").contains("- [ ] pan"))
    }

    @Test fun laPapeleraNoSeExporta() {
        val (result, files) = export(listOf(note("Hola"), note("Borrada", state = NoteState.TRASHED)))
        assertEquals(1, result.exported)
        assertEquals(setOf("notas/Hola.md"), files.keys)
    }

    @Test fun lasProtegidasNoSeExportanNiSuContenidoPeroSeCuentan() {
        val (result, files) = export(listOf(note("Hola"), note("", protectedNote = true, title = "Banco"), note("", protectedNote = true)))
        assertEquals(NotesExport.Result(exported = 1, omittedProtected = 2), result)
        assertEquals(setOf("notas/Hola.md", "protegidas-omitidas.txt"), files.keys)
        assertEquals("2 protegidas sin exportar", files.getValue("protegidas-omitidas.txt").trim())
        // Ni siquiera el nombre visible: el aviso solo da la cuenta.
        assertFalse(files.values.any { it.contains("Banco") })
    }

    @Test fun sinProtegidasNoHayFicheroDeAviso() {
        val (_, files) = export(listOf(note("Hola")))
        assertFalse("protegidas-omitidas.txt" in files)
    }

    @Test fun losTitulosRepetidosNoSePisan() {
        val (_, files) = export(listOf(note("Lista"), note("Lista"), note("lista")))
        assertEquals(setOf("notas/Lista.md", "notas/Lista (2).md", "notas/lista (3).md"), files.keys)
    }

    @Test fun elNombreSeLimpiaDeCaracteresProhibidosYReservados() {
        assertEquals("a b c d e f g h i.md", NotesExport.fileName("a/b\\c:d*e?f\"g<h>i", "x", mutableSetOf()))
        assertEquals("x.md", NotesExport.fileName("   ...  ", "x", mutableSetOf()))
        assertEquals("x.md", NotesExport.fileName("CON", "x", mutableSetOf()))
        assertEquals("x.md", NotesExport.fileName("", "x", mutableSetOf()))
    }

    @Test fun elNombreTieneTopeYNoPartePaaresSustitutos() {
        val name = NotesExport.fileName("a".repeat(NotesExport.MAX_NAME_LENGTH - 1) + "😀" + "zzz", "x", mutableSetOf()).removeSuffix(".md")
        assertTrue(name.length <= NotesExport.MAX_NAME_LENGTH)
        // Ningún sustituto suelto: lo que queda es todo «a».
        assertTrue(name.all { !it.isSurrogate() })
    }

    @Test fun elTextoSeEscribeEnUtf8() {
        val (_, files) = export(listOf(note("Acción ñ 🛒")))
        assertTrue(files.getValue("notas/Acción ñ 🛒.md").startsWith("# Acción ñ 🛒"))
    }

    @Test fun sinNotasDevuelveCeroYUnZipVacio() {
        val (result, files) = export(emptyList())
        assertEquals(NotesExport.Result(0, 0), result)
        assertTrue(files.isEmpty())
    }
}

package app.aldune.domain

import app.aldune.text.MarkdownExport
import kotlin.test.Test
import kotlin.test.assertContains
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class AutoExportPlannerTest {
    private val placeholder = "Nota"

    /** Una carpeta en memoria: lo que el planificador ve del disco (solo huellas). Las claves no distinguen mayúsculas, como en Windows. */
    private class FakeFolder {
        val files = java.util.TreeMap<String, String>(String.CASE_INSENSITIVE_ORDER)

        fun hashOf(path: String): String? = files[path]?.let(AutoExportPlanner::hashOf)

        fun apply(actions: List<ExportAction>) {
            for (a in actions) when (a) {
                is ExportAction.Write -> files[a.path] = a.content
                is ExportAction.Move -> files[a.to] = files.remove(a.from)!!
            }
        }
    }

    private var next = 0
    private fun note(text: String, state: NoteState = NoteState.ACTIVE, isProtected: Boolean = false, id: String = "n${next++}") = ExportNote(id, text, state, isProtected)

    private fun run(folder: FakeFolder, manifest: ExportManifest, notes: List<ExportNote>, archived: Boolean = true): AutoExportPlan {
        val plan = AutoExportPlanner.plan(notes, manifest, archived, placeholder, folder::hashOf)
        folder.apply(plan.actions)
        return plan
    }

    @Test
    fun `una nota nueva se escribe como Markdown en notas`() {
        val n = note("Lista\n☐ pan")
        val plan = run(FakeFolder(), ExportManifest(), listOf(n))
        val write = plan.actions.single() as ExportAction.Write
        assertEquals("notas/Lista.md", write.path)
        assertEquals(MarkdownExport.toMarkdown(n.text, placeholder), write.content)
        assertEquals("notas/Lista.md", plan.manifest.files[n.id]!!.path)
    }

    @Test
    fun `sin cambios no se escribe nada la segunda vez`() {
        val folder = FakeFolder()
        val notes = listOf(note("Uno"), note("Dos"))
        val first = run(folder, ExportManifest(), notes)
        assertTrue(run(folder, first.manifest, notes).actions.isEmpty())
    }

    @Test
    fun `una nota editada reescribe su fichero con el mismo nombre`() {
        val folder = FakeFolder()
        val first = run(folder, ExportManifest(), listOf(note("Titulo\nuno", id = "a")))
        val second = run(folder, first.manifest, listOf(note("Titulo nuevo\nuno", id = "a")))
        assertEquals("notas/Titulo.md", (second.actions.single() as ExportAction.Write).path)
        assertContains(folder.files["notas/Titulo.md"]!!, "Titulo nuevo")
    }

    @Test
    fun `dos notas con el mismo titulo tienen nombres distintos`() {
        val plan = run(FakeFolder(), ExportManifest(), listOf(note("Igual"), note("igual")))
        assertEquals(2, plan.actions.filterIsInstance<ExportAction.Write>().map { it.path.lowercase() }.distinct().size)
    }

    @Test
    fun `protegidas y papelera nunca se escriben`() {
        val plan = run(FakeFolder(), ExportManifest(), listOf(note("Secreto", isProtected = true), note("Basura", NoteState.TRASHED), note("Normal")))
        assertEquals(listOf("notas/Normal.md"), plan.actions.filterIsInstance<ExportAction.Write>().map { it.path })
    }

    @Test
    fun `las archivadas van a archivadas solo si se pide`() {
        val n = note("Vieja", NoteState.ARCHIVED)
        assertEquals("archivadas/Vieja.md", (run(FakeFolder(), ExportManifest(), listOf(n), archived = true).actions.single() as ExportAction.Write).path)
        assertTrue(run(FakeFolder(), ExportManifest(), listOf(n), archived = false).actions.isEmpty())
    }

    @Test
    fun `archivar mueve el fichero de carpeta en vez de duplicarlo`() {
        val folder = FakeFolder()
        val first = run(folder, ExportManifest(), listOf(note("Nota", id = "a")))
        val second = run(folder, first.manifest, listOf(note("Nota", NoteState.ARCHIVED, id = "a")))
        assertTrue(second.actions.any { it == ExportAction.Move("notas/Nota.md", "archivadas/Nota.md") })
        assertFalse("notas/Nota.md" in folder.files)
        assertTrue("archivadas/Nota.md" in folder.files)
    }

    @Test
    fun `una nota borrada se mueve a eliminadas y nunca se borra`() {
        val folder = FakeFolder()
        val first = run(folder, ExportManifest(), listOf(note("Adios", id = "a")))
        val second = run(folder, first.manifest, emptyList())
        assertTrue(second.actions.any { it == ExportAction.Move("notas/Adios.md", "_eliminadas/Adios.md") })
        assertTrue(second.manifest.files.isEmpty())
        assertTrue("_eliminadas/Adios.md" in folder.files)
    }

    @Test
    fun `una nota que pasa a protegida sale de notas`() {
        val folder = FakeFolder()
        val first = run(folder, ExportManifest(), listOf(note("Antes", id = "a")))
        run(folder, first.manifest, listOf(note("Antes", isProtected = true, id = "a")))
        assertFalse("notas/Antes.md" in folder.files)
        assertTrue("_eliminadas/Antes.md" in folder.files)
    }

    @Test
    fun `un fichero editado a mano no se pisa y la copia va al lado`() {
        val folder = FakeFolder()
        val first = run(folder, ExportManifest(), listOf(note("Mia\nuno", id = "a")))
        folder.files["notas/Mia.md"] = "editado a mano"
        val second = run(folder, first.manifest, listOf(note("Mia\ndos", id = "a")))
        assertEquals("editado a mano", folder.files["notas/Mia.md"])
        val side = second.actions.single() as ExportAction.Write
        assertEquals("notas/Mia (Aldune).md", side.path)
        assertContains(side.content, "dos")
    }

    @Test
    fun `la copia de al lado no se reescribe mientras nada cambie`() {
        val folder = FakeFolder()
        val first = run(folder, ExportManifest(), listOf(note("Mia\nuno", id = "a")))
        folder.files["notas/Mia.md"] = "editado a mano"
        val notes = listOf(note("Mia\ndos", id = "a"))
        val second = run(folder, first.manifest, notes)
        assertTrue(run(folder, second.manifest, notes).actions.isEmpty())
    }

    @Test
    fun `un fichero que desaparecio se vuelve a escribir`() {
        val folder = FakeFolder()
        val notes = listOf(note("Vuelve"))
        val first = run(folder, ExportManifest(), notes)
        folder.files.clear()
        assertEquals("notas/Vuelve.md", (run(folder, first.manifest, notes).actions.single() as ExportAction.Write).path)
    }

    @Test
    fun `un fichero ajeno con el mismo nombre nunca se pisa`() {
        val folder = FakeFolder()
        folder.files["notas/Lista.md"] = "de otra herramienta"
        val plan = run(folder, ExportManifest(), listOf(note("Lista")))
        assertEquals("de otra herramienta", folder.files["notas/Lista.md"])
        assertEquals("notas/Lista (Aldune).md", (plan.actions.single() as ExportAction.Write).path)
    }

    @Test
    fun `mover a eliminadas no pisa uno anterior`() {
        val folder = FakeFolder()
        folder.files["_eliminadas/Adios.md"] = "anterior"
        val first = run(folder, ExportManifest(), listOf(note("Adios", id = "a")))
        run(folder, first.manifest, emptyList())
        assertEquals("anterior", folder.files["_eliminadas/Adios.md"])
        assertTrue("_eliminadas/Adios (2).md" in folder.files)
    }

    @Test
    fun `el manifiesto va y vuelve por JSON y uno roto es vacio`() {
        val manifest = ExportManifest(mapOf("abc" to ExportEntry("notas/A.md", "123", "notas/A (Aldune).md", "456")))
        val back = ExportManifest.fromJson(manifest.toJson())
        assertEquals(manifest, back)
        assertTrue(ExportManifest.fromJson("{ esto no es json").files.isEmpty())
        assertTrue(ExportManifest.fromJson(null).files.isEmpty())
    }

    @Test
    fun `el manifiesto de Windows se lee tal cual`() {
        // El mismo formato que escribe ExportManifest.ToJson de Core: así una carpeta apuntada desde las dos apps sigue siendo entendida.
        val windows = """{"Version":1,"Files":{"abc":{"Path":"notas/A.md","Hash":"123","SidePath":null,"SideHash":null}}}"""
        assertEquals(ExportEntry("notas/A.md", "123"), ExportManifest.fromJson(windows).files["abc"])
    }

    @Test
    fun `las rutas del manifiesto que se salen de la carpeta se ignoran`() {
        val manifest = ExportManifest(mapOf("x" to ExportEntry("../fuera.md", "1"), "y" to ExportEntry("C:/windows/x.md", "1")))
        val plan = run(FakeFolder(), manifest, emptyList())
        assertTrue(plan.actions.isEmpty())
        assertTrue(plan.manifest.files.isEmpty())
    }
}

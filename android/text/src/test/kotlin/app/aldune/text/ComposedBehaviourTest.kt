package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Pruebas propias de Kotlin donde los vectores no llegan: operaciones encadenadas como las usará la app y robustez ante
 * entradas límite (los vectores prueban cada función por separado, no su composición).
 */
class ComposedBehaviourTest {
    /** Marcar una tarea y recolocarla, como hará la app: ToggleCheckboxAt → SettleToggled, con el cursor mapeado con mapIndex. */
    private fun toggleAndSettle(text: String, glyphIndex: Int): TextEdit {
        val toggled = assertNotNull(TaskLines.toggleCheckboxAt(text, glyphIndex))
        return TaskLists.settleToggled(toggled, glyphIndex)
    }

    private fun <T> assertNotNull(value: T?): T {
        org.junit.jupiter.api.Assertions.assertNotNull(value)
        return value!!
    }

    @Test fun marcarUnaTareaLaMandaAlFinalYElCursorLaSigue() {
        val text = "☐ a\n☐ b\n☐ c"
        val edit = toggleAndSettle(text, 4) // marca «b»
        assertEquals("☐ a\n☐ c\n☒ b", edit.text)
        assertTrue(edit.changed)
        // El cursor estaba al final de «b» (posición 7); «b» ahora es la última línea: 8 + 3 = 11.
        assertEquals(11, edit.mapIndex(7))
        // Lo que estaba en «c» sube una línea.
        assertEquals(7, edit.mapIndex(11))
    }

    @Test fun desmarcarUnaTareaLaSubeDelanteDeLaPrimeraHecha() {
        val text = "☐ a\n☒ b\n☒ c"
        val edit = toggleAndSettle(text, 8) // desmarca «c»
        assertEquals("☐ a\n☐ c\n☒ b", edit.text)
        assertEquals(4, edit.mapIndex(8)) // el glifo de «c» queda al principio de la segunda línea
    }

    @Test fun marcarLaUltimaNoMueveNada() {
        val edit = toggleAndSettle("☐ a\n☐ b", 4)
        // Ya era la última: SettleToggled no reordena nada (el cambio de la casilla lo hizo ToggleCheckboxAt).
        assertEquals("☐ a\n☒ b", edit.text)
        assertFalse(edit.changed)
    }

    @Test fun conSaltosCrlfLosTerminadoresNoSeMezclan() {
        val text = "☐ a\r\n☐ b\r\n☐ c"
        val edit = toggleAndSettle(text, 0) // marca «a»
        assertEquals("☐ b\r\n☐ c\r\n☒ a", edit.text)
    }

    @Test fun conSubtareasNoSeReordena() {
        val text = "☐ a\n    ☐ sub\n☐ b"
        val toggled = assertNotNull(TaskLines.toggleCheckboxAt(text, 0))
        val edit = TaskLists.settleToggled(toggled, 0)
        assertFalse(edit.changed)
        assertEquals(toggled, edit.text)
    }

    @Test fun uncheckAllEsIdempotente() {
        val text = "☒ a\r\n☑ b\n  ☒ c\n☐ d\nlibre ☒"
        val once = TaskLists.uncheckAll(text)
        assertTrue(once.changed)
        assertEquals("☐ a\r\n☐ b\n  ☐ c\n☐ d\nlibre ☒", once.text)
        val twice = TaskLists.uncheckAll(once.text)
        assertFalse(twice.changed)
        assertEquals(once.text, twice.text)
        // Sin cambios de longitud ni de líneas: el mapa es la identidad.
        for (i in 0..text.length) assertEquals(i, once.mapIndex(i))
    }

    @Test fun removeCheckedYSettleNoDejanTerminadoresColgando() {
        val edit = TaskLists.removeChecked("☐ a\r\n☒ b\r\n☐ c\r\n☒ d")
        assertEquals("☐ a\r\n☐ c", edit.text)
        assertEquals(edit.text, TaskLists.removeChecked(edit.text).text)
        assertFalse(TaskLists.removeChecked(edit.text).changed)
    }

    @Test fun conmutarUnaTareaDosVecesVuelveAlTextoOriginal() {
        for (text in listOf("hola", "  texto", "→ viñeta", "☐ tarea", "")) {
            val on = TaskLines.toggleTaskLineAt(text, 0)
            val off = TaskLines.toggleTaskLineAt(on.text, on.caret)
            // Una tarea ya hecha se quita; una línea normal se convierte y se desconvierte.
            if (TaskLines.isTaskLine(text)) assertFalse(TaskLines.isTaskLine(on.text)) else assertEquals(text, off.text)
        }
    }

    @Test fun enterContinuaYElSegundoEnterTermina() {
        val first = assertNotNull(TaskLines.enterContinuation("☐ a", 3, "\n"))
        assertEquals("☐ a\n☐ ", first.text)
        val second = assertNotNull(TaskLines.enterContinuation(first.text, first.caret, "\n"))
        assertEquals("☐ a\n", second.text)
        assertNull(TaskLines.enterContinuation(second.text, second.caret, "\n"))
    }

    @Test fun ningunaFuncionLanzaConCursoresFueraDeRangoOTextoVacio() {
        val texts = listOf("", "\n", "\r\n", "☐ ", "→ ☐ x", "a\r\n☐ b\n  → c", "😀 ☐ x", "☐")
        val carets = listOf(Int.MIN_VALUE, -100, -1, 0, 1, 2, 5, 100, Int.MAX_VALUE)
        for (text in texts) {
            for (caret in carets) {
                TaskLines.toggleTaskLineAt(text, caret)
                TaskLines.enterContinuation(text, caret)
                TaskLines.enterContinuation(text, caret, "\n")
                TaskLines.toggleCheckboxAt(text, caret)
                TaskLines.lineContaining(text, caret)
                TaskLines.lineStart(text, caret)
                BulletLines.toggleBulletLineAt(text, caret)
                BulletLines.enterContinuation(text, caret)
                ListPrefix.removeOnBackspace(text, caret)
                ListIndent.indent(text, caret)
                ListIndent.outdent(text, caret)
                ListIndent.isListLineAt(text, caret)
                LineMovement.move(text, caret, LineDirection.Up)
                LineMovement.move(text, caret, LineDirection.Down)
                TaskLists.checkboxOnLine(text, caret)
                TaskLists.settleToggled(text, caret).mapIndex(caret)
                for (length in listOf(Int.MIN_VALUE, -5, 0, 3, 1000, Int.MAX_VALUE)) {
                    ListIndent.indentLines(text, caret, length)
                    ListIndent.outdentLines(text, caret, length)
                }
                TaskLists.uncheckAll(text).mapIndex(caret)
                TaskLists.removeChecked(text).mapIndex(caret)
                TextEdit.unchanged(text).mapIndex(caret)
            }
            TaskLines.count(text)
            TaskLists.pendingTasks(text, 0)
            TaskLists.pendingTasks(text, -1)
            TaskLists.pendingTasks(text, 5)
            TaskLists.convertMarkdownTasks(text)
            NoteTitleHelper.getTitle(text)
            NoteTitleHelper.getPreview(text)
            NoteTitleHelper.getTabPreview(text)
            NoteText.split(text)
            NoteSearch.matches(text, "x")
        }
    }

    @Test fun conMapIndexEnTextoVacio() {
        assertEquals(0, TaskLists.removeChecked("").mapIndex(0))
        assertEquals(0, TaskLists.removeChecked("").mapIndex(7))
        // Todo borrado: los cursores caen al final del texto resultante.
        val edit = TaskLists.removeChecked("☒ a\n☒ b")
        assertEquals("", edit.text)
        for (i in -3..10) assertEquals(0, edit.mapIndex(i))
    }

    @Test fun elTituloUsaElRellenoPedido() {
        assertEquals("Nueva nota", NoteTitleHelper.getTitle("", "Nueva nota"))
        assertEquals("Neue Notiz", NoteTitleHelper.getTitle("# \nresto", "Neue Notiz"))
        assertEquals("New note", NoteTitleHelper.getTitle("   "))
        assertEquals("Tema 3", NoteTitleHelper.getTitle("# Tema 3\ncuerpo", "x"))
        assertEquals("#hashtag", NoteTitleHelper.getTitle("#hashtag", "x"))
    }

    @Test fun elTextoSePartePorElPrimerSaltoSinNormalizar() {
        val parts = NoteText.split("Título\r\ncuerpo\r\nmás")
        assertEquals("Título", parts.title)
        assertEquals("cuerpo\r\nmás", parts.body)
        assertEquals("Título\r\ncuerpo\r\nmás", NoteText.join(parts.title, parts.body))
    }

    @Test fun markdownSoloSeConvierteAlPrincipioDeLinea() {
        assertEquals("☐ a\r\n☒ b\n  ☐ c\nx [ ] y", TaskLists.convertMarkdownTasks("- [ ] a\r\n[x] b\n  * [ ] c\nx [ ] y"))
    }

    @Test fun losColoresDeSerieSonValidosYElRepartoNoLanza() {
        for (theme in NoteThemes.builtIn) {
            for (color in theme.darkColors + theme.lightColors) assertNotNull(OklchColor.fromHex(color))
            for (tone in NoteTone.entries) for (rule in NoteColorAssignment.entries) {
                NoteColorAssigner.assign(theme, tone, rule, null, emptyList())
                NoteColorAssigner.assign(theme, tone, rule, "#NOPE", listOf("#000000", "mal", theme.lightColors.firstOrNull() ?: "#FFF"))
            }
        }
        // Un tema sin colores cae en los del Clásico, sin lanzar.
        assertEquals("#EBD38B", NoteColorAssigner.assign(NoteTheme(id = "vacio"), NoteTone.Both, NoteColorAssignment.Rotate, null, emptyList()))
    }
}

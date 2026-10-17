package app.aldune.text

import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

/** Espejo del `Execute` de `TextVectorsTests.cs`: nombre de operación + argumentos → resultado en la forma JSON de los vectores. */
object Operations {
    fun execute(op: String, a: JsonObject): JsonElement = when (op) {
        "TaskLines.GlyphIndex" -> num(TaskLines.glyphIndex(a.s("line")))
        "TaskLines.IsTaskLine" -> bool(TaskLines.isTaskLine(a.s("line")))
        "TaskLines.IsChecked" -> bool(TaskLines.isChecked(a.s("line")))
        "TaskLines.PrefixLength" -> num(TaskLines.prefixLength(a.s("line"), a.i("glyphIndex")))
        "TaskLines.IsEmptyTaskLine" -> bool(TaskLines.isEmptyTaskLine(a.s("line")))
        "TaskLines.LineContaining" -> str(TaskLines.lineContaining(a.s("text"), a.i("index")))
        "TaskLines.LineStart" -> num(TaskLines.lineStart(a.s("text"), a.i("index")))
        "TaskLines.ToggleCheckboxAt" -> strOrNull(TaskLines.toggleCheckboxAt(a.s("text"), a.i("index")))
        "TaskLines.ToggleTaskLineAt" -> caret(TaskLines.toggleTaskLineAt(a.s("text"), a.i("caret")))
        "TaskLines.EnterContinuation" -> caret(TaskLines.enterContinuation(a.s("text"), a.i("caret"), a.s("newLine")))
        "TaskLines.Count" -> TaskLines.count(a.s("text")).let { obj("done" to num(it.done), "total" to num(it.total)) }

        "BulletLines.GlyphIndex" -> num(BulletLines.glyphIndex(a.s("line")))
        "BulletLines.IsBulletLine" -> bool(BulletLines.isBulletLine(a.s("line")))
        "BulletLines.PrefixLength" -> num(BulletLines.prefixLength(a.s("line"), a.i("glyphIndex")))
        "BulletLines.IsEmptyBulletLine" -> bool(BulletLines.isEmptyBulletLine(a.s("line")))
        "BulletLines.ToggleBulletLineAt" -> caret(BulletLines.toggleBulletLineAt(a.s("text"), a.i("caret")))
        "BulletLines.EnterContinuation" -> caret(BulletLines.enterContinuation(a.s("text"), a.i("caret"), a.s("newLine")))

        "ListPrefix.RemoveOnBackspace" -> caret(ListPrefix.removeOnBackspace(a.s("text"), a.i("caret")))

        "ListIndent.Indent" -> caret(ListIndent.indent(a.s("text"), a.i("caret")))
        "ListIndent.Outdent" -> caret(ListIndent.outdent(a.s("text"), a.i("caret")))
        "ListIndent.IndentLines" -> selection(ListIndent.indentLines(a.s("text"), a.i("selectionStart"), a.i("selectionLength")))
        "ListIndent.OutdentLines" -> selection(ListIndent.outdentLines(a.s("text"), a.i("selectionStart"), a.i("selectionLength")))
        "ListIndent.IsListLineAt" -> bool(ListIndent.isListLineAt(a.s("text"), a.i("caret")))

        "LineMovement.Move" -> caret(LineMovement.move(a.s("text"), a.i("caret"), LineDirection.entries[a.i("direction")]))

        "TaskLists.CheckboxOnLine" -> TaskLists.checkboxOnLine(a.s("text"), a.i("caret")).let { if (it == null) JsonNull else num(it) }
        "TaskLists.HasChecked" -> bool(TaskLists.hasChecked(a.s("text")))
        "TaskLists.IsAllDone" -> bool(TaskLists.isAllDone(a.s("text")))
        "TaskLists.UncheckAll" -> edit(TaskLists.uncheckAll(a.s("text")), a.s("text"))
        "TaskLists.RemoveChecked" -> edit(TaskLists.removeChecked(a.s("text")), a.s("text"))
        "TaskLists.SettleToggled" -> edit(TaskLists.settleToggled(a.s("text"), a.i("glyphIndex")), a.s("text"))
        "TaskLists.ConvertMarkdownTasks" -> str(TaskLists.convertMarkdownTasks(a.s("text")))
        "TaskLists.PendingTasks" -> strings(TaskLists.pendingTasks(a.s("text"), a.i("max")))

        // Los vectores usan el relleno por defecto de Core, "New note".
        "NoteTitleHelper.GetTitle" -> str(NoteTitleHelper.getTitle(a.s("text"), "New note"))
        "NoteTitleHelper.GetPreview" -> str(NoteTitleHelper.getPreview(a.s("text")))
        "NoteTitleHelper.GetTabPreview" -> str(NoteTitleHelper.getTabPreview(a.s("text")))
        "NoteText.Split" -> NoteText.split(a.s("text")).let { obj("title" to str(it.title), "body" to str(it.body)) }
        "NoteText.Join" -> str(NoteText.join(a.s("title"), a.s("body")))

        else -> throw NotImplementedError("Operación sin implementar: $op")
    }

    // Formas de resultado de los vectores (README: «Convenciones de la traducción a JSON»).
    fun num(v: Int): JsonElement = JsonPrimitive(v)
    fun num(v: Double): JsonElement = JsonPrimitive(v)
    fun bool(v: Boolean): JsonElement = JsonPrimitive(v)
    fun str(v: String): JsonElement = JsonPrimitive(v)
    fun strOrNull(v: String?): JsonElement = if (v == null) JsonNull else JsonPrimitive(v)
    fun obj(vararg pairs: Pair<String, JsonElement>): JsonElement = JsonObject(mapOf(*pairs))
    fun caret(v: TextAndCaret?): JsonElement =
        if (v == null) JsonNull else obj("text" to str(v.text), "caret" to num(v.caret))
    fun selection(v: SelectionEdit): JsonElement = obj(
        "text" to str(v.text), "selectionStart" to num(v.selectionStart), "selectionLength" to num(v.selectionLength)
    )
    /** `{text, changed, map}` con `map[i] = mapIndex(i)` para cada `i` de 0 a la longitud del texto original. */
    fun edit(e: TextEdit, original: String): JsonElement = obj(
        "text" to str(e.text), "changed" to bool(e.changed),
        "map" to JsonArray((0..original.length).map { JsonPrimitive(e.mapIndex(it)) })
    )
    fun strings(v: List<String>): JsonElement = JsonArray(v.map { JsonPrimitive(it) })
}

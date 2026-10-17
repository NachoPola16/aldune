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
    fun strings(v: List<String>): JsonElement = JsonArray(v.map { JsonPrimitive(it) })
}

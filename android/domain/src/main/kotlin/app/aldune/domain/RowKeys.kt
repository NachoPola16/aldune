package app.aldune.domain

/** Keys of the note list rows: a plain note id, or `<group>/<id>` in the grouped view, where a note repeats under each of its tags. */
object RowKeys {
    /** True if the row with this [key] is the note [noteId]. */
    fun shows(key: Any, noteId: String): Boolean = key == noteId || (key is String && key.endsWith("/$noteId"))
}

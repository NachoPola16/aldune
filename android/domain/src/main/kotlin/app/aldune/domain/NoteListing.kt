package app.aldune.domain

import app.aldune.text.NoteTitleHelper

/** Orden de la lista de notas; los números son los de `NoteListOrder` de Windows (se guardan como número). */
enum class NoteListOrder(val code: Int) {
    DOCK(0), NEWEST(1), OLDEST(2), TITLE(3);

    companion object {
        fun fromCode(code: Int): NoteListOrder = entries.firstOrNull { it.code == code } ?: DOCK
    }
}

/** Port de `NoteListing.Sort`. */
object NoteListing {
    fun sort(notes: List<Note>, order: NoteListOrder): List<Note> = when (order) {
        NoteListOrder.NEWEST -> notes.sortedByDescending { it.updatedAt }
        NoteListOrder.OLDEST -> notes.sortedBy { it.updatedAt }
        // Las protegidas no enseñan su título: al final, en vez de ordenarlas por un texto que no se ve.
        NoteListOrder.TITLE -> notes.sortedWith(
            compareBy<Note> { it.isProtected }.thenBy(String.CASE_INSENSITIVE_ORDER) { NoteTitleHelper.getTitle(it.text, "") },
        )
        NoteListOrder.DOCK -> notes
    }
}

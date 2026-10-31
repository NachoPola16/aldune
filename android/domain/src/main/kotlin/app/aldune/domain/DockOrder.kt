package app.aldune.domain

/**
 * El orden del mazo en un solo sitio: la lista de la app y el widget tienen que enseñar las notas exactamente igual.
 * Las numeradas por posición; las que no tienen posición detrás de todas (salvo una posición mayor que 1e18, que Core también
 * deja detrás: se copia tal cual). Core no define el desempate de un empate de fecha; aquí se añade el id para que el resultado
 * no cambie entre llamadas. Las fijadas arriba van delante de todas (como `ORDER BY PinnedToTop DESC` de `GetByState` en Core) y cada grupo
 * conserva este orden: el widget, el canal de color y el reordenado heredan la partición sin más.
 */
object DockOrder {
    private const val UNPOSITIONED = 1e18

    fun sort(notes: Collection<Note>): List<Note> =
        notes.sortedWith(compareBy<Note> { !it.pinnedToTop }.thenBy { it.dockPosition ?: UNPOSITIONED }.thenBy { it.createdAt }.thenBy { it.id })
}

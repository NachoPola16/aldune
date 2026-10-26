package app.aldune.text

/** Los colores que enseña el selector de color de una nota. */
object NoteColorChoices {
    /**
     * La paleta del tema más los colores que **no** son del tema y que la nota ha tenido mientras el diálogo está abierto (el de
     * apertura y los que se vayan eligiendo), delante y sin repetir. Con solo el color actual, el que traía la nota (el negro de un
     * aspecto retro, por ejemplo) desaparecía en cuanto se elegía otro y ya no había forma de volver a él.
     */
    fun palette(themeColors: List<String>, kept: List<String>): List<String> {
        val extras = ArrayList<String>()
        for (hex in kept) {
            if (themeColors.none { it.equals(hex, ignoreCase = true) } && extras.none { it.equals(hex, ignoreCase = true) }) extras += hex
        }
        return extras + themeColors
    }
}

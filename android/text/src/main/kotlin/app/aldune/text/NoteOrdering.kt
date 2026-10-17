package app.aldune.text

/** Orden manual del mazo: posiciones con huecos para insertar entre dos notas sin renumerar, y arrastre por casillas. Port de `NoteOrdering`. */
object NoteOrdering {
    /** Hueco entre posiciones consecutivas al renumerar. */
    const val GAP = 1024.0

    /** Una banda de casilla se cruza al pasar de ese tramo de casilla (0,75 = a tres cuartos de la siguiente). */
    const val SLOT_HYSTERESIS = 0.75

    /** Posición entre dos vecinas (`null` = no hay vecina de ese lado); `null` si ya no cabe ningún número entre ellas y hay que renumerar. */
    fun between(before: Double?, after: Double?): Double? {
        if (before == null && after == null) return 0.0
        if (before == null) return after!! - GAP
        if (after == null) return before + GAP

        val middle = before + (after - before) / 2

        // Sin sitio: el punto medio coincide con alguno de los extremos por precisión del double.
        if (middle <= before || middle >= after) return null

        return middle
    }

    fun spaced(count: Int): List<Double> = List(maxOf(count, 0)) { it * GAP }

    /** Cuántas casillas se ha movido algo arrastrado `delta` píxeles con casillas de `pitch`; el redondeo tiene histéresis. */
    fun slotShift(delta: Double, pitch: Double, hysteresis: Double = SLOT_HYSTERESIS): Int {
        if (pitch <= 0) return 0

        val slots = delta / pitch
        // (int)Math.Truncate de .NET: Kotlin también trunca hacia cero (y satura/NaN→0 igual que .NET moderno).
        var whole = slots.toInt()
        val fraction = slots - whole

        if (fraction >= hysteresis) whole += 1
        else if (fraction <= -hysteresis) whole -= 1

        return whole
    }

    fun targetIndex(originalIndex: Int, delta: Double, pitch: Double, count: Int): Int {
        if (count <= 0) return 0
        return (originalIndex + slotShift(delta, pitch)).coerceIn(0, count - 1)
    }
}

package app.aldune.text

import java.time.Instant

/** La señal ▂▄▆█ de cada nota: estado real de su sync, no decoración. Port de `NoteSyncSignal` (sin alcance selectivo: Android no lo tiene). */
object NoteSyncSignal {
    enum class State { HIDDEN, SYNCED, PENDING, CONFLICT }

    /** [base] es la última versión acordada con el almacén; la nota está sincronizada si su fecha es esa. */
    fun of(syncConfigured: Boolean, hasConflict: Boolean, base: Instant?, noteUpdatedAt: Instant): State = when {
        !syncConfigured -> State.HIDDEN
        hasConflict -> State.CONFLICT
        base != null && base == noteUpdatedAt -> State.SYNCED
        else -> State.PENDING
    }

    fun glyph(state: State): String = when (state) {
        State.SYNCED -> "▂▄▆█"
        State.PENDING -> "▂▄▆_"
        State.CONFLICT -> "▂▄!_"
        State.HIDDEN -> ""
    }
}

/** Lo que cada aspecto decide por defecto cuando el usuario no ha elegido (la parte de `AppSkin` que Android usa). */
object AspectSkin {
    private val terminal = setOf(AppPalette.TELECOM_LIGHT, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR)

    fun squareCorners(mode: Int): Boolean = mode == AppPalette.XP_DARK || mode in terminal

    fun syncSignal(mode: Int): Boolean = mode in terminal

    /** Color de la nota como fondo de toda la tarjeta: los aspectos de terminal ya deciden cómo llevarlo (franja, canal, sin color), así que van sin él. */
    fun colorFill(mode: Int): Boolean = mode !in terminal

    /** El aspecto decide solo cómo se muestra el color de la nota (como `AppearanceNoteColorStripeLocked` de Windows): el ajuste se bloquea. */
    fun colorLocked(mode: Int): Boolean = mode in terminal
}

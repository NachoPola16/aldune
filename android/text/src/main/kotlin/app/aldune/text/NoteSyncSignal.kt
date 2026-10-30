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

    /** El papel que se dibuja bajo el texto en las libretas (como `AppSkin.Paper` de Core). */
    enum class Paper { NONE, FOLDS, RULED }

    fun paper(mode: Int): Paper = when (mode) {
        AppPalette.TRAVEL_NOTEBOOK_LIGHT, AppPalette.TRAVEL_NOTEBOOK_DARK -> Paper.FOLDS
        AppPalette.FIELD_NOTEBOOK_LIGHT, AppPalette.FIELD_NOTEBOOK_DARK -> Paper.RULED
        else -> Paper.NONE
    }

    private val notebooks = setOf(
        AppPalette.TRAVEL_NOTEBOOK_LIGHT, AppPalette.TRAVEL_NOTEBOOK_DARK, AppPalette.FIELD_NOTEBOOK_LIGHT, AppPalette.FIELD_NOTEBOOK_DARK,
    )

    /** Las libretas llevan el color de la nota como franja sobre el papel, como Bash (`SkinCard.Stripe` de Core). */
    fun stripeCard(mode: Int): Boolean = mode == AppPalette.BASH || mode in notebooks

    /** Las libretas usan serifa en el cuerpo y los títulos. */
    fun serif(mode: Int): Boolean = mode in notebooks

    fun squareCorners(mode: Int): Boolean = mode == AppPalette.XP_DARK || mode in terminal

    fun syncSignal(mode: Int): Boolean = mode in terminal

    /** Color de la nota como fondo de toda la tarjeta: los aspectos de terminal ya deciden cómo llevarlo (franja, canal, sin color), así que van sin él. */
    fun colorFill(mode: Int): Boolean = mode !in terminal && mode !in notebooks

    /** El aspecto decide solo cómo se muestra el color de la nota (como `AppearanceNoteColorStripeLocked` de Windows): el ajuste se bloquea. */
    fun colorLocked(mode: Int): Boolean = mode in terminal || mode in notebooks
}

package app.aldune.ui

import androidx.annotation.StringRes
import app.aldune.R
import app.aldune.text.NoteThemes

/** Los nombres de los temas de serie se traducen (como `Strings.ThemeDisplayName` de Windows); un tema propio llega con su nombre. */
@StringRes
internal fun themeNameRes(id: String): Int? = when (id) {
    NoteThemes.CLASSIC_ID -> R.string.theme_classic
    NoteThemes.SERENE_ID -> R.string.theme_serene
    NoteThemes.GRAPHITE_ID, NoteThemes.GRAY_ID -> R.string.theme_graphite
    NoteThemes.PASTEL_ID -> R.string.theme_pastel
    NoteThemes.AUTUMN_ID -> R.string.theme_autumn
    NoteThemes.OCEAN_ID -> R.string.theme_ocean
    NoteThemes.XP_ID -> R.string.theme_xp
    else -> null
}

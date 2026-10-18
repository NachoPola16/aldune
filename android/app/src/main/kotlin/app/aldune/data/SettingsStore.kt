package app.aldune.data

import android.content.Context
import android.content.res.Configuration
import app.aldune.text.AppPalette
import java.util.Locale

/**
 * Ajustes de la app (spec de Android §2.6, los de F4). Sin migración: un campo que falta toma su valor por defecto, que es el de Windows
 * cuando existe el equivalente (papelera 30 días, «reordenar al marcar» apagado).
 */
data class AppSettings(
    /** Los números de `AppearanceMode`: Oscuro 0, Claro 1, Sistema 2, Pastel 3, Medianoche 4. */
    val appearance: Int = AppPalette.SYSTEM,
    /** Etiqueta de idioma (`en`, `es`, `de`, `fr`, `pt-BR`); vacío = el del sistema. */
    val language: String = "",
    val trashDays: Int = 30,
    val reorderOnCheck: Boolean = false,
    /** Tono de las notas nuevas: claros (por defecto, como Windows) u oscuros. */
    val darkNotes: Boolean = false,
    /** La notificación fija «Nota nueva» (spec de F6 §2.2). Apagada por defecto. */
    val captureNotification: Boolean = false,
)

object Languages {
    /** Idiomas de la interfaz y cómo se llama cada uno en su propio idioma (no se traduce: así se reconoce en cualquier idioma). */
    val all: List<Pair<String, String>> = listOf(
        "en" to "English", "es" to "Español", "de" to "Deutsch", "fr" to "Français", "pt-BR" to "Português (Brasil)",
    )

    fun locale(tag: String): Locale? = if (tag.isEmpty()) null else Locale.forLanguageTag(tag)

    /** El contexto con el idioma elegido; sin elección, el mismo. Se aplica en `attachBaseContext`, antes de que exista ninguna pantalla. */
    fun wrap(base: Context, tag: String): Context {
        val locale = locale(tag) ?: return base
        val config = Configuration(base.resources.configuration)
        config.setLocale(locale)
        return base.createConfigurationContext(config)
    }
}

class SettingsStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    fun load(): AppSettings {
        val d = AppSettings()
        return AppSettings(
            appearance = prefs.getInt(APPEARANCE, d.appearance).takeIf { it in AppPalette.DARK..AppPalette.MIDNIGHT } ?: d.appearance,
            language = prefs.getString(LANGUAGE, d.language).takeIf { tag -> tag == "" || Languages.all.any { it.first == tag } } ?: d.language,
            trashDays = prefs.getInt(TRASH_DAYS, d.trashDays).coerceIn(1, 365),
            reorderOnCheck = prefs.getBoolean(REORDER_ON_CHECK, d.reorderOnCheck),
            darkNotes = prefs.getBoolean(DARK_NOTES, d.darkNotes),
            captureNotification = prefs.getBoolean(CAPTURE_NOTIFICATION, d.captureNotification),
        )
    }

    fun save(settings: AppSettings) {
        prefs.edit()
            .putInt(APPEARANCE, settings.appearance)
            .putString(LANGUAGE, settings.language)
            .putInt(TRASH_DAYS, settings.trashDays)
            .putBoolean(REORDER_ON_CHECK, settings.reorderOnCheck)
            .putBoolean(DARK_NOTES, settings.darkNotes)
            .putBoolean(CAPTURE_NOTIFICATION, settings.captureNotification)
            .apply() // apply ya deja el valor visible en este proceso: el `recreate()` del cambio de idioma lo lee al instante.
    }

    private companion object {
        const val FILE = "settings"
        const val APPEARANCE = "appearance"
        const val LANGUAGE = "language"
        const val TRASH_DAYS = "trashDays"
        const val REORDER_ON_CHECK = "reorderOnCheck"
        const val DARK_NOTES = "darkNotes"
        const val CAPTURE_NOTIFICATION = "captureNotification"
    }
}

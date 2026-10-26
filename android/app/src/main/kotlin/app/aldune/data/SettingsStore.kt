package app.aldune.data

import android.content.Context
import android.content.res.Configuration
import app.aldune.capture.QuickList
import app.aldune.text.AppPalette
import app.aldune.text.AspectColorsJson
import app.aldune.text.AspectSkin
import app.aldune.text.NoteDisplayColor
import app.aldune.text.NoteThemes
import java.util.Locale

/**
 * Ajustes de la app (spec de Android §2.6, los de F4). Sin migración: un campo que falta toma su valor por defecto, que es el de Windows
 * cuando existe el equivalente (papelera 30 días, «reordenar al marcar» apagado).
 */
data class AppSettings(
    /** Los números de `AppearanceMode`: Oscuro 0, Claro 1, Sistema 2, Pastel 3, Medianoche 4, XP claro 5, XP oscuro 6, Telecom claro 7, Telecom oscuro 8, Bash 9, Fósforo 10. */
    val appearance: Int = AppPalette.SYSTEM,
    /** Los colores que el usuario eligió para cada aspecto retro (id de aspecto → id de hueco → `#RRGGBB`); vacío = los de fábrica. */
    val aspectColors: Map<String, Map<String, String>> = emptyMap(),
    /** Etiqueta de idioma (`en`, `es`, `de`, `fr`, `pt-BR`); vacío = el del sistema. */
    val language: String = "",
    val trashDays: Int = 30,
    val reorderOnCheck: Boolean = false,
    /** Casillas de tarea un 25 % mayores (fuente AlduneCheckLarge). Solo cambia cómo se dibujan. */
    val largeCheckboxes: Boolean = false,
    /** Tono de las notas nuevas: claros (por defecto, como Windows) u oscuros. */
    val darkNotes: Boolean = false,
    /** Tema de las notas nuevas y de la hoja de color (`ActiveThemeId` de Windows). Un id que no existe se resuelve a Clásico. */
    val noteThemeId: String = NoteThemes.CLASSIC_ID,
    /** La notificación fija «Nota nueva» (spec de F6 §2.2). Apagada por defecto. */
    val captureNotification: Boolean = false,
    /** El widget enseña también una vista previa del texto, no solo el título (spec de F6 §2.3). Apagado: el texto llega al lanzador. */
    val widgetPreview: Boolean = false,
    /** Recordatorios a la hora exacta, si el sistema concede el permiso (spec madre §2.9). Apagado: alarma no exacta. */
    val exactReminders: Boolean = false,
    /** «Mismo color en todas las notas»: `#RRGGBB` o `null` (apagado). Solo cambia cómo se ven; cada nota conserva el suyo. */
    val uniformNoteColor: String? = null,
    /** Esquinas rectas; `null` = lo que decida el aspecto (`AspectSkin`). Se vuelve a `null` al cambiar de aspecto, como en Windows. */
    val squareCorners: Boolean? = null,
    /** Señal de sincronización en cada nota; `null` = lo que decida el aspecto. */
    val showSyncSignal: Boolean? = null,
    /** Orden de la lista: los números de `NoteListOrder` (mazo 0, recientes 1, antiguas 2, título 3). Solo en el mazo se puede arrastrar. */
    val listOrder: Int = 0,
    /** Regla de color de las notas nuevas: los números de `NoteColorAssignment` (rotar evitando vecinas 0, rotar 1, más distinto 2, fijo 3). */
    val colorRule: Int = 0,
    /** Color de la regla «fijo»; `null` = el primero del tono pedido. */
    val fixedNoteColor: String? = null,
    /** El aviso de un recordatorio enseña las tareas que faltan (Windows: `ReminderIncludesTasks`, por defecto sí). */
    val reminderIncludesTasks: Boolean = true,
    /** Sincronizar también en segundo plano (el trabajo periódico de WorkManager). Apagado: solo al abrir, al editar y a mano. */
    val syncInBackground: Boolean = true,
    /** Esconder en el editor las tareas hechas pasado un plazo (`HiddenTasks`). El texto no cambia: se pueden volver a enseñar. */
    val hideDoneTasks: Boolean = false,
    /** Horas que una tarea hecha sigue a la vista antes de esconderse. */
    val hideDoneAfterHours: Int = 24,
    /** La bienvenida de la primera vez ya se enseñó (o no hacía falta: ya había notas). */
    val welcomeSeen: Boolean = false,
    /** La nota elegida como lista rápida (se le añaden artículos desde la notificación de captura); `null` = ninguna. */
    val quickListId: String? = null,
    /** El título que tenía esa nota la última vez que se vio: es lo que enseña la notificación sin abrir la base. */
    val quickListTitle: String = "",
) {
    val quickList: QuickList? get() = quickListId?.let { QuickList(it, quickListTitle) }
    val cornersSquare: Boolean get() = squareCorners ?: AspectSkin.squareCorners(appearance)
    val syncSignalVisible: Boolean get() = showSyncSignal ?: AspectSkin.syncSignal(appearance)
}

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
            appearance = prefs.getInt(APPEARANCE, d.appearance).takeIf { it in AppPalette.DARK..AppPalette.PHOSPHOR } ?: d.appearance,
            aspectColors = AspectColorsJson.parse(prefs.getString(ASPECT_COLORS, null)),
            language = prefs.getString(LANGUAGE, d.language).takeIf { tag -> tag == "" || Languages.all.any { it.first == tag } } ?: d.language,
            trashDays = prefs.getInt(TRASH_DAYS, d.trashDays).coerceIn(1, 365),
            reorderOnCheck = prefs.getBoolean(REORDER_ON_CHECK, d.reorderOnCheck),
            largeCheckboxes = prefs.getBoolean(LARGE_CHECKBOXES, d.largeCheckboxes),
            darkNotes = prefs.getBoolean(DARK_NOTES, d.darkNotes),
            noteThemeId = prefs.getString(NOTE_THEME_ID, d.noteThemeId) ?: d.noteThemeId,
            captureNotification = prefs.getBoolean(CAPTURE_NOTIFICATION, d.captureNotification),
            widgetPreview = prefs.getBoolean(WIDGET_PREVIEW, d.widgetPreview),
            exactReminders = prefs.getBoolean(EXACT_REMINDERS, d.exactReminders),
            uniformNoteColor = prefs.getString(UNIFORM_NOTE_COLOR, null)?.takeIf { NoteDisplayColor.isActive(it) },
            squareCorners = nullableBoolean(SQUARE_CORNERS),
            showSyncSignal = nullableBoolean(SHOW_SYNC_SIGNAL),
            listOrder = prefs.getInt(LIST_ORDER, d.listOrder).takeIf { it in 0..3 } ?: d.listOrder,
            colorRule = prefs.getInt(COLOR_RULE, d.colorRule).takeIf { it in 0..3 } ?: d.colorRule,
            fixedNoteColor = prefs.getString(FIXED_NOTE_COLOR, null)?.takeIf { NoteDisplayColor.isActive(it) },
            reminderIncludesTasks = prefs.getBoolean(REMINDER_INCLUDES_TASKS, d.reminderIncludesTasks),
            syncInBackground = prefs.getBoolean(SYNC_IN_BACKGROUND, d.syncInBackground),
            hideDoneTasks = prefs.getBoolean(HIDE_DONE_TASKS, d.hideDoneTasks),
            hideDoneAfterHours = prefs.getInt(HIDE_DONE_AFTER_HOURS, d.hideDoneAfterHours).coerceIn(1, 24 * 365),
            welcomeSeen = prefs.getBoolean(WELCOME_SEEN, d.welcomeSeen),
            quickListId = prefs.getString(QUICK_LIST_ID, null),
            quickListTitle = prefs.getString(QUICK_LIST_TITLE, d.quickListTitle) ?: d.quickListTitle,
        )
    }

    private fun nullableBoolean(key: String): Boolean? = if (prefs.contains(key)) prefs.getBoolean(key, false) else null

    fun save(settings: AppSettings) {
        prefs.edit()
            .putInt(APPEARANCE, settings.appearance)
            .putString(ASPECT_COLORS, AspectColorsJson.write(settings.aspectColors))
            .putString(LANGUAGE, settings.language)
            .putInt(TRASH_DAYS, settings.trashDays)
            .putBoolean(REORDER_ON_CHECK, settings.reorderOnCheck)
            .putBoolean(LARGE_CHECKBOXES, settings.largeCheckboxes)
            .putBoolean(DARK_NOTES, settings.darkNotes)
            .putString(NOTE_THEME_ID, settings.noteThemeId)
            .putBoolean(CAPTURE_NOTIFICATION, settings.captureNotification)
            .putBoolean(WIDGET_PREVIEW, settings.widgetPreview)
            .putBoolean(EXACT_REMINDERS, settings.exactReminders)
            .putOrRemove(UNIFORM_NOTE_COLOR, settings.uniformNoteColor)
            .putOrRemove(SQUARE_CORNERS, settings.squareCorners)
            .putOrRemove(SHOW_SYNC_SIGNAL, settings.showSyncSignal)
            .putInt(LIST_ORDER, settings.listOrder)
            .putInt(COLOR_RULE, settings.colorRule)
            .putOrRemove(FIXED_NOTE_COLOR, settings.fixedNoteColor)
            .putBoolean(REMINDER_INCLUDES_TASKS, settings.reminderIncludesTasks)
            .putBoolean(SYNC_IN_BACKGROUND, settings.syncInBackground)
            .putBoolean(HIDE_DONE_TASKS, settings.hideDoneTasks)
            .putInt(HIDE_DONE_AFTER_HOURS, settings.hideDoneAfterHours)
            .putBoolean(WELCOME_SEEN, settings.welcomeSeen)
            .putOrRemove(QUICK_LIST_ID, settings.quickListId)
            .putString(QUICK_LIST_TITLE, settings.quickListTitle)
            .apply() // apply ya deja el valor visible en este proceso: el `recreate()` del cambio de idioma lo lee al instante.
    }

    private fun android.content.SharedPreferences.Editor.putOrRemove(key: String, value: String?) = if (value == null) remove(key) else putString(key, value)

    private fun android.content.SharedPreferences.Editor.putOrRemove(key: String, value: Boolean?) = if (value == null) remove(key) else putBoolean(key, value)

    private companion object {
        const val FILE = "settings"
        const val WELCOME_SEEN = "welcomeSeen"
        const val QUICK_LIST_ID = "quickListId"
        const val QUICK_LIST_TITLE = "quickListTitle"
        const val APPEARANCE = "appearance"
        const val ASPECT_COLORS = "aspectColors"
        const val LANGUAGE = "language"
        const val TRASH_DAYS = "trashDays"
        const val REORDER_ON_CHECK = "reorderOnCheck"
        const val LARGE_CHECKBOXES = "largeCheckboxes"
        const val DARK_NOTES = "darkNotes"
        const val NOTE_THEME_ID = "noteThemeId"
        const val CAPTURE_NOTIFICATION = "captureNotification"
        const val WIDGET_PREVIEW = "widgetPreview"
        const val EXACT_REMINDERS = "exactReminders"
        const val UNIFORM_NOTE_COLOR = "uniformNoteColor"
        const val SQUARE_CORNERS = "squareCorners"
        const val SHOW_SYNC_SIGNAL = "showSyncSignal"
        const val LIST_ORDER = "listOrder"
        const val COLOR_RULE = "colorRule"
        const val FIXED_NOTE_COLOR = "fixedNoteColor"
        const val REMINDER_INCLUDES_TASKS = "reminderIncludesTasks"
        const val SYNC_IN_BACKGROUND = "syncInBackground"
        const val HIDE_DONE_TASKS = "hideDoneTasks"
        const val HIDE_DONE_AFTER_HOURS = "hideDoneAfterHours"
    }
}

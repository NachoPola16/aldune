package app.aldune.data

import android.content.Context
import android.content.res.Configuration
import app.aldune.capture.QuickList
import app.aldune.domain.KeyboardOnOpen
import app.aldune.domain.SoundEvent
import app.aldune.domain.SoundSettings
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
    /** Casillas de tarea un 25 % mayores (fuente AlduneCheckLarge). Solo cambia cómo se dibujan. En una instalación nueva viene activado (ver `load`). */
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
    /** Color de la nota como fondo de toda la tarjeta (como en Windows) o solo como tira lateral; `null` = lo que decida el aspecto (`AspectSkin`). */
    val fillNoteColor: Boolean? = null,
    /** Esquinas rectas; `null` = lo que decida el aspecto (`AspectSkin`). Se vuelve a `null` al cambiar de aspecto, como en Windows. */
    val squareCorners: Boolean? = null,
    /** Señal de sincronización en cada nota; `null` = lo que decida el aspecto. */
    val showSyncSignal: Boolean? = null,
    /** Forma del botón de nota nueva (el «+» de la app y los widgets): 0 como las esquinas de las notas (por defecto), 1 redondeado, 2 recto. */
    val newButtonShape: Int = 0,
    /** Mirar en GitHub, como mucho una vez al día, si hay una versión nueva de Android y avisarlo en la lista. */
    val checkUpdates: Boolean = true,
    /** Cuándo fue la última consulta (ms desde 1970) y la versión de la que el usuario ya descartó el aviso. */
    val lastUpdateCheck: Long = 0L,
    val dismissedUpdate: String? = null,
    /** El tag de la versión más nueva vista en la última consulta (así el aviso sobrevive entre arranques sin volver a consultar). */
    val knownUpdate: String? = null,
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
    /** Cuándo se abre el teclado solo al abrir una nota (`KeyboardOnOpen`): 0 nunca (por defecto), 1 solo en las nuevas, 2 siempre. */
    val keyboardOnOpen: Int = 0,
    /** Horas que una tarea hecha sigue a la vista antes de esconderse. */
    val hideDoneAfterHours: Int = 24,
    /** La bienvenida de la primera vez ya se enseñó (o no hacía falta: ya había notas). */
    val welcomeSeen: Boolean = false,
    /** El aviso de «sin sincronización no hay copia» de la lista se descartó (no vuelve a salir). */
    val backupNoticeDismissed: Boolean = false,
    /** La nota elegida como lista rápida (se le añaden artículos desde la notificación de captura); `null` = ninguna. */
    val quickListId: String? = null,
    /** El título que tenía esa nota la última vez que se vio: es lo que enseña la notificación sin abrir la base. */
    val quickListTitle: String = "",
    /** Modo simplificado: Ajustes enseña solo lo esencial (ver `SettingsSections`). Los demás ajustes no cambian al activarlo. */
    val simplifiedMode: Boolean = false,
    /** Los sonidos de la app (papel y pluma): mismo interruptor, volumen y claves que en Windows. Apagado de fábrica. */
    val sounds: SoundSettings = SoundSettings(),
    /** Copia automática de las notas en una carpeta (spec de la fase 4). Apagada de fábrica. La carpeta es un `content://` del selector del sistema, con permiso persistente. */
    val autoExportEnabled: Boolean = false,
    val autoExportUri: String? = null,
    /** Incluir también las archivadas (en `archivadas/`). */
    val autoExportArchived: Boolean = true,
) {
    val quickList: QuickList? get() = quickListId?.let { QuickList(it, quickListTitle) }
    val colorFill: Boolean get() = if (AspectSkin.colorLocked(appearance)) false else fillNoteColor ?: AspectSkin.colorFill(appearance)
    val cornersSquare: Boolean get() = squareCorners ?: AspectSkin.squareCorners(appearance)
    val syncSignalVisible: Boolean get() = showSyncSignal ?: AspectSkin.syncSignal(appearance)
    val newButtonSquare: Boolean get() = when (newButtonShape) { 1 -> false; 2 -> true; else -> cornersSquare }
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
            appearance = prefs.getInt(APPEARANCE, d.appearance).takeIf { it in AppPalette.DARK..AppPalette.GRID_NOTEBOOK_DARK } ?: d.appearance,
            aspectColors = AspectColorsJson.parse(prefs.getString(ASPECT_COLORS, null)),
            language = prefs.getString(LANGUAGE, d.language).takeIf { tag -> tag == "" || Languages.all.any { it.first == tag } } ?: d.language,
            trashDays = prefs.getInt(TRASH_DAYS, d.trashDays).coerceIn(1, 365),
            reorderOnCheck = prefs.getBoolean(REORDER_ON_CHECK, d.reorderOnCheck),
            // En un móvil táctil las casillas grandes son lo cómodo: una instalación nueva (sin ningún ajuste guardado) las trae activadas. Quien ya
            // tenía la app sin ese ajuste conserva lo que veía (apagado); `save` escribe el valor y, desde entonces, manda lo guardado.
            largeCheckboxes = if (prefs.contains(LARGE_CHECKBOXES)) prefs.getBoolean(LARGE_CHECKBOXES, false) else prefs.all.isEmpty(),
            darkNotes = prefs.getBoolean(DARK_NOTES, d.darkNotes),
            noteThemeId = prefs.getString(NOTE_THEME_ID, d.noteThemeId) ?: d.noteThemeId,
            captureNotification = prefs.getBoolean(CAPTURE_NOTIFICATION, d.captureNotification),
            widgetPreview = prefs.getBoolean(WIDGET_PREVIEW, d.widgetPreview),
            exactReminders = prefs.getBoolean(EXACT_REMINDERS, d.exactReminders),
            uniformNoteColor = prefs.getString(UNIFORM_NOTE_COLOR, null)?.takeIf { NoteDisplayColor.isActive(it) },
            fillNoteColor = nullableBoolean(FILL_NOTE_COLOR),
            squareCorners = nullableBoolean(SQUARE_CORNERS),
            showSyncSignal = nullableBoolean(SHOW_SYNC_SIGNAL),
            newButtonShape = prefs.getInt(NEW_BUTTON_SHAPE, d.newButtonShape).takeIf { it in 0..2 } ?: d.newButtonShape,
            checkUpdates = prefs.getBoolean(CHECK_UPDATES, d.checkUpdates),
            lastUpdateCheck = prefs.getLong(LAST_UPDATE_CHECK, d.lastUpdateCheck),
            dismissedUpdate = prefs.getString(DISMISSED_UPDATE, null),
            knownUpdate = prefs.getString(KNOWN_UPDATE, null),
            listOrder = prefs.getInt(LIST_ORDER, d.listOrder).takeIf { it in 0..3 } ?: d.listOrder,
            colorRule = prefs.getInt(COLOR_RULE, d.colorRule).takeIf { it in 0..3 } ?: d.colorRule,
            fixedNoteColor = prefs.getString(FIXED_NOTE_COLOR, null)?.takeIf { NoteDisplayColor.isActive(it) },
            reminderIncludesTasks = prefs.getBoolean(REMINDER_INCLUDES_TASKS, d.reminderIncludesTasks),
            syncInBackground = prefs.getBoolean(SYNC_IN_BACKGROUND, d.syncInBackground),
            hideDoneTasks = prefs.getBoolean(HIDE_DONE_TASKS, d.hideDoneTasks),
            keyboardOnOpen = KeyboardOnOpen.migrate(prefs.getInt(KEYBOARD_ON_OPEN, d.keyboardOnOpen), prefs.getBoolean(KEYBOARD_MIGRATED, false)),
            hideDoneAfterHours = prefs.getInt(HIDE_DONE_AFTER_HOURS, d.hideDoneAfterHours).coerceIn(1, 24 * 365),
            welcomeSeen = prefs.getBoolean(WELCOME_SEEN, d.welcomeSeen),
            backupNoticeDismissed = prefs.getBoolean(BACKUP_NOTICE_DISMISSED, d.backupNoticeDismissed),
            quickListId = prefs.getString(QUICK_LIST_ID, null),
            quickListTitle = prefs.getString(QUICK_LIST_TITLE, d.quickListTitle) ?: d.quickListTitle,
            simplifiedMode = prefs.getBoolean(SIMPLIFIED_MODE, d.simplifiedMode),
            autoExportEnabled = prefs.getBoolean(AUTO_EXPORT_ENABLED, d.autoExportEnabled),
            autoExportUri = prefs.getString(AUTO_EXPORT_URI, null),
            autoExportArchived = prefs.getBoolean(AUTO_EXPORT_ARCHIVED, d.autoExportArchived),
            sounds = SoundSettings(
                enabled = prefs.getBoolean(SOUNDS_ENABLED, false),
                volume = prefs.getInt(SOUND_VOLUME, 100).coerceIn(0, 100),
                choices = SoundEvent.entries.mapNotNull { e -> if (prefs.contains(SOUND_PREFIX + e.id)) e.id to prefs.getInt(SOUND_PREFIX + e.id, 0) else null }.toMap(),
            ),
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
            .putOrRemove(FILL_NOTE_COLOR, settings.fillNoteColor)
            .putOrRemove(SQUARE_CORNERS, settings.squareCorners)
            .putOrRemove(SHOW_SYNC_SIGNAL, settings.showSyncSignal)
            .putInt(NEW_BUTTON_SHAPE, settings.newButtonShape)
            .putBoolean(CHECK_UPDATES, settings.checkUpdates)
            .putLong(LAST_UPDATE_CHECK, settings.lastUpdateCheck)
            .putOrRemove(DISMISSED_UPDATE, settings.dismissedUpdate)
            .putOrRemove(KNOWN_UPDATE, settings.knownUpdate)
            .putInt(LIST_ORDER, settings.listOrder)
            .putInt(COLOR_RULE, settings.colorRule)
            .putOrRemove(FIXED_NOTE_COLOR, settings.fixedNoteColor)
            .putBoolean(REMINDER_INCLUDES_TASKS, settings.reminderIncludesTasks)
            .putBoolean(SYNC_IN_BACKGROUND, settings.syncInBackground)
            .putBoolean(HIDE_DONE_TASKS, settings.hideDoneTasks)
            .putInt(KEYBOARD_ON_OPEN, settings.keyboardOnOpen)
            .putBoolean(KEYBOARD_MIGRATED, true)
            .putInt(HIDE_DONE_AFTER_HOURS, settings.hideDoneAfterHours)
            .putBoolean(WELCOME_SEEN, settings.welcomeSeen)
            .putBoolean(BACKUP_NOTICE_DISMISSED, settings.backupNoticeDismissed)
            .putOrRemove(QUICK_LIST_ID, settings.quickListId)
            .putString(QUICK_LIST_TITLE, settings.quickListTitle)
            .putBoolean(SIMPLIFIED_MODE, settings.simplifiedMode)
            .putBoolean(AUTO_EXPORT_ENABLED, settings.autoExportEnabled)
            .putOrRemove(AUTO_EXPORT_URI, settings.autoExportUri)
            .putBoolean(AUTO_EXPORT_ARCHIVED, settings.autoExportArchived)
            .putBoolean(SOUNDS_ENABLED, settings.sounds.enabled)
            .putInt(SOUND_VOLUME, settings.sounds.volume)
            .also { editor -> for (e in SoundEvent.entries) settings.sounds.choices[e.id]?.let { editor.putInt(SOUND_PREFIX + e.id, it) } ?: editor.remove(SOUND_PREFIX + e.id) }
            .apply() // apply ya deja el valor visible en este proceso: el `recreate()` del cambio de idioma lo lee al instante.
    }

    private fun android.content.SharedPreferences.Editor.putOrRemove(key: String, value: String?) = if (value == null) remove(key) else putString(key, value)

    private fun android.content.SharedPreferences.Editor.putOrRemove(key: String, value: Boolean?) = if (value == null) remove(key) else putBoolean(key, value)

    private companion object {
        const val FILE = "settings"
        const val WELCOME_SEEN = "welcomeSeen"
        const val BACKUP_NOTICE_DISMISSED = "backupNoticeDismissed"
        const val QUICK_LIST_ID = "quickListId"
        const val QUICK_LIST_TITLE = "quickListTitle"
        const val SIMPLIFIED_MODE = "simplifiedMode"
        const val SOUNDS_ENABLED = "soundsEnabled"
        const val AUTO_EXPORT_ENABLED = "autoExportEnabled"
        const val AUTO_EXPORT_URI = "autoExportUri"
        const val AUTO_EXPORT_ARCHIVED = "autoExportArchived"
        const val SOUND_VOLUME = "soundVolume"
        const val SOUND_PREFIX = "sound_"
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
        const val FILL_NOTE_COLOR = "fillNoteColor"
        const val SQUARE_CORNERS = "squareCorners"
        const val SHOW_SYNC_SIGNAL = "showSyncSignal"
        const val NEW_BUTTON_SHAPE = "newButtonShape"
        const val CHECK_UPDATES = "checkUpdates"
        const val LAST_UPDATE_CHECK = "lastUpdateCheck"
        const val DISMISSED_UPDATE = "dismissedUpdate"
        const val KNOWN_UPDATE = "knownUpdate"
        const val LIST_ORDER = "listOrder"
        const val COLOR_RULE = "colorRule"
        const val FIXED_NOTE_COLOR = "fixedNoteColor"
        const val REMINDER_INCLUDES_TASKS = "reminderIncludesTasks"
        const val SYNC_IN_BACKGROUND = "syncInBackground"
        const val HIDE_DONE_TASKS = "hideDoneTasks"
        const val KEYBOARD_ON_OPEN = "keyboardOnOpen"
        const val KEYBOARD_MIGRATED = "keyboardOnOpenMigrated"
        const val HIDE_DONE_AFTER_HOURS = "hideDoneAfterHours"
    }
}

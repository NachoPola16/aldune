package app.aldune.ui

import android.Manifest
import android.app.Activity
import android.app.StatusBarManager
import android.appwidget.AppWidgetManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.graphics.drawable.Icon
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.annotation.RequiresApi
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.TextButton
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import app.aldune.capture.CaptureNotification
import app.aldune.capture.NewNoteTileService
import app.aldune.widget.DockWidgetReceiver
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.activity.compose.LocalActivity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import android.net.Uri
import app.aldune.reminders.ReminderScheduler
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.Lifecycle
import app.aldune.data.AppSettings
import app.aldune.data.Languages
import app.aldune.text.AppPalette
import app.aldune.text.AspectCatalog
import app.aldune.text.NoteThemes

private val TRASH_OPTIONS = listOf(7, 14, 30, 60, 90)

/** Ajustes (spec §2.6): sincronización, aspecto, idioma y notas. Captura y recordatorios llegan con sus fases. */
@Composable
fun SettingsScreen(viewModel: NotesViewModel, onClose: () -> Unit) {
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val activity = LocalActivity.current
    BackHandler(onBack = onClose)

    // En una pantalla ancha (tableta) los ajustes van en una columna centrada, no estirados a todo el ancho.
    Column(Modifier.fillMaxSize().safeDrawingPadding(), horizontalAlignment = Alignment.CenterHorizontally) {
        Column(Modifier.widthIn(max = 640.dp).fillMaxWidth()) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                IconButton(onClick = onClose) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.back)) }
                Text(stringResource(R.string.settings), style = MaterialTheme.typography.titleLarge)
            }
            Column(Modifier.verticalScroll(rememberScrollState()).padding(bottom = 24.dp)) {
                SyncSection(viewModel)
                CaptureSection(viewModel, settings.captureNotification)
                RemindersSection(viewModel, settings.exactReminders)

                Section(R.string.settings_appearance)
                val appearances = listOf(
                    AppPalette.SYSTEM to R.string.appearance_system, AppPalette.DARK to R.string.appearance_dark,
                    AppPalette.LIGHT to R.string.appearance_light, AppPalette.PASTEL to R.string.appearance_pastel,
                    AppPalette.MIDNIGHT to R.string.appearance_midnight,
                    AppPalette.XP_LIGHT to R.string.appearance_xp_light, AppPalette.XP_DARK to R.string.appearance_xp_dark,
                    AppPalette.TELECOM_LIGHT to R.string.appearance_telecom_light, AppPalette.TELECOM_DARK to R.string.appearance_telecom_dark,
                    AppPalette.BASH to R.string.appearance_bash, AppPalette.PHOSPHOR to R.string.appearance_phosphor,
                )
                // Los aspectos retro proponen un tema de notas; se pregunta, no se impone (como en Windows).
                // rememberSaveable: girar la pantalla no debe cerrar la pregunta sin avisar.
                var suggestedTheme by rememberSaveable { mutableStateOf<String?>(null) }
                for ((mode, label) in appearances) {
                    Choice(stringResource(label), settings.appearance == mode) {
                        // Volver a pulsar el aspecto activo no repite la pregunta.
                        val changed = settings.appearance != mode
                        // Elegir un aspecto vuelve a sus esquinas y su señal (como Windows): lo ajustado a mano no se arrastra al siguiente.
                        viewModel.updateSettings { it.copy(appearance = mode, squareCorners = null, showSyncSignal = null) }
                        if (changed) AspectCatalog.forMode(mode)?.suggestedThemeId?.takeIf { it != settings.noteThemeId }?.let { suggestedTheme = it }
                    }
                }
                AspectSection(settings, onChange = viewModel::updateSettings)
                NoteLookSection(settings, onChange = viewModel::updateSettings)
                suggestedTheme?.let { themeId ->
                    val themeName = themeNameRes(themeId)?.let { stringResource(it) } ?: themeId
                    AlertDialog(
                        onDismissRequest = { suggestedTheme = null },
                        title = { Text(stringResource(R.string.note_theme)) },
                        text = { Text(stringResource(R.string.aspect_suggest_body, themeName)) },
                        confirmButton = {
                            TextButton(onClick = {
                                viewModel.updateSettings { it.copy(noteThemeId = themeId) }
                                suggestedTheme = null
                            }) { Text(stringResource(R.string.aspect_suggest_apply)) }
                        },
                        dismissButton = { TextButton(onClick = { suggestedTheme = null }) { Text(stringResource(R.string.aspect_suggest_keep)) } },
                    )
                }

                Section(R.string.settings_language)
                Choice(stringResource(R.string.language_system), settings.language.isEmpty()) { changeLanguage(viewModel, activity, "") }
                for ((tag, name) in Languages.all) {
                    Choice(name, settings.language == tag) { changeLanguage(viewModel, activity, tag) }
                }

                Section(R.string.settings_notes)
                Text(stringResource(R.string.note_theme), Modifier.padding(horizontal = 16.dp), style = MaterialTheme.typography.labelLarge)
                for (theme in NoteThemes.builtIn) {
                    val name = themeNameRes(theme.id)?.let { stringResource(it) } ?: theme.name
                    Choice(name, settings.noteThemeId == theme.id) { viewModel.updateSettings { it.copy(noteThemeId = theme.id) } }
                }
                NoteBehaviourSettings(settings, onChange = viewModel::updateSettings)
                Text(stringResource(R.string.new_note_tone), Modifier.padding(horizontal = 16.dp), style = MaterialTheme.typography.labelLarge)
                Choice(stringResource(R.string.tone_light), !settings.darkNotes) { viewModel.updateSettings { it.copy(darkNotes = false) } }
                Choice(stringResource(R.string.tone_dark), settings.darkNotes) { viewModel.updateSettings { it.copy(darkNotes = true) } }
                Text(stringResource(R.string.trash_days), Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp), style = MaterialTheme.typography.labelLarge)
                for (days in TRASH_OPTIONS) {
                    Choice(stringResource(R.string.trash_days_value, days), settings.trashDays == days) { viewModel.updateSettings { it.copy(trashDays = days) } }
                }
                HorizontalDivider(Modifier.padding(vertical = 8.dp))
                SwitchRow(stringResource(R.string.reorder_on_check), stringResource(R.string.reorder_on_check_hint), settings.reorderOnCheck) { on ->
                    viewModel.updateSettings { it.copy(reorderOnCheck = on) }
                }
                HorizontalDivider(Modifier.padding(vertical = 8.dp))
                Text(
                    stringResource(R.string.about_version, versionName(activity)), Modifier.padding(16.dp),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

/**
 * Captura rápida (spec de F6 §2.2): la notificación fija y el mosaico. Activar la notificación en Android 13+ pide el permiso; si se deniega
 * (o las notificaciones de la app están bloqueadas) el interruptor se queda apagado y se explica, con un botón a los ajustes del sistema.
 */
@Composable
private fun CaptureSection(viewModel: NotesViewModel, notificationOn: Boolean) {
    val context = LocalContext.current
    var blocked by remember { mutableStateOf(false) }
    fun turnOn() {
        blocked = false
        viewModel.updateSettings { it.copy(captureNotification = true) }
        CaptureNotification.sync(context, true)
    }
    val permission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted && CaptureNotification.canPost(context)) turnOn() else blocked = true
    }

    Section(R.string.capture_section)
    SwitchRow(stringResource(R.string.capture_notification), stringResource(R.string.capture_notification_hint), notificationOn) { on ->
        when {
            !on -> {
                blocked = false
                viewModel.updateSettings { it.copy(captureNotification = false) }
                CaptureNotification.sync(context, false)
            }
            CaptureNotification.canPost(context) -> turnOn()
            // Sin permiso se pregunta; con el permiso pero las notificaciones bloqueadas en el sistema, solo se puede explicar.
            !CaptureNotification.hasPermission(context) -> permission.launch(Manifest.permission.POST_NOTIFICATIONS)
            else -> blocked = true
        }
    }
    if (blocked && !notificationOn) {
        Text(
            stringResource(R.string.capture_notification_denied), Modifier.padding(horizontal = 16.dp),
            style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error,
        )
        TextButton(
            onClick = {
                context.startActivity(
                    Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, context.packageName),
                )
            },
            Modifier.padding(horizontal = 8.dp),
        ) { Text(stringResource(R.string.open_system_settings)) }
    }

    val widgetPreview by viewModel.settings.collectAsStateWithLifecycle()
    SwitchRow(stringResource(R.string.widget_preview), stringResource(R.string.widget_preview_hint), widgetPreview.widgetPreview) { on ->
        viewModel.updateSettings { it.copy(widgetPreview = on) }
    }

    // Poner el widget sin ir al selector del lanzador: el sistema enseña su diálogo y el lanzador lo coloca. Si no lo admite, no se ofrece.
    val widgets = remember { AppWidgetManager.getInstance(context) }
    if (widgets.isRequestPinAppWidgetSupported) {
        Column(
            Modifier.fillMaxWidth().clickable { widgets.requestPinAppWidget(ComponentName(context, DockWidgetReceiver::class.java), null, null) }
                .padding(horizontal = 16.dp, vertical = 10.dp),
        ) {
            Text(stringResource(R.string.add_widget))
            Text(stringResource(R.string.add_widget_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
        TileRow(context)
    } else {
        Text(
            stringResource(R.string.add_tile_manual), Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
            style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/**
 * Recordatorios: «Hora exacta». Sin el permiso de alarmas exactas (API 31+) se manda al usuario a la pantalla del sistema y, al volver, se
 * activa si lo concedió; así el interruptor nunca queda encendido sin que la alarma pueda ser exacta.
 */
@Composable
private fun RemindersSection(viewModel: NotesViewModel, exactOn: Boolean) {
    val context = LocalContext.current
    var waiting by remember { mutableStateOf(false) }
    fun apply(on: Boolean) {
        viewModel.updateSettings { it.copy(exactReminders = on) }
        viewModel.reconcileReminders()
    }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        if (waiting) {
            waiting = false
            if (ReminderScheduler.canScheduleExact(context)) apply(true)
        }
    }

    Section(R.string.reminders_section)
    val withTasks by viewModel.settings.collectAsStateWithLifecycle()
    SwitchRow(stringResource(R.string.reminder_includes_tasks), stringResource(R.string.reminder_includes_tasks_hint), withTasks.reminderIncludesTasks) { on ->
        viewModel.updateSettings { it.copy(reminderIncludesTasks = on) }
    }
    SwitchRow(stringResource(R.string.reminder_exact), stringResource(R.string.reminder_exact_hint), exactOn && ReminderScheduler.canScheduleExact(context)) { on ->
        when {
            !on -> apply(false)
            ReminderScheduler.canScheduleExact(context) -> apply(true)
            else -> {
                waiting = true
                context.startActivity(Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM, Uri.parse("package:${context.packageName}")))
            }
        }
    }
}

/** «Añadir el mosaico»: desde la 13 el sistema enseña su propio diálogo de confirmación; no hace falta ningún permiso. */
@RequiresApi(Build.VERSION_CODES.TIRAMISU)
@Composable
private fun TileRow(context: Context) {
    Column(
        Modifier.fillMaxWidth().clickable {
            context.getSystemService(StatusBarManager::class.java).requestAddTileService(
                ComponentName(context, NewNoteTileService::class.java), context.getString(R.string.new_note),
                Icon.createWithResource(context, R.drawable.ic_new_note), context.mainExecutor,
            ) { }
        }.padding(horizontal = 16.dp, vertical = 10.dp),
    ) {
        Text(stringResource(R.string.add_tile))
        Text(stringResource(R.string.add_tile_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

private fun changeLanguage(viewModel: NotesViewModel, activity: Activity?, tag: String) {
    viewModel.updateSettings { it.copy(language = tag) }
    // El idioma se aplica al crear la actividad (`attachBaseContext`): hay que recrearla para ver el cambio.
    activity?.recreate()
}

private fun versionName(activity: Activity?): String =
    runCatching { activity?.packageManager?.getPackageInfo(activity.packageName, 0)?.versionName }.getOrNull() ?: ""

@Composable
internal fun Section(title: Int) {
    Text(
        stringResource(title), Modifier.padding(start = 16.dp, end = 16.dp, top = 20.dp, bottom = 4.dp),
        style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.primary,
    )
}

@Composable
internal fun Choice(label: String, selected: Boolean, onClick: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().selectable(selected = selected, role = Role.RadioButton, onClick = onClick).padding(horizontal = 16.dp, vertical = 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        RadioButton(selected = selected, onClick = null)
        Text(label, Modifier.padding(start = 16.dp).padding(vertical = 10.dp))
    }
}

@Composable
internal fun SwitchRow(label: String, hint: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(
        Modifier.fillMaxWidth().toggleable(value = checked, role = Role.Switch, onValueChange = onChange).padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(label)
            Text(hint, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Switch(checked = checked, onCheckedChange = null)
    }
}

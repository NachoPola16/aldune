package app.aldune.ui

import android.app.Activity
import androidx.activity.compose.BackHandler
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
import app.aldune.data.AppSettings
import app.aldune.data.Languages
import app.aldune.text.AppPalette

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

                Section(R.string.settings_appearance)
                val appearances = listOf(
                    AppPalette.SYSTEM to R.string.appearance_system, AppPalette.DARK to R.string.appearance_dark,
                    AppPalette.LIGHT to R.string.appearance_light, AppPalette.PASTEL to R.string.appearance_pastel,
                    AppPalette.MIDNIGHT to R.string.appearance_midnight,
                )
                for ((mode, label) in appearances) {
                    Choice(stringResource(label), settings.appearance == mode) { viewModel.updateSettings { it.copy(appearance = mode) } }
                }

                Section(R.string.settings_language)
                Choice(stringResource(R.string.language_system), settings.language.isEmpty()) { changeLanguage(viewModel, activity, "") }
                for ((tag, name) in Languages.all) {
                    Choice(name, settings.language == tag) { changeLanguage(viewModel, activity, tag) }
                }

                Section(R.string.settings_notes)
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
private fun Choice(label: String, selected: Boolean, onClick: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().selectable(selected = selected, role = Role.RadioButton, onClick = onClick).padding(horizontal = 16.dp, vertical = 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        RadioButton(selected = selected, onClick = null)
        Text(label, Modifier.padding(start = 16.dp).padding(vertical = 10.dp))
    }
}

@Composable
private fun SwitchRow(label: String, hint: String, checked: Boolean, onChange: (Boolean) -> Unit) {
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

package app.aldune.ui

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.relocation.BringIntoViewRequester
import androidx.compose.foundation.relocation.bringIntoViewRequester
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.text.FuzzySearch

/** The option the search just picked: once its tab is shown, the row with that name scrolls into view and flashes once. */
internal object SettingsFocus {
    var target by mutableStateOf<String?>(null)
}

/** Marks a row of Settings as findable by its [text]: when the search picks it, it comes into view and is lit for a moment. */
@Composable
internal fun Modifier.settingsTarget(text: String): Modifier {
    val requester = remember { BringIntoViewRequester() }
    val scope = rememberCoroutineScope()
    var flash by remember { mutableStateOf(false) }
    val mine = SettingsFocus.target?.let { FuzzySearch.normalize(it) == FuzzySearch.normalize(text) } == true
    LaunchedEffect(mine) {
        if (!mine) return@LaunchedEffect
        SettingsFocus.target = null
        // On the scope of the row, not of this effect: clearing the target re-runs the effect, which would cut the flash short.
        scope.launch {
            delay(150)
            requester.bringIntoView()
            flash = true
            delay(1300)
            flash = false
        }
    }
    return this.bringIntoViewRequester(requester).background(MaterialTheme.colorScheme.primary.copy(alpha = if (flash) 0.2f else 0f))
}

/** An option of Settings: the tab it lives in (as in `SettingsScreen`), its name and, when it has one, the line that explains it. */
internal class SettingsEntry(val tab: Int, @StringRes val label: Int, @StringRes val hint: Int? = null, @StringRes val group: Int? = null)

/**
 * What the Settings search can find. It is written out (Compose has no list of what a screen contains), so an option that is added to a tab
 * and not here is simply not found by the search; the tab numbers are the ones of `SettingsScreen`: 0 General, 1 Appearance, 2 Notes,
 * 3 Colors, 4 Alerts, 5 Sync.
 */
internal val SETTINGS_INDEX: List<SettingsEntry> = listOf(
    SettingsEntry(0, R.string.settings_language),
    SettingsEntry(0, R.string.settings_tabs_bottom, R.string.settings_tabs_bottom_hint),
    SettingsEntry(0, R.string.compact_settings_tabs, R.string.compact_settings_tabs_hint),
    SettingsEntry(0, R.string.check_updates, R.string.check_updates_hint),
    SettingsEntry(0, R.string.update_check_now),
    SettingsEntry(0, R.string.interface_mode_simplify, R.string.interface_mode_hint_full),
    SettingsEntry(0, R.string.whatsnew_open),

    SettingsEntry(1, R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_system, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_dark, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_light, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_pastel, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_midnight, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_aldune, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_xp_light, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_xp_dark, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_telecom_light, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_telecom_dark, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_bash, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_phosphor, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_travel_notebook_light, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_travel_notebook_dark, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_field_notebook_light, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_field_notebook_dark, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.appearance_grid_notebook_light, group = R.string.settings_appearance), SettingsEntry(1, R.string.appearance_grid_notebook_dark, group = R.string.settings_appearance),
    SettingsEntry(1, R.string.aspect_colors), SettingsEntry(1, R.string.aspect_presets),
    SettingsEntry(1, R.string.square_corners, R.string.square_corners_hint),
    SettingsEntry(1, R.string.stripe_note_color),
    SettingsEntry(1, R.string.sync_signal, R.string.sync_signal_hint),
    SettingsEntry(1, R.string.uniform_color, R.string.uniform_color_hint),
    SettingsEntry(1, R.string.new_button_shape),
    SettingsEntry(1, R.string.show_tag_hash, R.string.show_tag_hash_hint),
    SettingsEntry(1, R.string.group_by_tag, R.string.group_by_tag_hint),

    SettingsEntry(2, R.string.settings_notes),
    SettingsEntry(2, R.string.list_order), SettingsEntry(2, R.string.order_title),
    SettingsEntry(2, R.string.trash_days),
    SettingsEntry(2, R.string.keyboard_on_open),
    SettingsEntry(2, R.string.large_checkboxes, R.string.large_checkboxes_hint),
    SettingsEntry(2, R.string.reorder_on_check, R.string.reorder_on_check_hint),
    SettingsEntry(2, R.string.hide_done_tasks, R.string.hide_done_tasks_hint),
    SettingsEntry(2, R.string.hide_done_after),

    SettingsEntry(3, R.string.note_theme),
    SettingsEntry(3, R.string.new_note_tone),
    SettingsEntry(3, R.string.color_rule),
    SettingsEntry(3, R.string.apply_colors_button),

    SettingsEntry(4, R.string.capture_section),
    SettingsEntry(4, R.string.capture_notification, R.string.capture_notification_hint, R.string.capture_section),
    SettingsEntry(4, R.string.add_tile, R.string.add_tile_hint, R.string.capture_section),
    SettingsEntry(4, R.string.add_widget, R.string.add_widget_hint, R.string.capture_section),
    SettingsEntry(4, R.string.quick_list, R.string.quick_list_hint, R.string.capture_section),
    SettingsEntry(4, R.string.reminders_section),
    SettingsEntry(4, R.string.reminder_exact, R.string.reminder_exact_hint, R.string.reminders_section),
    SettingsEntry(4, R.string.reminder_includes_tasks, R.string.reminder_includes_tasks_hint, R.string.reminders_section),
    SettingsEntry(4, R.string.sounds_section, R.string.sounds_hint),
    SettingsEntry(4, R.string.sounds_enabled, group = R.string.sounds_section),
    SettingsEntry(4, R.string.sound_volume, group = R.string.sounds_section),
    SettingsEntry(4, R.string.sound_event_note_created, group = R.string.sounds_section), SettingsEntry(4, R.string.sound_event_note_opened, group = R.string.sounds_section),
    SettingsEntry(4, R.string.sound_event_note_closed, group = R.string.sounds_section), SettingsEntry(4, R.string.sound_event_note_archived, group = R.string.sounds_section),
    SettingsEntry(4, R.string.sound_event_note_deleted, group = R.string.sounds_section), SettingsEntry(4, R.string.sound_event_task_checked, group = R.string.sounds_section),
    SettingsEntry(4, R.string.sound_event_typing, group = R.string.sounds_section), SettingsEntry(4, R.string.sound_event_text_deleted, group = R.string.sounds_section),
    SettingsEntry(4, R.string.sound_event_sync_done, group = R.string.sounds_section), SettingsEntry(4, R.string.sound_event_sync_failed, group = R.string.sounds_section),

    SettingsEntry(5, R.string.sync_title),
    SettingsEntry(5, R.string.sync_now, group = R.string.sync_title),
    SettingsEntry(5, R.string.sync_transport_server, group = R.string.sync_title), SettingsEntry(5, R.string.sync_transport_webdav, group = R.string.sync_title),
    SettingsEntry(5, R.string.backup_section),
    SettingsEntry(5, R.string.export_all, R.string.export_all_hint, R.string.backup_section),
    SettingsEntry(5, R.string.auto_export, R.string.auto_export_hint, R.string.backup_section),
)

/**
 * The results of the Settings search: the options that answer [query] (forgiving about accents, typos and half-typed words, see
 * [FuzzySearch]), each with the tab it is in. [tabNames] are the labels of the tabs that exist now (the Sync one is missing in the
 * simplified mode); touching a result calls [onPick] with its tab.
 */
@Composable
internal fun SettingsSearchResults(query: String, tabNames: List<Int>, onPick: (tab: Int, title: String) -> Unit) {
    val found = rememberResolved(query, tabNames.size)
    if (found.isEmpty()) {
        Text(
            stringResource(R.string.settings_search_none), Modifier.padding(16.dp),
            style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        return
    }
    // Air between the divider under the tabs and the first result.
    androidx.compose.foundation.layout.Spacer(Modifier.height(14.dp))
    for (entry in found) {
        Column(Modifier.fillMaxWidth().clickable { onPick(entry.tab, entry.title) }.padding(horizontal = 16.dp, vertical = 10.dp)) {
            Text(entry.title, style = MaterialTheme.typography.bodyLarge)
            Text(entry.where ?: stringResource(tabNames[entry.tab]), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

/** [where] is the section the option belongs to, when it has one (the sounds, the reminders...): it is shown under the name and counts in the search. */
private class FoundSetting(val tab: Int, val title: String, val where: String?)

/** The options of the tabs that exist, with their texts in the current language, ranked for [query]. */
@Composable
private fun rememberResolved(query: String, tabCount: Int): List<FoundSetting> {
    val resolved = SETTINGS_INDEX.filter { it.tab < tabCount }.map { entry ->
        FoundSetting(entry.tab, stringResource(entry.label), entry.group?.let { stringResource(it) }) to entry.hint?.let { stringResource(it) }
    }
    return remember(query, resolved) {
        // The name and its section count in full; the explanation a little less, so an option is found by what it says about itself but not
        // above one named so. «sound volume» finds Volume under Sounds.
        resolved.map { (found, hint) ->
            Pair(found, maxOf(FuzzySearch.score(query, found.title + " " + (found.where ?: "")), 0.85 * FuzzySearch.score(query, hint)))
        }
            .filter { it.second >= FuzzySearch.MINIMUM_SCORE }
            .sortedByDescending { it.second }
            .map { it.first }
            .distinctBy { it.tab to it.title }
            .take(14)
    }
}

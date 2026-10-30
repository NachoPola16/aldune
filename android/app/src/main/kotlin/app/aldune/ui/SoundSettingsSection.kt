package app.aldune.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Slider
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.data.AppSettings
import app.aldune.domain.SoundChoice
import app.aldune.domain.SoundEvent
import app.aldune.domain.SoundPlan

/**
 * Los sonidos de papel y pluma (Ajustes → Notas): mismo interruptor, volumen y claves que en Windows. Apagados de fábrica.
 * Los recordatorios no se ofrecen: ya traen el sonido de su notificación.
 */
@Composable
fun SoundSettingsSection(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit, onPreview: (SoundEvent) -> Unit) {
    val sounds = settings.sounds
    SwitchRow(stringResource(R.string.sounds_enabled), stringResource(R.string.sounds_hint), sounds.enabled) { on ->
        onChange { it.copy(sounds = it.sounds.copy(enabled = on)) }
    }
    if (!sounds.enabled) return

    GroupLabel(stringResource(R.string.sound_volume))
    Slider(
        value = sounds.volume.toFloat(), onValueChange = { v -> onChange { it.copy(sounds = it.sounds.copy(volume = v.toInt())) } },
        valueRange = 0f..100f, steps = 9,
        modifier = Modifier.padding(horizontal = 16.dp).semantics { contentDescription = "${sounds.volume} %" },
    )
    OutlinedButton(
        onClick = { onChange { it.copy(sounds = it.sounds.copy(choices = SoundPlan.paperAndPen(it.sounds.choices))) } },
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
    ) { Text(stringResource(R.string.sound_paper_pen)) }

    for (event in EVENTS) SoundEventRow(event, settings, onChange, onPreview)
}

@Composable
private fun SoundEventRow(event: SoundEvent, settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit, onPreview: (SoundEvent) -> Unit) {
    var open by remember { mutableStateOf(false) }
    val current = settings.sounds.choices[event.id]?.let(SoundChoice::fromCode) ?: SoundChoice.NONE
    Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(stringResource(eventName(event)), Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium)
        TextButton(onClick = { open = true }) { Text(stringResource(choiceName(current))) }
        DropdownMenu(expanded = open, onDismissRequest = { open = false }) {
            for (choice in SoundChoice.entries) {
                DropdownMenuItem(
                    text = { Text(stringResource(choiceName(choice))) },
                    onClick = {
                        open = false
                        onChange { it.copy(sounds = it.sounds.copy(choices = it.sounds.choices + (event.id to choice.code))) }
                        // Escuchar lo elegido nada más elegirlo: el ajuste se guarda en el mismo gesto, así que se pide con la elección nueva.
                        if (choice != SoundChoice.NONE) onPreview(event)
                    },
                )
            }
        }
        TextButton(onClick = { onPreview(event) }, enabled = current != SoundChoice.NONE) { Text(stringResource(R.string.sound_test)) }
    }
}

private val EVENTS = listOf(
    SoundEvent.NOTE_OPENED, SoundEvent.NOTE_CLOSED, SoundEvent.NOTE_CREATED, SoundEvent.TASK_CHECKED, SoundEvent.NOTE_DELETED, SoundEvent.NOTE_ARCHIVED,
    SoundEvent.SYNC_DONE, SoundEvent.SYNC_FAILED, SoundEvent.TYPING,
)

private fun eventName(event: SoundEvent): Int = when (event) {
    SoundEvent.NOTE_OPENED -> R.string.sound_event_note_opened
    SoundEvent.NOTE_CLOSED -> R.string.sound_event_note_closed
    SoundEvent.TASK_CHECKED -> R.string.sound_event_task_checked
    SoundEvent.NOTE_DELETED -> R.string.sound_event_note_deleted
    SoundEvent.NOTE_ARCHIVED -> R.string.sound_event_note_archived
    SoundEvent.NOTE_CREATED -> R.string.sound_event_note_created
    SoundEvent.SYNC_DONE -> R.string.sound_event_sync_done
    SoundEvent.SYNC_FAILED -> R.string.sound_event_sync_failed
    else -> R.string.sound_event_typing
}

private fun choiceName(choice: SoundChoice): Int = when (choice) {
    SoundChoice.NONE -> R.string.sound_choice_none
    SoundChoice.PAGE_TURN -> R.string.sound_choice_page_turn
    SoundChoice.PAPER_FOLD -> R.string.sound_choice_paper_fold
    SoundChoice.PEN_TAP -> R.string.sound_choice_pen_tap
    SoundChoice.PAPER_CRUMPLE -> R.string.sound_choice_paper_crumple
    SoundChoice.PEN_SCRATCH -> R.string.sound_choice_pen_scratch
}

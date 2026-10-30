package app.aldune.ui

import android.widget.Toast
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.audio.UiSounds
import app.aldune.data.AppSettings
import app.aldune.domain.SoundChoice
import app.aldune.domain.SoundEvent
import app.aldune.domain.SoundPlan

/**
 * Los sonidos de papel y pluma (Ajustes → Notas): mismo interruptor, volumen y claves que en Windows. Apagados de fábrica.
 * Los recordatorios no se ofrecen: ya traen el sonido de su notificación.
 */
@Composable
fun SoundSettingsSection(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit, onPreview: (SoundEvent) -> UiSounds.Silence?) {
    val sounds = settings.sounds
    SwitchRow(stringResource(R.string.sounds_enabled), stringResource(R.string.sounds_hint), sounds.enabled) { on ->
        // La primera vez, ya con los sonidos sugeridos: encender el interruptor sin elegir nada no sonaba a nada.
        onChange { s -> s.copy(sounds = s.sounds.copy(enabled = on, choices = (if (on) SoundPlan.paperAndPenIfUnset(s.sounds.choices) else null) ?: s.sounds.choices)) }
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
private fun SoundEventRow(event: SoundEvent, settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit, onPreview: (SoundEvent) -> UiSounds.Silence?) {
    var open by remember { mutableStateOf(false) }
    val context = LocalContext.current
    // Probar nunca se queda mudo sin decir por qué: si el teléfono lo silencia, se avisa.
    fun preview() {
        when (onPreview(event)) {
            UiSounds.Silence.RINGER -> Toast.makeText(context, R.string.sound_silenced_ringer, Toast.LENGTH_LONG).show()
            UiSounds.Silence.DO_NOT_DISTURB -> Toast.makeText(context, R.string.sound_silenced_dnd, Toast.LENGTH_LONG).show()
            null -> Unit
        }
    }
    val current = settings.sounds.choices[event.id]?.let(SoundChoice::fromCode) ?: SoundChoice.NONE
    Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(stringResource(eventName(event)), Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium)
        // Un diálogo con la lista y no un menú desplegable: el menú salía lejos del botón (se coloca solo, según el sitio libre en
        // pantalla), y una lista de seis sonidos con «Probar» al elegir cabe mejor en un diálogo, también con TalkBack.
        TextButton(onClick = { open = true }) { Text(stringResource(choiceName(current))) }
        if (open) {
            AlertDialog(
                onDismissRequest = { open = false },
                title = { Text(stringResource(eventName(event)), Modifier.semantics { heading() }) },
                text = {
                    Column(Modifier.verticalScroll(rememberScrollState())) {
                        for (choice in SoundChoice.entries) {
                            Choice(stringResource(choiceName(choice)), choice == current) {
                                open = false
                                onChange { it.copy(sounds = it.sounds.copy(choices = it.sounds.choices + (event.id to choice.code))) }
                                // Escuchar lo elegido nada más elegirlo: el ajuste se guarda en el mismo gesto, así que se pide con la elección nueva.
                                if (choice != SoundChoice.NONE) preview()
                            }
                        }
                    }
                },
                confirmButton = { TextButton(onClick = { open = false }) { Text(stringResource(R.string.cancel)) } },
            )
        }
        TextButton(onClick = { preview() }, enabled = current != SoundChoice.NONE) { Text(stringResource(R.string.sound_test)) }
    }
}

private val EVENTS = listOf(
    SoundEvent.NOTE_OPENED, SoundEvent.NOTE_CLOSED, SoundEvent.NOTE_CREATED, SoundEvent.TASK_CHECKED, SoundEvent.NOTE_DELETED, SoundEvent.NOTE_ARCHIVED,
    SoundEvent.SYNC_DONE, SoundEvent.SYNC_FAILED, SoundEvent.TYPING, SoundEvent.TEXT_DELETED,
)

private fun eventName(event: SoundEvent): Int = when (event) {
    SoundEvent.NOTE_OPENED -> R.string.sound_event_note_opened
    SoundEvent.NOTE_CLOSED -> R.string.sound_event_note_closed
    SoundEvent.TASK_CHECKED -> R.string.sound_event_task_checked
    SoundEvent.NOTE_DELETED -> R.string.sound_event_note_deleted
    SoundEvent.NOTE_ARCHIVED -> R.string.sound_event_note_archived
    SoundEvent.NOTE_CREATED -> R.string.sound_event_note_created
    SoundEvent.TEXT_DELETED -> R.string.sound_event_text_deleted
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
    SoundChoice.ERASER -> R.string.sound_choice_eraser
}

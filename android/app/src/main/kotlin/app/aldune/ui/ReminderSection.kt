package app.aldune.ui

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.text.format.DateFormat
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.material3.rememberTimePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import app.aldune.R
import app.aldune.domain.ReminderLabel
import app.aldune.domain.ReminderPresets
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle

/**
 * «Hoy 20:00», «Mañana 9:00», «12 may, 18:30» o «Vencido», con el idioma de la app (el de [LocalConfiguration], ya envuelto por
 * `Languages.wrap`), no el del sistema. La hora sigue el ajuste de 24 h del dispositivo.
 */
@Composable
fun reminderText(label: ReminderLabel): String {
    val context = LocalContext.current
    val locale = LocalConfiguration.current.locales[0]
    val timeFormat = if (DateFormat.is24HourFormat(context)) "HH:mm" else "h:mm a"
    val time = DateTimeFormatter.ofPattern(timeFormat, locale)
    return when (label) {
        is ReminderLabel.Today -> stringResource(R.string.reminder_today, label.time.format(time))
        is ReminderLabel.Tomorrow -> stringResource(R.string.reminder_tomorrow, label.time.format(time))
        is ReminderLabel.Date -> stringResource(
            R.string.reminder_date_time,
            label.date.format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale)),
            label.time.format(time),
        )
        ReminderLabel.Overdue -> stringResource(R.string.reminder_overdue)
    }
}

/** Atajos, selector de fecha y hora y «Quitar» del recordatorio de una nota, dentro de su menú «⋯». */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun ReminderSection(noteId: String, viewModel: NotesViewModel) {
    val context = LocalContext.current
    var due by remember(noteId) { mutableStateOf<Instant?>(null) }
    LaunchedEffect(noteId) { due = viewModel.reminderFor(noteId) }
    var picking by remember { mutableStateOf(false) }

    val notifications = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {}

    // Sin el permiso (API 33+) el aviso no se vería: se pide al poner el primero, no al instalar la app.
    fun set(at: ZonedDateTime) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            notifications.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
        val instant = at.toInstant()
        due = instant
        viewModel.setReminder(noteId, instant)
    }

    Column(Modifier.padding(top = 8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(stringResource(R.string.reminder_title), style = MaterialTheme.typography.labelLarge)
        due?.let {
            Text(reminderText(ReminderLabel.of(it, Instant.now(), ZoneId.systemDefault())), style = MaterialTheme.typography.bodyLarge)
        }
        FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            AssistChip(onClick = { set(ReminderPresets.inOneHour(ZonedDateTime.now())) }, label = { Text(stringResource(R.string.reminder_in_one_hour)) })
            AssistChip(onClick = { set(ReminderPresets.tonight(ZonedDateTime.now())) }, label = { Text(stringResource(R.string.reminder_tonight)) })
            AssistChip(onClick = { set(ReminderPresets.tomorrowMorning(ZonedDateTime.now())) }, label = { Text(stringResource(R.string.reminder_snooze_morning)) })
            AssistChip(onClick = { picking = true }, label = { Text(stringResource(R.string.reminder_pick)) })
        }
        if (due != null) {
            TextButton(onClick = {
                due = null
                viewModel.clearReminder(noteId)
            }) { Text(stringResource(R.string.reminder_remove)) }
        }
        Text(stringResource(R.string.reminder_device_only), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }

    if (picking) DateTimePickerDialog(onDismiss = { picking = false }) { at ->
        picking = false
        set(at)
    }
}

/** Primero el día y luego la hora, en dos diálogos seguidos; no deja aceptar un instante que ya pasó. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DateTimePickerDialog(onDismiss: () -> Unit, onPicked: (ZonedDateTime) -> Unit) {
    val context = LocalContext.current
    val now = remember { ZonedDateTime.now() }
    var date by remember { mutableStateOf<LocalDate?>(null) }

    val day = date
    if (day == null) {
        val state = rememberDatePickerState(initialSelectedDateMillis = now.toLocalDate().atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli())
        DatePickerDialog(
            onDismissRequest = onDismiss,
            confirmButton = {
                TextButton(
                    onClick = { state.selectedDateMillis?.let { date = Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate() } },
                    enabled = state.selectedDateMillis != null,
                ) { Text(stringResource(R.string.reminder_ok)) }
            },
            dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.cancel)) } },
        ) { DatePicker(state) }
    } else {
        val next = now.plusHours(1)
        val time = rememberTimePickerState(next.hour, 0, DateFormat.is24HourFormat(context))
        val chosen = day.atTime(time.hour, time.minute).atZone(now.zone)
        AlertDialog(
            onDismissRequest = onDismiss,
            text = { TimePicker(time) },
            confirmButton = {
                TextButton(onClick = { onPicked(chosen) }, enabled = chosen.isAfter(ZonedDateTime.now())) { Text(stringResource(R.string.reminder_ok)) }
            },
            dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.cancel)) } },
        )
    }
}

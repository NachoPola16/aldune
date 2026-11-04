package app.aldune.ui

import android.text.format.DateFormat
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import app.aldune.domain.backup.BackupFailure
import app.aldune.domain.backup.RestoreMode
import app.aldune.ui.NotesViewModel.RestorePhase
import java.time.LocalDate
import java.util.Date

/**
 * «Back up now» and «Restore from a backup…»: the full backup of the notes as one .zip (see `BackupArchive`), through the system document
 * picker (no storage permission). The dialog state lives in the ViewModel, so a rotation keeps the preview and a started restore finishes.
 */
@Composable
fun FullBackupSection(viewModel: NotesViewModel) {
    val outcome by viewModel.backupOutcome.collectAsStateWithLifecycle()
    val saver = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("application/zip")) { uri ->
        if (uri != null) viewModel.backupTo(uri)
    }
    val opener = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) viewModel.restorePick(uri)
    }
    Text(
        stringResource(R.string.backup_now_hint), Modifier.padding(horizontal = 16.dp),
        style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    OutlinedButton(
        onClick = { saver.launch("aldune-backup-${LocalDate.now()}.zip") },
        Modifier.padding(horizontal = 16.dp, vertical = 8.dp).heightIn(min = 48.dp),
    ) { Text(stringResource(R.string.backup_now)) }
    val done = outcome
    if (done != null) {
        val message = when {
            done.notes == null -> stringResource(R.string.backup_failed)
            done.protectedNotes > 0 -> stringResource(R.string.backup_done_protected, done.notes, done.protectedNotes)
            else -> stringResource(R.string.backup_done, done.notes)
        }
        // Announced on its own: the picker closes and TalkBack users get no other signal.
        Text(message, Modifier.padding(horizontal = 16.dp).semantics { liveRegion = LiveRegionMode.Polite }, style = MaterialTheme.typography.bodyMedium)
    }
    Text(
        stringResource(R.string.restore_hint), Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp),
        style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    OutlinedButton(
        // Providers disagree on the MIME type of a .zip, so every type is allowed and a wrong file is reported by the check.
        onClick = { opener.launch(arrayOf("*/*")) },
        Modifier.padding(horizontal = 16.dp, vertical = 8.dp).heightIn(min = 48.dp),
    ) { Text(stringResource(R.string.restore_from_backup)) }
    RestoreDialog(viewModel)
}

@Composable
private fun RestoreDialog(viewModel: NotesViewModel) {
    val ui by viewModel.restoreUi.collectAsStateWithLifecycle()
    val state = ui ?: return
    val busy = state.phase == RestorePhase.READING || state.phase == RestorePhase.RESTORING
    val preview = state.preview

    if (state.phase == RestorePhase.CONFIRM_REPLACE) {
        AlertDialog(
            onDismissRequest = { viewModel.restoreBackToPreview() },
            title = { Text(stringResource(R.string.restore_confirm_title), Modifier.semantics { heading() }) },
            text = { Text(stringResource(R.string.restore_confirm_body, preview?.wouldDelete ?: 0)) },
            confirmButton = { TextButton({ viewModel.restoreConfirmReplace() }, Modifier.heightIn(min = 48.dp)) { Text(stringResource(R.string.restore_confirm_run)) } },
            dismissButton = { TextButton({ viewModel.restoreBackToPreview() }, Modifier.heightIn(min = 48.dp)) { Text(stringResource(R.string.restore_cancel)) } },
        )
        return
    }

    AlertDialog(
        // While reading or writing the dialog cannot be dismissed: closing would leave the user unsure what was restored.
        onDismissRequest = { if (!busy) viewModel.closeRestore() },
        title = { Text(stringResource(R.string.restore_title), Modifier.semantics { heading() }) },
        text = {
            // Progress, result and failure are announced; the preview is not a live region, or switching the mode would make TalkBack read all the counts again.
            val announce = state.phase != RestorePhase.PREVIEW && state.phase != RestorePhase.CONFIRM_REPLACE
            Column(
                Modifier.verticalScroll(rememberScrollState()).semantics { if (announce) liveRegion = LiveRegionMode.Polite },
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                when (state.phase) {
                    RestorePhase.READING -> {
                        Text(stringResource(R.string.restore_reading))
                        LinearProgressIndicator(Modifier.fillMaxWidth())
                    }
                    RestorePhase.RESTORING -> {
                        Text(stringResource(R.string.restore_running))
                        LinearProgressIndicator(Modifier.fillMaxWidth())
                    }
                    RestorePhase.FAILED -> Text(
                        stringResource(failureText(state.failure, state.safetyFailed)), color = MaterialTheme.colorScheme.error,
                    )
                    RestorePhase.PREVIEW, RestorePhase.CONFIRM_REPLACE -> if (preview != null) {
                        val context = androidx.compose.ui.platform.LocalContext.current
                        Text(stringResource(R.string.restore_contents, preview.inBackup), style = MaterialTheme.typography.titleSmall)
                        state.manifest?.createdAt?.let { at ->
                            Muted(stringResource(R.string.restore_made, DateFormat.getMediumDateFormat(context).format(Date.from(at)), state.manifest.appVersion))
                        }
                        Text(stringResource(R.string.restore_counts, preview.missing, preview.alreadyHere, preview.archivedInBackup, preview.trashedInBackup, preview.reminders))
                        if (preview.protectedInBackup > 0) Text(stringResource(R.string.restore_protected, preview.protectedInBackup))
                        if (state.skippedNotes > 0) Text(stringResource(R.string.restore_skipped, state.skippedNotes))
                        Column(Modifier.selectableGroup()) {
                            ModeChoice(R.string.restore_mode_add, stringResource(R.string.restore_mode_add_hint), RestoreMode.ADD_MISSING, state.mode, viewModel)
                            ModeChoice(R.string.restore_mode_replace, stringResource(R.string.restore_mode_replace_hint, preview.wouldDelete), RestoreMode.REPLACE_ALL, state.mode, viewModel)
                        }
                    }
                    RestorePhase.DONE -> {
                        val r = state.result
                        if (r != null) {
                            Text(
                                if (state.mode == RestoreMode.REPLACE_ALL) stringResource(R.string.restore_done_replace, r.added, r.replaced, r.deleted, r.kept)
                                else stringResource(R.string.restore_done_add, r.added, r.kept),
                                style = MaterialTheme.typography.bodyLarge,
                            )
                        }
                        state.safetyBackup?.let { Muted(stringResource(R.string.restore_safety_saved, "backups/$it")) }
                    }
                }
            }
        },
        confirmButton = {
            if (state.phase == RestorePhase.PREVIEW) {
                TextButton({ viewModel.restoreRun() }, Modifier.heightIn(min = 48.dp)) { Text(stringResource(R.string.restore_run)) }
            } else if (state.phase == RestorePhase.DONE || state.phase == RestorePhase.FAILED) {
                TextButton({ viewModel.closeRestore() }, Modifier.heightIn(min = 48.dp)) { Text(stringResource(R.string.restore_close)) }
            }
        },
        dismissButton = {
            if (state.phase == RestorePhase.PREVIEW) {
                TextButton({ viewModel.closeRestore() }, Modifier.heightIn(min = 48.dp)) { Text(stringResource(R.string.restore_cancel)) }
            }
        },
    )
}

@Composable
private fun ModeChoice(label: Int, hint: String, mode: RestoreMode, selected: RestoreMode, viewModel: NotesViewModel) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 48.dp).selectable(selected = mode == selected, role = Role.RadioButton) { viewModel.restoreMode(mode) },
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // The whole row is the target and speaks for the radio, so the button itself gets no click of its own.
        RadioButton(selected = mode == selected, onClick = null)
        Column(Modifier.padding(start = 12.dp)) {
            Text(stringResource(label), style = MaterialTheme.typography.bodyLarge)
            Muted(hint)
        }
    }
}

@Composable
private fun Muted(text: String) = Text(text, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)

private fun failureText(failure: BackupFailure?, safetyFailed: Boolean): Int = when {
    safetyFailed -> R.string.restore_safety_failed
    failure == BackupFailure.NOT_A_BACKUP -> R.string.restore_fail_not_backup
    failure == BackupFailure.TOO_NEW -> R.string.restore_fail_too_new
    failure == BackupFailure.CORRUPT -> R.string.restore_fail_corrupt
    failure == BackupFailure.TOO_LARGE -> R.string.restore_fail_too_large
    else -> R.string.restore_fail_io
}

package app.aldune.ui

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
import app.aldune.domain.importing.ImportFailure
import app.aldune.domain.importing.ImportLimits
import app.aldune.domain.importing.ImportSkipReason
import app.aldune.domain.importing.ImportSourceKind
import app.aldune.ui.NotesViewModel.ImportPhase

/**
 * Import notes from Google Keep, Evernote or Markdown/text files chosen with the system file picker. The state lives in the ViewModel (see
 * [NotesViewModel.ImportUi]), so the dialog comes back as it was after a rotation. Nothing is written until «Import» is pressed.
 */
@Composable
fun ImportDialog(viewModel: NotesViewModel) {
    val ui by viewModel.importUi.collectAsStateWithLifecycle()
    val state = ui ?: return
    // OpenMultipleDocuments goes through the system picker (no storage permission); every type is allowed because providers disagree on the
    // MIME type of .enex and .md, and a wrong file is reported by the preview instead.
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenMultipleDocuments()) { uris -> viewModel.importPick(uris) }
    val busy = state.phase == ImportPhase.READING || state.phase == ImportPhase.IMPORTING
    val canImport = state.phase == ImportPhase.PREVIEW && (state.plan?.toCreate?.isNotEmpty() == true)

    AlertDialog(
        // While reading or writing the dialog cannot be dismissed: closing would leave the user unsure whether anything was imported.
        onDismissRequest = { if (!busy) viewModel.closeImport() },
        title = { Text(stringResource(R.string.import_title), Modifier.semantics { heading() }) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                if (state.phase != ImportPhase.DONE) {
                    Muted(stringResource(R.string.import_hint))
                    Text(stringResource(R.string.import_from), style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.primary)
                    Column(Modifier.selectableGroup()) {
                        SourceChoice(R.string.import_source_keep, ImportSourceKind.KEEP, state.kind, !busy, viewModel)
                        SourceChoice(R.string.import_source_evernote, ImportSourceKind.EVERNOTE, state.kind, !busy, viewModel)
                        SourceChoice(R.string.import_source_text, ImportSourceKind.TEXT, state.kind, !busy, viewModel)
                    }
                    Muted(stringResource(helpFor(state.kind)))
                    OutlinedButton({ picker.launch(arrayOf("*/*")) }, Modifier.heightIn(min = 48.dp), enabled = !busy) {
                        Text(stringResource(R.string.import_choose_files))
                    }
                    Text(
                        if (state.files == 0) stringResource(R.string.import_nothing_chosen) else stringResource(R.string.import_files_chosen, state.files),
                        style = MaterialTheme.typography.bodyMedium,
                    )
                }
                when (state.phase) {
                    ImportPhase.READING -> {
                        Text(stringResource(R.string.import_reading), Modifier.semantics { liveRegion = LiveRegionMode.Polite })
                        LinearProgressIndicator(Modifier.fillMaxWidth())
                    }
                    ImportPhase.PREVIEW -> PreviewBlock(state)
                    ImportPhase.IMPORTING -> {
                        val total = state.plan?.toCreate?.size ?: 0
                        Text(stringResource(R.string.import_importing, state.done, total), Modifier.semantics { liveRegion = LiveRegionMode.Polite })
                        LinearProgressIndicator(progress = { if (total == 0) 0f else state.done.toFloat() / total }, modifier = Modifier.fillMaxWidth())
                    }
                    ImportPhase.DONE -> {
                        val result = state.result
                        val message = when {
                            result == null -> ""
                            result.failed == 0 -> stringResource(R.string.import_done, result.created)
                            else -> stringResource(R.string.import_done_failed, result.created, result.failed)
                        }
                        // Announced on its own: the dialog content changes under a user who may be navigating with TalkBack.
                        Text(message, Modifier.semantics { liveRegion = LiveRegionMode.Polite }, style = MaterialTheme.typography.bodyLarge)
                    }
                    ImportPhase.CHOOSE -> {}
                }
            }
        },
        confirmButton = {
            if (state.phase == ImportPhase.DONE) TextButton({ viewModel.closeImport() }, Modifier.heightIn(min = 48.dp)) { Text(stringResource(R.string.import_close)) }
            else TextButton({ viewModel.importRun() }, Modifier.heightIn(min = 48.dp), enabled = canImport) { Text(stringResource(R.string.import_run)) }
        },
        dismissButton = {
            if (state.phase != ImportPhase.DONE) TextButton({ viewModel.closeImport() }, Modifier.heightIn(min = 48.dp), enabled = !busy) {
                Text(stringResource(R.string.import_cancel))
            }
        },
    )
}

@Composable
private fun PreviewBlock(state: NotesViewModel.ImportUi) {
    val preview = state.preview ?: return
    val plan = state.plan
    Column(Modifier.semantics { liveRegion = LiveRegionMode.Polite }, verticalArrangement = Arrangement.spacedBy(6.dp)) {
        if (preview.failure != ImportFailure.NONE) {
            Text(stringResource(failureText(preview.failure)), color = MaterialTheme.colorScheme.error)
            return@Column
        }
        val add = plan?.toCreate?.size ?: 0
        Text(
            if (add > 0) stringResource(R.string.import_will_add, add) else stringResource(R.string.import_nothing_to_add),
            style = MaterialTheme.typography.titleSmall,
        )
        if ((plan?.duplicates ?: 0) > 0) Text(stringResource(R.string.import_already_there, plan!!.duplicates))
        val counts = preview.skipped.groupingBy { it.reason }.eachCount()
        counts[ImportSkipReason.EMPTY]?.let { Text(stringResource(R.string.import_skipped_empty, it)) }
        counts[ImportSkipReason.TOO_LARGE]?.let { Text(stringResource(R.string.import_skipped_too_large, it)) }
        counts[ImportSkipReason.NOT_TEXT]?.let { Text(stringResource(R.string.import_skipped_not_text, it)) }
        counts[ImportSkipReason.UNREADABLE]?.let { Text(stringResource(R.string.import_skipped_unreadable, it)) }
        counts[ImportSkipReason.LIMIT_REACHED]?.let { Text(stringResource(R.string.import_skipped_limit, it, ImportLimits.MAX_NOTES)) }
        if (preview.attachmentsIgnored > 0) Text(stringResource(R.string.import_attachments, preview.attachmentsIgnored, preview.notesWithAttachments))
        if (add > 0) Muted(stringResource(R.string.import_colours_note))
    }
}

@Composable
private fun SourceChoice(label: Int, kind: ImportSourceKind, selected: ImportSourceKind, enabled: Boolean, viewModel: NotesViewModel) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 48.dp).selectable(selected = kind == selected, enabled = enabled, role = Role.RadioButton) { viewModel.importKind(kind) },
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // The whole row is the target and speaks for the radio, so the button itself gets no click of its own.
        RadioButton(selected = kind == selected, onClick = null, enabled = enabled)
        Text(stringResource(label), Modifier.padding(start = 12.dp), style = MaterialTheme.typography.bodyLarge)
    }
}

@Composable
private fun Muted(text: String) =
    Text(text, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)

private fun helpFor(kind: ImportSourceKind) = when (kind) {
    ImportSourceKind.KEEP -> R.string.import_help_keep
    ImportSourceKind.EVERNOTE -> R.string.import_help_evernote
    ImportSourceKind.TEXT -> R.string.import_help_text
}

private fun failureText(failure: ImportFailure) = when (failure) {
    ImportFailure.NOT_FOUND -> R.string.import_fail_not_found
    ImportFailure.TOO_LARGE -> R.string.import_fail_too_large
    ImportFailure.WRONG_FORMAT -> R.string.import_fail_wrong_format
    ImportFailure.NOTHING_FOUND -> R.string.import_fail_nothing
    else -> R.string.import_fail_unreadable
}

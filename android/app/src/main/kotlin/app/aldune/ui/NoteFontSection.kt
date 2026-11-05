package app.aldune.ui

import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.collectAsState
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.text.FontFileInspector
import app.aldune.text.FontFileProblem
import app.aldune.text.NoteFonts
import java.io.File
import java.util.UUID

/**
 * «Note font» in Settings > Notes: system, serif, easy to read (Atkinson Hyperlegible, bundled), monospace, and one imported .ttf/.otf. Only the
 * note text changes, never the app's own text. The choice and the file stay on this device: they are not synced, backed up or exported.
 * The imported file is validated (extension, 10 MB, a real font header, a family name) and copied to the app's private `fonts` folder; the
 * system loader is the only thing that ever reads it. Spec: docs/specs/2026-11-04-note-font-choice-design.md.
 */
@Composable
fun NoteFontSection(viewModel: NotesViewModel) {
    val context = LocalContext.current
    val settings by viewModel.settings.collectAsState()
    val choice = NoteFonts.normalize(settings.noteFont)
    val hasImported = importedFontFile(context, settings) != null

    fun reject(problem: FontFileProblem) = Toast.makeText(
        context,
        when (problem) {
            FontFileProblem.EXTENSION -> R.string.note_font_rejected_extension
            FontFileProblem.TOO_LARGE -> R.string.note_font_rejected_too_large
            else -> R.string.note_font_rejected_invalid
        },
        Toast.LENGTH_LONG,
    ).show()

    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri == null) return@rememberLauncherForActivityResult
        val resolver = context.contentResolver
        // The display name carries the extension the picker's URI may hide; the size is checked while reading so a huge file is never held in memory.
        val name = runCatching {
            resolver.query(uri, arrayOf(android.provider.OpenableColumns.DISPLAY_NAME), null, null, null)?.use { if (it.moveToFirst()) it.getString(0) else null }
        }.getOrNull() ?: uri.lastPathSegment.orEmpty()
        val data = runCatching {
            resolver.openInputStream(uri)?.use { input -> input.readNBytes(FontFileInspector.MAX_BYTES + 1) }
        }.getOrNull()
        if (data == null) { reject(FontFileProblem.NOT_A_FONT); return@rememberLauncherForActivityResult }
        val info = FontFileInspector.inspect(data, name)
        if (!info.isValid) { reject(info.problem); return@rememberLauncherForActivityResult }
        // One imported font at a time. A fresh file name each time, so the loader never keeps a stale copy; the old file goes once the new one is in place.
        val extension = if (name.lowercase().endsWith(".otf")) ".otf" else ".ttf"
        val fileName = UUID.randomUUID().toString().replace("-", "") + extension
        val written = runCatching {
            noteFontsDir(context).apply { mkdirs() }
            File(noteFontsDir(context), fileName).writeBytes(data)
        }.isSuccess
        if (!written) { reject(FontFileProblem.NOT_A_FONT); return@rememberLauncherForActivityResult }
        val previous = settings.noteFontFile
        viewModel.updateSettings { it.copy(noteFont = NoteFonts.IMPORTED, noteFontName = info.family, noteFontFile = fileName) }
        previous?.takeIf { it == File(it).name && it != fileName }?.let { File(noteFontsDir(context), it).delete() }
    }

    Column {
        GroupLabel(stringResource(R.string.note_font))
        Choice(stringResource(R.string.note_font_system), choice == NoteFonts.SYSTEM) { viewModel.updateSettings { it.copy(noteFont = NoteFonts.SYSTEM) } }
        Choice(stringResource(R.string.note_font_serif), choice == NoteFonts.SERIF) { viewModel.updateSettings { it.copy(noteFont = NoteFonts.SERIF) } }
        Choice(stringResource(R.string.note_font_easy), choice == NoteFonts.EASY_TO_READ) { viewModel.updateSettings { it.copy(noteFont = NoteFonts.EASY_TO_READ) } }
        Choice(stringResource(R.string.note_font_mono), choice == NoteFonts.MONOSPACE) { viewModel.updateSettings { it.copy(noteFont = NoteFonts.MONOSPACE) } }
        if (hasImported) {
            Choice(stringResource(R.string.note_font_imported, settings.noteFontName.orEmpty()), choice == NoteFonts.IMPORTED) {
                viewModel.updateSettings { it.copy(noteFont = NoteFonts.IMPORTED) }
            }
        }
        Row(Modifier.fillMaxWidth().padding(start = 16.dp, end = 16.dp, top = 4.dp)) {
            OutlinedButton(onClick = { picker.launch(arrayOf("font/ttf", "font/otf", "font/sfnt", "application/x-font-ttf", "application/x-font-otf", "application/octet-stream")) }) {
                Text(stringResource(R.string.note_font_import))
            }
            if (hasImported) {
                OutlinedButton(
                    onClick = {
                        val file = settings.noteFontFile
                        viewModel.updateSettings { it.copy(noteFont = if (it.noteFont == NoteFonts.IMPORTED) NoteFonts.SYSTEM else it.noteFont, noteFontName = null, noteFontFile = null) }
                        file?.takeIf { it == File(it).name }?.let { File(noteFontsDir(context), it).delete() }
                    },
                    Modifier.padding(start = 8.dp),
                ) { Text(stringResource(R.string.note_font_remove)) }
            }
        }
        Text(
            stringResource(R.string.note_font_hint), Modifier.padding(start = 16.dp, end = 16.dp, top = 6.dp, bottom = 8.dp),
            style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

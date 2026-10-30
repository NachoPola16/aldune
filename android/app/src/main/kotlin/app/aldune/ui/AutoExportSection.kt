package app.aldune.ui

import android.content.Intent
import android.net.Uri
import android.provider.DocumentsContract
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import app.aldune.export.AutoExportStatus

/**
 * Copia automática de las notas en una carpeta (fase 4), igual que en Windows: apagada de fábrica, un `.md` por nota, sin borrar nunca, las
 * protegidas fuera. La carpeta se elige con el selector del sistema y se guarda el permiso persistente; si el sistema lo quita o la carpeta
 * desaparece, el trabajador apaga la copia y aquí (y en la lista) se explica una vez.
 */
@Composable
fun AutoExportSection(viewModel: NotesViewModel) {
    val context = LocalContext.current
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val lost by AutoExportStatus.lost.collectAsState()

    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocumentTree()) { uri ->
        if (uri == null) return@rememberLauncherForActivityResult   // cancelado: queda como estaba (apagada si no había carpeta)
        context.contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
        // Se suelta el permiso de la carpeta anterior: no hay que dejar acceso a un sitio que ya no se usa.
        settings.autoExportUri?.takeIf { it != uri.toString() }?.let { old ->
            runCatching { context.contentResolver.releasePersistableUriPermission(Uri.parse(old), Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION) }
        }
        AutoExportStatus.setLost(context, false)
        viewModel.updateSettings { it.copy(autoExportUri = uri.toString(), autoExportEnabled = true) }
    }

    SwitchRow(stringResource(R.string.auto_export), stringResource(R.string.auto_export_hint), settings.autoExportEnabled && !lost) { on ->
        when {
            !on -> viewModel.updateSettings { it.copy(autoExportEnabled = false) }
            // Sin carpeta (o con la carpeta perdida) se pide ahí mismo; si se cancela, sigue apagada.
            settings.autoExportUri == null || lost -> picker.launch(null)
            else -> viewModel.updateSettings { it.copy(autoExportEnabled = true) }
        }
    }
    if (lost) {
        Text(
            stringResource(R.string.auto_export_lost), Modifier.padding(horizontal = 16.dp),
            style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error,
        )
    }
    if (settings.autoExportEnabled && !lost) {
        Text(
            stringResource(R.string.auto_export_folder, folderLabel(settings.autoExportUri)), Modifier.padding(horizontal = 16.dp),
            style = MaterialTheme.typography.bodyMedium,
        )
        OutlinedButton(onClick = { picker.launch(null) }, Modifier.padding(horizontal = 16.dp, vertical = 4.dp)) { Text(stringResource(R.string.auto_export_choose)) }
        SwitchRow(stringResource(R.string.auto_export_archived), "", settings.autoExportArchived) { on ->
            viewModel.updateSettings { it.copy(autoExportArchived = on) }
        }
    }
}

/** El nombre de la carpeta como la ve el usuario (`Documents/Aldune`), sacado del identificador del árbol; sin él, el `Uri` tal cual. */
private fun folderLabel(uri: String?): String {
    if (uri == null) return ""
    return runCatching { DocumentsContract.getTreeDocumentId(Uri.parse(uri)).substringAfter(':') }.getOrNull()?.ifEmpty { "/" } ?: uri
}

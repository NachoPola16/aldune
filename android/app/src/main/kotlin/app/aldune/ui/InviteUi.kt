package app.aldune.ui

import android.content.ClipData
import android.content.Intent
import android.text.format.DateUtils
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.ClipEntry
import androidx.compose.ui.platform.LocalClipboard
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.data.SyncProfile
import app.aldune.sync.Invitations
import app.aldune.sync.InviteException
import app.aldune.sync.SpaceInfo
import app.aldune.syncclient.QrEncoder
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * «Invitar a alguien» y «Personas de este servidor» (docs/SYNC.md → «Invitations»), del lado de quien aloja. Las dos cosas piden el token de
 * administrador del servidor: **no se guarda en ningún sitio** (ni en el perfil ni en el estado guardado), se escribe cada vez y vive solo
 * mientras el diálogo está abierto. Las llamadas son red: fuera del hilo de la interfaz.
 */
@Composable
internal fun InviteDialog(profile: SyncProfile, onDone: () -> Unit) {
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    val clipboard = LocalClipboard.current
    // Nada de esto es rememberSaveable a propósito: el token de administrador no debe pasar por el estado guardado.
    var admin by remember { mutableStateOf("") }
    var label by remember { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<Int?>(null) }
    var notice by remember { mutableStateOf<Int?>(null) }
    var invitation by remember { mutableStateOf<String?>(null) }
    var people by remember { mutableStateOf<List<SpaceInfo>?>(null) }
    var revoking by remember { mutableStateOf<SpaceInfo?>(null) }
    val url = profile.url

    fun <T> call(work: () -> T, done: (T) -> Unit) {
        if (admin.isBlank()) { error = R.string.invite_err_need_admin; return }
        busy = true; error = null; notice = null
        scope.launch {
            val result = withContext(Dispatchers.IO) { runCatching(work) }
            busy = false
            result.onSuccess(done).onFailure { error = inviteErrorText((it as? InviteException)?.failure) }
        }
    }

    fun loadPeople(thenNotice: Int? = null) = call({ Invitations.listSpaces(url, admin) }) { people = it; notice = thenNotice }

    val title = when {
        people != null -> R.string.people_title
        else -> R.string.invite_create_title
    }
    AlertDialog(
        onDismissRequest = { if (!busy) onDone() },
        title = { Text(stringResource(title), Modifier.semantics { heading() }) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                val text = invitation
                val list = people
                when {
                    text != null -> {
                        Text(stringResource(R.string.invite_created_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Text(text, style = MaterialTheme.typography.bodySmall)
                        QrCode(text, Modifier.align(Alignment.CenterHorizontally))
                        Text(stringResource(R.string.invite_scan_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            TextButton(onClick = {
                                scope.launch { clipboard.setClipEntry(ClipEntry(ClipData.newPlainText("Aldune", text))) }
                                notice = R.string.invite_copied
                            }) { Text(stringResource(R.string.invite_copy)) }
                            TextButton(onClick = {
                                val send = Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, text)
                                runCatching { context.startActivity(Intent.createChooser(send, null)) }
                            }) { Text(stringResource(R.string.invite_share)) }
                        }
                        notice?.let { Text(stringResource(it), style = MaterialTheme.typography.bodySmall) }
                    }
                    list != null -> {
                        Text(stringResource(R.string.people_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        if (list.isEmpty()) Text(stringResource(R.string.people_empty))
                        list.forEach { space ->
                            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                Column(Modifier.weight(1f)) {
                                    Text(space.label?.takeIf { it.isNotBlank() } ?: stringResource(R.string.people_unnamed))
                                    Text(
                                        stringResource(R.string.people_joined, joinedDate(space)),
                                        style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    )
                                }
                                TextButton(enabled = !busy, onClick = { revoking = space }) {
                                    Text(stringResource(R.string.people_revoke), color = MaterialTheme.colorScheme.error)
                                }
                            }
                        }
                        notice?.let { Text(stringResource(it), style = MaterialTheme.typography.bodySmall) }
                    }
                    else -> {
                        Text(stringResource(R.string.invite_create_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        OutlinedTextField(
                            value = admin, onValueChange = { admin = it; error = null }, modifier = Modifier.fillMaxWidth(), singleLine = true,
                            label = { Text(stringResource(R.string.invite_admin_token)) }, visualTransformation = PasswordVisualTransformation(),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                        )
                        OutlinedTextField(
                            value = label, onValueChange = { label = it.take(60) }, modifier = Modifier.fillMaxWidth(), singleLine = true,
                            label = { Text(stringResource(R.string.invite_label_label)) },
                        )
                        TextButton(enabled = !busy, onClick = { loadPeople() }) { Text(stringResource(R.string.people_button)) }
                    }
                }
                error?.let { Text(stringResource(it), color = MaterialTheme.colorScheme.error) }
            }
        },
        confirmButton = {
            if (invitation == null && people == null) {
                TextButton(enabled = !busy, onClick = { call({ Invitations.create(url, admin, label) }) { invitation = it } }) {
                    Text(stringResource(R.string.invite_create_action))
                }
            } else {
                TextButton(onClick = { invitation = null; people = null; notice = null }) { Text(stringResource(R.string.scan_qr_back)) }
            }
        },
        dismissButton = { TextButton(enabled = !busy, onClick = onDone) { Text(stringResource(R.string.done)) } },
    )

    revoking?.let { space ->
        val who = space.label?.takeIf { it.isNotBlank() } ?: stringResource(R.string.people_unnamed)
        AlertDialog(
            onDismissRequest = { revoking = null },
            title = { Text(stringResource(R.string.people_revoke_title), Modifier.semantics { heading() }) },
            text = { Text(stringResource(R.string.people_revoke_body, who)) },
            confirmButton = {
                TextButton(onClick = {
                    revoking = null
                    call({ Invitations.revokeSpace(url, admin, space.name) }) { loadPeople(R.string.people_revoked) }
                }) { Text(stringResource(R.string.people_revoke), color = MaterialTheme.colorScheme.error) }
            },
            dismissButton = { TextButton(onClick = { revoking = null }) { Text(stringResource(R.string.cancel)) } },
        )
    }
}

@Composable
private fun joinedDate(space: SpaceInfo): String =
    DateUtils.formatDateTime(LocalContext.current, space.createdAt.toEpochMilli(), DateUtils.FORMAT_SHOW_DATE or DateUtils.FORMAT_SHOW_YEAR or DateUtils.FORMAT_ABBREV_MONTH)

/** El QR sobre blanco con su zona de silencio de 4 módulos: es lo que necesita un lector aunque la interfaz esté en modo oscuro. */
@Composable
private fun QrCode(text: String, modifier: Modifier = Modifier) {
    val modules = remember(text) { QrEncoder.modules(text) }
    Canvas(modifier.size(220.dp)) {
        drawRect(Color.White)
        val cell = size.width / (modules.size + 8)
        for (y in modules.indices) for (x in modules.indices) {
            if (modules[y][x]) drawRect(Color.Black, Offset((x + 4) * cell, (y + 4) * cell), Size(cell + 0.5f, cell + 0.5f))
        }
    }
}

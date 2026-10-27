package app.aldune.ui

import android.text.format.DateUtils
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalClipboard
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.data.BackgroundRunStore
import app.aldune.syncclient.BackgroundMessage
import app.aldune.syncclient.formatBackground
import app.aldune.R
import app.aldune.capture.CaptureRequest
import app.aldune.core.ProfileCodes
import app.aldune.data.SyncProfile
import app.aldune.data.SyncTransportKind
import app.aldune.sync.SyncScope
import app.aldune.syncclient.CredentialsCheck
import app.aldune.syncclient.CredentialsError
import app.aldune.syncclient.FirstConnectChoice
import app.aldune.syncclient.ProfileCredentials
import app.aldune.syncclient.ProfileParse
import app.aldune.syncclient.ProfileParseError
import app.aldune.syncclient.RelativeTime
import app.aldune.syncclient.StatusLine
import app.aldune.syncclient.StatusMessage
import app.aldune.syncclient.SyncFailureKind
import app.aldune.syncclient.SyncStatus
import app.aldune.syncclient.formatStatus
import app.aldune.syncclient.parseProfileInput
import app.aldune.syncclient.validateCredentials
import app.aldune.syncing.ConnectRequest
import app.aldune.syncing.ConnectResult
import app.aldune.syncing.SyncController
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/** La hora de ahora, refrescada cada 30 s: «hace 3 min» tiene que ir cambiando sin que ocurra nada más en pantalla. */
@Composable
fun rememberNow(): Instant {
    var now by remember { mutableStateOf(Instant.now()) }
    LaunchedEffect(Unit) {
        while (true) {
            delay(30_000)
            now = Instant.now()
        }
    }
    return now
}

@Composable
fun relativeText(relative: RelativeTime): String {
    val context = LocalContext.current
    return when (relative) {
        RelativeTime.JustNow -> stringResource(R.string.rel_just_now)
        is RelativeTime.Minutes -> stringResource(R.string.rel_minutes, relative.count)
        is RelativeTime.Hours -> stringResource(R.string.rel_hours, relative.count)
        is RelativeTime.Days -> stringResource(R.string.rel_days, relative.count)
        is RelativeTime.Date -> DateUtils.formatDateTime(context, relative.at.toEpochMilli(), DateUtils.FORMAT_SHOW_DATE or DateUtils.FORMAT_ABBREV_MONTH)
    }
}

/** La línea de estado en el idioma de la interfaz. Los casos y los tiempos los decide `formatStatus` (JVM); aquí solo se traducen. */
@Composable
fun statusText(line: StatusLine): String {
    val since = line.relative?.let { relativeText(it) }
    return when (line.message) {
        StatusMessage.NOT_CONFIGURED -> ""
        StatusMessage.NEVER_SYNCED -> stringResource(R.string.sync_status_never)
        StatusMessage.SYNCING -> stringResource(R.string.sync_status_syncing)
        StatusMessage.LAST_SYNC -> stringResource(R.string.sync_status_last, since.orEmpty())
        StatusMessage.OFFLINE ->
            if (since != null) stringResource(R.string.sync_status_offline_since, since) else stringResource(R.string.sync_status_offline)
        StatusMessage.UNAUTHORIZED -> stringResource(R.string.sync_status_unauthorized)
        StatusMessage.KEY_CHANGED -> stringResource(R.string.sync_status_key_changed)
        StatusMessage.SERVER_ERROR -> stringResource(R.string.sync_status_server_error)
        StatusMessage.TOO_LARGE -> stringResource(R.string.sync_status_too_large)
        StatusMessage.UNSUPPORTED_FORMAT -> stringResource(R.string.sync_status_unsupported)
        StatusMessage.UNKNOWN_ERROR -> stringResource(R.string.sync_status_unknown)
    }
}

/** El texto de un fallo al conectar (misma tabla que el estado). */
@Composable
private fun failureText(kind: SyncFailureKind): String = stringResource(
    when (kind) {
        SyncFailureKind.OFFLINE -> R.string.sync_status_offline
        SyncFailureKind.UNAUTHORIZED -> R.string.sync_status_unauthorized
        SyncFailureKind.KEY_CHANGED -> R.string.sync_status_key_changed
        SyncFailureKind.SERVER_ERROR -> R.string.sync_status_server_error
        SyncFailureKind.TOO_LARGE -> R.string.sync_status_too_large
        SyncFailureKind.UNSUPPORTED_FORMAT -> R.string.sync_status_unsupported
        SyncFailureKind.UNKNOWN -> R.string.sync_status_unknown
    },
)

/** Ajustes → Sincronización: la lista de perfiles conectados, «Añadir otro perfil» y el interruptor del segundo plano. */
@Composable
fun SyncSection(viewModel: NotesViewModel) {
    val controller by viewModel.sync.collectAsStateWithLifecycle()
    val c = controller ?: return
    val profiles by c.profiles.collectAsStateWithLifecycle()
    val status by c.status.collectAsStateWithLifecycle()
    Section(R.string.sync_title)
    var adding by remember { mutableStateOf(false) }
    // Un código compartido desde otra app (WhatsApp, correo…) llega aquí: se abre el formulario con el código puesto.
    val capture by viewModel.capture.collectAsStateWithLifecycle()
    var sharedCode by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(capture) {
        (capture as? CaptureRequest.ProfileCode)?.let { sharedCode = it.code; adding = true; viewModel.captureHandled() }
    }
    if (profiles.isEmpty()) {
        ConnectForm(c, additional = false, initialCode = sharedCode, onCodeTaken = { sharedCode = null }, onDone = { adding = false; sharedCode = null })
    } else {
        profiles.forEach { ConnectedPanel(c, it) }
        Column(Modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            // «Sincronizar ahora» es de toda la sync, no de un perfil: una pasada recorre todos.
            Button(enabled = !status.running, onClick = { c.request(app.aldune.syncclient.SyncEvent.ManualSync) }) { Text(stringResource(R.string.sync_now)) }
            Text(stringResource(R.string.sync_backup_note), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 8.dp))
            if (adding) ConnectForm(c, additional = true, initialCode = sharedCode, onCodeTaken = { sharedCode = null }, onDone = { adding = false; sharedCode = null })
            else OutlinedButton(onClick = { adding = true }) { Text(stringResource(R.string.sync_add_profile)) }
            HorizontalDivider(Modifier.padding(top = 8.dp))
        }
        val settings by viewModel.settings.collectAsStateWithLifecycle()
        SwitchRow(stringResource(R.string.sync_in_background), stringResource(R.string.sync_in_background_hint), settings.syncInBackground) { on ->
            viewModel.updateSettings { it.copy(syncInBackground = on) }
        }
        if (settings.syncInBackground) BackgroundInfo()
    }
}

/** Lo que Ajustes cuenta del segundo plano: la última pasada, que Android decide cuándo corre y si la tiene restringida. */
@Composable
private fun BackgroundInfo() {
    val context = LocalContext.current
    val store = remember { BackgroundRunStore(context) }
    var last by remember { mutableStateOf(store.load()) }
    var restricted by remember { mutableStateOf(isBackgroundRestricted(context)) }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { last = store.load(); restricted = isBackgroundRestricted(context) }
    val now = rememberNow()
    val line = formatBackground(enabled = true, last = last, restricted = restricted, now = now)
    val since = line.relative?.let { relativeText(it) }.orEmpty()
    Column(Modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        when (line.message) {
            BackgroundMessage.NEVER_RAN -> Text(stringResource(R.string.bg_never), style = MaterialTheme.typography.bodyMedium)
            BackgroundMessage.OK -> Text(stringResource(R.string.bg_ok, since), style = MaterialTheme.typography.bodyMedium)
            BackgroundMessage.OFFLINE -> Text(stringResource(R.string.bg_offline, since), style = MaterialTheme.typography.bodyMedium)
            BackgroundMessage.FAILED -> Text(
                stringResource(R.string.bg_failed, since, line.failure?.let { failureText(it) }.orEmpty()),
                style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.error,
            )
            BackgroundMessage.DISABLED -> Unit
        }
        if (line.restricted) {
            Text(stringResource(R.string.bg_restricted), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.error)
            OutlinedButton(onClick = {
                context.startActivity(
                    Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.fromParts("package", context.packageName, null)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
                )
            }) { Text(stringResource(R.string.bg_battery_settings)) }
        }
    }
}

/** El usuario (o el sistema) puso la app en modo restringido: no corre nada en segundo plano. Antes de la API 28 no existe. */
private fun isBackgroundRestricted(context: Context): Boolean =
    Build.VERSION.SDK_INT >= Build.VERSION_CODES.P &&
        (context.getSystemService(Context.ACTIVITY_SERVICE) as android.app.ActivityManager).isBackgroundRestricted

@Composable
private fun ConnectForm(controller: SyncController, additional: Boolean, initialCode: String? = null, onCodeTaken: () -> Unit = {}, onDone: () -> Unit) {
    val scope = rememberCoroutineScope()
    val clipboard = LocalClipboard.current
    // Nada de esto es rememberSaveable a propósito: el código lleva la clave y el token es un secreto; no deben pasar por el estado guardado.
    var code by remember { mutableStateOf(initialCode.orEmpty()) }
    // El código compartido se recoge una vez: si no, tras conectar el formulario reaparecería con el código viejo.
    LaunchedEffect(initialCode) { if (initialCode != null) { code = initialCode; onCodeTaken() } }
    var parsed by remember { mutableStateOf<ProfileParse.Parsed?>(null) }
    var transport by remember { mutableStateOf(SyncTransportKind.SERVER) }
    var url by remember { mutableStateOf("") }
    var token by remember { mutableStateOf("") }
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var errorText by remember { mutableStateOf<Int?>(null) }
    val scanner = rememberLauncherForActivityResult(ScanQrContract()) { scanned ->
        // Igual que «Pegar»: solo rellena el campo; validar y continuar sigue siendo de «Continuar».
        if (!scanned.isNullOrBlank()) { code = scanned; errorText = null }
    }
    var failureKind by remember { mutableStateOf<SyncFailureKind?>(null) }
    var busy by remember { mutableStateOf(false) }
    var pending by remember { mutableStateOf<ConnectRequest?>(null) }
    var pendingNotes by remember { mutableStateOf(0) }
    var discardRequest by remember { mutableStateOf<ConnectRequest?>(null) }
    var alreadyName by remember { mutableStateOf<String?>(null) }

    fun reset() {
        code = ""; parsed = null; url = ""; token = ""; username = ""; password = ""; errorText = null; failureKind = null; pending = null; alreadyName = null
    }

    fun connect(request: ConnectRequest, choice: FirstConnectChoice?) {
        busy = true
        errorText = null
        failureKind = null
        alreadyName = null
        scope.launch {
            when (val result = controller.connect(request, choice)) {
                ConnectResult.Connected -> { reset(); onDone() }
                is ConnectResult.NeedsChoice -> { pending = request; pendingNotes = result.noteCount }
                is ConnectResult.AlreadyConnected -> alreadyName = result.name
                is ConnectResult.Failed -> failureKind = result.failure.kind
            }
            busy = false
        }
    }

    Column(Modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        val step = parsed
        if (step == null) {
            Text(stringResource(if (additional) R.string.sync_add_intro else R.string.sync_intro), color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (!additional) Text(stringResource(R.string.sync_howto), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            OutlinedTextField(
                value = code, onValueChange = { code = it; errorText = null }, modifier = Modifier.fillMaxWidth(),
                label = { Text(stringResource(R.string.sync_code_label)) }, minLines = 2, maxLines = 4,
                keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None, autoCorrectEnabled = false, keyboardType = KeyboardType.Ascii),
                isError = errorText != null, supportingText = errorText?.let { { Text(stringResource(it)) } },
            )
            // Pegar solo cuando se pulsa: Aldune no lee el portapapeles por su cuenta.
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = {
                    scope.launch {
                        val pasted = clipboard.getClipEntry()?.clipData?.takeIf { it.itemCount > 0 }?.getItemAt(0)?.text?.toString()
                        if (!pasted.isNullOrBlank()) {
                            val share = ProfileCodes.findShareCode(pasted)
                            code = share ?: pasted.trim(); errorText = null
                            // El código lleva la clave del perfil: ya está en el campo, no hace falta que siga en el portapapeles (donde lo ven
                            // los gestores de portapapeles y otras apps con acceso). Solo se vacía si lo pegado era un código de perfil.
                            if (share != null) clipboard.setClipEntry(null)
                        }
                    }
                }) { Text(stringResource(R.string.sync_paste)) }
                OutlinedButton(onClick = { scanner.launch(Unit) }) { Text(stringResource(R.string.scan_qr)) }
            }
            Button(onClick = {
                when (val result = parseProfileInput(code)) {
                    is ProfileParse.Rejected -> errorText = parseErrorText(result.error)
                    is ProfileParse.Parsed -> {
                        parsed = result
                        when (result) {
                            is ProfileParse.Server -> { transport = SyncTransportKind.SERVER; url = result.url }
                            is ProfileParse.WebDav -> { transport = SyncTransportKind.WEBDAV; url = result.url }
                            is ProfileParse.NeedsTransport -> { transport = SyncTransportKind.SERVER; url = "" }
                        }
                    }
                }
            }) { Text(stringResource(R.string.sync_continue)) }
        } else {
            if (step is ProfileParse.NeedsTransport) {
                for ((kind, label) in listOf(SyncTransportKind.SERVER to R.string.sync_transport_server, SyncTransportKind.WEBDAV to R.string.sync_transport_webdav)) {
                    Row(
                        Modifier.fillMaxWidth().selectable(selected = transport == kind, role = Role.RadioButton, onClick = { transport = kind }),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        RadioButton(selected = transport == kind, onClick = null)
                        Text(stringResource(label), Modifier.padding(start = 12.dp, top = 8.dp, bottom = 8.dp))
                    }
                }
            }
            OutlinedTextField(
                value = url, onValueChange = { url = it }, modifier = Modifier.fillMaxWidth(), singleLine = true,
                label = { Text(stringResource(R.string.sync_url_label)) }, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
            )
            // La URL venía en el código: si es de otro (QR pegado por ahí), lo que se escriba abajo se le enviaría. Se destaca el host real.
            val fromCode = (step as? ProfileParse.Server)?.url ?: (step as? ProfileParse.WebDav)?.url
            if (fromCode != null) {
                val host = runCatching { java.net.URI(fromCode).host }.getOrNull()
                if (!host.isNullOrEmpty()) Text(stringResource(R.string.sync_code_host_warning, host), color = MaterialTheme.colorScheme.error, fontWeight = FontWeight.Bold)
            }
            if (transport == SyncTransportKind.WEBDAV) Text(stringResource(R.string.sync_webdav_slash_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (url.trim().startsWith("http://", ignoreCase = true)) Text(stringResource(R.string.sync_http_warning), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
            if (transport == SyncTransportKind.SERVER) {
                OutlinedTextField(
                    value = token, onValueChange = { token = it }, modifier = Modifier.fillMaxWidth(), singleLine = true,
                    label = { Text(stringResource(R.string.sync_token_label)) }, visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                )
            } else {
                OutlinedTextField(
                    value = username, onValueChange = { username = it }, modifier = Modifier.fillMaxWidth(), singleLine = true,
                    label = { Text(stringResource(R.string.sync_username_label)) },
                )
                OutlinedTextField(
                    value = password, onValueChange = { password = it }, modifier = Modifier.fillMaxWidth(), singleLine = true,
                    label = { Text(stringResource(R.string.sync_password_label)) }, visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                )
            }
            errorText?.let { Text(stringResource(it), color = MaterialTheme.colorScheme.error) }
            alreadyName?.let { Text(stringResource(R.string.sync_already_connected, it), color = MaterialTheme.colorScheme.error) }
            failureKind?.let { Text(failureText(it), color = MaterialTheme.colorScheme.error) }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) {
                Button(enabled = !busy, onClick = {
                    val credentials = if (transport == SyncTransportKind.SERVER) ProfileCredentials.Token(token) else ProfileCredentials.Basic(username, password)
                    when (val check = validateCredentials(url, credentials)) {
                        is CredentialsCheck.Invalid -> errorText = credentialErrorText(check.errors)
                        is CredentialsCheck.Valid -> connect(ConnectRequest(step.profile, transport, check.url, check.credentials), null)
                    }
                }) { Text(stringResource(if (busy) R.string.sync_connecting else R.string.sync_connect)) }
                TextButton(enabled = !busy, onClick = { reset(); if (additional) onDone() }) { Text(stringResource(R.string.cancel)) }
            }
        }
    }

    pending?.let { request ->
        // Subir es lo seguro y lo que ocupa el botón principal. Descartar no se deshace: va aparte, en rojo, y pide una segunda confirmación
        // (en un teléfono estrecho los botones se apilaban y «Descartarlas» quedaba pegado a «Cancelar»).
        AlertDialog(
            onDismissRequest = { pending = null },
            title = { Text(stringResource(R.string.first_connect_title)) },
            text = {
                Column {
                    Text(stringResource(R.string.first_connect_body, pendingNotes))
                    TextButton(onClick = { discardRequest = request; pending = null }, modifier = Modifier.padding(top = 8.dp)) {
                        Text(stringResource(R.string.first_connect_discard), color = MaterialTheme.colorScheme.error)
                    }
                }
            },
            confirmButton = { TextButton(onClick = { pending = null; connect(request, FirstConnectChoice.UPLOAD) }) { Text(stringResource(R.string.first_connect_upload)) } },
            dismissButton = { TextButton(onClick = { pending = null }) { Text(stringResource(R.string.cancel)) } },
        )
    }

    discardRequest?.let { request ->
        AlertDialog(
            onDismissRequest = { discardRequest = null },
            title = { Text(stringResource(R.string.first_connect_discard_title)) },
            text = { Text(stringResource(R.string.first_connect_discard_body, pendingNotes)) },
            confirmButton = {
                TextButton(onClick = { discardRequest = null; connect(request, FirstConnectChoice.DISCARD) }) {
                    Text(stringResource(R.string.first_connect_discard), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = { TextButton(onClick = { discardRequest = null }) { Text(stringResource(R.string.cancel)) } },
        )
    }
}

private fun parseErrorText(error: ProfileParseError): Int = when (error) {
    ProfileParseError.EMPTY -> R.string.sync_parse_empty
    ProfileParseError.NOT_A_CODE -> R.string.sync_parse_not_code
    ProfileParseError.INVALID -> R.string.sync_parse_invalid
    ProfileParseError.FOLDER_NOT_SUPPORTED -> R.string.sync_parse_folder
}

/** Un solo mensaje cada vez (el primero que toque): el campo concreto lo resuelve el usuario al verlo. */
private fun credentialErrorText(errors: Set<CredentialsError>): Int = when {
    CredentialsError.URL_EMPTY in errors -> R.string.sync_cred_url_empty
    CredentialsError.URL_INVALID in errors -> R.string.sync_cred_url_invalid
    CredentialsError.TOKEN_EMPTY in errors -> R.string.sync_cred_token_empty
    CredentialsError.USERNAME_EMPTY in errors -> R.string.sync_cred_username_empty
    else -> R.string.sync_cred_password_empty
}

@Composable
private fun ConnectedPanel(controller: SyncController, profile: SyncProfile) {
    val scope = rememberCoroutineScope()
    val status: SyncStatus = controller.statuses.collectAsStateWithLifecycle().value[profile.id] ?: SyncStatus()
    val unreadable = profile.id in controller.unreadable.collectAsStateWithLifecycle().value
    val now = rememberNow()
    val line = formatStatus(status, now)
    var confirming by remember { mutableStateOf(false) }

    Column(Modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(stringResource(R.string.sync_connected_to, profile.profileName?.takeIf { it.isNotBlank() } ?: profile.url))
        Text(scopeText(profile.scope), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        if (unreadable) {
            Text(stringResource(R.string.sync_secrets_unreadable), color = MaterialTheme.colorScheme.error)
        } else {
            val isError = line.icon == app.aldune.syncclient.StatusIcon.ERROR
            Text(statusText(line), color = if (isError) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurface)
            line.failure?.detail?.let {
                Text(
                    stringResource(R.string.sync_technical_detail) + ": " + it, style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        OutlinedButton(onClick = { confirming = true }, modifier = Modifier.padding(top = 4.dp)) { Text(stringResource(R.string.sync_disconnect)) }
        HorizontalDivider(Modifier.padding(top = 8.dp))
    }

    if (confirming) {
        AlertDialog(
            onDismissRequest = { confirming = false },
            title = { Text(stringResource(R.string.sync_disconnect_title)) },
            text = { Text(stringResource(R.string.sync_disconnect_body)) },
            confirmButton = { TextButton(onClick = { confirming = false; scope.launch { controller.disconnect(profile.id) } }) { Text(stringResource(R.string.sync_disconnect)) } },
            dismissButton = { TextButton(onClick = { confirming = false }) { Text(stringResource(R.string.cancel)) } },
        )
    }
}

@Composable
private fun scopeText(scope: SyncScope): String = when (scope) {
    SyncScope.All -> stringResource(R.string.sync_scope_all)
    is SyncScope.SelectedNotes -> stringResource(R.string.sync_scope_chosen, scope.noteIds.size)
    is SyncScope.Tag -> if (scope.tag.isNullOrBlank()) stringResource(R.string.sync_scope_all) else stringResource(R.string.sync_scope_tag, scope.tag!!)
}

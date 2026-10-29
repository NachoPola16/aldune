package app.aldune.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.core.PasswordProblem
import app.aldune.core.PasswordRules
import app.aldune.text.ProtectedTitleRules
import kotlinx.coroutines.launch

/** Para qué se pide la contraseña: elegir una (dos veces), abrir una nota o quitarle la protección. */
enum class PasswordMode { Protect, Unlock, Remove }

/** Por qué se rechazó lo escrito; el diálogo lo traduce al texto de la interfaz. */
enum class PasswordFailure { Wrong, Empty, TooShort, Mismatch, Unreadable, Unsupported, Changed }

/**
 * Pide una contraseña. [onSubmit] hace el trabajo (derivar la clave tarda: un indicador y nada que tocar mientras dura) y devuelve `null` si
 * salió bien (el diálogo se cierra) o el motivo del rechazo. La contraseña vive solo en este estado de Compose: nunca se guarda con
 * `rememberSaveable`, así que no entra en el estado de la actividad. Al proteger también se pide el nombre visible (opcional, en claro: se pasa
 * como tercer argumento, vacío si no se escribe).
 */
@Composable
fun PasswordDialog(
    mode: PasswordMode, onDismiss: () -> Unit,
    onSubmit: suspend (password: String, confirmation: String, visibleName: String) -> PasswordFailure?,
) {
    var password by remember { mutableStateOf("") }
    var confirmation by remember { mutableStateOf("") }
    var visibleName by remember { mutableStateOf("") }
    var failure by remember { mutableStateOf<PasswordFailure?>(null) }
    var busy by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    fun submit() {
        if (busy) return
        // Lo que se puede decir sin derivar nada se dice al instante.
        val local = when (if (mode == PasswordMode.Protect) PasswordRules.check(password, confirmation) else PasswordRules.check(password, null)) {
            PasswordProblem.None -> null
            PasswordProblem.Empty -> PasswordFailure.Empty
            PasswordProblem.TooShort -> PasswordFailure.TooShort
            PasswordProblem.Mismatch -> PasswordFailure.Mismatch
        }
        if (local != null) {
            failure = local
            return
        }
        failure = null
        busy = true
        scope.launch {
            val result = onSubmit(password, confirmation, visibleName)
            busy = false
            if (result == null) onDismiss() else failure = result
        }
    }

    val title = when (mode) {
        PasswordMode.Protect -> R.string.password_title_protect
        PasswordMode.Unlock -> R.string.password_title_unlock
        PasswordMode.Remove -> R.string.remove_protection
    }
    val action = when (mode) {
        PasswordMode.Protect -> R.string.password_protect_action
        PasswordMode.Unlock -> R.string.password_unlock_action
        PasswordMode.Remove -> R.string.password_remove_action
    }
    AlertDialog(
        // Mientras se deriva la clave no se puede cerrar: el resultado llegaría a un diálogo que ya no está.
        onDismissRequest = { if (!busy) onDismiss() },
        title = { Text(stringResource(title)) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                if (mode == PasswordMode.Protect) {
                    Text(stringResource(R.string.password_protect_hint), style = MaterialTheme.typography.bodySmall)
                }
                OutlinedTextField(
                    value = password, onValueChange = { password = it; failure = null }, singleLine = true, enabled = !busy,
                    label = { Text(stringResource(R.string.password_label)) },
                    visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(
                        keyboardType = KeyboardType.Password,
                        imeAction = if (mode == PasswordMode.Protect) ImeAction.Next else ImeAction.Done,
                    ),
                    keyboardActions = KeyboardActions(onDone = { submit() }),
                )
                if (mode == PasswordMode.Protect) {
                    OutlinedTextField(
                        value = confirmation, onValueChange = { confirmation = it; failure = null }, singleLine = true, enabled = !busy,
                        label = { Text(stringResource(R.string.password_repeat)) },
                        visualTransformation = PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Next),
                        keyboardActions = KeyboardActions(onDone = { submit() }),
                    )
                    OutlinedTextField(
                        value = visibleName, onValueChange = { visibleName = it.take(ProtectedTitleRules.MAX_LENGTH) }, singleLine = true, enabled = !busy,
                        label = { Text(stringResource(R.string.visible_name_label)) },
                        supportingText = { Text(stringResource(R.string.visible_name_hint)) },
                        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
                        keyboardActions = KeyboardActions(onDone = { submit() }),
                    )
                }
                failure?.let { Text(stringResource(it.message()), color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall) }
                if (busy) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
                        Text(stringResource(R.string.password_working), style = MaterialTheme.typography.bodySmall)
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = ::submit, enabled = !busy) { Text(stringResource(action)) } },
        dismissButton = { TextButton(onClick = onDismiss, enabled = !busy) { Text(stringResource(R.string.cancel)) } },
    )
}

private fun PasswordFailure.message(): Int = when (this) {
    PasswordFailure.Wrong -> R.string.password_wrong
    PasswordFailure.Empty -> R.string.password_empty
    PasswordFailure.TooShort -> R.string.password_too_short
    PasswordFailure.Mismatch -> R.string.password_mismatch
    PasswordFailure.Unreadable -> R.string.password_unreadable
    PasswordFailure.Unsupported -> R.string.password_unsupported
    PasswordFailure.Changed -> R.string.password_changed
}

/**
 * Pone o cambia el nombre visible de una nota protegida. Va aparte del diálogo de contraseña porque no la necesita: el nombre es público por
 * diseño y el aviso lo dice. Vacío lo oculta (la nota vuelve a salir como «Nota protegida»).
 */
@Composable
fun VisibleNameDialog(initial: String?, onDismiss: () -> Unit, onSave: (String?) -> Unit) {
    var name by remember { mutableStateOf(initial.orEmpty()) }
    fun save() {
        onSave(ProtectedTitleRules.normalize(name))
        onDismiss()
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.visible_name_title)) },
        text = {
            OutlinedTextField(
                value = name, onValueChange = { name = it.take(ProtectedTitleRules.MAX_LENGTH) }, singleLine = true,
                label = { Text(stringResource(R.string.visible_name_label)) },
                supportingText = { Text(stringResource(R.string.visible_name_hint)) },
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done), keyboardActions = KeyboardActions(onDone = { save() }),
            )
        },
        confirmButton = { TextButton(onClick = ::save) { Text(stringResource(R.string.visible_name_action)) } },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.cancel)) } },
    )
}

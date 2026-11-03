package app.aldune.ui

import androidx.compose.foundation.clickable
import androidx.compose.material3.minimumInteractiveComponentSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import app.aldune.text.ColorNames

/** What TalkBack reads for a colour: its name («Yellow»), not its code, which says nothing; a colour without a name falls back to the hex. */
@Composable
internal fun spokenColorName(hex: String): String = ColorNames.of(hex)?.let { stringResource(colorNameRes(it)) } ?: hex

/**
 * Makes a colour circle a proper control without changing how big it looks: put it BEFORE the visual `size`/`clip`/`background`, so the
 * touch target is at least 48 dp (the circle stays centred inside it), and it carries a spoken name plus a role. A [selected] of `null`
 * means a plain action (Role.Button); otherwise it is one option of a group (Role.RadioButton) and its state is announced.
 */
@Composable
internal fun Modifier.colorChoice(hex: String, selected: Boolean?, onClick: () -> Unit): Modifier {
    val name = spokenColorName(hex)
    return this
        .clickable(role = if (selected == null) Role.Button else Role.RadioButton, onClick = onClick)
        .semantics {
            contentDescription = name
            if (selected != null) this.selected = selected
        }
        .minimumInteractiveComponentSize()
}

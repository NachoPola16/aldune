package app.aldune.ui

import android.content.Context
import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.aldune.R

/**
 * What each version added, for the dialog shown once after an update (and from Settings).
 *
 * To add a release: write its strings in `strings.tsv` (all eight languages), add one line here and raise `versionCode`/`versionName`.
 * Only things a person will notice belong here: a few short lines per version, not the whole changelog (that is the GitHub release).
 * Which versions to list after an update is decided by `WhatsNewPlan` (module `text`).
 */
internal object WhatsNew {
    internal class Entry(val versionCode: Int, val versionName: String, @StringRes val items: List<Int>)

    val entries: List<Entry> = listOf(
        Entry(18, "1.2.0", listOf(R.string.whatsnew_120_tasks, R.string.whatsnew_120_history, R.string.whatsnew_120_tags, R.string.whatsnew_120_links, R.string.whatsnew_120_langs)),
        Entry(19, "1.3.0", listOf(R.string.whatsnew_130_news)),
    )

    val codes: List<Int> get() = entries.map { it.versionCode }

    /** The running app's `versionCode`. */
    fun currentCode(context: Context): Int =
        context.packageManager.getPackageInfo(context.packageName, 0).longVersionCode.toInt()
}

/** The news of [codes] (newest first): a heading per version and a line per item. Dismissing it is the only action. */
@Composable
internal fun WhatsNewDialog(codes: List<Int>, onDismiss: () -> Unit) {
    val shown = codes.mapNotNull { code -> WhatsNew.entries.firstOrNull { it.versionCode == code } }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.whatsnew_title), Modifier.semantics { heading() }) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                for (entry in shown) {
                    Text(
                        stringResource(R.string.app_name) + " " + entry.versionName,
                        Modifier.padding(top = 4.dp).semantics { heading() },
                        style = MaterialTheme.typography.titleSmall,
                    )
                    for (item in entry.items) Text("•  " + stringResource(item))
                }
                Text(stringResource(R.string.whatsnew_more) + " github.com/NachoPola16/aldune/releases", color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.whatsnew_ok)) } },
    )
}

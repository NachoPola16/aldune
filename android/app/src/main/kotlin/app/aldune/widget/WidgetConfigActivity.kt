package app.aldune.widget

import android.app.Activity
import android.appwidget.AppWidgetManager
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.R
import app.aldune.data.Languages
import app.aldune.data.SettingsStore
import app.aldune.domain.NoteState
import app.aldune.ui.AldunaTheme
import app.aldune.ui.NoteLook
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Asked by the launcher when a dock widget is placed (and again from its "reconfigure" handle on Android 9+): which notes it shows,
 * all of them or only those with one tag. Cancelling leaves the widget unplaced, as the system expects.
 */
class WidgetConfigActivity : ComponentActivity() {
    private var appWidgetId = AppWidgetManager.INVALID_APPWIDGET_ID

    override fun attachBaseContext(newBase: android.content.Context) {
        super.attachBaseContext(Languages.wrap(newBase, SettingsStore(newBase).load().language))
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        appWidgetId = intent?.extras?.getInt(AppWidgetManager.EXTRA_APPWIDGET_ID, AppWidgetManager.INVALID_APPWIDGET_ID) ?: AppWidgetManager.INVALID_APPWIDGET_ID
        // The system treats anything but RESULT_OK as "do not place the widget": set it first, so Back cancels cleanly.
        setResult(Activity.RESULT_CANCELED, Intent().putExtra(AppWidgetManager.EXTRA_APPWIDGET_ID, appWidgetId))
        if (appWidgetId == AppWidgetManager.INVALID_APPWIDGET_ID) {
            finish()
            return
        }
        val settings = SettingsStore(this).load()
        setContent {
            AldunaTheme(settings.appearance, settings.aspectColors, NoteLook(settings.uniformNoteColor, settings.cornersSquare, settings.syncSignalVisible, settings.colorFill, settings.newButtonSquare)) {
                Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
                    var tags by remember { mutableStateOf<List<String>>(emptyList()) }
                    var selected by remember { mutableStateOf(WidgetTagStore.get(this@WidgetConfigActivity, appWidgetId)) }
                    LaunchedEffect(Unit) { tags = withContext(Dispatchers.IO) { loadTags() } }
                    Column(Modifier.systemBarsPadding().padding(horizontal = 16.dp, vertical = 24.dp)) {
                        Text(stringResource(R.string.widget_config_title), style = MaterialTheme.typography.titleLarge)
                        Text(stringResource(R.string.widget_config_hint), style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 4.dp, bottom = 12.dp))
                        // A tag chosen earlier that no note carries any more stays in the list, so the choice is never invisible.
                        val options = listOf<String?>(null) + (tags + listOfNotNull(selected)).distinctBy { it.lowercase() }
                        LazyColumn {
                            items(options) { tag ->
                                Row(
                                    Modifier.fillMaxWidth().clickable { selected = tag; finishWith(tag) }.padding(vertical = 4.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                ) {
                                    RadioButton(selected = (tag == null && selected == null) || (tag != null && tag.equals(selected, ignoreCase = true)), onClick = { selected = tag; finishWith(tag) })
                                    Text(tag?.let { "#$it" } ?: stringResource(R.string.widget_config_all), style = MaterialTheme.typography.bodyLarge)
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /** Every tag on a note that is not in the trash, A to Z (an archived note still counts: the widget lists active ones, but the tag exists). */
    private fun loadTags(): List<String> {
        val ready = (applicationContext as AldunaApp).container.open() as? AppContainer.Opened.Ready ?: return emptyList()
        return listOf(NoteState.ACTIVE, NoteState.ARCHIVED).flatMap { ready.notes.list(it) }
            .flatMap { it.tags }.distinctBy { it.lowercase() }.sortedBy { it.lowercase() }
    }

    private fun finishWith(tag: String?) {
        WidgetTagStore.set(this, appWidgetId, tag)
        WidgetUpdater.request(this)
        setResult(Activity.RESULT_OK, Intent().putExtra(AppWidgetManager.EXTRA_APPWIDGET_ID, appWidgetId))
        finish()
    }
}

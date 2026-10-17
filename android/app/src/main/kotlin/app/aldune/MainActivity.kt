package app.aldune

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import app.aldune.text.NoteTitleHelper
import app.aldune.text.TaskLines

/** Esqueleto de F4: comprueba que la app arranca y que usa `:text` (el título y las tareas salen del mismo código que Windows). */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val sample = "# Aldune\n☐ una tarea\n☒ otra hecha"
        setContent {
            MaterialTheme {
                Column(Modifier.padding(24.dp)) {
                    Text(NoteTitleHelper.getTitle(sample, "Nota nueva"), style = MaterialTheme.typography.headlineMedium)
                    val count = TaskLines.count(sample)
                    Text("${count.done}/${count.total}")
                }
            }
        }
    }
}

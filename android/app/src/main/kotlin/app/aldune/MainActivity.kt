package app.aldune

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import app.aldune.ui.AldunaTheme
import app.aldune.ui.NotesApp
import app.aldune.ui.NotesViewModel

class MainActivity : ComponentActivity() {
    private val viewModel: NotesViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            AldunaTheme {
                NotesApp(viewModel)
            }
        }
    }
}

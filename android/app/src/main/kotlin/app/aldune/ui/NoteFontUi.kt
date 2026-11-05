package app.aldune.ui

import android.graphics.Typeface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.font.Typeface as ComposeTypeface
import app.aldune.R
import app.aldune.data.AppSettings
import app.aldune.text.AspectSkin
import app.aldune.text.NoteFonts
import java.io.File

/** The family chosen for the note text, or null for the look's own (the system font). Provided once at the root; the editor and the list read it. */
internal val LocalNoteFont = compositionLocalOf<FontFamily?> { null }

/** The folder of the imported font, inside the app's private files: nothing outside the app reads it and nothing syncs it. */
internal fun noteFontsDir(context: android.content.Context) = File(context.filesDir, "fonts")

/** The file of the imported font if the setting names one that is still there; a bare file name only (the value is app-written, but never followed as a path). */
internal fun importedFontFile(context: android.content.Context, settings: AppSettings): File? {
    val name = settings.noteFontFile ?: return null
    if (name != File(name).name) return null
    return File(noteFontsDir(context), name).takeIf { it.isFile }
}

/**
 * The note font as a Compose family. The looks that define their own text style (terminal and notebooks) keep it. An imported file that cannot be
 * loaded falls back to the system font instead of failing: the file is only ever handed to the platform loader.
 */
@Composable
internal fun rememberNoteFontFamily(settings: AppSettings): FontFamily? {
    val context = LocalContext.current
    val applies = NoteFonts.applies(isMonospaceAspect(settings.appearance), AspectSkin.serif(settings.appearance))
    val choice = NoteFonts.normalize(settings.noteFont)
    return remember(applies, choice, settings.noteFontFile) {
        if (!applies) return@remember null
        when (choice) {
            NoteFonts.SERIF -> FontFamily.Serif
            NoteFonts.MONOSPACE -> FontFamily.Monospace
            NoteFonts.EASY_TO_READ -> FontFamily(
                Font(R.font.atkinson_hyperlegible_regular, FontWeight.Normal),
                Font(R.font.atkinson_hyperlegible_bold, FontWeight.Bold),
            )
            NoteFonts.IMPORTED -> importedFontFile(context, settings)
                ?.let { file -> runCatching { Typeface.createFromFile(file) }.getOrNull() }
                ?.let { FontFamily(ComposeTypeface(it)) }
            else -> null
        }
    }
}

internal fun TextStyle.withNoteFont(family: FontFamily?): TextStyle = if (family == null) this else copy(fontFamily = family)

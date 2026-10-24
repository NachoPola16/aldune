package app.aldune.ui

import android.graphics.Color as AndroidColor
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Color
import app.aldune.text.AppPalette
import app.aldune.text.AspectCatalog

/** Cómo se elige el aspecto: los números son los de `AppearanceMode` de Core (Oscuro 0, Claro 1, Sistema 2, Pastel 3, Medianoche 4, XP claro 5, XP oscuro 6, Telecom claro 7, Telecom oscuro 8, Bash 9, Fósforo 10). */
const val APPEARANCE_SYSTEM = AppPalette.SYSTEM

/** `#RRGGBB` o `#AARRGGBB` (alfa primero) de la paleta → color de Compose. */
fun paletteColor(hex: String): Color = Color(AndroidColor.parseColor(hex))

/**
 * El aspecto de Aldune: la paleta de Core (`AppPalette`, ya portada y fijada por vectores) traducida al esquema de Material 3. Nada de
 * Material You (decisión del usuario): los colores son los de la app, no los del fondo de pantalla.
 */
@Composable
fun AldunaTheme(appearance: Int = APPEARANCE_SYSTEM, aspectColors: Map<String, Map<String, String>> = emptyMap(), content: @Composable () -> Unit) {
    val systemLight = !isSystemInDarkTheme()
    val chosen = AspectCatalog.forMode(appearance)?.let { aspectColors[it.id] }
    val scheme = remember(appearance, systemLight, chosen) {
        val palette = AppPalette.forMode(appearance, systemLight, chosen)
        fun c(key: String) = paletteColor(palette.getValue(key))
        val base = if (AppPalette.isLight(appearance, systemLight)) lightColorScheme() else darkColorScheme()
        base.copy(
            primary = c("Accent"), onPrimary = c("OnAccent"),
            primaryContainer = c("Selected"), onPrimaryContainer = c("Text"),
            secondary = c("Accent"), onSecondary = c("OnAccent"),
            secondaryContainer = c("Selected"), onSecondaryContainer = c("Text"),
            tertiary = c("Accent"), onTertiary = c("OnAccent"),
            background = c("Ground"), onBackground = c("Text"),
            surface = c("Surface"), onSurface = c("Text"),
            surfaceVariant = c("Raised"), onSurfaceVariant = c("MutedText"),
            surfaceContainerLowest = c("GroundDeep"), surfaceContainerLow = c("Ground"), surfaceContainer = c("Surface"),
            surfaceContainerHigh = c("Raised"), surfaceContainerHighest = c("RaisedHover"),
            // ErrorText y no DangerStrong: Material usa «error» para texto, iconos y bordes, y DangerStrong es un relleno que se apaga en el tema oscuro.
            error = c("ErrorText"), onError = c("OnDanger"),
            errorContainer = c("DangerBg"), onErrorContainer = c("OnDangerBg"),
            outline = c("Border"), outlineVariant = c("BorderSubtle"),
            // Los avisos (Snackbar) usan el esquema «inverso»; sin esto salían con el morado por defecto de Material.
            inverseSurface = c("RaisedHover"), inverseOnSurface = c("Text"), inversePrimary = c("Accent"),
        )
    }
    MaterialTheme(colorScheme = scheme, typography = typographyFor(appearance), content = content)
}

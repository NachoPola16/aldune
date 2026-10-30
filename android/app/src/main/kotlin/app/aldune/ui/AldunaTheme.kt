package app.aldune.ui

import android.graphics.Color as AndroidColor
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.RectangleShape
import androidx.compose.ui.unit.dp
import app.aldune.text.AppPalette
import app.aldune.text.AspectCatalog
import app.aldune.text.AspectSkin

/** Cómo se elige el aspecto: los números son los de `AppearanceMode` de Core (Oscuro 0, Claro 1, Sistema 2, Pastel 3, Medianoche 4, XP claro 5, XP oscuro 6, Telecom claro 7, Telecom oscuro 8, Bash 9, Fósforo 10). */
const val APPEARANCE_SYSTEM = AppPalette.SYSTEM

/** `#RRGGBB` o `#AARRGGBB` (alfa primero) de la paleta → color de Compose. */
fun paletteColor(hex: String): Color = Color(AndroidColor.parseColor(hex))

/** Cómo se pintan las notas según los ajustes: color único, esquinas y señal de sync (ya resueltos contra el aspecto). */
data class NoteLook(val uniformColor: String? = null, val squareCorners: Boolean = false, val syncSignal: Boolean = false, val fillColor: Boolean = true, val squareNewButton: Boolean = false,
    /** Paleta del aspecto Telecom (canales CH1–CH4); `null` en los demás aspectos. La rellena [AldunaTheme]. */
    val telecom: Map<String, String>? = null,
    /** La retícula de osciloscopio de fondo (solo Telecom oscuro). */
    val telecomGrid: Boolean = false,
    /** El papel de las libretas bajo el texto; `NONE` en los demás aspectos. */
    val paper: AspectSkin.Paper = AspectSkin.Paper.NONE,
) {
    /** El radio de una tarjeta de nota (12 dp de siempre, recto con «esquinas rectas»). */
    val cardShape get() = if (squareCorners) RectangleShape else RoundedCornerShape(12.dp)
}

val LocalNoteLook = staticCompositionLocalOf { NoteLook() }

/**
 * El aspecto de Aldune: la paleta de Core (`AppPalette`, ya portada y fijada por vectores) traducida al esquema de Material 3. Nada de
 * Material You (decisión del usuario): los colores son los de la app, no los del fondo de pantalla.
 */
@Composable
fun AldunaTheme(appearance: Int = APPEARANCE_SYSTEM, aspectColors: Map<String, Map<String, String>> = emptyMap(), look: NoteLook = NoteLook(), content: @Composable () -> Unit) {
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
    // Esquinas rectas: todas las formas de Material (tarjetas, diálogos, botones, campos) a cero, no solo las de las notas.
    val shapes = if (look.squareCorners) RoundedCornerShape(0.dp).let { Shapes(it, it, it, it, it) } else MaterialTheme.shapes
    val telecom = if (appearance == AppPalette.TELECOM_LIGHT || appearance == AppPalette.TELECOM_DARK) {
        remember(appearance, chosen) { AppPalette.forMode(appearance, systemLight, chosen) }
    } else null
    CompositionLocalProvider(LocalNoteLook provides look.copy(telecom = telecom, telecomGrid = appearance == AppPalette.TELECOM_DARK, paper = AspectSkin.paper(appearance))) {
        MaterialTheme(colorScheme = scheme, typography = remember(appearance) { typographyFor(appearance) }, shapes = shapes, content = content)
    }
}

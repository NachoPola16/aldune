package app.aldune.widget

import android.content.Context
import android.content.Intent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.unit.DpSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.glance.ColorFilter
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.GlanceTheme
import androidx.glance.Image
import androidx.glance.ImageProvider
import androidx.glance.LocalContext
import androidx.glance.LocalSize
import androidx.glance.action.clickable
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.SizeMode
import androidx.glance.appwidget.action.actionStartActivity
import androidx.glance.appwidget.cornerRadius
import androidx.glance.appwidget.lazy.GridCells
import androidx.glance.appwidget.lazy.LazyColumn
import androidx.glance.appwidget.lazy.LazyVerticalGrid
import androidx.glance.appwidget.lazy.items
import androidx.glance.appwidget.provideContent
import androidx.glance.background
import androidx.glance.color.ColorProvider as DayNightColorProvider
import androidx.glance.layout.Alignment
import androidx.glance.layout.Box
import androidx.glance.layout.Column
import androidx.glance.layout.Row
import androidx.glance.layout.fillMaxSize
import androidx.glance.layout.fillMaxWidth
import androidx.glance.layout.height
import androidx.glance.layout.padding
import androidx.glance.layout.size
import androidx.glance.text.FontFamily
import androidx.glance.text.FontWeight
import androidx.glance.text.Text
import androidx.glance.text.TextStyle
import androidx.glance.unit.ColorProvider
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.R
import app.aldune.capture.CaptureIntents
import app.aldune.data.Languages
import app.aldune.data.SettingsStore
import app.aldune.text.NoteDisplayColor
import app.aldune.domain.NoteState
import app.aldune.domain.WidgetContent
import app.aldune.domain.WidgetContentBuilder
import app.aldune.domain.WidgetOptions
import app.aldune.domain.WidgetStrip
import app.aldune.text.AppPalette
import app.aldune.text.AspectCatalog
import app.aldune.text.ColorFit
import app.aldune.ui.isMonospaceAspect
import app.aldune.ui.noteFace
import app.aldune.ui.noteInk
import app.aldune.ui.paletteColor
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/** Los textos del widget ya en el idioma elegido en la app (el widget no pasa por `attachBaseContext`). */
private class WidgetTexts(
    val appName: String, val newNote: String, val empty: String, val protectedTitle: String, val unavailable: String, val moreFormat: String,
)

private class WidgetColors(
    val ground: ColorProvider, val text: ColorProvider, val muted: ColorProvider, val surface: ColorProvider,
    /** El título y el «+»: el acento en los aspectos retro (si no, Bash y Oscuro se verían casi iguales), y los de siempre en el resto. */
    val title: ColorProvider, val plus: ColorProvider,
    /** Monoespaciada en Bash, Telecomunicaciones y Fósforo, como en la app; `null` es la de siempre. */
    val font: FontFamily?,
)

/** Qué se pinta: el contenido, o nada si la base no se pudo abrir (clave perdida). */
private class WidgetData(val content: WidgetContent?, val texts: WidgetTexts, val colors: WidgetColors, val showPreview: Boolean,
    val uniformColor: String? = null, val squareCorners: Boolean = false,
)

/**
 * El widget del mazo (spec de F6 §2.3): las notas activas en el orden del mazo, como tiras de color con su título, y un «+» para nota nueva.
 * Lo pinta el proceso de la app, que descifra con la clave del Keystore: no hay ninguna copia en claro en disco. Qué notas entran y cómo
 * (una protegida solo da su color, y no puede dar texto por tipo) lo decide `WidgetContentBuilder`, que se prueba en la JVM.
 */
class DockWidget : GlanceAppWidget() {
    // Una columna, dos o tres según el ancho que le dé el usuario (móvil estrecho, móvil ancho, tableta).
    override val sizeMode = SizeMode.Responsive(setOf(NARROW, MEDIUM, WIDE))

    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val firstVersion = WidgetUpdater.version.value
        val first = withContext(Dispatchers.IO) { load(context) }
        provideContent {
            // Glance reutiliza la sesión de un widget mientras sigue viva y NO vuelve a llamar a provideGlance: lo cargado arriba se
            // quedaría viejo. Por eso el widget recarga aquí dentro cada vez que el WidgetUpdater sube la versión.
            val version by WidgetUpdater.version.collectAsState()
            var shown by remember { mutableStateOf(first) }
            var shownVersion by remember { mutableLongStateOf(firstVersion) }
            LaunchedEffect(version) {
                if (version != shownVersion) {
                    shown = withContext(Dispatchers.IO) { load(context) }
                    shownVersion = version
                }
            }
            GlanceTheme { Body(shown) }
        }
    }

    private fun load(context: Context): WidgetData {
        val settings = SettingsStore(context).load()
        val localized = Languages.wrap(context, settings.language)
        val texts = WidgetTexts(
            appName = localized.getString(R.string.app_name), newNote = localized.getString(R.string.new_note),
            empty = localized.getString(R.string.widget_empty), protectedTitle = localized.getString(R.string.protected_note_title),
            unavailable = localized.getString(R.string.widget_unavailable), moreFormat = localized.getString(R.string.widget_more),
        )
        val content = (context.applicationContext as AldunaApp).container.open().let { opened ->
            (opened as? AppContainer.Opened.Ready)?.let { ready ->
                WidgetContentBuilder.build(
                    ready.notes.list(NoteState.ACTIVE),
                    WidgetOptions(placeholder = localized.getString(R.string.new_note_title), showPreview = settings.widgetPreview),
                )
            }
        }
        return WidgetData(content, texts, colors(settings.appearance, settings.aspectColors), settings.widgetPreview,
            settings.uniformNoteColor, settings.cornersSquare)
    }

    /** Con «seguir el sistema» el widget cambia solo de claro a oscuro; con un aspecto elegido se queda en ese. */
    private fun colors(appearance: Int, aspectColors: Map<String, Map<String, String>>): WidgetColors {
        fun pick(key: String): ColorProvider =
            if (appearance == AppPalette.SYSTEM) {
                DayNightColorProvider(day = paletteColor(AppPalette.light.getValue(key)), night = paletteColor(AppPalette.dark.getValue(key)))
            } else {
                ColorProvider(paletteColor(AppPalette.forMode(appearance, systemUsesLight = false, AspectCatalog.forMode(appearance)?.let { aspectColors[it.id] }).getValue(key)))
            }
        val retro = AspectCatalog.forMode(appearance) != null
        // El acento no está calculado contra el fondo (lo está contra el texto del botón): se acerca al fondo lo justo para leerse como texto.
        val accent = if (retro) {
            val palette = AppPalette.forMode(appearance, systemUsesLight = false, AspectCatalog.forMode(appearance)?.let { aspectColors[it.id] })
            ColorProvider(paletteColor(ColorFit.foreground(palette.getValue("Accent"), palette.getValue("Ground"), 4.5)))
        } else null
        return WidgetColors(
            ground = pick("Ground"), text = pick("Text"), muted = pick("MutedText"), surface = pick("Surface"),
            title = accent ?: pick("MutedText"), plus = accent ?: pick("Text"),
            font = if (isMonospaceAspect(appearance)) FontFamily.Monospace else null,
        )
    }

    @Composable
    private fun Body(data: WidgetData) {
        val context = LocalContext.current
        val columns = when {
            LocalSize.current.width >= WIDE.width -> 3
            LocalSize.current.width >= MEDIUM.width -> 2
            else -> 1
        }
        val openApp = actionStartActivity(Intent(context, app.aldune.MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        Column(GlanceModifier.fillMaxSize().background(data.colors.ground).cornerRadius(if (data.squareCorners) 0.dp else 20.dp).padding(8.dp)) {
            Row(GlanceModifier.fillMaxWidth().padding(start = 6.dp, end = 2.dp, bottom = 6.dp), verticalAlignment = Alignment.CenterVertically) {
                Text(
                    data.texts.appName, GlanceModifier.defaultWeight().clickable(openApp),
                    style = TextStyle(color = data.colors.title, fontSize = 13.sp, fontWeight = FontWeight.Medium, fontFamily = data.colors.font),
                )
                Image(
                    provider = ImageProvider(R.drawable.ic_new_note), contentDescription = data.texts.newNote,
                    colorFilter = ColorFilter.tint(data.colors.plus),
                    modifier = GlanceModifier.size(28.dp).clickable(actionStartActivity(CaptureIntents.newNote(context))),
                )
            }
            val content = data.content
            when {
                content == null -> Message(data.texts.unavailable, data.colors, openApp)
                content.strips.isEmpty() -> Message(data.texts.empty, data.colors, null)
                columns == 1 -> LazyColumn {
                    items(content.strips, itemId = { it.id.hashCode().toLong() }) { Strip(it, data) }
                    if (content.hiddenCount > 0) item { More(content.hiddenCount, data, openApp) }
                }
                else -> LazyVerticalGrid(gridCells = GridCells.Fixed(columns)) {
                    items(content.strips, itemId = { it.id.hashCode().toLong() }) { Strip(it, data) }
                    if (content.hiddenCount > 0) item { More(content.hiddenCount, data, openApp) }
                }
            }
        }
    }

    /** La última fila cuando hay más notas activas de las que caben (tope de seguridad del widget): abre la app. */
    @Composable
    private fun More(hidden: Int, data: WidgetData, openApp: androidx.glance.action.Action) {
        Box(GlanceModifier.fillMaxWidth().padding(6.dp).clickable(openApp)) {
            Text(String.format(data.texts.moreFormat, hidden), style = TextStyle(color = data.colors.muted, fontSize = 12.sp, fontFamily = data.colors.font))
        }
    }

    @Composable
    private fun Message(text: String, colors: WidgetColors, action: androidx.glance.action.Action?) {
        Box(GlanceModifier.fillMaxSize().let { if (action != null) it.clickable(action) else it }, contentAlignment = Alignment.Center) {
            Text(text, style = TextStyle(color = colors.muted, fontSize = 14.sp, fontFamily = colors.font))
        }
    }

    @Composable
    private fun Strip(strip: WidgetStrip, data: WidgetData) {
        val context = LocalContext.current
        val open = actionStartActivity(CaptureIntents.openNote(context, strip.id))
        // «Mismo color en todas las notas» también en el widget: solo cambia la presentación, la nota conserva el suyo.
        val shown = NoteDisplayColor.resolve(strip.color, data.uniformColor)
        val face = noteFace(shown)
        val ink = noteInk(shown)
        Box(GlanceModifier.fillMaxWidth().padding(2.dp)) {
            // Altura fija y la misma para todas: el GridView de un widget dimensiona todas las filas con la primera, y una tira con vista previa
            // quedaría recortada. Con el ajuste de vista previa las tiras son más altas, tengan o no segunda línea.
            val height = if (data.showPreview) STRIP_HEIGHT_PREVIEW else STRIP_HEIGHT
            Column(
                GlanceModifier.fillMaxWidth().height(height).background(face).cornerRadius(if (data.squareCorners) 0.dp else 12.dp).padding(horizontal = 10.dp, vertical = 8.dp).clickable(open),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                when (strip) {
                    // Una protegida solo enseña el candado y el texto fijo: `Locked` no tiene ningún texto suyo que pintar.
                    is WidgetStrip.Locked -> Text(
                        "🔒  " + data.texts.protectedTitle, style = TextStyle(color = ColorProvider(ink), fontSize = 14.sp, fontFamily = data.colors.font), maxLines = 1,
                    )
                    is WidgetStrip.Open -> {
                        Text(strip.title, style = TextStyle(color = ColorProvider(ink), fontSize = 14.sp, fontWeight = FontWeight.Medium, fontFamily = data.colors.font), maxLines = 1)
                        strip.preview?.let {
                            Text(it, style = TextStyle(color = ColorProvider(ink.copy(alpha = 0.75f)), fontSize = 12.sp, fontFamily = data.colors.font), maxLines = 2)
                        }
                    }
                }
            }
        }
    }

    private companion object {
        val STRIP_HEIGHT = 40.dp
        val STRIP_HEIGHT_PREVIEW = 66.dp
        val NARROW = DpSize(110.dp, 110.dp)
        val MEDIUM = DpSize(300.dp, 110.dp)
        val WIDE = DpSize(480.dp, 110.dp)
    }
}

package app.aldune.ui

import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.CompositingStrategy
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/** The height of the fade and, in the scrolling content above it, the padding that keeps the last item clear of it. */
internal val EDGE_FADE = 20.dp

/**
 * The content thins out over the last [height] instead of being cut in a straight line by whatever sits below it (the bar of the list, the
 * system bar). A transparency mask, not a coloured overlay, so it works on every look and on paper textures. Put it BEFORE the scroll modifier
 * (it must wrap the viewport), and give the scrolling content at least [height] of bottom padding: at the end of the list that strip is empty,
 * so the last item is never dimmed.
 */
internal fun Modifier.fadeBottomEdge(height: Dp = EDGE_FADE): Modifier = this
    .graphicsLayer { compositingStrategy = CompositingStrategy.Offscreen }
    .drawWithContent {
        drawContent()
        val fade = height.toPx()
        drawRect(
            brush = Brush.verticalGradient(listOf(Color.Black, Color.Transparent), startY = size.height - fade, endY = size.height),
            topLeft = Offset(0f, size.height - fade), size = Size(size.width, fade), blendMode = BlendMode.DstIn,
        )
    }

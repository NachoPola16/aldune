package app.aldune.text

/**
 * Where the ink of a task box (☐/☒, drawn by the app's own checkbox fonts) sits on its line. The box hangs from the baseline at a fixed
 * share of the text size, whatever the note font is, while the height of a line changes with the font, the look (ruled paper) and the
 * system's font padding. So what has to share the centre of the box, like the drag handle, starts from the baseline of the first visual
 * line and not from the middle of the line. Same idea as `TaskBoxMetrics` in Aldune.Core (the numbers differ: the fonts are placed differently).
 */
object TaskBoxMetrics {
    // Centre of the box above the baseline, in ems of the note text. Normal: the box is as tall as the capitals (0.71 em) and stands on the
    // baseline. Large: measured on the emulator (see TaskBoxMetricsTest).
    private const val NORMAL_CENTRE_EM = 0.357f
    private const val LARGE_CENTRE_EM = 0.345f

    /** Centre of the box, in the same coordinates as [baseline] (the baseline of the first visual line of the task). */
    fun centreY(baseline: Float, fontSize: Float, large: Boolean): Float =
        baseline - fontSize * (if (large) LARGE_CENTRE_EM else NORMAL_CENTRE_EM)
}

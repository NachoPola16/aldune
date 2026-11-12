namespace Aldune.Core;

/// <summary>
/// Where the ink of a task box (☐/☒, drawn by the fonts AlduneCheck and AlduneCheckLarge) sits on its line. The box hangs from the
/// baseline of the note font at a fixed share of the text size, whatever that font is; the height of the line is the note font's own and
/// varies a lot (Gabriola: 23.8 px for 14 px text). So anything that has to share the centre of the box, like the drag grip, must start
/// from the baseline: a share of the line height was only right for the font it was measured with.
/// </summary>
public static class TaskBoxMetrics
{
    // Centre of the box above the baseline, in ems. Measured on the real window at 4x with eight fonts (see TaskBoxMetricsTests); they
    // change only if the glyphs move in scripts/branding/generar-fuente-check.py.
    private const double NormalCentreEm = 0.309;
    private const double LargeCentreEm = 0.324;

    /// <summary>
    /// Centre of the box measured from the top of its line. <paramref name="baseline"/> is the distance from that top down to the baseline
    /// of the note font. NaN when the metrics cannot be used (a font that reports none), so the caller keeps its own fallback.
    /// </summary>
    public static double CentreFromLineTop(double baseline, double fontSize, bool large)
    {
        if (!double.IsFinite(baseline) || !double.IsFinite(fontSize) || baseline <= 0 || fontSize <= 0) return double.NaN;
        return baseline - fontSize * (large ? LargeCentreEm : NormalCentreEm);
    }
}

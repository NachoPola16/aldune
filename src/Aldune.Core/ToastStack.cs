namespace Aldune.Core;

/// <summary>
/// Dónde va cada aviso de la app (actualización, recordatorio, bienvenida) en la esquina inferior
/// derecha de una pantalla, como los de Windows: el más reciente abajo, junto a la bandeja, y los
/// anteriores suben. Los que ya no caben esperan fuera de la vista (null) hasta que se cierre alguno.
/// </summary>
public static class ToastStack
{
    public const double Margin = 16;
    public const double Gap = 10;

    /// <param name="heights">Alto de cada aviso, en orden de llegada (el primero, el más antiguo).</param>
    public static IReadOnlyList<(double Left, double Top)?> Place(WorkingArea area, double width, IReadOnlyList<double> heights)
    {
        var places = new (double Left, double Top)?[heights.Count];
        double left = area.X + area.Width - Margin - width;
        double bottom = area.Y + area.Height - Margin;

        for (int index = heights.Count - 1; index >= 0; index--)
        {
            double top = bottom - heights[index];
            if (top < area.Y + Margin) break; // este y los más antiguos no caben
            places[index] = (left, top);
            bottom = top - Gap;
        }

        return places;
    }
}

namespace Aldune.Core;

public static class PlacementValidation
{
    /// <summary>
    /// Comprueba si al menos una porci�n razonable (50x50 p�xeles) del rect�ngulo de la ventana
    /// cae dentro del �rea de trabajo de alguno de los monitores conectados, para evitar
    /// que una nota reaparezca fuera de la vista si se ha desconectado una pantalla.
    /// </summary>
    public static bool IsVisibleOnMonitors(double left, double top, double width, double height, IEnumerable<MonitorInfo> monitors)
    {
        foreach (var m in monitors)
        {
            var area = m.WorkArea;
            double overlapX = Math.Max(0, Math.Min(left + width, area.X + area.Width) - Math.Max(left, area.X));
            double overlapY = Math.Max(0, Math.Min(top + height, area.Y + area.Height) - Math.Max(top, area.Y));
            if (overlapX >= 50 && overlapY >= 50) return true;
        }
        return false;
    }
}

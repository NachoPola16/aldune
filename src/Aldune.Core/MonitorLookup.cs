namespace Aldune.Core;

public static class MonitorLookup
{
    /// <summary>
    /// El <see cref="MonitorInfo.DeviceName"/> del monitor cuya área de trabajo contiene el centro
    /// del rectángulo dado, o <c>null</c> si no cae en ninguno de los monitores conectados. Se usa
    /// para guardar la posición de una nota "por pantalla": el centro, y no una esquina, porque una
    /// ventana a caballo entre dos monitores debe contarse en aquel donde vive la mayor parte de
    /// ella.
    /// </summary>
    public static string? DeviceNameAt(double left, double top, double width, double height, IEnumerable<MonitorInfo> monitors) =>
        MonitorAt(left, top, width, height, monitors)?.DeviceName;

    /// <summary>Lo mismo que <see cref="DeviceNameAt"/> pero devuelve el monitor entero, no solo su
    /// nombre — para quien necesita su <see cref="MonitorInfo.WorkArea"/> (p. ej. el tope de
    /// crecimiento automático de <c>NoteWindow</c>), no solo identificarlo.</summary>
    public static MonitorInfo? MonitorAt(double left, double top, double width, double height, IEnumerable<MonitorInfo> monitors)
    {
        double centerX = left + width / 2;
        double centerY = top + height / 2;

        foreach (var monitor in monitors)
        {
            var area = monitor.WorkArea;
            if (centerX >= area.X && centerX < area.X + area.Width
                && centerY >= area.Y && centerY < area.Y + area.Height)
            {
                return monitor;
            }
        }

        return null;
    }

    /// <summary>
    /// El monitor con ese <see cref="MonitorInfo.DeviceName"/>, o <c>null</c> si ya no está conectado
    /// (p. ej. la pantalla elegida en un menú que se ha desenchufado antes del clic).
    /// </summary>
    public static MonitorInfo? ForDeviceName(string deviceName, IEnumerable<MonitorInfo> monitors)
    {
        foreach (var monitor in monitors)
        {
            if (monitor.DeviceName == deviceName) return monitor;
        }

        return null;
    }

    /// <summary>
    /// La pantalla a la que mandar una disposición de notas: la pedida en
    /// <paramref name="targetDeviceName"/> si se indicó una y sigue conectada, y si no la de
    /// <paramref name="fallbackDeviceName"/> (la del dock que pidió la acción). <c>null</c> solo si
    /// tampoco existe esa segunda.
    /// </summary>
    public static MonitorInfo? TargetOrFallback(
        string? targetDeviceName,
        string fallbackDeviceName,
        IEnumerable<MonitorInfo> monitors) =>
        targetDeviceName is not null
            ? ForDeviceName(targetDeviceName, monitors) ?? ForDeviceName(fallbackDeviceName, monitors)
            : ForDeviceName(fallbackDeviceName, monitors);
}

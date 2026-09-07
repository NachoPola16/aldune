namespace Fanote.Core;

public static class MonitorLookup
{
    /// <summary>
    /// El <see cref="MonitorInfo.DeviceName"/> del monitor cuya área de trabajo contiene el centro
    /// del rectángulo dado, o <c>null</c> si no cae en ninguno de los monitores conectados. Se usa
    /// para guardar la posición de una nota "por pantalla": el centro, y no una esquina, porque una
    /// ventana a caballo entre dos monitores debe contarse en aquel donde vive la mayor parte de
    /// ella.
    /// </summary>
    public static string? DeviceNameAt(double left, double top, double width, double height, IEnumerable<MonitorInfo> monitors)
    {
        double centerX = left + width / 2;
        double centerY = top + height / 2;

        foreach (var monitor in monitors)
        {
            var area = monitor.WorkArea;
            if (centerX >= area.X && centerX < area.X + area.Width
                && centerY >= area.Y && centerY < area.Y + area.Height)
            {
                return monitor.DeviceName;
            }
        }

        return null;
    }
}

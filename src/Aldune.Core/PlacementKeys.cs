namespace Aldune.Core;

/// <summary>
/// La clave con la que se guarda en disco dónde está una nota en cada pantalla. Va por
/// <see cref="MonitorInfo.StableId"/> y no por <see cref="MonitorInfo.DeviceName"/>: Windows renumera
/// los <c>\.\DISPLAYn</c> al apagar y encender pantallas, y con ellos la nota perdía su sitio y su
/// tamaño (o los heredaba de otra pantalla). Sin id estable se usa el nombre, como hasta ahora.
/// El resto de la app sigue localizando monitores por <c>DeviceName</c>: solo cambia lo persistido.
/// </summary>
public static class PlacementKeys
{
    public static string StorageKey(MonitorInfo monitor) => monitor.StableId ?? monitor.DeviceName;

    /// <summary>Claves a probar al leer, por orden: la estable y, para lo guardado antes de la 1.7.9, el nombre.</summary>
    public static IReadOnlyList<string> LookupKeys(MonitorInfo monitor) =>
        LegacyKey(monitor) is { } legacy ? [StorageKey(monitor), legacy] : [StorageKey(monitor)];

    /// <summary>La clave antigua (el nombre) si ya no es la que se usa para guardar; al guardar se borra.</summary>
    public static string? LegacyKey(MonitorInfo monitor) =>
        monitor.StableId is null ? null : monitor.DeviceName;
}

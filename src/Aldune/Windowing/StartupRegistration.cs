using Microsoft.Win32;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Arrancar Fanote al iniciar sesión, vía la clave <c>Run</c> del usuario.
///
/// Se usa <c>HKEY_CURRENT_USER</c> y no la de máquina a propósito: no requiere permisos de
/// administrador, y una app de notas personales no tiene por qué arrancar para todos los usuarios
/// del equipo. Es además la clave que el propio Windows enseña y deja desactivar en
/// Administrador de tareas &gt; Inicio, así que el usuario siempre tiene una segunda forma de
/// quitarlo aunque nunca vuelva a abrir el menú de la bandeja.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = BrandIdentity.StartupRegistryKey;
    private const string LegacyValueName = BrandIdentity.LegacyStartupRegistryKey;

    /// <summary>
    /// Ruta del ejecutable, entrecomillada.
    ///
    /// Las comillas no son cosmética: sin ellas, una ruta con espacios (y
    /// <c>C:\Program Files\...</c> los tiene) haría que Windows intentara ejecutar el primer
    /// trozo y pasara el resto como argumentos.
    /// </summary>
    internal static string? CommandLine()
    {
        // ProcessPath da el .exe real que está corriendo. Assembly.Location apunta al .dll en
        // .NET moderno, que no es lanzable.
        var path = Environment.ProcessPath;
        return string.IsNullOrEmpty(path) ? null : $"\"{path}\"";
    }

    internal static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key is null) return false;

            if (key.GetValue(ValueName) is null && key.GetValue(LegacyValueName) is not null &&
                string.Equals(System.IO.Path.GetFileName(Environment.ProcessPath), "aldune.exe", StringComparison.OrdinalIgnoreCase))
            {
                key.Close();
                using var writableKey = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (writableKey is not null)
                {
                    writableKey.SetValue(ValueName, CommandLine()!);
                    writableKey.DeleteValue(LegacyValueName, throwOnMissingValue: false);
                }
            }

            using var currentKey = Registry.CurrentUser.OpenSubKey(RunKey);
            return currentKey?.GetValue(ValueName) is not null || currentKey?.GetValue(LegacyValueName) is not null;
        }
        catch (Exception)
        {
            // El registro puede estar restringido por directiva de grupo. No poder leerlo no es
            // motivo para tumbar la app: se informa como "desactivado" y el menú lo reflejará.
            return false;
        }
    }

    /// <summary>Activa o desactiva el arranque. Devuelve el estado real tras intentarlo.</summary>
    internal static bool SetEnabled(bool enabled)
    {
        var command = CommandLine();
        if (enabled && command is null) return false;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return IsEnabled();

            if (enabled)
            {
                key.SetValue(ValueName, command!);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // Igual que arriba: sin permisos sobre la clave, se devuelve lo que haya de verdad en
            // vez de dejar el menú marcando algo que no ha pasado.
        }

        return IsEnabled();
    }
}

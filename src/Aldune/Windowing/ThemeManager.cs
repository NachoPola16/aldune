using System.Windows;
using System.Windows.Media;
using Aldune.Core;
using Microsoft.Win32;

namespace Aldune.Windowing;

/// <summary>
/// Pone la paleta del chrome (<see cref="AppPalette"/>) en los recursos de la aplicación como
/// pinceles <c>Aldune{Clave}Brush</c>. Las ventanas los usan con <c>DynamicResource</c>, así que
/// cambiar de modo repinta todo en vivo, sin reiniciar. Lo que se pinta desde código (bandeja,
/// iconos de los avisos) lee <see cref="Color"/> o se enlaza con <c>SetResourceReference</c>.
/// </summary>
public static class ThemeManager
{
    private static ResourceDictionary? _current;
    private static IReadOnlyDictionary<string, string> _palette = AppPalette.For(light: false);

    public static bool IsLight { get; private set; }

    public static event Action? Changed;

    /// <summary>Valor actual de una clave de la paleta, para quien pinta fuera de WPF.</summary>
    public static string Color(string token) => _palette[token];

    public static void Apply(Application app, bool light) =>
        Apply(app, light ? AppearanceMode.Light : AppearanceMode.Dark);

    public static void Apply(Application app, AppearanceMode mode)
    {
        bool windowsLight = WindowsUsesLightTheme();
        var palette = AppPalette.For(mode, windowsLight);
        var dictionary = new ResourceDictionary();
        foreach (var (token, hex) in palette)
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            dictionary[$"Aldune{token}Brush"] = brush;
        }

        if (_current is not null) app.Resources.MergedDictionaries.Remove(_current);
        app.Resources.MergedDictionaries.Add(dictionary);
        _current = dictionary;
        _palette = palette;
        IsLight = AppPalette.IsLight(mode, windowsLight);
        Changed?.Invoke();
    }

    /// <summary>"Modo de aplicación predeterminado" de Windows. Sin el valor (Windows antiguo o
    /// política que lo quita), oscuro: es el aspecto de siempre de Aldune.</summary>
    public static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

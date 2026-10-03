using System.Windows;
using System.Windows.Media;
using Aldune.Core;
using Aldune.Interop;
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

    private static ResourceDictionary? _shape;

    // Los radios que usa la app. Con esquinas rectas todos valen 0; si no, su valor. Recursos y no
    // números escritos en cada XAML para que el cambio se vea en vivo, como la paleta.
    private static readonly double[] Radii = [3, 4, 5, 6, 7, 8, 9, 10, 11, 27];

    public static bool IsSquare { get; private set; }

    public static void ApplyShape(Application app, bool square)
    {
        var dictionary = new ResourceDictionary();
        foreach (var radius in Radii)
            dictionary[$"AlduneRadius{radius}"] = new CornerRadius(square ? 0 : radius);
        // Pestaña pegada al borde derecho de la pantalla (el dock la espeja por código a la izquierda).
        dictionary["AlduneRadiusTabRight"] = square ? new CornerRadius(0) : new CornerRadius(10, 0, 0, 10);
        dictionary["AlduneRadiusEndRight"] = square ? new CornerRadius(0) : new CornerRadius(0, 5, 5, 0);

        if (_shape is not null) app.Resources.MergedDictionaries.Remove(_shape);
        app.Resources.MergedDictionaries.Add(dictionary);
        _shape = dictionary;
        IsSquare = square;
        NativeMethods.ReapplyCornerPreference();
        Changed?.Invoke();
    }

    /// <summary>Radio para quien lo fija por código (pestañas espejadas, paneles creados a mano).</summary>
    public static CornerRadius Radius(double uniform) => new(IsSquare ? 0 : uniform);

    public static CornerRadius Radius(double left, double top, double right, double bottom) =>
        IsSquare ? new CornerRadius(0) : new CornerRadius(left, top, right, bottom);

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

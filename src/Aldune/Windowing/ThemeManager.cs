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
    private static ResourceDictionary? _skinResources;
    private static IReadOnlyDictionary<string, string> _palette = AppPalette.For(light: false);

    public static bool IsLight { get; private set; }

    public static AppSkin Skin { get; private set; } = AppSkin.Default;

    /// <summary>Paleta activa, para quien calcula colores en Core (NoteFace).</summary>
    public static IReadOnlyDictionary<string, string> Palette => _palette;

    public static event Action? Changed;

    /// <summary>Valor actual de una clave de la paleta, para quien pinta fuera de WPF.</summary>
    public static string Color(string token) => _palette[token];

    public static void Apply(Application app, bool light) =>
        Apply(app, light ? AppearanceMode.Light : AppearanceMode.Dark);

    public static void Apply(Application app, AppearanceMode mode, IReadOnlyDictionary<string, string>? aspectColors = null)
    {
        bool windowsLight = WindowsUsesLightTheme();
        var palette = AppPalette.For(mode, windowsLight, aspectColors);
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
        // El degradado de la barra sale de la paleta: se rehace con ella. También deja las fuentes
        // puestas a quien solo llama a Apply.
        ApplySkin(app, Skin);
        IsLight = AppPalette.IsLight(mode, windowsLight);
        Changed?.Invoke();
    }

    /// <summary>
    /// Pone la piel en los recursos (fuentes, tamaño del título, fondo de la barra) y en
    /// <see cref="SkinState"/>. Quien la cambie con notas abiertas llama después a
    /// AppCoordinator.RefreshNoteAppearance: las caras de las notas y las pestañas se calculan al pintarse.
    /// </summary>
    public static void ApplySkin(Application app, AppSkin skin)
    {
        var dictionary = new ResourceDictionary
        {
            ["AldunePrimaryFont"] = // Vacío = la fuente de sistema, igual que la de la nota.
            string.IsNullOrEmpty(skin.ChromeFont) ? SystemFonts.MessageFontFamily : new FontFamily(skin.ChromeFont),
            ["AlduneNoteFont"] = // Vacío = la fuente de sistema (no un nombre fijo): así vale también fuera de Windows 11.
            string.IsNullOrEmpty(skin.NoteFont) ? SystemFonts.MessageFontFamily : new FontFamily(skin.NoteFont),
            ["AlduneNoteTitleFont"] = new FontFamily(skin.NoteTitleFont),
            ["AlduneNoteTitleFontSize"] = skin.NoteTitleFontSize,
            ["AlduneTitleBarFill"] = TitleBarFill(skin.TitleBar),
        };

        if (_skinResources is not null) app.Resources.MergedDictionaries.Remove(_skinResources);
        app.Resources.MergedDictionaries.Add(dictionary);
        _skinResources = dictionary;
        Skin = skin;
        SkinState.Current.Skin = skin;
        Changed?.Invoke();
    }

    // Plain es transparente, no el color de la barra: así las cabeceras de hoy (nota, Ajustes, gestor)
    // siguen pintando lo que tienen debajo, sea cual sea el fondo de cada ventana.
    private static Brush TitleBarFill(SkinTitleBar style)
    {
        var start = (System.Windows.Media.Color)ColorConverter.ConvertFromString(_palette["TitleBar"]);
        var end = (System.Windows.Media.Color)ColorConverter.ConvertFromString(_palette["TitleBarEnd"]);
        Brush brush = style switch
        {
            SkinTitleBar.GradientVertical => new LinearGradientBrush(start, end, 90),
            SkinTitleBar.GradientHorizontal => new LinearGradientBrush(start, end, 0),
            _ => Brushes.Transparent,
        };
        if (brush.CanFreeze) brush.Freeze();
        return brush;
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
        SkinState.Current.Square = square;
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

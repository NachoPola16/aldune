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

    /// <summary>Hay un tema de alto contraste de Windows activo y la paleta sale de sus colores de sistema.</summary>
    public static bool IsHighContrast { get; private set; }

    /// <summary>
    /// El aspecto con el que se pide la piel (fuentes, esquinas, bordes): en alto contraste los aspectos retro no valen, ni sus
    /// degradados ni sus biseles, y se usa la piel lisa de Oscuro o Claro según el fondo del sistema.
    /// </summary>
    public static AppearanceMode SkinMode(AppearanceMode mode) =>
        !SystemParameters.HighContrast ? mode
        : AppPalette.IsLightHighContrast(Hex(SystemColors.WindowColor)) ? AppearanceMode.Light : AppearanceMode.Dark;

    private static string Hex(System.Windows.Media.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static AppSkin Skin { get; private set; } = AppSkin.Default;

    /// <summary>Ajuste «Casillas grandes»: elige la fuente de las casillas y la zona de clic de NoteWindow.</summary>
    public static bool LargeCheckboxes { get; set; }

    /// <summary>Paleta activa, para quien calcula colores en Core (NoteFace).</summary>
    public static IReadOnlyDictionary<string, string> Palette => _palette;

    public static event Action? Changed;

    // Tamaños base de la escala tipográfica del chrome (los de App.xaml) y del cuerpo de la nota.
    private static readonly (string Key, double Size)[] TextSizes =
    [
        ("AlduneTextSmall", 11), ("AlduneTextCaption", 12), ("AlduneTextBody", 13), ("AlduneTextLarge", 14),
        ("AlduneTextTitle", 15), ("AlduneTextHeading", 17),
    ];

    private const double NoteBodyBaseSize = 14;

    /// <summary>«Tamaño del texto» activo, como multiplicador (1 = el de siempre).</summary>
    public static double TextFactor { get; private set; } = 1;

    /// <summary>
    /// Pone el tamaño del texto de Ajustes: reescribe los cuatro tamaños de la escala (que las ventanas leen con
    /// <c>DynamicResource</c>, así que se ven al instante) y los de la nota (cuerpo, título e interlineado, vía la piel).
    /// Redondea a píxeles enteros: el texto se renderiza en modo Display y un tamaño con fracción sale borroso.
    /// </summary>
    /// <summary>
    /// Agranda una ventana de tamaño fijo (Ajustes, gestor, diálogos) en la misma proporción que el texto, al crearla: un diálogo de
    /// 380 px con letra al 150 % partiría cada frase en tres. Sin pasarse de la pantalla. Las notas (que el usuario dimensiona), el dock
    /// (que calcula su geometría) y los avisos (que se ajustan solos) no pasan por aquí.
    /// </summary>
    internal static void ScaleWindowToText(Window window)
    {
        if (TextFactor <= 1.001) return;
        var work = SystemParameters.WorkArea;
        double Grow(double value, double limit) => double.IsNaN(value) || value <= 0 ? value : Math.Min(Math.Round(value * TextFactor), limit);
        window.Width = Grow(window.Width, work.Width * 0.9);
        window.MinWidth = Grow(window.MinWidth, work.Width * 0.9);
        // Con SizeToContent=Height el alto lo da el contenido: tocarlo aquí lo fijaría.
        if (window.SizeToContent is not (SizeToContent.Height or SizeToContent.WidthAndHeight))
            window.Height = Grow(window.Height, work.Height * 0.9);
        window.MinHeight = Grow(window.MinHeight, work.Height * 0.9);
    }

    public static void ApplyTextScale(Application app, int percent)
    {
        TextFactor = TextScale.Factor(percent);
        foreach (var (key, size) in TextSizes) app.Resources[key] = Math.Round(size * TextFactor);
        ApplySkin(app, Skin);
    }

    /// <summary>Valor actual de una clave de la paleta, para quien pinta fuera de WPF.</summary>
    public static string Color(string token) => _palette[token];

    public static void Apply(Application app, bool light) =>
        Apply(app, light ? AppearanceMode.Light : AppearanceMode.Dark);

    public static void Apply(Application app, AppearanceMode mode, IReadOnlyDictionary<string, string>? aspectColors = null)
    {
        bool windowsLight = WindowsUsesLightTheme();
        bool highContrast = SystemParameters.HighContrast;
        // Con un tema de alto contraste, los colores son los del sistema (fondo, texto, resaltado...), elija lo que elija Ajustes.
        var palette = highContrast
            ? AppPalette.HighContrast(Hex(SystemColors.WindowColor), Hex(SystemColors.WindowTextColor), Hex(SystemColors.HighlightColor),
                Hex(SystemColors.HighlightTextColor), Hex(SystemColors.GrayTextColor), Hex(SystemColors.HotTrackColor))
            : AppPalette.For(mode, windowsLight, aspectColors);
        var dictionary = new ResourceDictionary();
        foreach (var (token, hex) in palette)
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            dictionary[$"Aldune{token}Brush"] = brush;
        }

        // El anillo del foco se calcula de la paleta (no es una de sus claves): ver AppPalette.FocusRing.
        var ring = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(AppPalette.FocusRing(palette)));
        ring.Freeze();
        dictionary["AlduneFocusRingBrush"] = ring;

        if (_current is not null) app.Resources.MergedDictionaries.Remove(_current);
        app.Resources.MergedDictionaries.Add(dictionary);
        _current = dictionary;
        _palette = palette;
        // El degradado de la barra sale de la paleta: se rehace con ella. También deja las fuentes
        // puestas a quien solo llama a Apply.
        ApplySkin(app, Skin);
        IsHighContrast = highContrast;
        IsLight = highContrast ? AppPalette.IsLightHighContrast(Hex(SystemColors.WindowColor)) : AppPalette.IsLight(mode, windowsLight);
        Changed?.Invoke();
    }

    /// <summary>
    /// Pone la piel en los recursos (fuentes, tamaño del título, fondo de la barra) y en
    /// <see cref="SkinState"/>. Quien la cambie con notas abiertas llama después a
    /// AppCoordinator.RefreshNoteAppearance: las caras de las notas y las pestañas se calculan al pintarse.
    /// </summary>
    public static void ApplySkin(Application app, AppSkin skin)
    {
        var noteFont = string.IsNullOrEmpty(skin.NoteFont) ? SystemFonts.MessageFontFamily : new FontFamily(skin.NoteFont);
        var dictionary = new ResourceDictionary
        {
            ["AldunePrimaryFont"] = // Vacío = la fuente de sistema, igual que la de la nota.
            string.IsNullOrEmpty(skin.ChromeFont) ? SystemFonts.MessageFontFamily : new FontFamily(skin.ChromeFont),
            ["AlduneNoteFont"] = // Vacío = la fuente de sistema (no un nombre fijo): así vale también fuera de Windows 11.
            noteFont,
            // El cuerpo de la nota: la fuente de la nota y, de respaldo, AlduneCheck, que solo trae ☐ y ☒ (con el
            // tick). Va DETRÁS para que el interlineado lo marque la fuente de la nota, no ella.
            ["AlduneNoteBodyFont"] = new FontFamily(new Uri("pack://application:,,,/aldune;component/"), $"{noteFont.Source}, ./Assets/#{(LargeCheckboxes ? "AlduneCheckLarge" : "AlduneCheck")}"),
            ["AlduneNoteTitleFont"] = new FontFamily(skin.NoteTitleFont),
            ["AlduneNoteTitleFontSize"] = Math.Round(skin.NoteTitleFontSize * TextFactor),
            ["AlduneNoteBodySize"] = Math.Round(NoteBodyBaseSize * TextFactor),
            // NaN = el interlineado natural de la fuente (lo de siempre).
            ["AlduneNoteLineHeight"] = skin.NoteLineHeight > 0 ? Math.Round(skin.NoteLineHeight * TextFactor) : double.NaN,
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

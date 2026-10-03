using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// Relieve de Windows 95 sobre una pieza (tarjeta, cápsula, pie, botón, ventana): claro arriba e
/// izquierda, oscuro abajo y derecha (al revés si está hundida). Solo pinta con la piel de bordes
/// <c>Bevel</c>; con la de siempre no dibuja nada. Va encima de la pieza y no recibe el ratón. Un
/// elemento propio y no dos Border superpuestos: un Border tiene un solo color de borde.
/// </summary>
public sealed class BevelEdge : FrameworkElement
{
    public static readonly DependencyProperty SunkenProperty = DependencyProperty.Register(
        nameof(Sunken), typeof(bool), typeof(BevelEdge), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Para piezas redondas (los botones del pie): el relieve recto solo cuadra cuando son cuadradas.</summary>
    public static readonly DependencyProperty OnlyWhenSquareProperty = DependencyProperty.Register(
        nameof(OnlyWhenSquare), typeof(bool), typeof(BevelEdge), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EdgeThicknessProperty = DependencyProperty.Register(
        nameof(EdgeThickness), typeof(double), typeof(BevelEdge), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool Sunken { get => (bool)GetValue(SunkenProperty); set => SetValue(SunkenProperty, value); }
    public bool OnlyWhenSquare { get => (bool)GetValue(OnlyWhenSquareProperty); set => SetValue(OnlyWhenSquareProperty, value); }
    public double EdgeThickness { get => (double)GetValue(EdgeThicknessProperty); set => SetValue(EdgeThicknessProperty, value); }

    public BevelEdge()
    {
        IsHitTestVisible = false;
        // Suscripción solo mientras está en pantalla: SkinState vive toda la app y retendría cada
        // pestaña que se ha pintado alguna vez.
        Loaded += (_, _) =>
        {
            SkinState.Current.PropertyChanged += OnSkinChanged;
            ThemeManager.Changed += InvalidateVisual;
        };
        Unloaded += (_, _) =>
        {
            SkinState.Current.PropertyChanged -= OnSkinChanged;
            ThemeManager.Changed -= InvalidateVisual;
        };
    }

    private void OnSkinChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var state = SkinState.Current;
        if (!state.Bevel || (OnlyWhenSquare && !state.Square)) return;
        if (TryFindResource("AlduneBevelLightBrush") is not Brush light ||
            TryFindResource("AlduneBevelDarkBrush") is not Brush dark) return;
        if (Sunken) (light, dark) = (dark, light);

        double t = EdgeThickness, w = ActualWidth, h = ActualHeight;
        if (w < 2 * t || h < 2 * t) return;
        dc.DrawRectangle(light, null, new Rect(0, 0, w, t));
        dc.DrawRectangle(light, null, new Rect(0, 0, t, h));
        dc.DrawRectangle(dark, null, new Rect(0, h - t, w, t));
        dc.DrawRectangle(dark, null, new Rect(w - t, 0, t, h));
    }
}

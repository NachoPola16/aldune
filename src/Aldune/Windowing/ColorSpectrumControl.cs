using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// A compact HSV spectrum: saturation and value live in the square, hue in the strip at its right.
/// It keeps the custom-color flow visual without bringing back the unrelated native color dialog.
/// </summary>
public sealed class ColorSpectrumControl : FrameworkElement
{
    private const double HueStripWidth = 22;
    private const double Gap = 8;
    private Color _selectedColor = Color.FromRgb(245, 227, 179);
    private double _hue;
    private double _saturation;
    private double _value;
    private bool _draggingSpectrum;
    private bool _draggingHue;

    public event EventHandler? ColorChanged;

    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            _selectedColor = value;
            RgbToHsv(value, out _hue, out _saturation, out _value);
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double squareWidth = Math.Max(1, ActualWidth - HueStripWidth - Gap);
        double height = Math.Max(1, ActualHeight);
        var hueColor = HsvToColor(_hue, 1, 1);

        var saturationBrush = new LinearGradientBrush(
            Color.FromRgb(247, 241, 232), hueColor, new Point(0, 0.5), new Point(1, 0.5));
        drawingContext.DrawRectangle(saturationBrush, null, new Rect(0, 0, squareWidth, height));

        var valueBrush = new LinearGradientBrush(
            Colors.Transparent, Color.FromRgb(30, 26, 20), new Point(0.5, 0), new Point(0.5, 1));
        drawingContext.DrawRectangle(valueBrush, null, new Rect(0, 0, squareWidth, height));

        var hueBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(225, 94, 80), 0));
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(232, 194, 83), 0.17));
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(116, 197, 111), 0.34));
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(91, 198, 202), 0.51));
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(104, 126, 207), 0.68));
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(201, 103, 190), 0.84));
        hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(225, 94, 80), 1));
        var hueRect = new Rect(squareWidth + Gap, 0, HueStripWidth, height);
        drawingContext.DrawRoundedRectangle(hueBrush, null, hueRect, 5, 5);

        var squareBorder = new Pen(new SolidColorBrush(Color.FromRgb(90, 81, 70)), 1);
        drawingContext.DrawRoundedRectangle(null, squareBorder, new Rect(0, 0, squareWidth, height), 7, 7);
        drawingContext.DrawRoundedRectangle(null, squareBorder, hueRect, 5, 5);

        var selectorCenter = new Point(
            Math.Clamp(_saturation * squareWidth, 7, Math.Max(7, squareWidth - 7)),
            Math.Clamp((1 - _value) * height, 7, Math.Max(7, height - 7)));
        drawingContext.DrawEllipse(new SolidColorBrush(_selectedColor),
            new Pen(new SolidColorBrush(Color.FromRgb(247, 241, 232)), 2), selectorCenter, 6, 6);

        double hueY = Math.Clamp(_hue / 360 * height, 3, Math.Max(3, height - 3));
        drawingContext.DrawRoundedRectangle(null,
            new Pen(new SolidColorBrush(Color.FromRgb(247, 241, 232)), 2),
            new Rect(squareWidth + Gap - 2, hueY - 3, HueStripWidth + 4, 6), 4, 4);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        UpdateFromPoint(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingSpectrum && !_draggingHue) return;
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _draggingSpectrum = false;
        _draggingHue = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateFromPoint(Point point)
    {
        double squareWidth = Math.Max(1, ActualWidth - HueStripWidth - Gap);
        if (point.X > squareWidth + Gap)
        {
            _draggingHue = true;
            _hue = Math.Clamp(point.Y / Math.Max(1, ActualHeight), 0, 1) * 360;
        }
        else
        {
            _draggingSpectrum = true;
            _saturation = Math.Clamp(point.X / squareWidth, 0, 1);
            _value = 1 - Math.Clamp(point.Y / Math.Max(1, ActualHeight), 0, 1);
        }

        _selectedColor = HsvToColor(_hue, _saturation, _value);
        InvalidateVisual();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        value = max;
        saturation = max <= 0 ? 0 : delta / max;
        if (delta <= 0.00001)
        {
            hue = 0;
            return;
        }

        hue = max == r
            ? 60 * (((g - b) / delta) % 6)
            : max == g
                ? 60 * (((b - r) / delta) + 2)
                : 60 * (((r - g) / delta) + 4);
        if (hue < 0) hue += 360;
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        double match = value - chroma;
        (double r, double g, double b) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return Color.FromRgb(
            (byte)Math.Round((r + match) * 255),
            (byte)Math.Round((g + match) * 255),
            (byte)Math.Round((b + match) * 255));
    }
}

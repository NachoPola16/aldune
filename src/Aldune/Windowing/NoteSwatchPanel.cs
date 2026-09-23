using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Las pastillas de color de un tema: una fila de oscuros y otra de claros. Las usan el menú "⋯" de
/// la nota, el menú de la pestaña del dock y Ajustes; antes había dos copias casi iguales, cada una
/// con los seis colores fijos.
/// </summary>
internal static class NoteSwatchPanel
{
    private const double SwatchSize = 22;

    internal static void Fill(Panel host, NoteTheme theme, string? selectedColor, MouseButtonEventHandler onClick)
    {
        host.Children.Clear();
        foreach (var row in new[] { theme.DarkColors, theme.LightColors })
        {
            if (row.Count == 0) continue;

            var panel = new WrapPanel();
            foreach (var color in row)
            {
                var swatch = CreateSwatch(color);
                swatch.MouseLeftButtonUp += onClick;
                panel.Children.Add(swatch);
            }
            host.Children.Add(panel);
        }
        MarkSelected(host, selectedColor);
    }

    internal static void MarkSelected(Panel host, string? selectedColor)
    {
        foreach (var swatch in host.Children.OfType<Panel>().SelectMany(row => row.Children.OfType<Border>()))
        {
            bool selected = string.Equals((string)swatch.Tag, selectedColor, StringComparison.OrdinalIgnoreCase);
            // Sin seleccionar llevan un contorno fino y claro: una pastilla oscura sobre el fondo
            // oscuro del menú, sin él, casi no se distingue. Seleccionada, el anillo de la etiqueta.
            swatch.BorderBrush = selected ? LabelBrush((string)swatch.Tag) : Hairline;
            swatch.BorderThickness = new Thickness(selected ? 2 : 1);
            if (swatch.Child is UIElement tick) tick.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static readonly Brush Hairline = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)));

    private static Brush LabelBrush(string color) =>
        (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.LabelFor(color))!;

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Border CreateSwatch(string color)
    {
        var label = LabelBrush(color);
        return new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString(color)!,
            Width = SwatchSize,
            Height = SwatchSize,
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(5),
            // El tick en el color de la etiqueta de esa cara: se ve igual sobre una pastilla clara
            // que sobre una oscura, cosa que la tinta fija de antes no hacía. El borde lo pone
            // MarkSelected.
            Cursor = Cursors.Hand,
            Tag = color,
            ToolTip = color,
            Child = new TextBlock
            {
                Text = "✓",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = label,
                IsHitTestVisible = false,
            },
        };
    }
}

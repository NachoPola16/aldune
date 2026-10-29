using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    // 24 px: el mínimo de WCAG 2.5.8 para un objetivo táctil o de puntero.
    private const double SwatchSize = 24;

    /// <param name="onClick">Qué pasa al elegir una pastilla (clic, Espacio o Intro). Null = solo muestra los colores, sin foco ni clic.</param>
    internal static void Fill(Panel host, NoteTheme theme, string? selectedColor, RoutedEventHandler? onClick)
    {
        host.Children.Clear();
        // Una sola parada de Tab para toda la paleta y flechas dentro de ella: son hasta una docena de pastillas.
        KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(host, KeyboardNavigationMode.Contained);
        foreach (var row in new[] { theme.DarkColors, theme.LightColors })
        {
            if (row.Count == 0) continue;

            var panel = new WrapPanel();
            KeyboardNavigation.SetDirectionalNavigation(panel, KeyboardNavigationMode.Contained);
            foreach (var color in row)
            {
                var swatch = CreateSwatch(color);
                if (onClick is null)
                {
                    swatch.Focusable = false;
                    swatch.IsHitTestVisible = false;
                }
                else
                {
                    // Es un interruptor: pulsarlo lo dejaría apagado al volver a elegir el mismo color. Aquí siempre queda elegido.
                    swatch.Click += (sender, e) =>
                    {
                        ((ToggleButton)sender).IsChecked = true;
                        onClick(sender, e);
                    };
                }
                panel.Children.Add(swatch);
            }
            host.Children.Add(panel);
        }
        MarkSelected(host, selectedColor);
    }

    internal static void MarkSelected(Panel host, string? selectedColor)
    {
        foreach (var swatch in host.Children.OfType<Panel>().SelectMany(row => row.Children.OfType<ToggleButton>()))
        {
            bool selected = string.Equals((string)swatch.Tag, selectedColor, StringComparison.OrdinalIgnoreCase);
            // Sin seleccionar llevan un contorno fino y claro: una pastilla oscura sobre el fondo
            // oscuro del menú, sin él, casi no se distingue. Seleccionada, el anillo de la etiqueta.
            // Sin seleccionar, contorno de la paleta (claro sobre fondo oscuro, oscuro sobre claro):
            // un blanco fijo desaparecía en los aspectos claros y las muestras pálidas se perdían.
            if (selected) swatch.BorderBrush = LabelBrush((string)swatch.Tag);
            else swatch.SetResourceReference(Control.BorderBrushProperty, "AlduneHairlineBrush");
            swatch.BorderThickness = new Thickness(selected ? 2 : 1);
            swatch.IsChecked = selected;
            if (swatch.Content is UIElement tick) tick.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static Brush LabelBrush(string color) =>
        (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.LabelFor(color))!;

    private static ToggleButton CreateSwatch(string color)
    {
        var label = LabelBrush(color);
        var swatch = new ToggleButton
        {
            Style = (Style)Application.Current.FindResource("NoteSwatchStyle"),
            Background = (Brush)new BrushConverter().ConvertFromString(color)!,
            Width = SwatchSize,
            Height = SwatchSize,
            Margin = new Thickness(3),
            // El tick en el color de la etiqueta de esa cara: se ve igual sobre una pastilla clara
            // que sobre una oscura, cosa que la tinta fija de antes no hacía. El borde lo pone
            // MarkSelected.
            Tag = color,
            ToolTip = color,
            Content = new TextBlock
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
        AutomationProperties.SetName(swatch, color);
        return swatch;
    }
}

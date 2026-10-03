using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Nota → uno de sus colores según la piel (<see cref="NoteFace"/>). El parámetro elige cuál: Face,
/// Ink, Rim, Label, Snippet, Accent, Pill, PillRim; PillRimThickness da el grosor del contorno de la
/// cápsula (0 sin contorno). Se reevalúa al rehacer el mazo (RefreshAll), que es lo que hace quien
/// cambia la piel o el color único.
/// </summary>
public sealed class NoteFaceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Note note) return Brushes.Transparent;
        var face = NoteFace.For(ThemeManager.Skin.Card, note.Color, NoteColorDisplay.Uniform,
            NoteChannelDisplay.Of(note.Id), ThemeManager.Palette);

        // Mismo grosor que NoteRestOutlineConverter (1 con contorno, 0 sin él): la cápsula de la piel
        // de siempre no puede cambiar.
        if (parameter as string == "PillRimThickness")
            return face.PillRim is null ? new Thickness(0) : new Thickness(1);

        string? hex = (parameter as string) switch
        {
            "Face" => face.Face,
            "Ink" => face.Ink,
            "Rim" => face.Rim,
            "Label" => face.Label,
            "Snippet" => face.Snippet,
            "Accent" => face.Accent,
            "Pill" => face.Pill,
            "PillRim" => face.PillRim,
            _ => null,
        };
        if (hex is null) return Brushes.Transparent;
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

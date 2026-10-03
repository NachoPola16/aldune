using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>Nota → forma de onda de su canal (piel de osciloscopio). Cuatro geometrías congeladas.</summary>
public sealed class NoteWaveConverter : IValueConverter
{
    private static readonly Geometry[] Waves = Enumerable.Range(0, NoteChannels.Count)
        .Select(channel =>
        {
            var geometry = Geometry.Parse(NoteChannels.WavePath(channel));
            geometry.Freeze();
            return geometry;
        })
        .ToArray();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Note note ? Waves[NoteChannelDisplay.Of(note.Id)] : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

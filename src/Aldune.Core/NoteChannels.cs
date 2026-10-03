namespace Aldune.Core;

/// <summary>
/// Canales del osciloscopio (piel <see cref="SkinCard.Tinted"/>): cada nota es un canal según su puesto
/// en el mazo, cíclico de 4 en 4 (CH1 amarillo, CH2 cian, CH3 magenta, CH4 azul), con su forma de onda.
/// </summary>
public static class NoteChannels
{
    public const int Count = 4;

    // Senoidal, cuadrada, triangular y diente de sierra, en una caja de 54×18 (las de las maquetas).
    private static readonly string[] Waves =
    [
        "M0 9 C4.5 0 9 0 13.5 9 S22.5 18 27 9 S36 0 40.5 9 S49.5 18 54 9",
        "M0 15 H7 V3 H20 V15 H33 V3 H46 V15 H54",
        "M0 15 L9 3 L18 15 L27 3 L36 15 L45 3 L54 15",
        "M0 15 L13 3 V15 L26 3 V15 L39 3 V15 L52 3 V15",
    ];

    public static int Of(int position) => ((position % Count) + Count) % Count;

    public static string Number(int channel) => $"CH{Of(channel) + 1}";

    public static string PaletteKey(int channel) => $"Channel{Of(channel) + 1}";

    public static string WavePath(int channel) => Waves[Of(channel)];
}

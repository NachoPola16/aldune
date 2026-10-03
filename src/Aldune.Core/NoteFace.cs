namespace Aldune.Core;

/// <summary>Colores con los que se pinta una nota (ventana, pestaña del dock y cápsula de la tira).</summary>
/// <param name="Face">Fondo.</param>
/// <param name="Ink">Texto del cuerpo.</param>
/// <param name="Rim">Borde.</param>
/// <param name="Label">Título en el dock (y en la ventana con <see cref="SkinCard.Tinted"/>).</param>
/// <param name="Snippet">Vista previa en el dock.</param>
/// <param name="Accent">Franja (<see cref="SkinCard.Stripe"/>) o color del canal (<see cref="SkinCard.Tinted"/>); null en <see cref="SkinCard.Filled"/>.</param>
/// <param name="Pill">Relleno de la cápsula de la tira de reposo.</param>
/// <param name="PillRim">Contorno de la cápsula, o null si no lleva.</param>
public sealed record NoteFaceColors(
    string Face, string Ink, string Rim, string Label, string Snippet, string? Accent, string Pill, string? PillRim);

/// <summary>
/// Un único sitio decide cómo se pinta una nota según la piel, para que la ventana y el dock no puedan
/// discrepar. Recibe el color guardado: el color único de "mismo color" se aplica aquí.
/// </summary>
public static class NoteFace
{
    public static NoteFaceColors For(
        SkinCard card, string noteColor, string? uniformColor, int channel, IReadOnlyDictionary<string, string> palette)
    {
        var color = NoteDisplayColor.Resolve(noteColor, uniformColor);
        var ground = palette["Ground"];
        var text = palette["Text"];

        switch (card)
        {
            case SkinCard.Stripe:
            {
                var stripe = NoteColorDerivation.StripeColor(color, ground);
                return new NoteFaceColors(ground, text, palette["Border"], text, text, stripe,
                    ColorMix.Toward(stripe, ground, 0.35), stripe);
            }
            case SkinCard.Tinted:
            {
                // El color es el del canal (por puesto en el dock), no el de la nota: es lo que da
                // sentido a CH1… Con "mismo color", el color único sustituye a todos los canales, pero
                // pasado por StripeColor: tal cual casi se confundiría con la cara tintada (que sale de él)
                // y la franja y la onda no se verían.
                var accent = NoteDisplayColor.IsActive(uniformColor)
                    ? NoteColorDerivation.StripeColor(color, ground)
                    : palette[NoteChannels.PaletteKey(channel)];
                var face = ColorMix.Toward(accent, ground, 0.88);
                var label = NoteColorContrast.IsReadable(face, accent) ? accent : text;
                return new NoteFaceColors(face, text, ColorMix.Toward(accent, ground, 0.5), label, text, accent,
                    ColorMix.Toward(accent, ground, 0.55), accent);
            }
            case SkinCard.Mono:
            {
                // Monitor monocromo: no enseña colores, ni el de la nota ni el único.
                var border = palette["Border"];
                return new NoteFaceColors(ground, text, border, text, text, null, palette["Raised"], border);
            }
            default:
            {
                var label = NoteColorDerivation.LabelFor(color);
                return new NoteFaceColors(color, NoteColorContrast.ForegroundFor(color), NoteColorDerivation.RimFor(color),
                    label, label, null, color, NoteColorDerivation.RestOutlineFor(color));
            }
        }
    }
}

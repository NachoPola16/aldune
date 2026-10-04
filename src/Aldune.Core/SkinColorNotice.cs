namespace Aldune.Core;

public enum SkinColorNoticeKind
{
    None,
    /// <summary>Un color sin matiz (negro, blanco, gris) se pinta como una franja gris clara, igual para todos.</summary>
    GrayStripe,
    /// <summary>La piel no enseña colores de nota.</summary>
    ColorsNotShown,
    /// <summary>El color lo da el canal de cada nota, no el que se elija.</summary>
    ChannelColors,
}

/// <summary>
/// Cuándo el color que se elige para una nota no se va a ver tal cual con la piel activa, para avisarlo en el
/// selector en vez de dejar que parezca un fallo. Va con las mismas reglas que <see cref="NoteFace"/>.
/// </summary>
public static class SkinColorNotice
{
    // Mismo umbral que NoteColorDerivation.StripeColor: por debajo, el color se considera un gris.
    private const double AchromaticChroma = 0.02;

    public static SkinColorNoticeKind For(SkinCard card, string noteColor, string? uniformColor)
    {
        switch (card)
        {
            case SkinCard.Mono:
                return SkinColorNoticeKind.ColorsNotShown;
            case SkinCard.Tinted when !NoteDisplayColor.IsActive(uniformColor):
                return SkinColorNoticeKind.ChannelColors;
            case SkinCard.Stripe or SkinCard.Tinted:
                var color = NoteDisplayColor.Resolve(noteColor, uniformColor);
                return OklchColor.TryFromHex(color, out var oklch) && oklch.C < AchromaticChroma
                    ? SkinColorNoticeKind.GrayStripe
                    : SkinColorNoticeKind.None;
            default:
                return SkinColorNoticeKind.None;
        }
    }
}

namespace Fanote.Core;

/// <summary>
/// Parte el texto de una nota en título (la primera línea) y cuerpo (el resto), y lo vuelve a
/// juntar.
///
/// Existe para que la ventana de nota pueda enseñar el título en su cabecera y el cuerpo debajo
/// —dos cuadros de texto— **sin que eso cambie nada de cómo se guarda**: la nota sigue siendo un
/// único texto que se cifra de una pieza. Un título guardado aparte tendría que cifrarse por su
/// cuenta (su propio blob, nonce y tag) o quedarse en claro, filtrando justo lo más descriptivo de
/// cada nota; y meterlo dentro del mismo bloque cifrado es exactamente esto.
/// </summary>
public static class NoteText
{
    private const string NewLine = "\r\n";

    /// <summary>
    /// Título y cuerpo de <paramref name="text"/>. El título nunca lleva salto de línea; el cuerpo
    /// es todo lo que venga después del primero.
    /// </summary>
    public static (string Title, string Body) Split(string text)
    {
        if (string.IsNullOrEmpty(text)) return (string.Empty, string.Empty);

        int firstBreak = text.IndexOf('\n');
        if (firstBreak < 0) return (text.TrimEnd('\r'), string.Empty);

        var title = text[..firstBreak].TrimEnd('\r');
        var body = text[(firstBreak + 1)..];
        return (title, body);
    }

    /// <summary>
    /// El texto completo de la nota a partir de sus dos cuadros.
    ///
    /// Con el cuerpo vacío no se añade el salto de línea: si no, una nota de una sola línea se
    /// guardaría con un salto al final que no escribió nadie, y volvería a aparecer cada vez que se
    /// abre y se cierra.
    /// </summary>
    public static string Join(string title, string body)
    {
        if (string.IsNullOrEmpty(body)) return title ?? string.Empty;
        return (title ?? string.Empty) + NewLine + body;
    }
}

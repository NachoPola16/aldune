namespace Aldune.Core;

/// <summary>
/// Dónde empieza y acaba la línea que contiene una posición del texto — el único cálculo que
/// comparten <see cref="TaskLines"/> y <see cref="BulletLines"/> (dos familias de prefijo de línea
/// distintas, cada una con su propio glifo y su propio significado, pero ambas necesitan lo mismo
/// para saber "qué línea estoy tocando"). Extraído aquí para no duplicarlo entre las dos.
/// </summary>
internal static class LineText
{
    internal static int Start(string text, int index)
    {
        for (int i = Math.Min(index, text.Length) - 1; i >= 0; i--)
        {
            if (text[i] == '\n') return i + 1;
        }
        return 0;
    }

    internal static int End(string text, int index)
    {
        for (int i = Math.Max(index, 0); i < text.Length; i++)
        {
            if (text[i] == '\r' || text[i] == '\n') return i;
        }
        return text.Length;
    }
}

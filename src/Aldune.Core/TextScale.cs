namespace Aldune.Core;

/// <summary>
/// «Tamaño del texto» de Ajustes: la letra de la interfaz y de las notas se escala por pasos fijos, no a cualquier valor, para que
/// el diseño (dock, ventanas, diálogos) se pruebe en un puñado de tamaños. El 100 % es el de siempre.
/// </summary>
public static class TextScale
{
    public static readonly IReadOnlyList<int> Steps = [100, 115, 130, 150];

    public const int Default = 100;

    /// <summary>El paso válido para un valor guardado: el mismo si lo es; si no, el más cercano (el menor en caso de empate), y el
    /// de fábrica si está fuera de lo razonable (dato roto o editado a mano).</summary>
    public static int Normalize(int percent)
    {
        if (percent < 50 || percent > 200) return Default;
        return Steps.OrderBy(step => Math.Abs(step - percent)).ThenBy(step => step).First();
    }

    public static double Factor(int percent) => Normalize(percent) / 100.0;

    /// <summary>Un paso más grande (<paramref name="direction"/> &gt; 0) o más pequeño (&lt; 0), sin salirse de los extremos.</summary>
    public static int Step(int percent, int direction)
    {
        int index = Steps.ToList().IndexOf(Normalize(percent));
        return Steps[Math.Clamp(index + Math.Sign(direction), 0, Steps.Count - 1)];
    }
}

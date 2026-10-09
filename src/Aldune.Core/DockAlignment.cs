namespace Aldune.Core;

/// <summary>
/// Dónde, a lo largo del borde, se coloca el dock. Centro es lo de siempre (y el 0 que un
/// <c>settings.json</c> antiguo, sin el campo, carga sin migración). Inicio es arriba en los bordes
/// laterales y a la izquierda en los de arriba y abajo; Final, el extremo opuesto.
/// </summary>
public enum DockAlignment
{
    Center = 0,
    Start = 1,
    End = 2
}

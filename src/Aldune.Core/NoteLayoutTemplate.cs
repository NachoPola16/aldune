namespace Aldune.Core;

/// <summary>Distribuciones explícitas para ordenar varias notas abiertas de una vez.</summary>
public enum NoteLayoutTemplate
{
    Normal,
    Grid,
    Columns,
    /// <summary>Cascada junto al dock desplegado. Al final del enum: el valor se guarda como número.</summary>
    DockCascade
}

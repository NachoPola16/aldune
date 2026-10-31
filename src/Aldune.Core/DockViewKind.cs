namespace Aldune.Core;

/// <summary>Qué subconjunto de notas muestra el dock.</summary>
public enum DockViewKind
{
    Active,
    Archived,
    Trashed,
    Tag,
    /// <summary>Activas y archivadas juntas (la papelera no cuenta como «notas»). Va al final: el enum se guarda como número.</summary>
    All
}

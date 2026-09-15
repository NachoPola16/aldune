namespace Aldune.Core;

public enum NoteState
{
    Active,
    Archived,
    Trashed
}

public sealed class Note
{
    public required Guid Id { get; init; }
    public required string Text { get; set; }
    public required string Color { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public required NoteState State { get; set; }
    public required string ScreenOrigin { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    /// <summary>Posición de la nota dentro del mazo del dock (<c>NoteOrder.Position</c>). Es
    /// <c>null</c> si nunca se ha arrastrado, en cuyo caso ordena por fecha de creación como
    /// siempre. Viaja dentro del sobre de sincronización para que el orden del mazo sea el mismo
    /// en todos los dispositivos; la posición de la ventana en pantalla sigue siendo local, porque
    /// un mismo escritorio no significa nada en monitores distintos.</summary>
    public double? DockPosition { get; set; }

    public bool IsProtected { get; set; }
    public ProtectedNoteContent? ProtectedContent { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUnlocked { get; set; }
}

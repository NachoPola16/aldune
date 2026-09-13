namespace Fanote.Core;

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
}

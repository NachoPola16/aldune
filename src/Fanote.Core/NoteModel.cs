namespace Fanote.Core;

public sealed class NoteModel
{
    public required Guid Id { get; init; }
    public required string Text { get; set; }
    public required string Color { get; set; }
}

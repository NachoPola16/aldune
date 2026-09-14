using System.ComponentModel;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// UI-only wrapper around a <see cref="Note"/> that adds bulk-selection state for the dock's
/// archive view — kept out of the Core domain model on purpose, since selection is a presentation
/// concern, not something that gets persisted.
/// </summary>
public sealed class NoteRow : INotifyPropertyChanged
{
    public Note Note { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public NoteRow(Note note) => Note = note;

    public event PropertyChangedEventHandler? PropertyChanged;
}

using System.Windows;
using Fanote.Core;

namespace Fanote.Windowing;

public partial class NoteWindow : Window
{
    // TODO(Task 9): NotesRepository and EdgeDockWindow are accepted-and-ignored for now.
    // Task 9 gives this constructor its real implementation (autosave, archive/trash) using them.
    public NoteWindow(Note note, NotesRepository repository, EdgeDockWindow owner)
    {
        InitializeComponent();
        TextBody.Text = note.Text;

        Loaded += (_, _) => TextBody.Focus();
    }
}

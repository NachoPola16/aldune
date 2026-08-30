using System.Windows;
using Fanote.Core;

namespace Fanote.Windowing;

public partial class NoteWindow : Window
{
    public NoteWindow(NoteModel note)
    {
        InitializeComponent();
        TextBody.Text = note.Text;

        Loaded += (_, _) => TextBody.Focus();
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Las etiquetas de una nota: una casilla por etiqueta existente, que se guarda al marcarla, y un
/// campo para crear una nueva (Enter o "+") que queda ya asignada. Guarda en el repositorio al
/// momento, pero no refresca el dock ni el gestor: quien lo aloja lo hace al cerrar su popup, porque
/// refrescar con el popup abierto regeneraba la pestaña a la que está anclado y, en la vista de una
/// etiqueta, podía hacer desaparecer la nota que se está editando.
/// </summary>
public partial class TagAssignmentPanel : UserControl
{
    private NotesRepository? _repository;
    private Note? _note;

    public TagAssignmentPanel()
    {
        InitializeComponent();
    }

    /// <summary>Si se ha guardado algún cambio desde el último <see cref="Bind"/>.</summary>
    internal bool Changed { get; private set; }

    internal void Bind(NotesRepository repository, Note note)
    {
        _repository = repository;
        _note = note;
        Changed = false;
        NewTagBox.Text = string.Empty;
        Render();
    }

    private void Render()
    {
        Items.Children.Clear();
        if (_repository is null || _note is null) return;

        var tags = _repository.GetAllTags();
        EmptyText.Visibility = tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tag in tags)
        {
            var checkBox = new CheckBox
            {
                Content = tag,
                Tag = tag,
                IsChecked = _note.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase),
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#EDE7DC")!,
                Background = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.Ground)!,
                FontSize = 13,
                Style = (Style)FindResource("AppCheckBoxStyle"),
            };
            checkBox.Click += (_, _) => Save();
            Items.Children.Add(checkBox);
        }
    }

    private void Save()
    {
        if (_repository is null || _note is null) return;

        var tags = Items.Children.OfType<CheckBox>()
            .Where(checkBox => checkBox.IsChecked == true)
            .Select(checkBox => (string)checkBox.Tag)
            .ToArray();
        _repository.SetTags(_note.Id, tags);
        _note.Tags = tags;
        Changed = true;
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => AddTypedTag();

    private void OnNewTagKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddTypedTag();
        e.Handled = true;
    }

    private void OnNewTagTextChanged(object sender, TextChangedEventArgs e)
    {
        bool empty = string.IsNullOrWhiteSpace(NewTagBox.Text);
        NewTagPlaceholder.Visibility = NewTagBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddTagButton.IsEnabled = !empty;
    }

    /// <summary>Crea la etiqueta escrita (si ya existe, la reutiliza) y la marca en esta nota.</summary>
    private void AddTypedTag()
    {
        if (_repository is null || _note is null) return;

        var name = NewTagBox.Text.Trim();
        if (name.Length == 0) return;

        _repository.CreateTag(name);
        // La etiqueta puede existir ya con otras mayúsculas: se marca la existente, no un duplicado.
        var existing = _repository.GetAllTags()
            .FirstOrDefault(tag => string.Equals(tag, name, StringComparison.OrdinalIgnoreCase)) ?? name;
        if (!_note.Tags.Contains(existing, StringComparer.OrdinalIgnoreCase))
        {
            var tags = _note.Tags.Append(existing).ToArray();
            _repository.SetTags(_note.Id, tags);
            _note.Tags = tags;
        }

        Changed = true;
        NewTagBox.Text = string.Empty;
        Render();
        NewTagBox.Focus();
    }
}

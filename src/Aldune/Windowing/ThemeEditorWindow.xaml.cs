using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Crear o editar un tema propio: nombre y dos filas de colores. Un color se selecciona pulsándolo,
/// y "←", "→" y "Quitar" actúan sobre él. Se usan botones y no arrastre porque es más fiable y va
/// con teclado. Los colores nuevos salen del CustomColorWindow de siempre, que solo deja elegir
/// colores legibles; van a la fila que les toca por su claridad.
/// </summary>
public partial class ThemeEditorWindow : Window
{
    private readonly NoteTheme _theme;
    private string? _selected;

    private ThemeEditorWindow(NoteTheme theme)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        _theme = new NoteTheme
        {
            Id = theme.Id,
            Name = theme.Name,
            DarkColors = theme.DarkColors.ToList(),
            LightColors = theme.LightColors.ToList(),
        };
        NameBox.Text = _theme.Name;
        Render();
        Loaded += (_, _) => NameBox.Focus();
    }

    public static NoteTheme? Show(Window owner, NoteTheme theme, bool isNew)
    {
        var dialog = new ThemeEditorWindow(theme) { Owner = owner, Topmost = owner.Topmost };
        if (isNew) dialog.TitleText.Text = dialog.Title = Strings.ThemeEditorNewTitle;
        return dialog.ShowDialog() == true ? dialog._theme : null;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    private void Render()
    {
        // Rehacer las filas destruye la pastilla que tenía el foco: se recuerda cuál era y se le
        // devuelve, o quien navega con teclado perdería su sitio en cada selección.
        var focusedColor = (Keyboard.FocusedElement as Border)?.Tag as string;

        FillRow(DarkRow, _theme.DarkColors);
        FillRow(LightRow, _theme.LightColors);

        if (focusedColor is not null)
        {
            DarkRow.Children.OfType<Border>().Concat(LightRow.Children.OfType<Border>())
                .FirstOrDefault(swatch => (string)swatch.Tag == focusedColor)?.Focus();
        }

        MoveLeftButton.IsEnabled = MoveRightButton.IsEnabled = RemoveButton.IsEnabled = _selected is not null;
        bool empty = _theme.DarkColors.Count == 0 && _theme.LightColors.Count == 0;
        SaveButton.IsEnabled = !empty;
        ErrorText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FillRow(WrapPanel row, List<string> colors)
    {
        row.Children.Clear();
        foreach (var color in colors)
        {
            var label = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.LabelFor(color))!;
            bool selected = color == _selected;
            var swatch = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(color)!,
                Width = 30, Height = 30, Margin = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                // Mismo criterio que NoteSwatchPanel: contorno fino y claro si no está seleccionado,
                // para que un oscuro se distinga sobre el fondo oscuro de la ventana.
                BorderBrush = selected ? label : new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = Cursors.Hand,
                Tag = color,
                ToolTip = color,
            };
            // Enfocable y con Espacio/Enter: "←", "→" y "Quitar" actúan sobre el seleccionado, así que
            // sin esto no se podía editar un tema sin ratón.
            swatch.Focusable = true;
            AutomationProperties.SetName(swatch, color);
            swatch.MouseLeftButtonUp += (_, _) => { _selected = color; Render(); };
            swatch.KeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Space or Key.Enter)) return;
                _selected = color;
                Render();
                e.Handled = true;
            };
            row.Children.Add(swatch);
        }
    }

    private List<string>? ListOfSelected() =>
        _selected is null ? null
        : _theme.DarkColors.Contains(_selected) ? _theme.DarkColors
        : _theme.LightColors.Contains(_selected) ? _theme.LightColors
        : null;

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var color = CustomColorWindow.Show(this, _selected ?? "#EBE6D9");
        if (color is null) return;

        color = color.ToUpperInvariant();
        var row = NoteColorDerivation.IsDark(color) ? _theme.DarkColors : _theme.LightColors;
        if (!row.Contains(color)) row.Add(color);
        _selected = color;
        Render();
    }

    private void OnMoveLeftClick(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveRightClick(object sender, RoutedEventArgs e) => Move(+1);

    private void Move(int delta)
    {
        if (ListOfSelected() is not { } list) return;
        int index = list.IndexOf(_selected!);
        int target = index + delta;
        if (target < 0 || target >= list.Count) return;
        (list[index], list[target]) = (list[target], list[index]);
        Render();
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        ListOfSelected()?.Remove(_selected!);
        _selected = null;
        Render();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_theme.DarkColors.Count == 0 && _theme.LightColors.Count == 0) return;
        _theme.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? Strings.ThemeNewName : NameBox.Text.Trim();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}

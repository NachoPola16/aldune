using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Vista previa de una importación: qué ajustes cambiaría el archivo, antes de tocar nada. Solo enseña
/// el plan y devuelve si se aplica; la copia previa y la aplicación en vivo las hace Ajustes.
/// </summary>
public partial class ConfigImportWindow : Window
{
    private ConfigImportWindow(ConfigImportPlan plan)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        // El foco empieza en el botón principal, como en AppDialog: Enter hace lo esperado.
        Loaded += (_, _) => (ApplyButton.Visibility == Visibility.Visible ? ApplyButton : CancelButton).Focus();

        if (!string.IsNullOrWhiteSpace(plan.FileApp))
            FromVersionText.Text = Strings.ConfigImportFromVersion(plan.FileApp);
        else
            FromVersionText.Visibility = Visibility.Collapsed;

        if (plan.Changes.Count == 0)
        {
            // Sin cambios no hay nada que aplicar ni que copiar: solo se puede cerrar.
            SummaryText.Text = Strings.ConfigImportNoChanges;
            ChangesScroller.Visibility = Visibility.Collapsed;
            BackupText.Visibility = Visibility.Collapsed;
            ApplyButton.Visibility = Visibility.Collapsed;
            CancelButton.Content = Strings.ConfigImportClose;
            CancelButton.Style = (Style)FindResource("PrimaryColorDialogButtonStyle");
            return;
        }

        SummaryText.Text = Strings.ConfigImportSummary(plan.Changes.Count);
        foreach (var change in plan.Changes) ChangesPanel.Children.Add(Row(change));
        if (plan.ChangesLanguage) RestartText.Visibility = Visibility.Visible;
    }

    /// <summary>True solo con «Aplicar». Cancelar, cerrar con Esc o con el botón de un archivo sin cambios dan false.</summary>
    public static bool Show(Window owner, ConfigImportPlan plan)
    {
        var dialog = new ConfigImportWindow(plan) { Owner = owner, Topmost = owner.Topmost };
        return dialog.ShowDialog() == true;
    }

    // Nombre en negrita y debajo «antes → ahora» en monoespaciada pequeña, a una línea con elipsis: los
    // valores largos (temas propios, colores por aspecto) se leen enteros en el tooltip.
    private static StackPanel Row(ConfigChange change)
    {
        var name = new TextBlock
        {
            Text = Strings.ConfigFieldName(change.FieldId), FontSize = 12, FontWeight = FontWeights.SemiBold,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextBrush");

        var values = new TextBlock
        {
            Text = $"{change.OldValue} → {change.NewValue}",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 2, 0, 0),
            ToolTip = new TextBlock
            {
                Text = $"{change.OldValue}\n→ {change.NewValue}", TextWrapping = TextWrapping.Wrap, MaxWidth = 460,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11,
            },
        };
        values.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextSoftBrush");

        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
        row.Children.Add(name);
        row.Children.Add(values);
        return row;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    private void OnApplyClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        // Enter con el foco en un botón lo pulsa él mismo: con Cancelar enfocado no debe aplicar. Sin cambios
        // el único botón es «Cerrar» y Enter también cierra.
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not Button)
        {
            DialogResult = ApplyButton.Visibility == Visibility.Visible;
            e.Handled = true;
        }
    }
}

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
    private ConfigImportWindow(ConfigImportPlan plan, AppSettings current)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        // El foco empieza en el botón principal, como en AppDialog: Enter hace lo esperado.
        Loaded += (_, _) => (ApplyButton.Visibility == Visibility.Visible ? ApplyButton : CancelButton).Focus();

        if (!string.IsNullOrWhiteSpace(plan.FileApp))
            FromVersionText.Text = Strings.ConfigImportFromVersion(plan.FileApp);
        else
            FromVersionText.Visibility = Visibility.Collapsed;

        // Filas ya presentables (atajos juntos, valores traducidos). Vacías aunque el plan traiga cambios si
        // solo difieren en cómo se escriben (p. ej. un atajo explícito que ya era el de fábrica).
        var rows = ConfigPreview.Rows(plan, current);
        if (rows.Count == 0)
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

        SummaryText.Text = Strings.ConfigImportSummary(rows.Count);
        foreach (var row in rows) ChangesPanel.Children.Add(Row(row));
        if (plan.ChangesLanguage) RestartText.Visibility = Visibility.Visible;
    }

    /// <summary>True solo con «Aplicar». Cancelar, cerrar con Esc o con el botón de un archivo sin cambios dan false.</summary>
    internal static bool Show(Window owner, ConfigImportPlan plan, AppSettings current)
    {
        var dialog = new ConfigImportWindow(plan, current) { Owner = owner, Topmost = owner.Topmost };
        return dialog.ShowDialog() == true;
    }

    // Nombre en negrita y debajo «antes → ahora» en monoespaciada pequeña, a una línea con elipsis: los
    // valores largos (temas propios, colores por aspecto) se leen enteros en el tooltip.
    private static StackPanel Row(ConfigPreviewRow change)
    {
        var name = new TextBlock
        {
            Text = change.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextBrush");

        var values = new TextBlock
        {
            Text = $"{change.Old} → {change.New}",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 2, 0, 0),
            ToolTip = new TextBlock
            {
                Text = $"{change.Old}\n→ {change.New}", TextWrapping = TextWrapping.Wrap, MaxWidth = 460,
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

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Qué secciones llevar al archivo de configuración. Solo pregunta: elegir el archivo y escribirlo lo hace
/// quien llama (Ajustes), para que esa parte se pueda ejercitar sin abrir diálogos modales.
/// </summary>
public partial class ConfigExportWindow : Window
{
    private ConfigExportWindow()
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
    }

    /// <summary>Las secciones marcadas, o null si se cancela.</summary>
    public static ConfigSections? Show(Window owner)
    {
        var dialog = new ConfigExportWindow { Owner = owner, Topmost = owner.Topmost };
        return dialog.ShowDialog() == true ? dialog.Chosen : null;
    }

    private ConfigSections Chosen =>
        (AppearanceCheck.IsChecked == true ? ConfigSections.Appearance : ConfigSections.None)
        | (SettingsCheck.IsChecked == true ? ConfigSections.Settings : ConfigSections.None);

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    // Sin ninguna sección marcada no hay nada que exportar: el botón se apaga en vez de dar un archivo vacío.
    private void OnSectionToggled(object sender, RoutedEventArgs e) =>
        ExportButton.IsEnabled = Chosen != ConfigSections.None;

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (Chosen != ConfigSections.None) DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        // Enter con el foco en un botón lo pulsa él mismo: con Cancelar enfocado no debe exportar.
        else if (e.Key == Key.Enter && ExportButton.IsEnabled && Keyboard.FocusedElement is not System.Windows.Controls.Button)
        {
            DialogResult = true;
            e.Handled = true;
        }
    }
}

using System.Windows;
using System.Windows.Interop;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

/// <summary>
/// Los ajustes de la app. Existe porque el sitio de un ajuste no es ni el menú contextual de la
/// bandeja (donde nadie lo busca) ni la cabecera del gestor de notas, donde el interruptor de
/// arranque competía visualmente con los filtros y parecía uno más.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly GlobalHotkey _hotkey;

    // Constructor interno, no publico: GlobalHotkey es internal, y la ventana solo se crea desde
    // AppCoordinator.SettingsWindowFactory. El XAML generado solo llama a InitializeComponent, asi
    // que no necesita un constructor accesible desde fuera.
    internal SettingsWindow(SettingsService settingsService, AppSettings settings, GlobalHotkey hotkey)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _hotkey = hotkey;

        StartupCheck.IsChecked = StartupRegistration.IsEnabled();
        HotkeyCheck.IsChecked = _settings.GlobalHotkeyEnabled;
        UpdateHotkeyHint();

        SourceInitialized += (_, _) =>
            NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// La casilla refleja lo que de verdad quedó en el registro, no lo que se pidió: si una
    /// directiva de grupo lo impide, marcarla igualmente sería mentir.
    /// </summary>
    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        StartupCheck.IsChecked = StartupRegistration.SetEnabled(StartupCheck.IsChecked == true);
    }

    private void OnHotkeyToggled(object sender, RoutedEventArgs e)
    {
        bool wanted = HotkeyCheck.IsChecked == true;

        if (wanted) _hotkey.Enable();
        else _hotkey.Disable();

        // Igual que arriba: se guarda y se muestra lo que se consiguió. Windows no comparte una
        // combinación entre aplicaciones — se la queda la primera que la pide —, así que activarla
        // puede fallar por causas ajenas a Fanote.
        _settings.GlobalHotkeyEnabled = _hotkey.IsRegistered;
        HotkeyCheck.IsChecked = _hotkey.IsRegistered;
        _settingsService.Save(_settings);

        UpdateHotkeyHint();
    }

    private void UpdateHotkeyHint()
    {
        HotkeyCheck.Content = $"Crear una nota con {GlobalHotkey.DisplayName}";

        HotkeyHint.Text = HotkeyCheck.IsChecked == true
            ? "Funciona desde cualquier aplicación, sin tener que ir al borde de la pantalla."
            : _settings.GlobalHotkeyEnabled
                ? $"No se ha podido activar: otra aplicación ya está usando {GlobalHotkey.DisplayName}. " +
                  "Ciérrala o cambia su atajo y vuelve a intentarlo."
                : "Desactivado.";
    }
}

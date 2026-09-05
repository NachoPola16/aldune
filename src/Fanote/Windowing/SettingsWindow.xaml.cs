using System.Windows;
using System.Windows.Input;
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
    private bool _recording;

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
        UpdateHotkeyUi();

        PreviewKeyDown += OnPreviewKeyDown;

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

        if (wanted) _hotkey.Enable(_settings.Hotkey);
        else _hotkey.Disable();

        // Igual que arriba: se guarda y se muestra lo que se consiguió. Windows no comparte una
        // combinación entre aplicaciones — se la queda la primera que la pide —, así que activarla
        // puede fallar por causas ajenas a Fanote.
        _settings.GlobalHotkeyEnabled = wanted;
        HotkeyCheck.IsChecked = wanted;
        _settingsService.Save(_settings);

        UpdateHotkeyUi();
    }

    /// <summary>
    /// Captura la siguiente combinación que se pulse. Se hace con un botón que escucha, y no con
    /// dos desplegables de modificador y tecla: elegir de una lista obliga a traducir mentalmente
    /// lo que uno ya sabe pulsar.
    /// </summary>
    private void OnRecordHotkeyClick(object sender, RoutedEventArgs e)
    {
        _recording = true;
        HotkeyButton.Content = "Pulsa una combinación… (Esc para cancelar)";
        HotkeyButton.Focus();
    }

    private void OnResetHotkeyClick(object sender, RoutedEventArgs e)
    {
        _recording = false;
        ApplyBinding(HotkeyBinding.Default);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _recording = false;
            UpdateHotkeyUi();
            return;
        }

        // Un modificador suelto no es una combinación: mientras el usuario mantiene Ctrl y aún no
        // ha elegido tecla, se sigue escuchando en vez de registrar "Ctrl + Ctrl".
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        uint modifiers = 0;
        var active = Keyboard.Modifiers;
        if (active.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyBinding.ModControl;
        if (active.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyBinding.ModAlt;
        if (active.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyBinding.ModShift;
        if (active.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyBinding.ModWin;

        var candidate = new HotkeyBinding(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
        if (!candidate.IsValid)
        {
            // Sin modificador, el atajo se tragaria esa tecla en todo el sistema.
            HotkeyHint.Text = "Añade al menos Ctrl, Alt, Shift o Win a la combinación.";
            return;
        }

        _recording = false;
        ApplyBinding(candidate);
    }

    private void ApplyBinding(HotkeyBinding binding)
    {
        _settings.HotkeyModifiers = binding.Modifiers;
        _settings.HotkeyKey = binding.Key;

        if (_settings.GlobalHotkeyEnabled) _hotkey.Enable(binding);
        _settingsService.Save(_settings);

        UpdateHotkeyUi();
    }

    private void UpdateHotkeyUi()
    {
        HotkeyButton.Content = _settings.Hotkey.DisplayName;
        HotkeyButton.IsEnabled = HotkeyCheck.IsChecked == true;
        HotkeyResetButton.IsEnabled = HotkeyCheck.IsChecked == true;

        if (HotkeyCheck.IsChecked != true)
        {
            HotkeyHint.Text = "Desactivado.";
            return;
        }

        HotkeyHint.Text = _hotkey.IsRegistered
            ? "Funciona desde cualquier aplicación, sin tener que ir al borde de la pantalla."
            : $"No se ha podido activar: otra aplicación ya usa {_settings.Hotkey.DisplayName}. " +
              "Elige otra combinación.";
    }
}

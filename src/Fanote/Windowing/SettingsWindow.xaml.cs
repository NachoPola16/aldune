using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Fanote.Core;
using Fanote.Interop;
using Fanote.Resources;

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
    private readonly AppCoordinator? _coordinator;
    private bool _recording;

    // Constructor interno, no publico: GlobalHotkey es internal, y la ventana solo se crea desde
    // AppCoordinator.SettingsWindowFactory. El XAML generado solo llama a InitializeComponent, asi
    // que no necesita un constructor accesible desde fuera.
    internal SettingsWindow(
        SettingsService settingsService,
        AppSettings settings,
        GlobalHotkey hotkey,
        AppCoordinator? coordinator = null)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _hotkey = hotkey;
        _coordinator = coordinator;

        StartupCheck.IsChecked = StartupRegistration.IsEnabled();
        HotkeyCheck.IsChecked = _settings.GlobalHotkeyEnabled;
        HideOnFullscreenCheck.IsChecked = _settings.HideOnFullscreen;
        RememberPositionsCheck.IsChecked = _settings.RememberNotePositions;
        UpdateHotkeyUi(); // tambien deja lista la seccion de Ayuda rapida, ver UpdateQuickHelp
        PopulateMonitors();
        PopulateEdges();
        PopulateLanguages();

        // Tope de alto contra la pantalla real, no un número fijo: con SizeToContent="Height" la
        // ventana crece con su contenido, y en un portátil con escalado las últimas secciones se
        // quedaban fuera sin scroll para alcanzarlas. El ScrollViewer del XAML se encarga del resto.
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;

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
        HotkeyButton.Content = Strings.HotkeyRecording;
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
            HotkeyHint.Text = Strings.HotkeyNeedsModifier;
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
            HotkeyHint.Text = Strings.HotkeyDisabled;
            UpdateQuickHelp();
            return;
        }

        HotkeyHint.Text = _hotkey.IsRegistered
            ? Strings.HotkeyWorks
            : Strings.HotkeyConflict(_settings.Hotkey.DisplayName);
        UpdateQuickHelp();
    }

    /// <summary>
    /// Texto de la sección "Ayuda rápida". Se recalcula tras cualquier cambio del atajo (ver
    /// <see cref="UpdateHotkeyUi"/>) porque uno de los gestos lo menciona por su nombre.
    /// </summary>
    private void UpdateQuickHelp()
    {
        string hotkeyLine = _settings.GlobalHotkeyEnabled
            ? Strings.QuickHelpHotkeyOn(_settings.Hotkey.DisplayName)
            : Strings.QuickHelpHotkeyOff;

        QuickHelpText.Text = string.Join("\n", new[]
        {
            Strings.QuickHelpHover,
            Strings.QuickHelpDrag,
            Strings.QuickHelpRightClick,
            Strings.QuickHelpTask,
            Strings.QuickHelpMenu,
            Strings.QuickHelpEscape,
            hotkeyLine,
            Strings.QuickHelpTray
        });
    }

    private void PopulateMonitors()
    {
        MonitorListContainer.Children.Clear();

        var monitors = MonitorEnumerator.EnumerateMonitors();

        var allScreensRadio = new RadioButton
        {
            GroupName = "MonitorGroup",
            Style = (Style)FindResource("MonitorRadioStyle"),
            Tag = null,
            Content = Strings.AllScreens,
            IsChecked = _settings.TargetMonitorIndex == null || _settings.TargetMonitorIndex >= monitors.Count
        };
        allScreensRadio.Checked += OnMonitorSelectionChanged;
        MonitorListContainer.Children.Add(allScreensRadio);

        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            int monitorIndex = i;
            string labelText = Strings.ScreenLabel(i + 1, m.IsPrimary, (int)m.WorkArea.Width, (int)m.WorkArea.Height);

            var radio = new RadioButton
            {
                GroupName = "MonitorGroup",
                Style = (Style)FindResource("MonitorRadioStyle"),
                Tag = monitorIndex,
                Content = labelText,
                IsChecked = _settings.TargetMonitorIndex == monitorIndex
            };
            radio.Checked += OnMonitorSelectionChanged;
            MonitorListContainer.Children.Add(radio);
        }
    }

    private void OnMonitorSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true } radio)
        {
            var targetIndex = (int?)radio.Tag;
            if (_settings.TargetMonitorIndex != targetIndex)
            {
                _settings.TargetMonitorIndex = targetIndex;
                _settingsService.Save(_settings);
                _coordinator?.RebuildDocks();
            }
        }
    }

    private void OnHideOnFullscreenToggled(object sender, RoutedEventArgs e)
    {
        _settings.HideOnFullscreen = HideOnFullscreenCheck.IsChecked == true;
        _settingsService.Save(_settings);
    }

    // Solo Izquierda/Derecha: son los dos bordes donde una nota se abre deslizándose en
    // horizontal (ver EdgeDockWindow.PositionNoteWindow). Arriba/Abajo exigiría deslizar en
    // vertical, que queda fuera de esta ronda.
    private void PopulateEdges()
    {
        EdgeListContainer.Children.Clear();
        AddEdgeRadio(EdgePosition.Right, Strings.EdgeRight);
        AddEdgeRadio(EdgePosition.Left, Strings.EdgeLeft);
    }

    private void AddEdgeRadio(EdgePosition edge, string label)
    {
        var radio = new RadioButton
        {
            GroupName = "EdgeGroup",
            Style = (Style)FindResource("MonitorRadioStyle"),
            Tag = edge,
            Content = label,
            IsChecked = _settings.DockEdge == edge
        };
        radio.Checked += OnEdgeSelectionChanged;
        EdgeListContainer.Children.Add(radio);
    }

    private void OnEdgeSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: EdgePosition edge } && _settings.DockEdge != edge)
        {
            _settings.DockEdge = edge;
            _settingsService.Save(_settings);
            _coordinator?.RebuildDocks();
        }
    }

    private void OnRememberPositionsToggled(object sender, RoutedEventArgs e)
    {
        _settings.RememberNotePositions = RememberPositionsCheck.IsChecked == true;
        _settingsService.Save(_settings);
    }

    // Los nombres de los idiomas ("Español"/"English") no se traducen: un idioma se nombra a sí
    // mismo igual sea cual sea el idioma activo de la interfaz, que es la convención de cualquier
    // selector de idioma.
    private void PopulateLanguages()
    {
        LanguageListContainer.Children.Clear();
        AddLanguageRadio("es", "Español");
        AddLanguageRadio("en", "English");
    }

    private void AddLanguageRadio(string code, string label)
    {
        var radio = new RadioButton
        {
            GroupName = "LanguageGroup",
            Style = (Style)FindResource("MonitorRadioStyle"),
            Tag = code,
            Content = label,
            // Contra Strings.Current (ya resuelto al arrancar), no contra _settings.Language: así
            // la tarjeta correcta sale marcada incluso cuando el usuario nunca ha elegido idioma
            // explícitamente y se está siguiendo el de Windows.
            IsChecked = Strings.Current == code
        };
        radio.Checked += OnLanguageSelectionChanged;
        LanguageListContainer.Children.Add(radio);
    }

    private void OnLanguageSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string code } && _settings.Language != code)
        {
            _settings.Language = code;
            _settingsService.Save(_settings);
            // No se recarga en caliente: los enlaces {x:Static} del XAML se resuelven al construir
            // cada ventana, así que un cambio de idioma solo se ve entero tras reiniciar (ver
            // Fanote.Resources.Strings). Rehacer todas las ventanas abiertas —incluidas notas con
            // texto sin guardar— para simular un cambio en caliente sería más frágil que pedir un
            // reinicio, así que no se intenta.
        }
    }
}

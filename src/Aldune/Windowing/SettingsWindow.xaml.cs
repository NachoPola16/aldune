using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

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
    private readonly SyncService? _syncService;
    private readonly Action? _checkForUpdates;
    private bool _recording;
    private bool _syncUiReady;
    private bool _loadingSyncProfile;

    // Constructor interno, no publico: GlobalHotkey es internal, y la ventana solo se crea desde
    // AppCoordinator.SettingsWindowFactory. El XAML generado solo llama a InitializeComponent, asi
    // que no necesita un constructor accesible desde fuera.
    internal SettingsWindow(
        SettingsService settingsService,
        AppSettings settings,
        GlobalHotkey hotkey,
        AppCoordinator? coordinator = null,
        SyncService? syncService = null,
        Action? checkForUpdates = null)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        // SizeToContent="Height" todavía no conoce el alto final hasta que la ventana entra en
        // el árbol visual. Mantener la ventana invisible durante ese primer layout evita que en
        // una pantalla vertical se vea un fotograma en (0,0) antes de recentrarla.
        Opacity = 0;
        _settingsService = settingsService;
        _settings = settings;
        _hotkey = hotkey;
        _coordinator = coordinator;
        _syncService = syncService;
        _checkForUpdates = checkForUpdates;

        StartupCheck.IsChecked = StartupRegistration.IsEnabled();
        HotkeyCheck.IsChecked = _settings.GlobalHotkeyEnabled;
        HideOnFullscreenCheck.IsChecked = _settings.HideOnFullscreen;
        KeepDockOpenCheck.IsChecked = _settings.KeepDockOpen;
        RememberPositionsCheck.IsChecked = _settings.RememberNotePositions;
        PopulateTrackpadGestures();
        AutoHideTasksCheck.IsChecked = _settings.AutoHideCompletedTasks;
        AutoHideTasksDelayValueBox.Text = _settings.AutoHideCompletedTasksDelayValue.ToString();
        TrashRetentionValueBox.Text = _settings.TrashRetentionDays.ToString();
        UpdateHotkeyUi(); // tambien deja lista la seccion de Ayuda rapida, ver UpdateQuickHelp
        PopulateMonitors();
        PopulateEdges();
        PopulateLanguages();
        RefreshThemeSection();
        PopulateDelayUnits();
        UpdateInterfaceModeUi();
        UpdateAutoHideTasksUi();
        SyncProfileStore.Ensure(_settings);
        PopulateSyncProfiles();
        PopulateSyncTags();
        LoadSyncProfileFields();
        _syncUiReady = true;

        // Tope de alto contra la pantalla real, no un número fijo: con SizeToContent="Height" la
        // ventana crece con su contenido, y en un portátil con escalado las últimas secciones se
        // quedaban fuera sin scroll para alcanzarlas. El ScrollViewer del XAML se encarga del resto.
        //
        // SystemParameters.WorkArea es SIEMPRE el área de trabajo del monitor PRIMARIO del sistema,
        // nunca la del monitor donde esta ventana vaya a mostrarse de verdad — con portátil +
        // monitor externo, si el externo es el primario (caso típico), esto calculaba el tope
        // contra la pantalla grande y Ajustes se salía por abajo en la pequeña del portátil
        // (reportado por el usuario, 2026-09-11). Valor de reserva aquí, por si algún día se
        // muestra sin pasar por AppCoordinator.OpenSettings (que sí conoce el monitor correcto):
        // CenterOnThisMonitor lo corrige contra el monitor real justo después, en el camino normal.
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;

        PreviewKeyDown += OnPreviewKeyDown;

        SourceInitialized += (_, _) =>
            NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// Mismo fundido + crecimiento desde el 95% que usa <c>NoteWindow.PlayOpenAnimation</c> — el
    /// usuario pidió que abrir Ajustes se sintiera igual que abrir una nota, en vez de aparecer de
    /// golpe. Se llama después del layout inicial, cuando la ventana ya está centrada en su monitor.
    /// </summary>
    internal void PlayOpenAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Opacity = 1;
            return;
        }

        var content = (UIElement)Content;
        content.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.95, 0.95);
        content.RenderTransform = scale;

        var duration = new Duration(OpenDuration);
        IEasingFunction Ease() => new QuinticEase { EasingMode = EasingMode.EaseOut };

        content.Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = Ease() });
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = Ease() });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.95, 1, duration) { EasingFunction = Ease() });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.95, 1, duration) { EasingFunction = Ease() });
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnCheckForUpdatesClick(object sender, RoutedEventArgs e) => _checkForUpdates?.Invoke();

    private void OnExitClick(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;

        _settingsService.Save(_settings);
        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        Application.Current.Shutdown();
    }

    private void OnInterfaceModeClick(object sender, RoutedEventArgs e)
    {
        _settings.SimplifiedMode = !_settings.SimplifiedMode;
        _settingsService.Save(_settings);
        UpdateInterfaceModeUi();
    }

    private void UpdateInterfaceModeUi()
    {
        AdvancedSettingsPanel.Visibility = _settings.SimplifiedMode
            ? Visibility.Collapsed
            : Visibility.Visible;
        InterfaceModeButton.Content = _settings.SimplifiedMode
            ? Strings.SwitchToCompleteMode
            : Strings.SwitchToSimplifiedMode;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        ToggleMaximized();

    private void ToggleMaximized()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            SizeToContent = SizeToContent.Height;
            RestoreNormalHeightLimit();
            return;
        }

        // SizeToContent y MaxHeight son útiles en modo normal para no cortar Ajustes, pero ambos
        // interfieren con el estado maximizado: WPF intenta medir el contenido y lo deja en el
        // límite del 90% en vez de ocupar el área de trabajo completa.
        SizeToContent = SizeToContent.Manual;
        MaxHeight = double.PositiveInfinity;
        WindowState = WindowState.Maximized;
    }

    private void RestoreNormalHeightLimit()
    {
        var monitor = MonitorLookup.MonitorAt(Left, Top, Width, Height, MonitorEnumerator.EnumerateMonitors());
        MaxHeight = (monitor?.WorkArea.Height ?? SystemParameters.WorkArea.Height) * 0.9;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaxHeight = double.PositiveInfinity;
            }
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized
                ? Strings.RestoreWindowTooltip
                : Strings.MaximizeWindowTooltip;
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
    }

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
        // puede fallar por causas ajenas a Aldune.
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
            Strings.QuickHelpMoveLine,
            Strings.QuickHelpMenu,
            Strings.QuickHelpEscape,
            hotkeyLine,
            Strings.QuickHelpTray,
            Strings.QuickHelpHideDock,
            Strings.QuickHelpDockMenus,
            Strings.QuickHelpAutoHideTasks,
            Strings.QuickHelpConflicts,
            Strings.QuickHelpSync,
            Strings.QuickHelpSearch,
            Strings.QuickHelpAutoScroll
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
            IsChecked = _settings.TargetMonitorId == null
                && (_settings.TargetMonitorIndex == null || _settings.TargetMonitorIndex >= monitors.Count)
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
                // Por identificador si lo hay: la posición de cada pantalla en la lista puede cambiar.
                IsChecked = _settings.TargetMonitorId is { } id
                    ? m.StableId == id
                    : _settings.TargetMonitorIndex == monitorIndex
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
            var targetId = targetIndex is { } index
                ? DockMonitorSelection.IdForLegacyIndex(MonitorEnumerator.EnumerateMonitors(), index)
                : null;
            if (_settings.TargetMonitorIndex != targetIndex || _settings.TargetMonitorId != targetId)
            {
                _settings.TargetMonitorIndex = targetIndex;
                _settings.TargetMonitorId = targetId;
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

    private void OnKeepDockOpenToggled(object sender, RoutedEventArgs e)
    {
        _settings.KeepDockOpen = KeepDockOpenCheck.IsChecked == true;
        _settingsService.Save(_settings);
        _coordinator?.RefreshAll();
    }

    /// <summary>
    /// El ajuste de gestos de trackpad solo se deja tocar si de verdad hay uno: sin hardware no hay
    /// nada que interpretar, y un interruptor que se puede encender sin efecto es peor que uno que
    /// explica por qué no está disponible. Se apaga también el valor guardado, para que no quede un
    /// <c>true</c> heredado de otro equipo dando vueltas.
    /// </summary>
    private void PopulateTrackpadGestures()
    {
        bool hasTouchpad = TouchpadDetector.HasPrecisionTouchpad();
        TrackpadGesturesCheck.IsEnabled = hasTouchpad;

        if (!hasTouchpad && _settings.TrackpadGestures)
        {
            _settings.TrackpadGestures = false;
            _settingsService.Save(_settings);
        }

        TrackpadGesturesCheck.IsChecked = hasTouchpad && _settings.TrackpadGestures;
        TrackpadGesturesHint.Text = hasTouchpad
            ? Strings.TrackpadGesturesHint
            : Strings.TrackpadGesturesNotFound;
    }

    private void OnTrackpadGesturesToggled(object sender, RoutedEventArgs e)
    {
        _settings.TrackpadGestures = TrackpadGesturesCheck.IsChecked == true;
        _settingsService.Save(_settings);
    }

    private void PopulateEdges()
    {
        EdgeListContainer.Children.Clear();
        AddEdgeRadio(EdgePosition.Right, Strings.EdgeRight);
        AddEdgeRadio(EdgePosition.Left, Strings.EdgeLeft);
        AddEdgeRadio(EdgePosition.Top, Strings.EdgeTop);
        AddEdgeRadio(EdgePosition.Bottom, Strings.EdgeBottom);
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

    private void OnAutoHideTasksToggled(object sender, RoutedEventArgs e)
    {
        _settings.AutoHideCompletedTasks = AutoHideTasksCheck.IsChecked == true;
        _settingsService.Save(_settings);
        UpdateAutoHideTasksUi();
    }

    /// <summary>El campo de plazo solo tiene sentido con el ajuste activado.</summary>
    private void UpdateAutoHideTasksUi()
    {
        AutoHideTasksDelayPanel.IsEnabled = AutoHideTasksCheck.IsChecked == true;
    }

    // Solo digitos: un desplegable de unidad al lado ya cubre "cuanto tiempo", y dejar pasar
    // letras o signos obligaria a validar despues en vez de evitarlo al teclear.
    private void OnDelayValuePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void OnDelayValueChanged(object sender, TextChangedEventArgs e)
    {
        // Mientras el numero no sea valido (cuadro vacio a mitad de borrar, o "0") no hay nada que
        // guardar todavia -- se guarda en cuanto vuelva a serlo, sin forzar un valor por defecto a
        // mitad de escritura.
        if (!int.TryParse(AutoHideTasksDelayValueBox.Text, out var value) || value < 1) return;

        if (_settings.AutoHideCompletedTasksDelayValue == value) return;
        _settings.AutoHideCompletedTasksDelayValue = value;
        _settingsService.Save(_settings);
    }

    private void OnTrashRetentionPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void OnTrashRetentionChanged(object sender, TextChangedEventArgs e)
    {
        if (!int.TryParse(TrashRetentionValueBox.Text, out var value) || value < 1) return;

        if (_settings.TrashRetentionDays == value) return;
        _settings.TrashRetentionDays = value;
        _settingsService.Save(_settings);
    }

    private void OnSyncEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        _settings.SyncEnabled = SyncEnabledCheck.IsChecked == true;
        _settingsService.Save(_settings);
        _coordinator?.ConfigureAutomaticSync();
        UpdateSyncUi();
    }

    private void OnSyncProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncUiReady || _loadingSyncProfile || SyncProfileBox.SelectedItem is not ComboBoxItem { Tag: string id }) return;

        // Persistir antes de cambiar evita que los cambios escritos en el perfil anterior se
        // mezclen con el nuevo (en especial la clave envuelta y las notas seleccionadas).
        _settingsService.Save(_settings);
        _settings.ActiveSyncProfileId = id;
        SyncProfileStore.LoadActiveToLegacy(_settings);

        _loadingSyncProfile = true;
        _syncUiReady = false;
        try
        {
            LoadSyncProfileFields();
            _settingsService.Save(_settings);
        }
        finally
        {
            _syncUiReady = true;
            _loadingSyncProfile = false;
        }
        _coordinator?.ConfigureAutomaticSync();
    }

    private void OnSyncNewProfileClick(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        _settingsService.Save(_settings);
        SyncProfileStore.Create(_settings, Strings.SyncProfileNewName(_settings.SyncProfiles.Count + 1));
        _settingsService.Save(_settings);
        ReloadSyncProfileUi();
        _coordinator?.ConfigureAutomaticSync();
    }

    private void OnSyncDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        if (_settings.SyncProfiles.Count <= 1)
        {
            MessageBox.Show(this, Strings.SyncProfileLastRemaining, Strings.SyncSectionTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, Strings.SyncProfileDeleteConfirm, Strings.SyncSectionTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _settingsService.Save(_settings);
        SyncProfileStore.DeleteActive(_settings);
        _settingsService.Save(_settings);
        ReloadSyncProfileUi();
        _coordinator?.ConfigureAutomaticSync();
    }

    private void OnSyncProfileNameLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        var profile = SyncProfileStore.GetActive(_settings);
        var name = SyncProfileNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = Strings.SyncProfileNewName(1);
        if (profile.Name == name) return;

        profile.Name = name;
        _settingsService.Save(_settings);
        PopulateSyncProfiles();
    }

    private void PopulateSyncProfiles()
    {
        _loadingSyncProfile = true;
        try
        {
            SyncProfileBox.Items.Clear();
            foreach (var profile in _settings.SyncProfiles)
            {
                SyncProfileBox.Items.Add(new ComboBoxItem { Content = profile.Name, Tag = profile.Id });
            }

            var index = _settings.SyncProfiles.FindIndex(profile => profile.Id == _settings.ActiveSyncProfileId);
            SyncProfileBox.SelectedIndex = index < 0 ? 0 : index;
            var activeName = SyncProfileStore.GetActive(_settings).Name;
            SyncProfileNameBox.Text = activeName;
            SyncProfileNameDisplayText.Text = activeName;
            bool hasProfileChoices = _settings.SyncProfiles.Count > 1;
            SyncProfileBox.Visibility = hasProfileChoices ? Visibility.Visible : Visibility.Collapsed;
            SyncProfileNameDisplay.Visibility = hasProfileChoices ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            _loadingSyncProfile = false;
        }
    }

    private void ReloadSyncProfileUi()
    {
        _loadingSyncProfile = true;
        _syncUiReady = false;
        try
        {
            PopulateSyncProfiles();
            LoadSyncProfileFields();
        }
        finally
        {
            _syncUiReady = true;
            _loadingSyncProfile = false;
        }
    }

    private void LoadSyncProfileFields()
    {
        SyncEnabledCheck.IsChecked = _settings.SyncEnabled;
        SyncAllNotesRadio.IsChecked = _settings.SyncScope == SyncScopeKind.AllNotes;
        SyncSelectedNotesRadio.IsChecked = _settings.SyncScope == SyncScopeKind.SelectedNotes;
        SyncTagRadio.IsChecked = _settings.SyncScope == SyncScopeKind.Tag;
        SyncFolderRadio.IsChecked = _settings.SyncTransport == SyncTransportKind.Folder;
        SyncServerRadio.IsChecked = _settings.SyncTransport == SyncTransportKind.Server;
        SyncWebDavRadio.IsChecked = _settings.SyncTransport == SyncTransportKind.WebDav;
        SyncFolderPathBox.Text = _settings.SyncFolderPath ?? string.Empty;
        SyncServerUrlBox.Text = _settings.SyncServerUrl ?? string.Empty;
        SyncServerTokenBox.Text = _syncService?.GetServerToken() ?? string.Empty;
        SyncWebDavUsernameBox.Text = _settings.SyncWebDavUsername ?? string.Empty;
        SyncWebDavPasswordBox.Password = _syncService?.GetWebDavPassword() ?? string.Empty;
        SyncCodeBox.Clear();
        SyncAutomaticCheck.IsChecked = _settings.SyncAutomatically;
        SyncIntervalValueBox.Text = Math.Clamp(_settings.SyncIntervalMinutes, 1, 1440).ToString();
        UpdateSyncLastSyncUi();
        UpdateSyncConflictsUi();
        UpdateSyncUi();
    }

    private void PopulateSyncTags()
    {
        SyncTagBox.Items.Clear();
        foreach (var tag in _coordinator?.GetSyncTags() ?? Array.Empty<string>())
            SyncTagBox.Items.Add(new ComboBoxItem { Content = tag, Tag = tag });
        SyncTagBox.SelectedIndex = SyncTagBox.Items.OfType<ComboBoxItem>()
            .ToList().FindIndex(item => string.Equals(item.Tag as string, _settings.SyncTag,
                StringComparison.OrdinalIgnoreCase));
    }

    private void OnSyncScopeChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady || sender is not RadioButton { IsChecked: true } radio) return;

        _settings.SyncScope = radio == SyncSelectedNotesRadio ? SyncScopeKind.SelectedNotes
            : radio == SyncTagRadio ? SyncScopeKind.Tag : SyncScopeKind.AllNotes;
        _settingsService.Save(_settings);
        UpdateSyncUi();
    }

    private void OnSyncChooseNotesClick(object sender, RoutedEventArgs e)
    {
        _coordinator?.OpenSyncNotesSelector(this);
        UpdateSyncUi();
    }

    private void OnSyncTagChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncUiReady || SyncTagBox.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.SyncTag = tag;
        _settingsService.Save(_settings);
        UpdateSyncUi();
    }

    private void OnSyncProviderChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady || sender is not RadioButton { IsChecked: true } radio) return;
        _settings.SyncTransport = radio == SyncServerRadio
            ? SyncTransportKind.Server
            : radio == SyncWebDavRadio
                ? SyncTransportKind.WebDav
                : SyncTransportKind.Folder;
        _settingsService.Save(_settings);
        UpdateSyncUi();
    }

    private void OnSyncValueChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncUiReady) return;
        _settings.SyncFolderPath = SyncFolderPathBox.Text.Trim();
        _settings.SyncServerUrl = SyncServerUrlBox.Text.Trim();
        _settingsService.Save(_settings);
        UpdateInsecureUrlWarning();
    }

    private void UpdateInsecureUrlWarning()
    {
        bool usesUrl = SyncServerRadio.IsChecked == true || SyncWebDavRadio.IsChecked == true;
        SyncInsecureUrlText.Visibility = usesUrl && SyncEndpointSecurity.ExposesCredentials(SyncServerUrlBox.Text.Trim())
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnSyncTokenLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady || _syncService is null) return;
        _syncService.SetServerToken(SyncServerTokenBox.Text);
        SyncServerTokenBox.Text = _syncService.GetServerToken();
        _settingsService.Save(_settings);
    }

    private void OnSyncWebDavUsernameLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        _settings.SyncWebDavUsername = SyncWebDavUsernameBox.Text.Trim();
        _settingsService.Save(_settings);
    }

    private void OnSyncWebDavPasswordLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady || _syncService is null) return;
        _syncService.SetWebDavPassword(SyncWebDavPasswordBox.Password);
        SyncWebDavPasswordBox.Password = _syncService.GetWebDavPassword();
    }

    private void OnSyncAutomaticToggled(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        _settings.SyncAutomatically = SyncAutomaticCheck.IsChecked == true;
        _settingsService.Save(_settings);
        _coordinator?.ConfigureAutomaticSync();
        UpdateSyncUi();
    }

    private void OnSyncIntervalPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void OnSyncIntervalLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_syncUiReady) return;
        if (!int.TryParse(SyncIntervalValueBox.Text, out var minutes))
            minutes = _settings.SyncIntervalMinutes;

        _settings.SyncIntervalMinutes = Math.Clamp(minutes, 1, 1440);
        SyncIntervalValueBox.Text = _settings.SyncIntervalMinutes.ToString();
        _settingsService.Save(_settings);
        _coordinator?.ConfigureAutomaticSync();
    }

    private void OnSyncBrowseClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Strings.SyncFolderLabel,
            SelectedPath = SyncFolderPathBox.Text
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        SyncFolderPathBox.Text = dialog.SelectedPath;
        _settings.SyncFolderPath = dialog.SelectedPath;
        _settingsService.Save(_settings);
    }

    private void OnSyncGenerateCodeClick(object sender, RoutedEventArgs e)
    {
        if (_syncService is null) return;
        try
        {
            SyncCodeBox.Text = _syncService.GetOrCreateSyncCode();
            Clipboard.SetText(SyncCodeBox.Text);
            SyncStatusText.Text = Strings.SyncCodeCopiedStatus;
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = Strings.SyncErrorStatus(ex.Message);
        }
    }

    private void OnSyncShareProfileClick(object sender, RoutedEventArgs e)
    {
        if (_syncService is null) return;
        try
        {
            SyncCodeBox.Text = _syncService.GetOrCreateShareCode();
            Clipboard.SetText(SyncCodeBox.Text);
            SyncStatusText.Text = Strings.SyncShareCodeCopiedStatus;
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = Strings.SyncErrorStatus(ex.Message);
        }
    }

    private void OnSyncRevokeAccessClick(object sender, RoutedEventArgs e)
    {
        if (_syncService is null || _settings.SyncEnabled != true) return;

        var answer = MessageBox.Show(this, Strings.SyncRevokeAccessConfirm, Strings.SyncSectionTitle,
            MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;

        try
        {
            if (_syncService.RevokeSharedAccess(out var error))
            {
                SyncCodeBox.Clear();
                SyncStatusText.Text = Strings.SyncRevokeAccessCompletedStatus;
            }
            else
            {
                SyncStatusText.Text = Strings.SyncErrorStatus(error ?? "The old profile codes could not be revoked.");
            }
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = Strings.SyncErrorStatus(ex.Message);
        }
    }

    private void OnSyncImportCodeClick(object sender, RoutedEventArgs e)
    {
        if (_syncService is null) return;
        try
        {
            _syncService.ImportSyncCode(SyncCodeBox.Text);
            ReloadSyncProfileUi();
            SyncStatusText.Text = Strings.SyncCodeImportedStatus;
        }
        catch (FormatException)
        {
            SyncStatusText.Text = Strings.SyncInvalidCode;
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = Strings.SyncErrorStatus(ex.Message);
        }
    }

    private void OnSyncNowClick(object sender, RoutedEventArgs e)
    {
        if (_syncService is null || _coordinator is null) return;
        try
        {
            var result = _coordinator.Synchronize();
            SyncStatusText.Text = result.Succeeded
                ? Strings.SyncCompletedStatus(result.Uploaded, result.Downloaded)
                : result.Error?.Contains("401", StringComparison.Ordinal) == true
                    ? Strings.SyncUnauthorizedStatus
                    : Strings.SyncErrorStatus(result.Error ?? "Unknown error");
            UpdateSyncLastSyncUi();
            UpdateSyncConflictsUi();
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = Strings.SyncErrorStatus(ex.Message);
        }
    }

    private void UpdateSyncUi()
    {
        bool enabled = SyncEnabledCheck.IsChecked == true;
        bool folder = SyncFolderRadio.IsChecked == true;
        bool server = SyncServerRadio.IsChecked == true;
        bool webDav = SyncWebDavRadio.IsChecked == true;
        SyncFolderRadio.IsEnabled = enabled;
        SyncServerRadio.IsEnabled = enabled;
        SyncWebDavRadio.IsEnabled = enabled;
        SyncFolderPathBox.IsEnabled = enabled && folder;
        SyncBrowseButton.IsEnabled = enabled && folder;
        SyncServerUrlBox.IsEnabled = enabled && !folder;
        SyncServerTokenGrid.Visibility = server ? Visibility.Visible : Visibility.Collapsed;
        SyncServerTokenBox.IsEnabled = enabled && server;
        SyncWebDavUsernameGrid.Visibility = webDav ? Visibility.Visible : Visibility.Collapsed;
        SyncWebDavPasswordGrid.Visibility = webDav ? Visibility.Visible : Visibility.Collapsed;
        SyncWebDavHintText.Visibility = webDav ? Visibility.Visible : Visibility.Collapsed;
        UpdateInsecureUrlWarning();
        SyncWebDavUsernameBox.IsEnabled = enabled && webDav;
        SyncWebDavPasswordBox.IsEnabled = enabled && webDav;
        SyncCodeBox.IsEnabled = enabled;
        SyncGenerateCodeButton.IsEnabled = enabled;
        SyncShareProfileButton.IsEnabled = enabled;
        SyncRevokeAccessButton.IsEnabled = enabled && _settings.WrappedSyncKey is not null;
        SyncImportCodeButton.IsEnabled = enabled;
        SyncNowButton.IsEnabled = enabled;
        SyncAllNotesRadio.IsEnabled = enabled;
        SyncSelectedNotesRadio.IsEnabled = enabled;
        SyncTagRadio.IsEnabled = enabled;
        SyncChooseNotesButton.IsEnabled = enabled;
        SyncTagBox.Visibility = _settings.SyncScope == SyncScopeKind.Tag ? Visibility.Visible : Visibility.Collapsed;
        SyncTagBox.IsEnabled = enabled;
        SyncAutomaticCheck.IsEnabled = enabled;
        SyncIntervalValueBox.IsEnabled = enabled && _settings.SyncAutomatically;
        SyncNewProfileButton.IsEnabled = true;
        SyncDeleteProfileButton.IsEnabled = _settings.SyncProfiles.Count > 1;
        SyncProfileNameBox.IsEnabled = true;
        if (!enabled) SyncStatusText.Text = Strings.SyncDisabledStatus;
        else if (string.IsNullOrWhiteSpace(SyncStatusText.Text)) SyncStatusText.Text = Strings.SyncReadyStatus;
        SyncSelectionSummary.Text = _settings.SyncScope == SyncScopeKind.SelectedNotes
            ? Strings.SyncSelectedCount(_settings.SyncNoteIds.Count)
            : _settings.SyncScope == SyncScopeKind.Tag ? (_settings.SyncTag ?? Strings.SyncChooseTag)
            : Strings.SyncScopeAll;
    }

    private void UpdateSyncLastSyncUi()
    {
        SyncLastSyncText.Text = _settings.LastSyncAt is { } at
            ? Strings.SyncLastSyncAt(at)
            : Strings.SyncLastSyncNever;
    }

    private void UpdateSyncConflictsUi()
    {
        var count = _syncService?.GetConflicts().Count ?? 0;
        SyncConflictsText.Text = count == 0 ? Strings.SyncNoConflicts : Strings.SyncConflictsCount(count);
        SyncConflictsButton.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        SyncConflictsButton.IsEnabled = _settings.SyncEnabled;
    }

    private void OnSyncConflictsClick(object sender, RoutedEventArgs e)
    {
        _coordinator?.OpenSyncConflicts();
    }

    private void PopulateDelayUnits()
    {
        DelayUnitContainer.Children.Clear();
        AddDelayUnitRadio(TaskDelayUnit.Minutes, Strings.TaskDelayMinutesUnit);
        AddDelayUnitRadio(TaskDelayUnit.Hours, Strings.TaskDelayHoursUnit);
        AddDelayUnitRadio(TaskDelayUnit.Days, Strings.TaskDelayDaysUnit);
        AddDelayUnitRadio(TaskDelayUnit.Weeks, Strings.TaskDelayWeeksUnit);
    }

    private void AddDelayUnitRadio(TaskDelayUnit unit, string label)
    {
        var radio = new RadioButton
        {
            GroupName = "DelayUnitGroup",
            Style = (Style)FindResource("DelayUnitToggleStyle"),
            Tag = unit,
            Content = label,
            IsChecked = _settings.AutoHideCompletedTasksDelayUnit == unit
        };
        radio.Checked += OnDelayUnitChanged;
        DelayUnitContainer.Children.Add(radio);
    }

    private void OnDelayUnitChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: TaskDelayUnit unit } && _settings.AutoHideCompletedTasksDelayUnit != unit)
        {
            _settings.AutoHideCompletedTasksDelayUnit = unit;
            _settingsService.Save(_settings);
        }
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
            // Aldune.Resources.Strings). Rehacer todas las ventanas abiertas —incluidas notas con
            // texto sin guardar— para simular un cambio en caliente sería más frágil que pedir un
            // reinicio, así que no se intenta.
        }
    }

    // --- Temas ---------------------------------------------------------------------------------

    private bool _loadingThemes;

    private NoteTheme ActiveTheme => NoteThemes.Resolve(_settings.ActiveThemeId, _settings.CustomThemes);

    /// <summary>Rehace la sección entera a partir de los ajustes. Se llama al abrir y cuando cambia
    /// la lista de temas (nuevo, duplicar, editar, eliminar). Los cambios de una opción no pasan por
    /// aquí: reconstruir el radio o el desplegable que se está usando le quitaba el foco de teclado a
    /// quien navega con las flechas.</summary>
    internal void RefreshThemeSection()
    {
        _loadingThemes = true;
        try
        {
            var active = ActiveTheme;
            ThemeCombo.Items.Clear();
            foreach (var theme in NoteThemes.All(_settings.CustomThemes))
            {
                ThemeCombo.Items.Add(new ComboBoxItem
                {
                    Content = Strings.ThemeDisplayName(theme),
                    Tag = theme.Id,
                    Style = (Style)FindResource("SyncProfileItemStyle"),
                    IsSelected = theme.Id == active.Id,
                });
            }

            RefreshActiveThemeDetails();

            FillRadios(ToneListContainer, "ToneGroup", _settings.NewNoteTone,
                (NoteTone.Light, Strings.ToneLight), (NoteTone.Dark, Strings.ToneDark), (NoteTone.Both, Strings.ToneBoth));
            FillRadios(AssignmentListContainer, "AssignmentGroup", _settings.ColorAssignment,
                (NoteColorAssignment.RotateAvoidNeighbors, Strings.AssignAvoidNeighbors),
                (NoteColorAssignment.Rotate, Strings.AssignRotate),
                (NoteColorAssignment.MostDistinct, Strings.AssignMostDistinct),
                (NoteColorAssignment.Fixed, Strings.AssignFixed));
        }
        finally
        {
            _loadingThemes = false;
        }
    }

    /// <summary>Lo que depende del tema activo y de la regla, sin tocar el desplegable ni los
    /// radios: la tira del tema, los botones de editar y eliminar, las pastillas del color fijo y el
    /// botón de aplicar.</summary>
    private void RefreshActiveThemeDetails()
    {
        var active = ActiveTheme;
        NoteSwatchPanel.Fill(ThemePreview, active, selectedColor: null, onClick: (_, _) => { });
        ThemeEditButton.IsEnabled = ThemeDeleteButton.IsEnabled = !active.IsBuiltIn;
        ApplyThemeButton.IsEnabled = _coordinator is { ActiveNoteCount: > 0 };

        bool isFixed = _settings.ColorAssignment == NoteColorAssignment.Fixed;
        FixedColorSwatches.Visibility = isFixed ? Visibility.Visible : Visibility.Collapsed;
        if (isFixed)
        {
            var fixedColor = NoteColorAssigner.Assign(active, _settings.NewNoteTone,
                NoteColorAssignment.Fixed, _settings.FixedNoteColor, []);
            NoteSwatchPanel.Fill(FixedColorSwatches, active, fixedColor, OnFixedColorClick);
        }
    }

    private void FillRadios<T>(Panel host, string group, T current, params (T Value, string Label)[] options)
        where T : struct, Enum
    {
        host.Children.Clear();
        foreach (var (value, label) in options)
        {
            var radio = new RadioButton
            {
                GroupName = group,
                Style = (Style)FindResource("MonitorRadioStyle"),
                Content = label,
                Tag = value,
                IsChecked = EqualityComparer<T>.Default.Equals(value, current),
            };
            radio.Checked += OnThemeRadioChecked;
            host.Children.Add(radio);
        }
    }

    private void OnThemeRadioChecked(object sender, RoutedEventArgs e)
    {
        if (_loadingThemes || sender is not RadioButton { Tag: var tag }) return;
        if (tag is NoteTone tone) _settings.NewNoteTone = tone;
        if (tag is NoteColorAssignment rule) _settings.ColorAssignment = rule;
        SaveThemeSettings(rebuild: false);
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingThemes || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        _settings.ActiveThemeId = id;
        SaveThemeSettings(rebuild: false);
    }

    private void OnFixedColorClick(object sender, MouseButtonEventArgs e)
    {
        _settings.FixedNoteColor = (string)((Border)sender).Tag;
        SaveThemeSettings(rebuild: false);
    }

    private void OnThemeNewClick(object sender, RoutedEventArgs e)
    {
        var created = ThemeEditorWindow.Show(this, new NoteTheme
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Strings.ThemeNewName,
        }, isNew: true);
        if (created is null) return;

        _settings.CustomThemes.Add(created);
        _settings.ActiveThemeId = created.Id;
        SaveThemeSettings();
    }

    private void OnThemeDuplicateClick(object sender, RoutedEventArgs e)
    {
        var source = ActiveTheme;
        var copy = NoteThemes.Duplicate(source, Strings.ThemeCopyName(Strings.ThemeDisplayName(source)));
        _settings.CustomThemes.Add(copy);
        _settings.ActiveThemeId = copy.Id;
        SaveThemeSettings();
    }

    private void OnThemeEditClick(object sender, RoutedEventArgs e)
    {
        var current = ActiveTheme;
        if (current.IsBuiltIn) return;

        var edited = ThemeEditorWindow.Show(this, current, isNew: false);
        if (edited is null) return;

        int index = _settings.CustomThemes.FindIndex(theme => theme.Id == current.Id);
        if (index >= 0) _settings.CustomThemes[index] = edited;
        SaveThemeSettings();
    }

    private void OnThemeDeleteClick(object sender, RoutedEventArgs e)
    {
        var current = ActiveTheme;
        if (current.IsBuiltIn) return;

        var answer = MessageBox.Show(this, Strings.ThemeDeleteConfirm(current.Name), Strings.AppName,
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        _settings.CustomThemes.RemoveAll(theme => theme.Id == current.Id);
        _settings.ActiveThemeId = null;
        SaveThemeSettings();
    }

    private void OnApplyThemeClick(object sender, RoutedEventArgs e)
    {
        if (_coordinator is null) return;

        int count = _coordinator.ActiveNoteCount;
        if (count == 0) return;

        var answer = MessageBox.Show(this, Strings.ApplyThemeConfirm(count),
            Strings.ApplyThemeConfirmTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        _coordinator.ApplyThemeToActiveNotes();
    }

    /// <summary>Guarda y repinta. <paramref name="rebuild"/> solo cuando cambia la lista de temas; si
    /// no, se actualiza lo que depende de la opción sin quitar el foco al control que se usa.</summary>
    private void SaveThemeSettings(bool rebuild = true)
    {
        _settingsService.Save(_settings);
        if (rebuild) RefreshThemeSection();
        else RefreshActiveThemeDetails();
        _coordinator?.RefreshAll();
    }
}

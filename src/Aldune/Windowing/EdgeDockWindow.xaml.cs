using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();

    /// <summary>Cuándo abrir y cerrar según el ratón (tiempos y reglas en Core, con tests).</summary>
    private readonly DockHoverPolicy _hover;
    private readonly System.Diagnostics.Stopwatch _hoverClock = System.Diagnostics.Stopwatch.StartNew();
    private readonly DispatcherTimer _hoverPollTimer;
    private readonly DispatcherTimer _fullscreenPollTimer;
    private readonly DispatcherTimer _arrowScrollTimer;
    private readonly DispatcherTimer _openAllMenuTopmostTimer;
    private readonly DispatcherTimer _syncFeedbackTimer;
    private readonly AutoScrollManager? _autoScroll;
    private readonly PopupToggle _newNoteToggle;
    private readonly PopupToggle _openAllToggle;
    private readonly PopupToggle _dockViewToggle;
    private readonly PopupToggle _tabMenuToggle;

    /// <summary>Si hay una sincronización en curso desde el botón del dock (ver OnSyncClick).</summary>
    private bool _syncBusy;
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardHookProc;
    private readonly NativeMethods.LowLevelMouseProc _mouseHookProc;
    private IntPtr _mouseHook;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;

    /// <summary>El área de trabajo de la pantalla tal cual: <see cref="_workingArea"/> puede dejar libre
    /// el canto a una barra de tareas que se oculta sola, y esa diferencia no es un cambio de pantalla.</summary>
    private readonly WorkingArea _monitorWorkArea;
    private readonly string _monitorKey;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly AppSettings? _settings;
    private int _noteCount;
    private DockViewKind? _lastView;
    private string? _lastTagFilter;
    /// <summary>Set by <see cref="ReloadLookSoftly"/>: the next <see cref="Refresh"/> goes through the same soft swap as a change of view.</summary>
    private bool _lookReload;
    /// <summary>1 for a change of view; a bit lower for a change of look (shadow, corners...), which people toggle back and forth and which should not make them wait.</summary>
    private double _swapSpeed = 1;

    /// <summary>Pantalla de destino elegida en el menú de "abrir todas", o <c>null</c> para la de este
    /// dock. Se reinicia cada vez que se abre el menú (ver <see cref="PopulateOpenAllMonitors"/>).</summary>
    private string? _layoutMonitorKey;

    /// <summary>Margen que el dock aguanta desplegado tras una acción que invita a otra consecutiva
    /// (cambiar de vista o de etiqueta para buscar una nota), aunque el cursor ya no esté encima.</summary>
    private static readonly TimeSpan InteractionGrace = TimeSpan.FromSeconds(4);

    /// <summary>Hasta cuándo vale ese margen; <see cref="DateTime.MinValue"/> si no hay ninguno activo.</summary>
    private DateTime _interactionGraceUntil = DateTime.MinValue;

    /// <summary>
    /// Margen que el dock aguanta desplegado tras abrir otra ventana desde él (gestionar notas,
    /// ajustes, sincronizar…). Sin él, si el cursor llegó al botón antes de que el abanico terminara
    /// de desplegarse —pulsación rápida nada más asomar el dock— la ventana abierta roba el foco y el
    /// cursor queda sobre un hueco que el sondeo interpreta como salida: el dock se pliega bajo los
    /// pies.
    /// </summary>
    private static readonly TimeSpan HoverOpenGrace = TimeSpan.FromMilliseconds(1200);

    /// <summary>The grace after opening the manager or Settings: enough for the window to appear and take the focus; the dock does not hold on for longer.</summary>
    private static readonly TimeSpan OwnWindowGrace = TimeSpan.FromMilliseconds(500);

    /// <summary>Hasta cuándo vale <see cref="HoverOpenGrace"/>; <c>MinValue</c> si no hay ninguno.</summary>
    private DateTime _hoverGraceUntil = DateTime.MinValue;

    private bool _hoverLayoutHold;
    private bool _buttonWasDown;
    private double _hoverLayoutAnchorX;
    private double _hoverLayoutAnchorY;
    private bool _hiddenByFullscreenApp;
    private bool _scrollIndicatorDragging;
    private int _arrowScrollDirection;
    private int _arrowScrollTicks;
    private double _scrollIndicatorDragStartY;
    private double _scrollIndicatorDragStartOffset;
    private double _scrollIndicatorInset = 8;
    private bool _scrollIndicatorUpdateQueued;

    private IntPtr _hwnd;
    private IntPtr _keyboardHook;

    // La ventana no se redimensiona nunca (ver EdgeGeometry): siempre ocupa WindowRect. Animar
    // Left/Top/Width/Height de un HWND obliga a WPF a rehacer el layout en cada frame intermedio, y
    // de ahí salía toda la familia de fallos que documenta docs/STATUS.md.
    //
    // Lo que se anima es puro WPF sobre el contenido. Ya no hay región que recalcular por frame ni
    // bucle de CompositionTarget.Rendering: con AllowsTransparency la forma la dibuja WPF.
    private readonly Dictionary<int, Button> _tabButtons = new();

    // Para animar solo las pestañas nuevas al crear una nota, en vez de rehacer la entrada entera.
    private HashSet<Guid> _knownNoteIds = new();

    private IReadOnlyDictionary<Guid, DateTimeOffset> _pendingReminders = new Dictionary<Guid, DateTimeOffset>();

    private Thickness _tabMargin = new(0, 0, 0, EdgeGeometry.TabGap);

    private bool IsTopBottomEdge => _edge is EdgePosition.Top or EdgePosition.Bottom;

    public EdgeDockWindow(
        EdgePosition edge,
        MonitorInfo monitor,
        NotesRepository repository,
        AppCoordinator coordinator,
        AppSettings? settings = null)
    {
        InitializeComponent();
        _edge = edge;
        ApplyEdgeAlignment();
        SpaceFooterButtons();
        // Una barra de tareas que se oculta sola en este mismo canto aparece al llevar el ratón a su
        // última fila, y la botonera del dock desplegado la tapaba (medido: la barra no llegaba a
        // salir). El dock le deja esas filas libres, y empujar contra el canto pasa a ser su gesto.
        _monitorWorkArea = monitor.WorkArea;
        bool sharesTaskbar = NativeMethods.HasAutoHideTaskbar(edge, monitor.WorkArea, monitor.DpiScale);
        _workingArea = sharesTaskbar
            ? DockHoverZone.Inset(monitor.WorkArea, edge, DockHoverZone.AutoHideTaskbarClearance)
            : monitor.WorkArea;
        var (beyondX, beyondY) = DockHoverZone.PointBeyondEdge(monitor.WorkArea, edge);
        bool sharesEdge = sharesTaskbar
            || NativeMethods.HasMonitorAt(beyondX * monitor.DpiScale, beyondY * monitor.DpiScale);
        _hover = new DockHoverPolicy(sharesEdge ? DockHoverTuning.SharedEdge : DockHoverTuning.PhysicalEdge);
        _monitorKey = monitor.DeviceName;
        _repository = repository;
        _coordinator = coordinator;
        _settings = settings;
        ViewButton.Visibility = settings?.DockViewButton == true ? Visibility.Visible : Visibility.Collapsed;
        _keyboardHookProc = OnGlobalKeyboardHook;
        _mouseHookProc = OnGlobalMouseHook;

        TabsScroll.ScrollChanged += OnTabsScrollChanged;
        TabsScroll.SizeChanged += (_, _) => { QueueScrollIndicatorUpdate(); ReplaceViewLabelIfOpen(); };
        SizeChanged += (_, _) => ReplaceViewLabelIfOpen();
        ScrollOverlay.SizeChanged += (_, _) => QueueScrollIndicatorUpdate();

        _fanState.ExpansionChanged += (_, _) => ApplyState(animate: true);

        // Deliberadamente no MouseEnter/MouseLeave de WPF: sondear la posición real del cursor no
        // depende de que Win32 acierte con su seguimiento, y la ventana es mayormente transparente,
        // así que sus propios eventos de ratón no coinciden con lo que se ve. Ver docs/STATUS.md.
        _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _hoverPollTimer.Tick += (_, _) => PollHoverState();
        _wheelSessionTimer.Tick += OnWheelSessionTimer;
        _wheelHookProc = OnWheelHook;
        _hoverPollTimer.Start();

        // Aparte y mucho más lento: pasar a pantalla completa no hay que detectarlo en 50ms, y
        // quien tiene un juego delante agradece que no le sondeen el primer plano 20 veces/s.
        _fullscreenPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _fullscreenPollTimer.Tick += (_, _) =>
        {
            PollFullscreenApp();
            KeepWindowInPlace();
            KeepStripOnTop();
            RepaintStripIfUnpainted();
        };
        _fullscreenPollTimer.Start();

        // No depende del foco de la ventana. El dock usa WS_EX_NOACTIVATE para no robar el foco a la
        // aplicación que el usuario está usando, pero sus flechas deben responder al clic y mantener
        // pulsado aunque esa aplicación siga activa.
        _arrowScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _arrowScrollTimer.Tick += (_, _) =>
        {
            if (_arrowScrollDirection == 0) return;

            _arrowScrollTicks++;
            double multiplier = _arrowScrollTicks < 5
                ? 1
                : _arrowScrollTicks < 12
                    ? 1.35
                    : 1.7;
            ScrollByArrow(_arrowScrollDirection, ArrowScrollStep * multiplier);
        };

        // Un Popup tiene su propio HWND y se crea de forma asíncrona. Mientras el selector está
        // abierto lo reafirmamos por encima de las notas Topmost, que de otro modo pueden volver a
        // colocarse delante justo después de abrirse.
        _openAllMenuTopmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _openAllMenuTopmostTimer.Tick += (_, _) =>
        {
            RaiseOpenAllMenu();
            RaiseNewNoteMenu();
            RaiseDockViewPopup();
        };

        // El menú de una pestaña se queda fijo: no se cierra solo por alejar el ratón. Antes había un
        // timer de 550ms que lo pliegueaba en cuanto el cursor salía de la pestaña o del propio menú,
        // y el usuario lo pedía abierto "más rato o fijo" — el cierre pasa a ser suyo (un clic fuera,
        // o elegir una opción), que es como se comportan los menús del resto de la app.

        // El feedback de sincronizar (✓ o aviso en el botón) se deshace solo: al poco rato vuelve la
        // flecha de siempre con su tooltip.
        _syncFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _syncFeedbackTimer.Tick += (_, _) =>
        {
            _syncFeedbackTimer.Stop();
            RestoreSyncButton();
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNonActivating(_hwnd);
            _keyboardHook = NativeMethods.InstallKeyboardHook(_keyboardHookProc);
            ApplyWindowRect();
        };

        foreach (var button in new[] { OpenAllButton, ManageArchiveButton, SyncButton, NewNoteButton })
            ToolTipService.SetInitialShowDelay(button, (int)DockHoverTuning.ButtonTooltipDelay.TotalMilliseconds);
        SyncButton.ToolTip = Strings.WithHint(Strings.SyncNowButton, Strings.SyncRightClickHint);

        _autoScroll = new AutoScrollManager(NotesColumn, TabsScroll, null);
        _newNoteToggle = new PopupToggle(NewNoteMenuPopup);
        _openAllToggle = new PopupToggle(OpenAllMenuPopup);
        _dockViewToggle = new PopupToggle(DockViewPopup);
        _tabMenuToggle = new PopupToggle(TabMenuPopup);
        Closed += (_, _) => _autoScroll.Stop();

        foreach (var popup in DockPopups)
        {
            popup.Opened += (_, _) => WatchClicksOutside();
            popup.Closed += (_, _) => { if (!DockPopups.Any(p => p.IsOpen)) StopWatchingClicksOutside(); };
        }

        ApplyWindowRect();
        ApplyState(animate: false);
    }

    /// <summary>
    /// Ajusta el layout interno al borde físico. Derecha/izquierda usan una columna vertical;
    /// arriba/abajo mantienen una tira horizontal en reposo, pero despliegan las tarjetas en una
    /// columna vertical ancha. El HWND ya tiene el rectángulo correcto gracias a
    /// <see cref="EdgeGeometry.WindowRect"/>; aquí solo se cambia la disposición interna.
    /// </summary>
    /// <summary>El relleno del pie del dock: el mismo en los cuatro lados (y en los cuatro cantos del dock).</summary>
    private const double FooterPadding = 5;

    /// <summary>The round bar's ends curve away from the first and last button, so with the same 5 px as above and below it looked tighter at the
    /// sides; the round look gets 4 px more there, the square one keeps its even 5.</summary>
    private void ApplyFooterPadding()
    {
        double side = ThemeManager.IsSquare ? FooterPadding : FooterPadding + 4.5;
        FooterBorder.Padding = new Thickness(side, FooterPadding, side, FooterPadding);
    }

    /// <summary>Separación entre los botones del pie: igual que el relleno, para que el ritmo sea el mismo en todos los sentidos (y el pie cabe en los docks laterales).</summary>
    private const double FooterButtonGap = 5;

    /// <summary>
    /// La separación va entre botones, no a la izquierda de cada uno: con <c>Margin="10,0,0,0"</c> en todos, el primero (el del extremo) tenía 10 px
    /// de más por ese lado (17 frente a 7 en el otro extremo) y el pie parecía descentrado. Se pone después de espejar el orden (dock a la
    /// izquierda), porque el primero de la fila cambia.
    /// </summary>
    private void SpaceFooterButtons()
    {
        bool first = true;
        foreach (var child in FooterPanel.Children.OfType<FrameworkElement>())
        {
            child.Margin = new Thickness(first ? 0 : FooterButtonGap, 0, 0, 0);
            first = false;
        }
    }

    private void ApplyEdgeAlignment()
    {
        // La columna útil tiene el mismo ancho que una pestaña en todas las orientaciones. Al ser un
        // elemento real del árbol visual, WPF la centra desde la primera medida; no hay que calcular
        // márgenes con ActualWidth, que todavía vale cero durante el primer pintado.
        NotesColumn.Width = EdgeGeometry.TabWidth;
        NotesColumn.HorizontalAlignment = HorizontalAlignment.Center;

        if (IsTopBottomEdge)
        {
            ConfigureTopBottomLayout();
            SetScrollIndicatorInset(8);
            return;
        }

        if (_edge != EdgePosition.Left)
        {
            SetScrollIndicatorInset(EdgeGeometry.TabShadowHeadroom);
            return;
        }

        // La causa raíz: el Grid que contiene todo lo demás reserva su margen de sombra en los tres
        // lados "de dentro" (izquierda/arriba/abajo) y ninguno "a ras" (derecha) — correcto para
        // EdgePosition.Right, donde ese lado ya toca el canto real de la pantalla. A la izquierda es
        // al revés. Sin espejar esto, da igual cómo se alineen RestStrip/FooterBorder/pestañas por
        // dentro: seguirían viviendo en un lienzo ya encogido por el lado equivocado.
        ContentGrid.Margin = new Thickness(0, ContentGrid.Margin.Top, ContentGrid.Margin.Left, ContentGrid.Margin.Bottom);

        RestStrip.HorizontalAlignment = HorizontalAlignment.Left;
        RestStrip.Margin = new Thickness(RestStrip.Margin.Right, 0, 0, 0);

        // El pie va centrado bajo la columna de notas en los dos lados, así que no hay nada que espejar (antes iba pegado al borde y,
        // con los botones de 40, quedaba casi centrado por casualidad; con los de 32 se habría ido 19 px hacia el canto).
        FooterBorder.HorizontalAlignment = HorizontalAlignment.Center;
        FooterBorder.Margin = new Thickness(0, FooterBorder.Margin.Top, 0, 0);

        // La barra acompaña el canto físico del dock, no el canto interior de la tarjeta. Así en el
        // lado izquierdo queda a la izquierda y deja de parecer una pieza flotante dentro del panel.
        ScrollOverlay.Width = 12;
        ScrollOverlay.HorizontalAlignment = HorizontalAlignment.Left;
        ScrollOverlay.Margin = new Thickness(0);
        ScrollOverlay.RenderTransform = new TranslateTransform();
        SetScrollIndicatorInset(EdgeGeometry.TabShadowHeadroom);

        // Orden invertido, no solo el grupo movido de sitio: en el XAML (pensado para la derecha) el
        // botón "+" es el último y por tanto el más cercano al canto real de la pantalla (el grupo
        // está pegado a la derecha, así que el último de la fila es el que toca el borde). Si solo
        // se movía el grupo entero a la izquierda sin tocar el orden, "+" pasaba a ser el más LEJANO
        // del canto en vez del más cercano — cada botón cambiaba su posición relativa a la pantalla,
        // que es justo lo que un espejo no debería hacer. Invertir la lista de hijos conserva la
        // distancia de cada botón al borde real, en vez de conservar su orden de lectura.
        var buttons = FooterPanel.Children.Cast<UIElement>().Reverse().ToList();
        FooterPanel.Children.Clear();
        foreach (var button in buttons) FooterPanel.Children.Add(button);
    }

    private void ConfigureTopBottomLayout()
    {
        bool top = _edge == EdgePosition.Top;

        // El canto físico queda sin margen; los otros tres lados conservan espacio para la sombra.
        // El contenedor raíz ocupa toda la ventana para que la barra de reposo tenga 226px. El
        // panel desplegado conserva los 9px laterales y deja las tarjetas en sus 208px útiles.
        ContentGrid.Margin = top
            ? new Thickness(0, 0, 0, 18)
            : new Thickness(0, 18, 0, 0);
        FanPanel.Margin = new Thickness(9, 0, 9, 0);

        // Resting state is a horizontal rail; expanded top/bottom docks stack wide cards inward.
        RestStrip.HorizontalAlignment = HorizontalAlignment.Center;
        RestStrip.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        // La tira crece con el número de notas, mientras FanPanel conserva el ancho útil de las
        // tarjetas. Al empezar la app todavía no conocemos el monitor final de SetNotes, así que
        // aquí se usa el mínimo y luego se recalcula al cargar las notas.
        RestStrip.Width = EdgeGeometry.WindowThickness;
        RestStrip.Height = 20;
        RestStrip.Margin = top
            ? new Thickness(0, 5, 0, 0)
            : new Thickness(0, 0, 0, 5);
        RestStrip.Padding = new Thickness(0);
        // No padding here, so nothing to cancel: with the -5 the relief stuck out 5 px over the edge of the strip (an extra band of light grey on the
        // top and bottom docks in the looks that have relief).
        RestStripBevel.Margin = new Thickness(0);
        RestList.ItemsPanel = (ItemsPanelTemplate)FindResource("HorizontalItemsPanelTemplate");
        RestList.ItemTemplate = (DataTemplate)FindResource("HorizontalRestDashTemplate");
        // Cada guion lleva margen derecho para crear el hueco entre elementos. Se descuenta el
        // último de esos márgenes para que la pastilla se ciña al contenido y deje el mismo aire a
        // izquierda y derecha.
        RestList.Margin = new Thickness(0, 0, -EdgeGeometry.RestGap, 0);
        RestList.HorizontalAlignment = HorizontalAlignment.Center;
        RestList.VerticalAlignment = VerticalAlignment.Center;

        FanPanel.RowDefinitions.Clear();
        if (top)
        {
            FanPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FanPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        else
        {
            FanPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            FanPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        FanPanel.ColumnDefinitions.Clear();

        // Los controles viven junto al canto físico. Las tarjetas ocupan el espacio interior y
        // empiezan justo después de la botonera, en vez de dejar un hueco grande entre ambas capas.
        Grid.SetRow(FooterBorder, top ? 0 : 1);
        Grid.SetRow(NotesColumn, top ? 1 : 0);

        // En arriba/abajo la columna de tarjetas y sus controles comparten el mismo rectángulo desde
        // el primer layout. Así no aparecen descentrados durante la primera apertura ni se recolocan
        // después de que WPF haya medido FanPanel.
        NotesColumn.Width = EdgeGeometry.TabWidth;
        NotesColumn.HorizontalAlignment = HorizontalAlignment.Center;
        TabsScroll.ClearValue(FrameworkElement.WidthProperty);
        TabsScroll.Width = EdgeGeometry.TabWidth;
        TabsScroll.HorizontalAlignment = HorizontalAlignment.Center;
        TabsScroll.Margin = new Thickness(0);
        ScrollOverlay.Width = 12;
        ScrollOverlay.HorizontalAlignment = HorizontalAlignment.Right;
        ScrollOverlay.Margin = new Thickness(0);
        ScrollOverlay.RenderTransform = null;
        ScrollOverlay.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        ScrollArrowOverlay.Orientation = Orientation.Vertical;
        ScrollArrowOverlay.Width = 26;
        ScrollArrowOverlay.HorizontalAlignment = HorizontalAlignment.Right;
        // Fuera de la columna, a su derecha: centradas encima de las tarjetas tapaban el borde de una. La ventana de arriba/abajo es
        // más ancha que la columna cuando hay muchas notas (que es cuando salen estas flechas), así que ahí hay sitio.
        ScrollArrowOverlay.Margin = new Thickness(0, 0, -(ScrollArrowOverlay.Width + 6), 0);
        ScrollArrowOverlay.RenderTransform = null;
        if (ScrollArrowOverlay.Children.Count >= 2)
        {
            ((Button)ScrollArrowOverlay.Children[0]).Content = new TextBlock
            {
                Text = "↑",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            ((Button)ScrollArrowOverlay.Children[1]).Content = new TextBlock
            {
                Text = "↓",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
        }

        TabsScroll.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        TabsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        TabsScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        TabsList.ClearValue(ItemsControl.ItemsPanelProperty);
        TabsList.Margin = new Thickness(0, EdgeGeometry.TabShadowHeadroom, 0, 0);

        FooterBorder.HorizontalAlignment = HorizontalAlignment.Center;
        FooterBorder.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        ApplyFooterPadding();
        // Visible gap of ~8 px between the button bar and the notes on the four edges (it was 16, then 24 before that). On top the first card
        // already carries 14 px of shadow headroom (TabShadowHeadroom), so the margin there is negative (-6); at the bottom and on the sides
        // the last card sits flush with its zone. EdgeGeometry.FooterLength follows (button 32 + margin 8 + padding 10 + shadow air).
        FooterBorder.Margin = top
            ? new Thickness(0, 0, 0, -6)
            : new Thickness(0, 8, 0, 0);

    }

    /// <summary>
    /// Además de alinearse a la izquierda (ver <see cref="ApplyEdgeAlignment"/>), cada pestaña tiene
    /// su propia forma pensada para el dock a la derecha: redondeada solo por la izquierda, con el
    /// lado derecho a ras del canto físico de la pantalla ("redondearlo dejaría ver el escritorio
    /// por una muesca", ver <c>NoteTabButtonStyle</c> en el XAML). Con el dock a la izquierda es al
    /// revés: el lado izquierdo es el que toca el canto real, así que la curva y el filo del borde
    /// tienen que espejarse — pedido explícito del usuario tras ver la captura ("la curva debería
    /// estar a la derecha, como un espejo"). El texto de dentro (título/vista previa) no se toca:
    /// sigue alineado a la izquierda igual que siempre, según pidió también.
    /// </summary>
    private void ApplyLeftEdgeTabShape(Button button)
    {
        button.HorizontalAlignment = HorizontalAlignment.Left;

        if (button.Template.FindName("CardBorder", button) is Border card)
        {
            card.CornerRadius = Mirror(card.CornerRadius);
            card.BorderThickness = MirrorHorizontal(card.BorderThickness);
            // Lo lee el XAML para espejar el hueco del texto junto a la onda (ver LabelStack).
            card.Tag = "LeftEdge";
        }

        if (button.Template.FindName("SheenBorder", button) is Border sheen)
        {
            sheen.CornerRadius = Mirror(sheen.CornerRadius);
            sheen.Background = (Brush)FindResource("TabSheenLeftEdge");
        }

        if (button.Template.FindName("HoverOverlay", button) is Border hover)
        {
            hover.CornerRadius = Mirror(hover.CornerRadius);
        }

        // La franja de las pieles Stripe/Tinted va en el lado interior (aquí, el derecho) y la onda
        // del canal en el opuesto, lejos del aviso de recordatorio.
        if (button.Template.FindName("StripeBar", button) is Border stripe)
            stripe.BorderThickness = new Thickness(0, 0, 3, 0);

        if (button.Template.FindName("Waveform", button) is System.Windows.Shapes.Path wave)
        {
            wave.HorizontalAlignment = HorizontalAlignment.Left;
            wave.Margin = MirrorHorizontal(wave.Margin);
        }
    }

    private static CornerRadius Mirror(CornerRadius r) => new(r.TopRight, r.TopLeft, r.BottomLeft, r.BottomRight);

    private static Thickness MirrorHorizontal(Thickness t) => new(t.Right, t.Top, t.Left, t.Bottom);

    /// <summary>
    /// Fija el rectángulo de la ventana. Se llama al construir y cuando cambia el número de notas
    /// (que cambia su longitud), nunca como parte de una animación.
    /// </summary>
    private void ApplyWindowRect()
    {
        var rect = EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount, Alignment);
        if (_hwnd != IntPtr.Zero && IsVisible)
        {
            // One step: with four separate assignments the window passed through a rectangle that mixed the old position and the new size.
            var dpi = VisualTreeHelper.GetDpi(this);
            int Px(double dip, double scale) => (int)Math.Round(dip * scale, MidpointRounding.AwayFromZero);
            if (NativeMethods.SetBounds(_hwnd, Px(rect.X, dpi.DpiScaleX), Px(rect.Y, dpi.DpiScaleY), Px(rect.Width, dpi.DpiScaleX), Px(rect.Height, dpi.DpiScaleY))) return;
        }
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    /// <summary>Runs <paramref name="action"/> after <paramref name="frames"/> frames have been rendered (one tick of CompositionTarget.Rendering is one frame).</summary>
    private static void AfterFrames(int frames, Action action)
    {
        int seen = 0;
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (++seen < frames) return;
            CompositionTarget.Rendering -= handler;
            action();
        };
        CompositionTarget.Rendering += handler;
    }

    private void PollHoverState()
    {
        if (_hiddenByFullscreenApp) return;

        // Fuera de pantalla completa el dock no tiene ningún camino válido que lo oculte. Si WPF o
        // el gestor de ventanas lo deja no visible, repararlo aquí evita que vuelva solo al pasar el
        // ratón por la zona sin convertir la inactividad en una orden de ocultar.
        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            ApplyState(animate: false);
            if (_hwnd != IntPtr.Zero) NativeMethods.EnsureTopmost(_hwnd);
        }

        if (_settings?.KeepDockOpen == true)
        {
            _hover.Hold();
            if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
            return;
        }

        // Con el menú de una pestaña abierto, el abanico se queda como está: el menú sale fuera de
        // la zona sensible del dock, así que mover el ratón hacia él contaría como salir y lo
        // cerraría justo cuando el usuario va a pulsarlo. Hold: al cerrarse el menú cuenta como uso,
        // y el dock espera el cierre largo en vez de plegarse al instante (medido: 90 ms).
        if (TabMenuPopup.IsOpen
            || OpenAllMenuPopup.IsOpen
            || DockViewPopup.IsOpen
            || NewNoteMenuPopup.IsOpen
            || TagEditorPopup.IsOpen
            || _autoScroll is { IsActive: true })
        {
            _hover.Hold();
            return;
        }

        // A click outside the dock folds it at once, whatever grace is running (the one after a change of view, after opening a window, while
        // syncing): those graces exist so that the dock does not fold under a cursor that is about to use it, and a click elsewhere says it is not.
        // Only the moment of the press counts, so a drag that starts on the dock (its scroll bar) and leaves it is not a click outside.
        bool buttonDown = NativeMethods.IsLeftButtonDown();
        bool freshPress = buttonDown && !_buttonWasDown;
        _buttonWasDown = buttonDown;
        if (freshPress && _fanState.IsExpanded && !_dragging && !_keyboardMode && IsVisible && _noteCount > 0)
        {
            var clickDpi = VisualTreeHelper.GetDpi(this);
            var click = NativeMethods.GetCursorScreenPosition();
            if (!IsInsideExpandedSurface(ExpandedSurfaces().ToList(), click.X / clickDpi.DpiScaleX, click.Y / clickDpi.DpiScaleY))
            {
                _interactionGraceUntil = DateTime.MinValue;
                _hoverGraceUntil = DateTime.MinValue;
                _hoverLayoutHold = false;
                _hover.Collapsed();
                _fanState.PointerLeft();
                _fanState.CollapseTimerElapsed();
                return;
            }
        }

        // Lo mismo durante el margen de cortesía de una acción recién hecha (ver
        // HoldOpenForNextInteraction), y mientras haya una sincronización en curso (ver
        // SetSyncBusy): ni sondeo ni plegado hasta que termine.
        if (_syncBusy || DateTime.UtcNow < _interactionGraceUntil)
        {
            _hover.Hold();
            if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
            return;
        }

        // Lo mismo mientras se arrastra una pestaña: el gesto puede salirse de la zona sensible, y
        // colapsar el abanico a mitad de arrastre dejaría la nota en el aire.
        if (_dragging)
        {
            _hover.Hold();
            return;
        }

        // Un dock oculto no responde al ratón: el sondeo compara el cursor contra el rectángulo de la
        // ventana, y una ventana oculta conserva el suyo, así que sin esto intentaría desplegar algo
        // que no se ve (y al volver lo haría por sorpresa).
        if (!IsVisible) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorScreen = NativeMethods.GetCursorScreenPosition();
        double cursorX = cursorScreen.X / dpi.DpiScaleX;
        double cursorY = cursorScreen.Y / dpi.DpiScaleY;

        // Crear una nota cambia el alto de la ventana y, al estar centrada en el monitor, desplaza
        // también la botonera. El cursor sigue físicamente donde estaba el botón , así que durante
        // ese instante queda en un hueco que no pertenece a ningún elemento WPF y el sondeo lo
        // interpretaría como una salida. Mantenemos el abanico mientras el cursor no se mueva: en
        // cuanto el usuario lo aparta, la comprobación normal vuelve a decidir si debe cerrarse.
        if (_hoverLayoutHold)
        {
            if (Math.Abs(cursorX - _hoverLayoutAnchorX) > 4
                || Math.Abs(cursorY - _hoverLayoutAnchorY) > 4)
            {
                _hoverLayoutHold = false;
            }
            else
            {
                _hover.Hold();
                return;
            }
        }

        // In keyboard mode the fan stays open wherever the cursor is.
        if (_keyboardMode)
        {
            _hover.Hold();
            return;
        }

        // Cortesía al abrir otra ventana desde el dock: mientras corre, el sondeo no decide nada y
        // el abanico se queda abierto aunque el cursor esté fuera. En cuanto caduca, el sondeo
        // normal vuelve a mandar y, si el cursor sigue fuera, lo pliega como siempre.
        if (DateTime.UtcNow < _hoverGraceUntil)
        {
            _hover.Hold();
            return;
        }

        // Contra la zona realmente visible, no contra la ventana entera: casi toda es transparente,
        // y desplegarse al entrar ahí sería desplegarse por pasar el ratón sobre nada.
        // Sin notas la ventana entera es zona sensible: no hay tira que sobrevolar, y los botones
        // tienen que poder pulsarse sin desplegar nada primero.
        var windowRect = EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount, Alignment);
        bool empty = _noteCount == 0;
        bool inWindow = Contains(windowRect, cursorX, cursorY);
        bool inRest = empty
            ? inWindow
            : Contains(EdgeGeometry.RestingVisibleRect(_workingArea, _edge, _noteCount, Alignment), cursorX, cursorY);

        // La superficie desplegada se mide también plegado (sus elementos siguen colocados, solo
        // transparentes), aunque plegado ya no abre nada: la política exige volver a la tira. Se calcula una vez por tick:
        // cada pestaña cuesta un TranslatePoint, y con muchas notas se notaba hacerlo dos veces.
        List<Aldune.Core.Rect> surfaces = empty && !_fanState.IsExpanded ? [] : ExpandedSurfaces().ToList();
        var sample = new DockHoverSample(
            _hoverClock.Elapsed,
            cursorX,
            cursorY,
            _fanState.IsExpanded,
            inRest,
            empty ? inWindow : IsInsideExpandedSurface(surfaces, cursorX, cursorY),
            IsOverDockTarget(surfaces, cursorX, cursorY),
            DockHoverZone.IsAgainstEdge(_monitorWorkArea, _edge, cursorX, cursorY),
            // Arrastrar otra cosa que comparta el borde (la barra de scroll de un navegador) no abre.
            NativeMethods.IsLeftButtonDown());

        switch (_hover.Update(sample))
        {
            case DockHoverDecision.Open:
                _fanState.PointerEntered();
                NativeMethods.EnsureTopmost(_hwnd);
                break;

            case DockHoverDecision.Close:
                _fanState.PointerLeft();
                _fanState.CollapseTimerElapsed();
                break;
        }
    }

    // --- La ventana, donde tiene que estar -------------------------------------------------------

    private static DateTime _lastPlacementRebuild = DateTime.MinValue;

    /// <summary>
    /// Devuelve la ventana a su sitio si Windows la ha movido. Al cambiar la disposición de las
    /// pantallas (apagar la principal, volver a encenderla) Windows desplaza ventanas por su cuenta, y
    /// el dock no se enteraba: la tira quedaba fuera de su canto, debajo de otras ventanas, y el
    /// abanico se abría "en medio de la pantalla" porque el ratón se compara con la posición calculada
    /// (reportado por el usuario y reproducido con DisplaySwitch; ver docs/DOCK_DIAGNOSTICS.md).
    /// Solo en reposo: desplegado, el abanico vive dentro del mismo rectángulo, y nada del dock mueve
    /// su ventana salvo <see cref="ApplyWindowRect"/>.
    /// </summary>
    private void KeepWindowInPlace()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible || _hiddenByFullscreenApp || _fanState.IsExpanded || _dragging) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var expected = EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount, Alignment);
        var expectedPx = new Aldune.Core.Rect(expected.X * dpi.DpiScaleX, expected.Y * dpi.DpiScaleY,
            expected.Width * dpi.DpiScaleX, expected.Height * dpi.DpiScaleY);
        var (x, y, width, height) = NativeMethods.GetWindowRectPx(_hwnd);
        var actual = new Aldune.Core.Rect(x, y, width, height);

        // Lo normal: en su sitio. Solo entonces se consulta la lista de pantallas, que es más cara.
        if (DockPlacement.Check(actual, expectedPx, _monitorWorkArea, _monitorWorkArea) == DockPlacementFix.None) return;

        // Contra el área de la pantalla tal cual, no contra _workingArea: con la barra de tareas oculta
        // esa va recortada y nunca coincidiría con la de Windows, así que todo desplazamiento parecería
        // un cambio de pantalla y reconstruiría los docks en vez de devolver la ventana a su sitio.
        var current = MonitorLookup.ForDeviceName(_monitorKey, MonitorEnumerator.EnumerateMonitors())?.WorkArea;
        switch (DockPlacement.Check(actual, expectedPx, _monitorWorkArea, current))
        {
            case DockPlacementFix.MoveBack:
                ApplyWindowRect();
                NativeMethods.EnsureTopmost(_hwnd);
                break;

            case DockPlacementFix.Rebuild when DateTime.UtcNow - _lastPlacementRebuild > TimeSpan.FromSeconds(5):
                _lastPlacementRebuild = DateTime.UtcNow;
                // Fuera de este tick: reconstruir cierra este mismo dock.
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(_coordinator.RebuildDocks));
                break;
        }
    }

    private DateTime _lastOnTopFix = DateTime.MinValue;

    /// <summary>
    /// Si sobre la tira en reposo hay una ventana que NO es de la capa superior (Firefox, Code,
    /// WhatsApp... en los registros del portátil y del PC), el dock ha perdido su sitio entre las
    /// siempre-encima sin que nadie se lo quitara a propósito: se vuelve a subir sin esperar a que el
    /// ratón entre. Las que sí son de la capa superior (menú Inicio, recortes) se respetan: tapar a
    /// esas sería peor. Antes esto solo se arreglaba al pasar el ratón por la tira.
    /// </summary>
    /// <summary>
    /// Vuelve a poner el dock por encima de las notas (que también son Topmost: entre ventanas Topmost manda la que se
    /// activó la última). Sin esto, una nota arrastrada o activada sobre la tira plegada la tapaba y no se veía dónde
    /// estaba el dock. Con el abanico abierto ya se mantenía encima; esto cubre el dock en reposo.
    /// </summary>
    internal void RaiseAboveNotes()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible || _hiddenByFullscreenApp) return;
        NativeMethods.EnsureTopmost(_hwnd);
    }

    private void KeepStripOnTop()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible || _hiddenByFullscreenApp || _fanState.IsExpanded || _dragging || _noteCount == 0) return;
        if (DateTime.UtcNow - _lastOnTopFix < TimeSpan.FromSeconds(3)) return;

        var center = RestStrip.PointToScreen(new Point(RestStrip.ActualWidth / 2, RestStrip.ActualHeight / 2));
        var above = DockDiagnostics.RootWindowAt((int)center.X, (int)center.Y);
        if (above == IntPtr.Zero || above == _hwnd || DockDiagnostics.IsTopmost(above)) return;

        _lastOnTopFix = DateTime.UtcNow;
        NativeMethods.EnsureTopmost(_hwnd);
    }

    // --- La tira que deja de pintarse ------------------------------------------------------------

    /// <summary>Dónde va el dock a lo largo de su borde (Ajustes). Se lee cada vez: el dock se reconstruye al cambiarla.</summary>
    private DockAlignment Alignment => _settings?.DockAlignment ?? DockAlignment.Center;

    private int _paintChecks;
    private int _unpaintedReadings;

    /// <summary>
    /// La tira de reposo llegó a quedarse sin pintar (una ventana «layered» que deja de recomponerse) hasta que se pasaba el ratón por
    /// encima. Cada 5 s, con el dock en reposo y encima de la tira, se lee el color que se ve de verdad en pantalla en su primer guion; con
    /// dos lecturas seguidas sin su color (no es un cambio de aspecto a medias) se la repinta y se vuelve a subir. Leer la pantalla fuerza
    /// a DWM a componer, por eso no va en cada tick.
    /// </summary>
    private void RepaintStripIfUnpainted()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible || _hiddenByFullscreenApp || _fanState.IsExpanded || _noteCount == 0) return;

        // Sobre la tira REAL (el elemento, en su posición de verdad en pantalla): si hay otra ventana encima, lo que se lee no es del dock.
        var stripCenter = RestStrip.PointToScreen(new Point(RestStrip.ActualWidth / 2, RestStrip.ActualHeight / 2));
        if (DockDiagnostics.RootWindowAt((int)stripCenter.X, (int)stripCenter.Y) != _hwnd) return;
        if (_paintChecks++ % 10 != 0) return;

        // El punto va 4 px dentro del principio de la tira, en su eje: su fondo o, si no hay relleno, el primer guion.
        bool vertical = _edge is EdgePosition.Left or EdgePosition.Right;
        var sample = RestStrip.PointToScreen(vertical
            ? new Point(RestStrip.ActualWidth / 2, 4)
            : new Point(4, RestStrip.ActualHeight / 2));
        var (r, g, b) = DockDiagnostics.ScreenPixel((int)sample.X, (int)sample.Y);
        bool Near(string hex) =>
            System.Windows.Media.ColorConverter.ConvertFromString(hex) is Color c
            && Math.Abs(r - c.R) <= 16 && Math.Abs(g - c.G) <= 16 && Math.Abs(b - c.B) <= 16;
        bool painted = Near(ThemeManager.Color("Ground"))
            || RestList.Items.OfType<Note>().Any(note => Near(NoteColorDisplay.Resolve(note.Color)));
        if (painted)
        {
            _unpaintedReadings = 0;
            return;
        }

        if (++_unpaintedReadings < 2) return;
        _unpaintedReadings = 0;
        NativeMethods.EnsureTopmost(_hwnd);
        InvalidateVisual();
        UpdateLayout();
    }

    /// <summary>
    /// Esconde el dock mientras haya una aplicación a pantalla completa en su monitor. El dock es
    /// <c>Topmost</c>: sin esto se queda dibujado encima de un juego o un vídeo.
    /// </summary>
    private void PollFullscreenApp()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Si el usuario desactivó el auto-ocultado ante juegos a pantalla completa en Ajustes, no esconder.
        if (_settings is { HideOnFullscreen: false })
        {
            if (_hiddenByFullscreenApp)
            {
                _hiddenByFullscreenApp = false;
                Visibility = Visibility.Visible;
                NativeMethods.EnsureTopmost(_hwnd);
            }
            return;
        }

        bool covered = NativeMethods.IsFullscreenAppCovering(_hwnd);
        if (covered == _hiddenByFullscreenApp) return;
        _hiddenByFullscreenApp = covered;

        if (covered)
        {
            // Colapsar antes de esconder, y de golpe: si se escondiera desplegado volvería con el
            // abanico abierto sin el ratón encima.
            _hover.Collapsed();
            CloseDockPopups();
            _fanState.PointerLeft();
            _fanState.CollapseTimerElapsed();
            ApplyState(animate: false);

            // Visibility en vez de Hide(): esas arrastran semántica de activación, y este dock es
            // WS_EX_NOACTIVATE a propósito — no debe robar el foco al volver, y menos a un juego
            // que acaba de salir de pantalla completa.
            Visibility = Visibility.Hidden;
        }
        else
        {
            Visibility = Visibility.Visible;
            NativeMethods.EnsureTopmost(_hwnd);
        }
    }

    // --- Estado y animación ---------------------------------------------------------------------

    private static readonly Duration SideCrossfadeDuration = new(TimeSpan.FromMilliseconds(170));
    private static readonly Duration TopBottomCrossfadeDuration = new(TimeSpan.FromMilliseconds(140));

    private static IEasingFunction EaseOut() =>
        new QuinticEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// Curve for opacity fades. Quintic-out spends almost its whole change in the first frames (a 150 ms fade was over by ~60 ms and read
    /// as a pop); cubic-out still answers at once but keeps changing for most of the duration.
    /// </summary>
    private static IEasingFunction EaseFade() =>
        new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// Cruza entre la tira de reposo y el abanico. Sin región que mantener sincronizada, esto es
    /// una animación WPF normal sobre dos capas superpuestas.
    /// </summary>
    private void ApplyState(bool animate)
    {
        UpdateWheelHook();
        // The pinned view label follows the fan: up when it opens, down when it folds (after this layout pass).
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshViewLabel));
        // Sin notas no hay tira de reposo que ensenar — quedaba un pegote oscuro diminuto en el
        // canto, sin nada dentro y sin decir nada — ni abanico que desplegar. Se muestran los
        // botones directamente: son la unica accion posible en ese estado, y ademas la unica forma
        // de crear la primera nota.
        bool empty = _noteCount == 0;
        bool expanded = _fanState.IsExpanded || empty;

        // Los Popup viven en HWNDs independientes. Si el abanico pasa a reposo, deben cerrarse
        // también para no quedar flotando sin relación visual con el dock.
        if (!expanded)
            CloseDockPopups();

        RestStrip.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        // Opacity NO desactiva el hit-testing en WPF: sin esto, la capa invisible se come los clics
        // de la visible. Es el mismo tropiezo que ya documenta docs/STATUS.md.
        FanPanel.IsHitTestVisible = expanded;
        RestStrip.IsHitTestVisible = !expanded;

        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            FanPanel.BeginAnimation(OpacityProperty, null);
            RestStrip.BeginAnimation(OpacityProperty, null);
            FanPanel.Opacity = expanded ? 1 : 0;
            RestStrip.Opacity = expanded ? 0 : 1;
            ResetTabEntrance(settled: expanded);
            UpdateScrollIndicator();
            return;
        }

        var duration = IsTopBottomEdge ? TopBottomCrossfadeDuration : SideCrossfadeDuration;
        Fade(FanPanel, expanded ? 1 : 0, duration);
        Fade(RestStrip, expanded ? 0 : 1, duration);
        UpdateScrollIndicator();

        if (expanded) ReplayTabEntrance();
    }

    private static void Fade(UIElement element, double to, Duration duration)
    {
        var animation = new DoubleAnimation(to, duration) { EasingFunction = EaseFade() };
        // FillBehavior.HoldEnd deja la animación enganchada por encima de cualquier asignación
        // posterior; limpiarla y fijar el valor final evita que un cambio de estado posterior sea
        // un no-op silencioso.
        animation.Completed += (_, _) =>
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = to;
        };
        element.BeginAnimation(OpacityProperty, animation);
    }

    /// <summary>Entrada escalonada de las pestañas al desplegar el abanico.</summary>
    private void ReplayTabEntrance()
    {
        foreach (var (index, button) in _tabButtons)
        {
            PlayEntrance(button, FanTiming.StaggerDelayMs(index), slideIn: true);
        }
    }

    private void ResetTabEntrance(bool settled)
    {
        foreach (var button in _tabButtons.Values)
        {
            button.BeginAnimation(OpacityProperty, null);
            button.Opacity = settled ? 1 : 0;
            if (button.RenderTransform is TranslateTransform translate && !translate.IsFrozen)
            {
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.X = 0;
                translate.Y = 0;
            }
        }
    }

    /// <summary>Una pestaña entra deslizándose desde el canto de la pantalla y apareciendo.</summary>
    private void PlayEntrance(Button button, double delayMs, bool slideIn, double durationMs = FanTiming.TabSweepMs)
    {
        var delay = TimeSpan.FromMilliseconds(delayMs);
        var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

        // Las dos animaciones se limpian al terminar y fijan su valor final como valor local plano.
        // Sin eso quedan enganchadas para siempre (FillBehavior.HoldEnd) y **cualquier asignación
        // posterior a esa propiedad es un no-op silencioso**: es lo que hacía que, tras crear una
        // nota, el abanico se desplegara mal en el siguiente hover. Crear una nota es el único
        // camino que llama aquí fuera del ciclo de despliegue (ver SetNotes), así que dejaba una
        // pestaña con la animación colgada mientras el resto del estado daba por hecho que podía
        // escribir su Opacity a mano. Mismo patrón que ya arreglaron Fade y NoteWindow, ver
        // docs/STATUS.md.
        var fade = new DoubleAnimation(0, 1, duration) { BeginTime = delay };
        fade.Completed += (_, _) =>
        {
            button.BeginAnimation(OpacityProperty, null);
            button.Opacity = 1;
        };
        button.BeginAnimation(OpacityProperty, null);
        // El valor base tiene que ser el de partida de la animación, y no lo que hubiera antes.
        // Con BeginTime (el escalonado), mientras el retardo corre la animación todavía no manda y
        // WPF pinta el valor base: si la pestaña se quedó en 1 —lo que pasaba justo después de
        // crear una nota, porque OnTabLoaded la fija a 1 con el abanico abierto— se veía entera,
        // pegaba un salto a invisible al arrancar su animación, y entonces hacía el fundido. Un
        // parpadeo escalonado en vez de una entrada. Y no se arreglaba solo: al colapsar nadie
        // devuelve las pestañas a 0, así que seguía mal en cada despliegue hasta que un SetNotes
        // con el dock cerrado (abrir y cerrar una nota) las reseteaba de casualidad.
        button.Opacity = 0;
        button.BeginAnimation(OpacityProperty, fade);

        if (!slideIn)
        {
            // Al insertar una nota la lista ya está recolocándose por el nuevo paso. Otro
            // TranslateTransform encima de ese layout deja el texto en coordenadas fraccionarias y
            // lo hace parecer borroso; el fundido conserva la entrada sin mover la tarjeta.
            if (button.RenderTransform is TranslateTransform existing && !existing.IsFrozen)
            {
                existing.BeginAnimation(TranslateTransform.XProperty, null);
                existing.BeginAnimation(TranslateTransform.YProperty, null);
                existing.X = 0;
                existing.Y = 0;
            }

            return;
        }

        // Instancia nueva por pestaña: un TranslateTransform declarado en XAML dentro de un
        // DataTemplate acaba congelado y compartido entre contenedores (Freezable), y animarlo
        // lanza "Cannot animate ... because the object is sealed or frozen" — ya pasó una vez, ver
        // docs/STATUS.md.
        if (button.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            translate = new TranslateTransform();
            button.RenderTransform = translate;
        }

        bool vertical = IsTopBottomEdge;
        bool fromTop = _edge == EdgePosition.Top;
        double from = (vertical ? EdgeGeometry.TabHeight : EdgeGeometry.TabWidth) * 0.55;
        if (vertical && fromTop) from = -from;

        var slide = new DoubleAnimation(from, 0, duration)
        {
            BeginTime = delay,
            EasingFunction = EaseOut()
        };
        slide.Completed += (_, _) =>
        {
            if (vertical)
            {
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.Y = 0;
            }
            else
            {
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.X = 0;
            }
        };
        if (vertical)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = from; // mismo motivo que la opacidad de arriba: el retardo pinta el valor base
            translate.BeginAnimation(TranslateTransform.YProperty, slide);
        }
        else
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = from; // mismo motivo que la opacidad de arriba: el retardo pinta el valor base
            translate.BeginAnimation(TranslateTransform.XProperty, slide);
        }
    }

    // --- Contenido ------------------------------------------------------------------------------

    /// <summary>
    /// A setting that changes how every tab is drawn (card shadow, corners, the colour strip, the view button, the look) is taken up without
    /// closing and recreating the window, which showed as a flash: the strip fades out, the tabs are rebuilt with the new setting while nothing is
    /// drawn and they come back through the same swap a change of view uses. Tabs read these settings when they are created, hence the rebuild.
    /// </summary>
    /// <summary>The whole window fades out before it is closed to be built again somewhere else (another edge, alignment or screen).</summary>
    public void FadeOutThen(Action done)
    {
        var fade = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(110))) { EasingFunction = EaseFade() };
        // At zero and with two frames drawn BEFORE the heavy work (closing and creating windows): otherwise the compositor keeps showing the
        // last frame it was given, a half-faded dock, for as long as the interface thread is busy.
        fade.Completed += (_, _) => { BeginAnimation(OpacityProperty, null); Opacity = 0; AfterFrames(2, done); };
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>The new window of a rebuild comes in with a fade (it is created at opacity 0).</summary>
    public void FadeInAfterRebuild()
    {
        var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(170))) { EasingFunction = EaseFade() };
        fade.Completed += (_, _) => { BeginAnimation(OpacityProperty, null); Opacity = 1; };
        BeginAnimation(OpacityProperty, fade);
    }

    public void ReloadLookSoftly()
    {
        ViewButton.Visibility = _settings?.DockViewButton == true ? Visibility.Visible : Visibility.Collapsed;
        _lookReload = true;
        Refresh();
    }

    public void Refresh()
    {
        var view = _settings?.DockView ?? DockViewKind.Active;
        var tagFilter = _settings?.DockTagFilter;
        bool viewChanged = _lookReload || _lastView != view
            || !string.Equals(_lastTagFilter, tagFilter, StringComparison.OrdinalIgnoreCase);
        _swapSpeed = _lookReload ? 0.65 : 1;
        _lookReload = false;
        _lastView = view;
        _lastTagFilter = tagFilter;
        bool swap = viewChanged && IsVisible && _fanState.IsExpanded && SystemParameters.ClientAreaAnimation;
        // Folded dock (the view was changed from Manage notes or by the wheel's last notch): the strip of pills changes in a soft swap too.
        bool restSwap = !swap && viewChanged && IsVisible && !_fanState.IsExpanded && _noteCount > 0 && SystemParameters.ClientAreaAnimation;
        double? resumeStripFrom = null;
        if (_restSwapActive && !restSwap)
        {
            // A refresh that is not a view change arrived while the strip was fading (a sync, a saved note): that swap is superseded, so the
            // strip would have stayed invisible. Put it back as the state says.
            _restSwapActive = false;
            resumeStripFrom = RestStrip.Opacity; // animated value: read before the animation is cleared
            RestStrip.BeginAnimation(OpacityProperty, null);
            ApplyState(animate: false);
        }
        // With the swap the name of the view changes together with the cards (inside the swap), not a moment before them.
        if (!swap && !restSwap) UpdateViewIndicator();
        ApplyFooterPadding();
        if (swap)
        {
            SwapNotesThroughFade();
        }
        else if (restSwap)
        {
            SwapRestStrip();
        }
        else
        {
            _viewSwapToken++; // a swap still waiting for its fade is superseded by this one
            _viewLabelHold = false;
            ResetTabsFadeIfStuck();
            var notes = LoadNotesForView();
            if (!viewChanged && _tabsSignature is { } known && known == TabsSignature(notes, _repository.GetPendingReminders()))
            {
                // Nothing a tab draws has changed (a done-tasks policy, a note minimized, a reminder that was already counted): rebuilding every
                // tab would flash the strip and send its scroll back to the top for nothing. Only what depends on the open windows is updated.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshOpenState));
            }
            else
            {
                SetNotes(notes, animateArrivals: !viewChanged, fadeAll: viewChanged);
            }
            if (_labelOnSwap) { _labelOnSwap = false; ShowViewLabel(CurrentViewLabel()); }
        }
        // Continue the strip's fade from where the superseded swap left it instead of showing it at full strength in one frame. After
        // SetNotes, which may itself reset the strip (a change in the number of notes calls ApplyState).
        if (resumeStripFrom is { } from && from < 0.99 && !_fanState.IsExpanded && _noteCount > 0 && SystemParameters.ClientAreaAnimation)
        {
            var resume = new DoubleAnimation(from, 1, new Duration(TimeSpan.FromMilliseconds(150))) { EasingFunction = EaseFade() };
            resume.Completed += (_, _) => { RestStrip.BeginAnimation(OpacityProperty, null); RestStrip.Opacity = 1; };
            RestStrip.Opacity = from; // until the clock ticks the base value shows, which must not be the full one
            RestStrip.BeginAnimation(OpacityProperty, resume);
        }
        if (viewChanged)
        {
            HoldHoverDuringLayout();
            UpdateTagAwareTooltips();
        }
    }

    private IReadOnlyList<Note> LoadNotesForView()
    {
        var view = _settings?.DockView ?? DockViewKind.Active;
        var notes = view switch
        {
            DockViewKind.Archived => _repository.GetByState(NoteState.Archived),
            DockViewKind.Trashed => _repository.GetByState(NoteState.Trashed),
            DockViewKind.All => _repository.GetByState(NoteState.Active).Concat(_repository.GetByState(NoteState.Archived)).ToList(),
            DockViewKind.Tag when !string.IsNullOrWhiteSpace(_settings?.DockTagFilter) =>
                _repository.GetByTag(_settings!.DockTagFilter!, NoteState.Active),
            _ => _repository.GetByState(NoteState.Active)
        };
        // El canal (piel de osciloscopio) sale siempre del mazo ACTIVO en su orden, sea cual sea la
        // vista o el filtro de etiqueta: archivar una nota no puede cambiarla de canal, ni filtrar
        // hacer que su ventana y su pestaña discrepen. Con la vista Activa sin filtro ya está cargado.
        NoteChannelDisplay.Set(view == DockViewKind.Active ? notes : _repository.GetByState(NoteState.Active));
        LinkedNoteDisplay.Set(_repository.GetFileLinks());
        return notes;
    }

    private int _viewSwapToken;
    private bool _viewLabelHold; // the name card is leaving for a change of view: it keeps its place until it comes back with the new name

    // What fades is the whole column (tabs, scroll rail and the arrows of the top and bottom docks), not only the list: the arrows light up for a
    // frame while the new list is measured in a window that has not reached its final height yet, and showed as a flash.
    private void ResetTabsFadeIfStuck()
    {
        if (NotesColumn.Opacity < 1 || NotesColumn.HasAnimatedProperties)
        {
            NotesColumn.BeginAnimation(OpacityProperty, null);
            NotesColumn.Opacity = 1;
        }
    }

    private const int SwapBlankFrames = 2;
    private const double DeckGatherMs = 95;
    private const double DeckSpreadMs = 190;

    /// <summary>
    /// A change of view fades through nothing, with the cards moving like a deck: the old ones are gathered into a pile at the end next to the
    /// button bar and fade out; the list, the window and the name of the view change while nothing is drawn; the new ones come out of that same
    /// pile and spread to their places. The strip is never drawn with both lists (two sets of text on top of each other) and a window that
    /// grows or shrinks (a tag with more or fewer notes) does so unseen, so nothing jumps. On every edge the pile is at the end next to the button bar (the top card on the top edge, the last one on the others). With very many
    /// notes it is the plain fade. Turns of the wheel in a row restart it from wherever the strip is; the notes are loaded when the swap happens.
    /// </summary>
    private void SwapNotesThroughFade()
    {
        int token = ++_viewSwapToken;
        var root = (UIElement)Content;
        bool deck = _tabButtons.Count > 1;
        if (deck) AnimateDeck(gather: true);
        // The name card leaves with the old cards (even the one-second flash) and stays where it is, invisible, until the new list is in
        // place: the strip changes size under it and it used to slide to the new end while still fading out, and then change text in view.
        if (_viewLabelPopup is { IsOpen: true })
        {
            _viewLabelTimer.Stop();
            _viewLabelHold = true;
            FadeViewLabel(show: false, milliseconds: 90);
        }

        var fadeOut = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(deck ? DeckGatherMs - 35 : 75))) { EasingFunction = EaseOut(), BeginTime = TimeSpan.FromMilliseconds(deck ? 35 : 0) };
        void SwapNow()
        {
            if (token != _viewSwapToken) { Cloak(false); return; }
            foreach (var old in _tabButtons.Values)
                if (old.RenderTransform is TranslateTransform { IsFrozen: false } oldMove) { oldMove.BeginAnimation(TranslateTransform.YProperty, null); oldMove.Y = 0; }
            SetNotes(LoadNotesForView(), animateArrivals: false, fadeAll: false);
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(deck ? 90 : 140))) { EasingFunction = EaseOut() };
            fadeIn.Completed += (_, _) =>
            {
                if (token != _viewSwapToken) return;
                NotesColumn.BeginAnimation(OpacityProperty, null);
                NotesColumn.Opacity = 1;
            };
            // After the layout of the new list, so it never shows for a frame at full strength (and the pile can be measured).
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (token != _viewSwapToken) return;
                // The cards wait in the pile while the window is still transparent, and the spread (and the fade of the column) start only once
                // the window is visible again: started earlier, the first 50-60 ms of the spread, the fastest part, ran unseen.
                var startSpread = _tabButtons.Count > 1 ? PrepareDeckSpread() : null;
                // The name of the view comes back when the cards are nearly in place: it sits at the far end of the new strip, and shown
                // before them it hung there alone (with many notes, far from where the old pile was).
                bool flash = _labelOnSwap;
                _labelOnSwap = false;
                UpdateTagAwareTooltips();
                AfterFrames(4, () =>
                {
                    if (token != _viewSwapToken) { Cloak(false); return; }
                    Opacity = 1;
                    // The bar comes back with the cards instead of popping in.
                    var rootFadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(90))) { EasingFunction = EaseOut() };
                    rootFadeIn.Completed += (_, _) => { root.BeginAnimation(OpacityProperty, null); root.Opacity = 1; };
                    root.BeginAnimation(OpacityProperty, rootFadeIn);
                    startSpread?.Invoke();
                    NotesColumn.BeginAnimation(OpacityProperty, fadeIn);
                    // After the interface thread has caught up with the first frames of the new list (a long strip takes a while), so the name does
                    // not come back before the cards.
                    Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(StartLabelTimer));
                });
                void StartLabelTimer()
                {
                var labelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(deck ? DeckSpreadMs * 0.45 : 90) };
                labelTimer.Tick += (_, _) =>
                {
                    labelTimer.Stop();
                    if (token != _viewSwapToken) return;
                    _viewLabelHold = false;
                    if (flash) ShowViewLabel(CurrentViewLabel());
                    else RefreshViewLabel();
                };
                labelTimer.Start();
                }
            }));
        }
        fadeOut.Completed += (_, _) =>
        {
            if (token != _viewSwapToken) return;
            NotesColumn.BeginAnimation(OpacityProperty, null);
            NotesColumn.Opacity = 0;
            // The whole window goes transparent and that has to reach the screen BEFORE the window changes size: while the interface thread builds the
            // new cards, the composition thread keeps showing the last picture it was given (the old layout) pinned to the top-left corner of the
            // enlarged window, and the button bar was seen there for about 30 ms. Two frames later the window is already invisible.
            // The window now waits a few frames with nothing drawn: the picture the enlarged window shows for its first frames comes from an
            // older buffer, and with the button bar still in it the bar showed pinned to the top-left corner for ~100 ms.
            Cloak(true);
            root.BeginAnimation(OpacityProperty, null); // the local 0 set by Cloak takes over from the finished fade
            AfterFrames(SwapBlankFrames, SwapNow);
        };
        // The button bar fades out with the last of the cards (it is part of what is cleared before the window changes size).
        var barFade = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(deck ? DeckGatherMs - 45 : 40))) { EasingFunction = EaseOut(), BeginTime = TimeSpan.FromMilliseconds(deck ? 45 : 35) };
        root.BeginAnimation(OpacityProperty, barFade);
        NotesColumn.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// The folded dock changing view: the strip of pills fades out, the list (and the size of the window, unseen) changes while nothing is
    /// drawn, and the strip comes back with its pills appearing one after another. Before this the pills and the strip's length jumped.
    /// </summary>
    private bool _restSwapActive;

    private void SwapRestStrip()
    {
        int token = ++_viewSwapToken; // supersedes any swap still waiting
        _restSwapActive = true;
        _viewLabelHold = false;
        ResetTabsFadeIfStuck();
        double speed = _swapSpeed;
        var fadeOut = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(130 * speed))) { EasingFunction = EaseFade() };
        fadeOut.Completed += (_, _) =>
        {
            if (token != _viewSwapToken) return;
            UpdateViewIndicator();
            SetNotes(LoadNotesForView(), animateArrivals: false, fadeAll: false);
            // A change in the number of notes makes ApplyState show the strip at once: it stays hidden until the new pills are in place.
            RestStrip.BeginAnimation(OpacityProperty, null);
            RestStrip.Opacity = 0;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => AfterFrames(1, () =>
            {
                if (token != _viewSwapToken) return;
                _restSwapActive = false;
                if (_fanState.IsExpanded || _noteCount == 0) { ApplyState(animate: false); return; }
                int count = RestList.Items.Count;
                for (int i = 0; i < count; i++)
                {
                    if (RestList.ItemContainerGenerator.ContainerFromIndex(i) is not UIElement pill) continue;
                    pill.BeginAnimation(OpacityProperty, null);
                    pill.Opacity = 0;
                    // The stagger is capped as a whole (not per pill count), so 40 notes take as long to settle as 10.
                    var appear = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(190 * speed)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(speed * Math.Min(i * 35.0, 150.0 * i / Math.Max(1, count - 1))),
                        EasingFunction = EaseFade(),
                    };
                    appear.Completed += (_, _) => { pill.BeginAnimation(OpacityProperty, null); pill.Opacity = 1; };
                    pill.BeginAnimation(OpacityProperty, appear);
                }
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(160 * speed))) { EasingFunction = EaseFade() };
                fadeIn.Completed += (_, _) => { RestStrip.BeginAnimation(OpacityProperty, null); RestStrip.Opacity = 1; };
                RestStrip.BeginAnimation(OpacityProperty, fadeIn);
            })));
        };
        RestStrip.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// Hides (or shows) the whole dock while the window changes size (see <see cref="SwapNotesThroughFade"/>).
    /// </summary>
    private void Cloak(bool hidden)
    {
        Opacity = hidden ? 0 : 1;
        if (Content is UIElement root) root.Opacity = hidden ? 0 : 1;

    }

    /// <summary>Moves the cards in view to the pile (the card nearest the button bar) and fades them out as they go.</summary>
    private void AnimateDeck(bool gather)
    {
        foreach (var (move, offset) in DeckCards())
        {
            var animation = new DoubleAnimation(0, offset, new Duration(TimeSpan.FromMilliseconds(DeckGatherMs))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }, FillBehavior = FillBehavior.HoldEnd };
            move.BeginAnimation(TranslateTransform.YProperty, animation);
        }
    }

    /// <summary>
    /// Puts the cards of the new list in the pile, unseen, and returns what starts them spreading to their places. A long strip takes a little longer:
    /// 1100 px in 190 ms was a rush, and it arrived blurred.
    /// </summary>
    private Action? PrepareDeckSpread()
    {
        var cards = DeckCards();
        if (cards.Count == 0) return null;
        double span = cards.Max(card => Math.Abs(card.Offset));
        var duration = new Duration(TimeSpan.FromMilliseconds(DeckSpreadMs + Math.Min(110, span * 0.08)));
        foreach (var (move, offset) in cards) move.Y = offset; // before the first frame: the cards start in the pile, not in their places
        return () =>
        {
            foreach (var (move, offset) in cards)
            {
                var animation = new DoubleAnimation(offset, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                animation.Completed += (_, _) => { move.BeginAnimation(TranslateTransform.YProperty, null); move.Y = 0; };
                move.BeginAnimation(TranslateTransform.YProperty, animation);
            }
        };
    }

    /// <summary>
    /// The cards in view and how far each is from the pile. Only those move (the rest are clipped anyway), so the deck costs the same with 8
    /// notes as with 80.
    /// </summary>
    private List<(TranslateTransform Move, double Offset)> DeckCards()
    {
        double viewport = TabsScroll.ActualHeight;
        var cards = _tabButtons.Values
            .Where(button => button.IsVisible && button.ActualHeight > 0)
            .Select(button => (Button: button, Y: button.TransformToAncestor(TabsScroll).Transform(new Point(0, 0)).Y))
            .Where(card => card.Y + card.Button.ActualHeight > 0 && card.Y < viewport)
            .OrderBy(card => card.Y)
            .ToList();
        var result = new List<(TranslateTransform, double)>();
        if (cards.Count < 2) return result;
        double pile = _edge == EdgePosition.Top ? cards[0].Y : cards[^1].Y;
        foreach (var (button, y) in cards)
        {
            if (button.RenderTransform is not TranslateTransform { IsFrozen: false } move)
            {
                move = new TranslateTransform();
                button.RenderTransform = move;
            }
            result.Add((move, pile - y));
        }
        return result;
    }

    /// <summary>
    /// Etiqueta de la vista en curso, o null si el dock no está filtrando por etiqueta. La usan los
    /// botones del pie para actuar dentro de la vista que se está viendo en vez de sobre todas las
    /// notas: crear una nota desde la vista de una etiqueta y que nazca sin ella obligaría a buscarla
    /// después en "todas" para etiquetarla a mano, justo después de haber elegido no verlas todas.
    /// </summary>
    private string? CurrentTagFilter
    {
        get
        {
            var tag = _settings?.DockTagFilter;
            return _settings?.DockView == DockViewKind.Tag && !string.IsNullOrWhiteSpace(tag) ? tag : null;
        }
    }

    /// <summary>
    /// Los tooltips del pie describen lo que va a hacer cada botón, así que cambian con la vista: en
    /// la vista de una etiqueta "+" ya no crea "una nota" a secas, sino una nota de esa etiqueta.
    /// </summary>
    private void UpdateTagAwareTooltips()
    {
        var tag = CurrentTagFilter;
        NewNoteButton.ToolTip = Strings.WithHint(
            tag is null ? Strings.NewNoteTooltip : Strings.NewNoteTaggedTooltip(tag),
            Strings.NewNoteRightClickHint);
        // Fuera de «Activas» el tooltip dice también en qué vista está el dock (la rueda sobre el engranaje la cambia).
        var viewLine = (_settings?.DockView ?? DockViewKind.Active) == DockViewKind.Active ? "" : Environment.NewLine + CurrentViewLabel();
        ManageArchiveButton.ToolTip = Strings.WithHint(
            (tag is null ? Strings.ManageNotesTooltip : Strings.ManageNotesTaggedTooltip(tag)) + viewLine,
            Strings.ManageNotesRightClickHint);
        OpenAllButton.ToolTip = Strings.WithHint(
            tag is null ? Strings.OpenAllNotesTooltip : Strings.OpenAllNotesTaggedTooltip(tag),
            Strings.OpenAllRightClickHint);
    }

    /// <summary>
    /// Recalcula qué pestañas están ocultas por tener su nota abierta, sin reconstruir la lista:
    /// abrir o cerrar una nota no cambia qué notas hay, y pasar por SetNotes reiniciaría la entrada
    /// para nada.
    /// </summary>
    public void RefreshOpenState()
    {
        foreach (var button in _tabButtons.Values)
        {
            // Minimizada cuenta como "fuera de la mesa": su ficha vuelve al mazo, y pulsarla la
            // restaura (ver AppCoordinator.OpenOrActivateNote). Solo desaparece la pestaña cuando la
            // ventana está a la vista de verdad — antes bastaba con que existiera, así que minimizar
            // una nota la borraba del dock sin forma de recuperarla desde ahí.
            bool onDesk = button.Tag is Note note && IsOnDesk(note);
            // Hidden y no Collapsed: conserva su hueco, y el mazo enseña el sitio vacío de donde se
            // sacó la ficha.
            bool returning = !onDesk && button.Visibility == Visibility.Hidden && IsVisible;
            button.Visibility = onDesk ? Visibility.Hidden : Visibility.Visible;
            if (returning && SystemParameters.ClientAreaAnimation)
            {
                // The tab fades back in while its note fades out (the reverse of opening), instead of popping in when the note is gone.
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(140))) { EasingFunction = EaseOut() };
                fadeIn.Completed += (_, _) => { button.BeginAnimation(OpacityProperty, null); button.Opacity = 1; };
                button.Opacity = 0;
                button.BeginAnimation(OpacityProperty, fadeIn);
            }
        }
    }

    /// <summary>
    /// The list can keep the height it had with the previous view: the items presenter caches a measure taken for four tabs and, with one left, the
    /// scroll area still reports 246 px of content for 52 px of tab. The wheel then scrolled that empty extent and pushed the only tab out of sight (a
    /// thin line was all that remained). Invalidating the presenter itself, which the panel's own invalidation does not reach, measures it again.
    /// </summary>
    private void RefreshTabsMeasure()
    {
        ItemsPresenter? Find(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is ItemsPresenter presenter) return presenter;
                if (Find(child) is { } inner) return inner;
            }
            return null;
        }
        Find(TabsList)?.InvalidateMeasure();
        TabsScroll.InvalidateScrollInfo();
        if (TabsScroll.VerticalOffset > Math.Max(0, TabsScroll.ExtentHeight - TabsScroll.ViewportHeight)) TabsScroll.ScrollToHome();
    }

    private int? _tabsSignature;
    private List<Guid> _tabsOrder = new();

    /// <summary>
    /// Everything the tabs draw, folded into one number: the notes' visible fields, their channel, link and reminder, and the settings (which
    /// decide label lengths, sizes and looks). Not <c>UpdatedAt</c> or the done-tasks policy: nothing on a tab shows them.
    /// </summary>
    private int TabsSignature(IReadOnlyList<Note> notes, IReadOnlyDictionary<Guid, DateTimeOffset> reminders)
    {
        var hash = new HashCode();
        hash.Add(_settings is null ? 0 : System.Text.Json.JsonSerializer.Serialize(_settings).GetHashCode());
        foreach (var n in notes)
        {
            hash.Add(n.Id); hash.Add(n.Text); hash.Add(n.Color); hash.Add(n.State); hash.Add(n.IsPinnedToTop);
            hash.Add(n.IsProtected); hash.Add(n.IsUnlocked); hash.Add(n.ProtectedTitle); hash.Add(n.DockPosition); hash.Add(n.ScreenOrigin);
            foreach (var tag in n.Tags) hash.Add(tag);
            hash.Add(NoteChannelDisplay.Of(n.Id));
            hash.Add(LinkedNoteDisplay.IsLinked(n.Id) ? LinkedNoteDisplay.PathOf(n.Id) : null);
            hash.Add(reminders.TryGetValue(n.Id, out var due) ? due : (DateTimeOffset?)null);
        }
        return hash.ToHashCode();
    }

    public void SetNotes(IReadOnlyList<Note> notes, bool animateArrivals = true, bool fadeAll = false)
    {
        var previousIds = _knownNoteIds;
        _knownNoteIds = notes.Select(n => n.Id).ToHashSet();
        _pendingReminders = _repository.GetPendingReminders();
        _tabsSignature = TabsSignature(notes, _pendingReminders);

        // The same notes in the same order (a colour or a tag changed): the strip keeps where it was scrolled instead of jumping to the start.
        var order = notes.Select(n => n.Id).ToList();
        bool sameOrder = !fadeAll && order.SequenceEqual(_tabsOrder);
        _tabsOrder = order;
        double keepV = TabsScroll.VerticalOffset, keepH = TabsScroll.HorizontalOffset;

        _tabButtons.Clear();
        TabsScroll.ScrollToHome();
        TabsList.ItemsSource = notes;
        if (sameOrder && (keepV > 0 || keepH > 0))
        {
            TabsScroll.UpdateLayout();
            TabsScroll.ScrollToVerticalOffset(keepV);
            TabsScroll.ScrollToHorizontalOffset(keepH);
        }

        // La tira de reposo no hace scroll, así que solo se dibujan los guiones que caben: el resto
        // se recortarían contra el borde de la ventana (ver EdgeGeometry.VisibleRestDashes).
        int visibleRestDashes = EdgeGeometry.VisibleRestDashes(_workingArea, _edge, notes.Count);
        RestList.ItemsSource = notes.Take(visibleRestDashes).ToList();

        bool countChanged = notes.Count != _noteCount;
        _noteCount = notes.Count;

        if (IsTopBottomEdge)
        {
            RestStrip.Width = Math.Max(
                EdgeGeometry.RestContainerWidth,
                EdgeGeometry.RestStripLength(_edge, visibleRestDashes)
                    + EdgeGeometry.RestContainerPad * 2);
        }

        // El solape depende del número de notas y va en Margin (layout). Solo cambia aquí, nunca
        // durante una transición de hover, así que no reintroduce el medir-a-tamaños-intermedios.
        double pitch = EdgeGeometry.PitchFor(_workingArea, _edge, _noteCount);
        _tabMargin = new Thickness(0, 0, 0, pitch - EdgeGeometry.TabHeight);

        if (countChanged)
        {
            ApplyWindowRect();
            // Pasar de cero a una nota (o al reves) cambia que capa se ensena, no solo el tamano.
            ApplyState(animate: false);
        }

        // Solo las notas que no estaban antes. Crear una nota anima esa pestaña y deja las demás
        // quietas, en vez de rehacer la entrada del abanico entero — que es lo que hacía que añadir
        // una nota pareciera un refresco y no una inserción.
        var arrived = animateArrivals
            ? _knownNoteIds.Except(previousIds).ToHashSet()
            : new HashSet<Guid>();

        // After the tabs are in place, not before: see RefreshTabsMeasure.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshTabsMeasure));

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            RefreshOpenState();
            UpdateScrollIndicator();

            if (!_fanState.IsExpanded)
            {
                ResetTabEntrance(settled: false);
                return;
            }

            // A change of view (wheel, menu) swaps every tab: they fade in one after another, a short ripple down the strip, instead of the
            // old ones blinking out and the new ones blinking in. Only a fade, no slide: a transform on the cards would blur their text.
            int ripple = 0;
            foreach (var button in _tabButtons.Values)
            {
                if (fadeAll && SystemParameters.ClientAreaAnimation)
                {
                    PlayEntrance(button, Math.Min(ripple++, 8) * 16, slideIn: false);
                }
                else if (button.Tag is Note note && arrived.Contains(note.Id))
                {
                    PlayEntrance(button, 0, slideIn: false);
                }
                else
                {
                    button.Opacity = 1;
                }
            }
        }));
    }

    private void OnTabsScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateScrollIndicator();

    private const double ArrowScrollStep = EdgeGeometry.NaturalPitch;

    private void OnTabsPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_fanState.IsExpanded || e.Delta == 0) return;

        // Cambiar de vista con la rueda: con Ctrl siempre; sin él, solo si todas las notas caben (no hay barra de scroll). Si no caben, la
        // rueda sigue desplazando la lista y la vista se cambia sobre el engranaje o con Ctrl.
        if (_settings is { WheelChangesDockView: true })
        {
            // Al cambiar de vista la altura de la lista se anima y durante un instante «no cabe» aunque sí quepa: un giro que continúa el anterior
            // (menos de 1,5 s) sigue cambiando de vista en vez de empezar a desplazar la lista a mitad del gesto.
            bool fits = TabsScroll.ScrollableHeight <= 0.5 || DateTime.UtcNow < _wheelViewUntil;
            if (Keyboard.Modifiers == ModifierKeys.Control || (Keyboard.Modifiers == ModifierKeys.None && fits))
            {
                CycleDockView(e.Delta < 0 ? 1 : -1, showLabel: true);
                e.Handled = true;
                return;
            }
        }

        // La rueda de un ratón llega en muescas de 120; un trackpad de precisión (gesto de dos dedos)
        // manda muchos eventos pequeños y seguidos. Con el paso fijo de una muesca por evento, un
        // solo gesto de trackpad saltaría media lista: con el ajuste de gestos de trackpad encendido
        // el paso pasa a ser proporcional al delta, y un ratón sigue comportándose igual (sus deltas
        // son muescas completas). Ver AppSettings.TrackpadGestures.
        if (_settings is { TrackpadGestures: true } && !IsFullWheelNotch(e.Delta))
        {
            SmoothScrollBy(-e.Delta / 120.0 * ArrowScrollStep);
            e.Handled = true;
            return;
        }

        SmoothScrollBy((e.Delta < 0 ? 1 : -1) * ArrowScrollStep);
        e.Handled = true;
    }

    // Rueda suave: cada muesca saltaba de golpe una pestaña entera. Ahora se fija un destino (varias muescas seguidas se acumulan) y se
    // avanza hacia él en cada fotograma con una curva de frenada corta. Se cancela si otra cosa mueve la lista (arrastrar la barra, el
    // autoscroll, las flechas): comparar con lo último escrito es más fiable que un indicador, porque ScrollChanged llega más tarde.
    private double? _smoothTarget;
    private double _smoothLastWritten;
    private bool _smoothRunning;
    private readonly System.Diagnostics.Stopwatch _smoothClock = new();
    private TimeSpan _smoothLastFrame;

    private void SmoothScrollBy(double delta)
    {
        double max = Math.Max(0, TabsScroll.ExtentHeight - TabsScroll.ViewportHeight);
        double from = _smoothRunning && _smoothTarget is { } pending ? pending : TabsScroll.VerticalOffset;
        _smoothTarget = Math.Clamp(from + delta, 0, max);
        if (_smoothRunning) return;

        _smoothRunning = true;
        _smoothLastWritten = TabsScroll.VerticalOffset;
        _smoothClock.Restart();
        _smoothLastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnSmoothScrollFrame;
    }

    private void StopSmoothScroll()
    {
        CompositionTarget.Rendering -= OnSmoothScrollFrame;
        _smoothRunning = false;
        _smoothTarget = null;
    }

    private void OnSmoothScrollFrame(object? sender, EventArgs e)
    {
        var now = _smoothClock.Elapsed;
        double dt = Math.Min((now - _smoothLastFrame).TotalSeconds, 0.05);
        _smoothLastFrame = now;
        if (_smoothTarget is not { } target) { StopSmoothScroll(); return; }

        double current = TabsScroll.VerticalOffset;
        if (Math.Abs(current - _smoothLastWritten) > 1) { StopSmoothScroll(); return; }

        double diff = target - current;
        if (Math.Abs(diff) < 0.5)
        {
            TabsScroll.ScrollToVerticalOffset(target);
            StopSmoothScroll();
            return;
        }

        double next = current + diff * (1 - Math.Exp(-dt / 0.05));
        TabsScroll.ScrollToVerticalOffset(next);
        _smoothLastWritten = next;
    }

    /// <summary>Una muesca completa de rueda de ratón: Windows la define como múltiplo de 120.</summary>
    private static bool IsFullWheelNotch(int delta) => Math.Abs(delta) >= 120;

    private void ScrollByArrow(int direction, double step = ArrowScrollStep)
    {
        TabsScroll.ScrollToVerticalOffset(
            Math.Clamp(
                TabsScroll.VerticalOffset + direction * step,
                0,
                Math.Max(0, TabsScroll.ExtentHeight - TabsScroll.ViewportHeight)));
    }

    private void OnScrollArrowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var direction)) return;

        _arrowScrollDirection = direction;
        _arrowScrollTicks = 0;
        ScrollByArrow(direction);
        _arrowScrollTimer.Start();
        ((UIElement)sender).CaptureMouse();

        // El clic ya es una elección explícita del usuario. Solo en ese caso permitimos que el dock
        // reciba el foco, para que después ↑/↓ y PageUp/PageDown funcionen; el simple hover sigue sin
        // robar el foco a la aplicación que estaba usando.
        NativeMethods.AllowActivation(_hwnd);
        NativeMethods.ForceActivate(this);
        Keyboard.Focus(TabsScroll);
        e.Handled = true;
    }

    private void OnScrollArrowMouseUp(object sender, MouseButtonEventArgs e)
    {
        _arrowScrollDirection = 0;
        _arrowScrollTicks = 0;
        _arrowScrollTimer.Stop();
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }

    // --- Keyboard mode (Ctrl+Alt+D) ---------------------------------------------------------------
    //
    // The dock is WS_EX_NOACTIVATE so that hovering never steals focus, and that also left its tabs
    // out of reach of the keyboard. This mode lifts the flag for as long as it lasts: it opens the fan,
    // activates the window and focuses a tab. Up/Down move between tabs, Home/End jump to the ends,
    // Enter or Space opens the note, Tab reaches the footer buttons and Esc gives the keyboard back.

    private bool _keyboardMode;
    private IntPtr _keyboardReturnWindow;

    /// <summary>Starts keyboard mode (a second call leaves it). Does nothing if the dock is hidden or empty.</summary>
    internal void EnterKeyboardMode()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible || _noteCount == 0) return;
        if (_keyboardMode)
        {
            ExitKeyboardMode(restoreFocus: true);
            return;
        }

        _keyboardReturnWindow = NativeMethods.ForegroundWindow();
        _keyboardMode = true;
        FocusRingState.Current.Visible = true;
        if (!_fanState.IsExpanded) _fanState.PointerEntered();
        NativeMethods.EnsureTopmost(_hwnd);
        NativeMethods.AllowActivation(_hwnd);
        NativeMethods.ForceActivate(this);
        // The tab buttons only exist once the fan has been laid out.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => FocusTab(TabButtons().FirstOrDefault())));
    }

    private void ExitKeyboardMode(bool restoreFocus)
    {
        if (!_keyboardMode) return;
        _keyboardMode = false;
        // The tab or footer button that had the focus must not keep its ring once the keyboard is given back.
        FocusRingState.Current.Visible = false;
        Keyboard.ClearFocus();
        NativeMethods.MakeNonActivating(_hwnd);
        if (restoreFocus) NativeMethods.Reactivate(_keyboardReturnWindow);
        _keyboardReturnWindow = IntPtr.Zero;
    }

    // Clicking elsewhere, or opening a note (which takes the focus), ends the mode without grabbing it back.
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        ExitKeyboardMode(restoreFocus: false);
    }

    private IEnumerable<Button> TabButtons()
    {
        for (int i = 0; i < TabsList.Items.Count; i++)
        {
            if (TabsList.ItemContainerGenerator.ContainerFromIndex(i) is { } container && FirstButtonIn(container) is { } button)
                yield return button;
        }
    }

    private static Button? FirstButtonIn(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button button) return button;
            if (FirstButtonIn(child) is { } inner) return inner;
        }

        return null;
    }

    private static void FocusTab(Button? tab)
    {
        if (tab is null) return;
        tab.BringIntoView();
        tab.Focus();
        Keyboard.Focus(tab);
    }

    /// <summary>Returns true when the key was consumed by keyboard mode.</summary>
    private bool HandleKeyboardModeKey(KeyEventArgs e)
    {
        var tabs = TabButtons().ToList();
        int current = tabs.FindIndex(t => t.IsKeyboardFocused);
        switch (e.Key)
        {
            case Key.Escape:
                ExitKeyboardMode(restoreFocus: true);
                return true;
            case Key.Up:
            case Key.Down:
                if (tabs.Count == 0) return true;
                int step = e.Key == Key.Up ? -1 : 1;
                FocusTab(tabs[current < 0 ? 0 : Math.Clamp(current + step, 0, tabs.Count - 1)]);
                return true;
            case Key.Home:
                FocusTab(tabs.FirstOrDefault());
                return true;
            case Key.End:
                FocusTab(tabs.LastOrDefault());
                return true;
            default:
                return false;
        }
    }

    private void OnDockPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_keyboardMode && HandleKeyboardModeKey(e))
        {
            e.Handled = true;
            return;
        }

        if (!_fanState.IsExpanded) return;

        switch (e.Key)
        {
            case Key.Up:
                ScrollByArrow(-1);
                e.Handled = true;
                break;
            case Key.Down:
                ScrollByArrow(1);
                e.Handled = true;
                break;
            case Key.PageUp:
                TabsScroll.PageUp();
                e.Handled = true;
                break;
            case Key.PageDown:
                TabsScroll.PageDown();
                e.Handled = true;
                break;
            case Key.Home:
                TabsScroll.ScrollToHome();
                e.Handled = true;
                break;
            case Key.End:
                TabsScroll.ScrollToEnd();
                e.Handled = true;
                break;
        }
    }

    private IntPtr OnGlobalKeyboardHook(int code, IntPtr message, IntPtr data)
    {
        // Esc durante un arrastre lo cancela, esté el cursor donde esté: el gesto puede haber salido
        // del dock. Se lo come para que no llegue también a la aplicación que tiene el foco.
        if (code >= 0 && _dragging
            && (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_KEYUP)
            && NativeMethods.GetKeyboardVirtualKey(data) == 0x1B) // VK_ESCAPE
        {
            if (message == NativeMethods.WM_KEYDOWN)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(CancelDrag));
            return (IntPtr)1;
        }

        if (code >= 0
            && _fanState.IsExpanded
            && _hover.IsPointerInside
            && _hwnd != IntPtr.Zero
            && NativeMethods.IsCursorOverWindow(_hwnd)
            && message is { } currentMessage
            && (currentMessage == NativeMethods.WM_KEYDOWN
                || currentMessage == NativeMethods.WM_KEYUP
                || currentMessage == NativeMethods.WM_SYSKEYDOWN
                || currentMessage == NativeMethods.WM_SYSKEYUP))
        {
            int virtualKey = NativeMethods.GetKeyboardVirtualKey(data);
            int direction = virtualKey switch
            {
                0x26 => -1, // VK_UP
                0x28 => 1,  // VK_DOWN
                _ => 0
            };

            if (direction != 0)
            {
                bool keyDown = currentMessage is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
                if (keyDown)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        if (_fanState.IsExpanded) ScrollByArrow(direction);
                    }));
                }

                // Evita que la aplicación que está debajo también consuma la flecha mientras el
                // cursor está sobre el dock. Fuera de esta zona el teclado sigue siendo totalmente
                // transparente para el resto del escritorio.
                return (IntPtr)1;
            }
        }

        return NativeMethods.ContinueHook(_keyboardHook, code, message, data);
    }

    private void SetScrollIndicatorInset(double inset)
    {
        _scrollIndicatorInset = inset;
        ScrollRailTrack.Margin = new Thickness(0, inset, 0, inset);
        ScrollRailCanvas.Margin = new Thickness(0, inset, 0, inset);
    }

    private void OnScrollIndicatorMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!HasScrollableTabs()) return;

        Point point = e.GetPosition(ScrollOverlay);
        double thumbTop = Canvas.GetTop(ScrollRailThumb);
        if (double.IsNaN(thumbTop)) thumbTop = 0;

        // El Canvas vive dentro de un margen, por lo que su coordenada 0 no coincide con la
        // coordenada 0 de ScrollOverlay. Sin este inset el primer clic se interpreta fuera del thumb
        // y el arrastre parece saltar hasta que el movimiento vuelve a sincronizarlo.
        double thumbTopInOverlay = _scrollIndicatorInset + thumbTop;
        if (point.Y >= thumbTopInOverlay && point.Y <= thumbTopInOverlay + ScrollRailThumb.ActualHeight)
        {
            _scrollIndicatorDragging = true;
            _scrollIndicatorDragStartY = point.Y;
            _scrollIndicatorDragStartOffset = TabsScroll.VerticalOffset;
            ScrollOverlay.CaptureMouse();
        }
        else if (point.Y < thumbTopInOverlay)
        {
            TabsScroll.PageUp();
        }
        else
        {
            TabsScroll.PageDown();
        }

        e.Handled = true;
    }

    private void OnScrollIndicatorMouseMove(object sender, MouseEventArgs e)
    {
        if (!_scrollIndicatorDragging) return;

        double trackHeight = Math.Max(0, ScrollOverlay.ActualHeight - _scrollIndicatorInset * 2);
        double thumbHeight = ScrollRailThumb.ActualHeight;
        double scrollRange = Math.Max(0, trackHeight - thumbHeight);
        double offsetRange = Math.Max(0, TabsScroll.ExtentHeight - TabsScroll.ViewportHeight);
        if (scrollRange > 0 && offsetRange > 0)
        {
            double delta = e.GetPosition(ScrollOverlay).Y - _scrollIndicatorDragStartY;
            TabsScroll.ScrollToVerticalOffset(
                _scrollIndicatorDragStartOffset + delta * offsetRange / scrollRange);
        }

        e.Handled = true;
    }

    private void OnScrollIndicatorMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_scrollIndicatorDragging) return;

        _scrollIndicatorDragging = false;
        ScrollOverlay.ReleaseMouseCapture();
        e.Handled = true;
    }

    private static bool Contains(Aldune.Core.Rect rect, double x, double y) =>
        x >= rect.X && x <= rect.X + rect.Width
            && y >= rect.Y && y <= rect.Y + rect.Height;

    /// <summary>
    /// La superficie que mantiene el dock abierto: pestañas visibles, botonera y controles de scroll,
    /// con <see cref="DockHoverZone.ExpandedSlop"/> de holgura, más la tira de reposo (la puerta de
    /// entrada: volver hacia el canto con un movimiento corto no debe iniciar un cierre). Igual en los
    /// cuatro bordes; antes arriba/abajo usaba la ventana entera, que crece con el número de notas.
    /// </summary>
    private bool IsInsideExpandedSurface(IReadOnlyList<Aldune.Core.Rect> surfaces, double cursorX, double cursorY) =>
        Contains(EdgeGeometry.RestingVisibleRect(_workingArea, _edge, _noteCount, Alignment), cursorX, cursorY)
        || DockHoverZone.Contains(surfaces, cursorX, cursorY, DockHoverZone.ExpandedSlop);

    /// <summary>Encima de una pestaña o de un control del dock, sin holgura: el dock se está usando.</summary>
    private bool IsOverDockTarget(IReadOnlyList<Aldune.Core.Rect> surfaces, double cursorX, double cursorY) =>
        _fanState.IsExpanded && DockHoverZone.Contains(surfaces, cursorX, cursorY, 0);

    private Aldune.Core.Rect? ViewLabelRect()
    {
        if (!ViewLabelPinned || _viewLabelPopup is not { IsOpen: true, Child: FrameworkElement label } || label.ActualWidth <= 0) return null;
        try
        {
            var origin = label.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(this);
            const double slop = ViewLabelGap + 2;
            return new Aldune.Core.Rect(origin.X / dpi.DpiScaleX - slop, origin.Y / dpi.DpiScaleY - slop, label.ActualWidth + slop * 2, label.ActualHeight + slop * 2);
        }
        catch (InvalidOperationException)
        {
            return null; // the popup went away between the check and the read
        }
    }

    private IEnumerable<Aldune.Core.Rect> ExpandedSurfaces()
    {
        // The fixed view label counts as part of the dock (a little margin bridges the gap to the notes): going up to click it must not fold the fan.
        if (ViewLabelRect() is { } labelRect) yield return labelRect;

        foreach (var element in new FrameworkElement[] { FooterBorder, ScrollArrowOverlay, ScrollOverlay })
            if (ElementRect(element) is { } rect) yield return rect;

        // Solo la parte de cada pestaña que se ve en el viewport: las que el scroll deja fuera no
        // están ahí para el ratón.
        if (ElementRect(TabsScroll) is not { } viewport) yield break;

        // Antes de que WPF genere los primeros contenedores, el viewport sirve como reserva para
        // que la primera apertura no se cierre durante el layout.
        if (_tabButtons.Count == 0)
        {
            yield return viewport;
            yield break;
        }

        foreach (var button in _tabButtons.Values)
            if (ElementRect(button) is { } tab && Intersect(tab, viewport) is { } visible)
                yield return visible;
    }

    private Aldune.Core.Rect? ElementRect(FrameworkElement element)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return null;

        try
        {
            Point origin = element.TranslatePoint(new Point(0, 0), this);
            return new Aldune.Core.Rect(Left + origin.X, Top + origin.Y, element.ActualWidth, element.ActualHeight);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static Aldune.Core.Rect? Intersect(Aldune.Core.Rect a, Aldune.Core.Rect b)
    {
        double left = Math.Max(a.X, b.X), top = Math.Max(a.Y, b.Y);
        double right = Math.Min(a.X + a.Width, b.X + b.Width), bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return right > left && bottom > top ? new Aldune.Core.Rect(left, top, right - left, bottom - top) : null;
    }

    private void UpdateScrollIndicator()
    {
        double extent = TabsScroll.ExtentHeight;
        double viewport = TabsScroll.ViewportHeight;
        bool scrollable = HasScrollableTabs();

        ScrollOverlay.Visibility = scrollable && !IsTopBottomEdge
            ? Visibility.Visible
            : Visibility.Collapsed;
        ScrollArrowOverlay.Visibility = scrollable && IsTopBottomEdge
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Arriba/abajo usan flechas y mantienen ScrollOverlay colapsado. No intentes medir el rail en
        // esa orientación: su tamaño será siempre cero y volver a encolarlo aquí crea un ciclo de
        // Dispatcher que congela toda la interfaz al abrir el dock por primera vez.
        if (IsTopBottomEdge)
        {
            ScrollOverlay.Clip = null;
            return;
        }

        if (!scrollable)
        {
            ScrollOverlay.Clip = null;
            return;
        }

        if (ScrollOverlay.ActualHeight <= 0 || ScrollOverlay.ActualWidth <= 0)
        {
            QueueScrollIndicatorUpdate();
            return;
        }

        double trackHeight = Math.Max(0, ScrollOverlay.ActualHeight - _scrollIndicatorInset * 2);
        if (trackHeight <= 0) return;

        // WPF no recorta un Grid a sus límites por defecto. El clip hace explícito el rectángulo
        // útil y evita que el rail asome fuera de la casilla transparente en los docks laterales.
        ScrollOverlay.Clip = new RectangleGeometry(new System.Windows.Rect(
            0,
            _scrollIndicatorInset,
            ScrollOverlay.ActualWidth,
            trackHeight));

        double thumbHeight = Math.Max(28, trackHeight * TabsScroll.ViewportHeight / TabsScroll.ExtentHeight);
        thumbHeight = Math.Min(thumbHeight, trackHeight);
        ScrollRailThumb.Height = thumbHeight;
        Canvas.SetLeft(ScrollRailThumb, Math.Max(0, (ScrollOverlay.ActualWidth - ScrollRailThumb.Width) / 2));

        double scrollRange = Math.Max(0, trackHeight - thumbHeight);
        double offsetRange = Math.Max(0, TabsScroll.ExtentHeight - TabsScroll.ViewportHeight);
        Canvas.SetTop(ScrollRailThumb, offsetRange <= 0
            ? 0
            : scrollRange * TabsScroll.VerticalOffset / offsetRange);
    }

    private bool HasScrollableTabs()
    {
        return _fanState.IsExpanded
            && _noteCount > 1
            && TabsScroll.ViewportHeight > 0
            && TabsScroll.ExtentHeight > TabsScroll.ViewportHeight
                + EdgeGeometry.TabShadowHeadroom + 0.5;
    }

    private void QueueScrollIndicatorUpdate()
    {
        if (_scrollIndicatorUpdateQueued) return;

        _scrollIndicatorUpdateQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _scrollIndicatorUpdateQueued = false;
            UpdateScrollIndicator();
        }));
    }

    /// <summary>
    /// Esconde la vista previa de una pestaña cuando el solape no le deja sitio.
    ///
    /// Se limpia el valor local en vez de fijarlo a Visible: la plantilla ya tiene un DataTrigger
    /// que la colapsa cuando la nota no tiene cuerpo, y en WPF un valor local gana a un trigger —
    /// fijarlo aquí a Visible resucitaría la línea vacía de las notas sin cuerpo.
    /// </summary>
    private void ApplyPreviewVisibility(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("SnippetText", button) is not TextBlock snippet) return;

        if (EdgeGeometry.ShowsPreview(_workingArea, _edge, _noteCount))
        {
            snippet.ClearValue(VisibilityProperty);
        }
        else
        {
            snippet.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Muestra u oculta el glifo de reloj de una pestaña según si su nota tiene un
    /// recordatorio pendiente — ver ApplyPreviewVisibility para el mismo patrón de FindName.</summary>
    private void ApplyReminderBadge(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("ReminderBadge", button) is not TextBlock badge) return;

        bool hasReminder = button.Tag is Note note && _pendingReminders.ContainsKey(note.Id);
        badge.Visibility = hasReminder ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
    {
        HoldHoverDuringLayout();
        HoldOpenForWindow(ownWindow: true);
        _coordinator.OpenOrActivateNotesManager(this, CurrentTagFilter);
        e.Handled = true;
    }

    private void OnViewButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_dockViewToggle.ShouldConsumeOpen()) return;
        OpenDockViewPopupFrom(ViewButton);
    }

    private void OnManageArchiveRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_dockViewToggle.ShouldConsumeOpen())
        {
            e.Handled = true;
            return;
        }

        // Right click: the list of views and tags plus the options for when the view name shows (the same as in Settings).
        OpenDockViewPopup(labelOptions: true);
        e.Handled = true;
    }

    // --- Cambiar de vista con la rueda --------------------------------------------------------------

    private DateTime _wheelViewUntil = DateTime.MinValue;

    private DockViewCycle.Counts BuildViewCounts()
    {
        var tags = _repository.GetAllTags().ToDictionary(t => t, t => _repository.GetByTag(t, NoteState.Active).Count, StringComparer.OrdinalIgnoreCase);
        return new DockViewCycle.Counts(
            _repository.GetByState(NoteState.Active).Count, _repository.GetByState(NoteState.Archived).Count,
            _repository.GetByState(NoteState.Trashed).Count, tags, _settings?.TagOrder);
    }

    private string CurrentViewLabel() => DockViewCycle.Label(
        _settings?.DockView ?? DockViewKind.Active, _settings?.DockTagFilter,
        Strings.DockViewActive, Strings.DockViewArchived, Strings.DockViewTrash, Strings.DockViewAll);

    // --- Wheel session -------------------------------------------------------------------------------
    //
    // Turning the wheel from a tag with a long list to one with few notes shrinks the window with the new note count: the cursor was left
    // over transparent pixels (the OS sends the wheel to whatever is behind) and the user could not keep turning. While a session is
    // alive (1.5 s after the last notch) a global wheel hook, limited to the area the dock covered when the session began, keeps feeding
    // the view change wherever the cursor is inside it. Nothing is painted and nothing is kept: the dock shrinks at once as usual.
    // (A nearly transparent background to catch the wheel was tried and rendered as black bands on some systems.)

    // 1 s after the last notch (it was 1.5 s): while it lasts the wheel over the zone the dock covered is taken, even over what is now behind it
    // (a browser under a dock that has just shrunk). A turn of the wheel arrives every 100-600 ms, so a second is enough to keep cycling.
    private static readonly TimeSpan WheelSessionLength = TimeSpan.FromMilliseconds(1000);
    private readonly DispatcherTimer _wheelSessionTimer = new() { Interval = WheelSessionLength };
    private IntPtr _wheelHook;
    private readonly NativeMethods.LowLevelMouseProc _wheelHookProc;
    private Aldune.Core.Rect _wheelZonePx;

    private bool WheelSessionActive => DateTime.UtcNow < _wheelViewUntil;

    // While the fan is open the wheel hook is installed too, not only for the second after a notch: the gaps of the dock (between the last
    // note and the button bar, beside the cards) are transparent and the wheel over them goes to whatever is behind, so the first notch had to
    // land on a card or on the bar.
    private bool WantsWheelHook => _settings is { WheelChangesDockView: true } && _fanState.IsExpanded && _noteCount > 0 && IsVisible;

    private void UpdateWheelHook()
    {
        if (WantsWheelHook)
        {
            if (_wheelHook == IntPtr.Zero) _wheelHook = NativeMethods.InstallMouseHook(_wheelHookProc);
        }
        else if (!WheelSessionActive && !_wheelSessionTimer.IsEnabled)
        {
            UninstallWheelHook();
        }
    }

    private void UninstallWheelHook()
    {
        NativeMethods.UninstallHook(_wheelHook);
        _wheelHook = IntPtr.Zero;
    }

    private Aldune.Core.Rect CurrentWheelZonePx()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount, Alignment);
        return new Aldune.Core.Rect(rect.X * dpi.DpiScaleX, rect.Y * dpi.DpiScaleY, rect.Width * dpi.DpiScaleX, rect.Height * dpi.DpiScaleY);
    }

    private void BeginWheelSession()
    {
        if (_wheelHook == IntPtr.Zero)
        {
            // The zone is what the dock covers NOW (before the view changes), in physical pixels.
            _wheelZonePx = CurrentWheelZonePx();
            _wheelHook = NativeMethods.InstallMouseHook(_wheelHookProc);
        }
        else if (!WheelSessionActive && !_wheelSessionTimer.IsEnabled)
        {
            _wheelZonePx = CurrentWheelZonePx(); // the hook was already there for the open fan: the zone is where the dock is now
        }
        _wheelSessionTimer.Stop();
        _wheelSessionTimer.Start();
    }

    private void EndWheelSession()
    {
        _wheelSessionTimer.Stop();
        if (WantsWheelHook) return; // the fan is still open: the hook stays for the gaps
        UninstallWheelHook();
    }

    private void OnWheelSessionTimer(object? sender, EventArgs e) => EndWheelSession();

    private IntPtr OnWheelHook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && NativeMethods.IsMouseWheel(message) && (WheelSessionActive || WantsWheelHook) && _settings is { WheelChangesDockView: true })
        {
            // Without a session the zone is where the dock is right now; with one, where it was when the session began (the dock may have shrunk).
            if (!WheelSessionActive) _wheelZonePx = CurrentWheelZonePx();
            var point = NativeMethods.MouseHookPoint(data);
            int delta = NativeMethods.MouseHookWheelDelta(data);
            if (delta != 0 && point.X >= _wheelZonePx.X && point.X <= _wheelZonePx.X + _wheelZonePx.Width
                && point.Y >= _wheelZonePx.Y && point.Y <= _wheelZonePx.Y + _wheelZonePx.Height)
            {
                // Another window of Aldune over the zone (Manage notes, a note, Settings): the wheel is for it, not for the dock. The zone is where
                // the dock WAS when the session began, and with the dock folded the cursor over such a window used to be swallowed to cycle the view
                // (reproduced with a real mouse: probe37, before and after).
                if (NativeMethods.IsOtherOwnWindowAt((int)point.X, (int)point.Y, _hwnd))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(EndWheelSession));
                    return NativeMethods.ContinueHook(_wheelHook, code, message, data);
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => CycleDockView(delta < 0 ? 1 : -1, showLabel: true)));
                return (IntPtr)1; // swallowed: the view change is the only effect of this notch
            }
        }
        return NativeMethods.ContinueHook(_wheelHook, code, message, data);
    }

    private void CycleDockView(int direction, bool showLabel)
    {
        BeginWheelSession();
        var (view, tag) = DockViewCycle.Next(_settings?.DockView ?? DockViewKind.Active, _settings?.DockTagFilter, BuildViewCounts(), direction, _settings?.WheelStops);
        // Whether the view really changes, worked out before applying it (applying it refreshes the dock and updates what is compared here).
        bool changes = view != (_settings?.DockView ?? DockViewKind.Active)
            || !string.Equals(tag, _settings?.DockTagFilter, StringComparison.OrdinalIgnoreCase) && view == DockViewKind.Tag;
        _coordinator.SetDockViewCoalesced(view, tag);
        _wheelViewUntil = DateTime.UtcNow + WheelSessionLength;
        HoldOpenForNextInteraction(grace: false);
        if (!showLabel) return;
        // The name of the view appears when the cards change (inside the swap), not the instant the wheel turns: it used to name the new view
        // over cards that still showed the old one. With nothing to wait for (no animation, or the view did not change) it shows at once.
        bool willSwap = changes && IsVisible && _fanState.IsExpanded && SystemParameters.ClientAreaAnimation;
        if (willSwap) _labelOnSwap = true;
        else ShowViewLabel(CurrentViewLabel());
    }

    private bool _labelOnSwap;

    /// <summary>The view was a stop before the change and no longer is: go to the next stop (a no-op otherwise, or when no other stop exists).</summary>
    internal void EnsureViewIsAnEnabledWheelStop(DockViewCycle.Stops before)
    {
        if (_settings is null) return;
        var view = _settings.DockView;
        var tag = _settings.DockTagFilter;
        // Only when the switch that was just moved is the one that took THIS view off the wheel: a view picked by hand that was never a stop
        // (Trash taken off the wheel earlier, then chosen from the gear menu) must not be left because an unrelated switch was moved.
        if (!DockViewCycle.IsEnabledStop(view, tag, before) || DockViewCycle.IsEnabledStop(view, tag, _settings.WheelStops)) return;
        var (nextView, nextTag) = DockViewCycle.Next(view, tag, BuildViewCounts(), 1, _settings.WheelStops);
        if (nextView == view && string.Equals(nextTag, tag, StringComparison.OrdinalIgnoreCase)) return;
        _coordinator.SetDockView(nextView, nextTag);
    }

    private void OnManageArchiveWheel(object sender, MouseWheelEventArgs e)
    {
        if (_settings is not { WheelChangesDockView: true } || e.Delta == 0) return;
        CycleDockView(e.Delta < 0 ? 1 : -1, showLabel: true);
        e.Handled = true;
    }

    private const double ViewLabelGap = 4;      // margin of the hover surface around the label
    private const double ViewLabelAir = 8;      // visible air between the label and the notes: the same as above the button bar
    private const double ViewLabelSideGap = 10; // distance to the notes when the label has to go beside them
    private System.Windows.Controls.Primitives.Popup? _viewLabelPopup;
    private TextBlock? _viewLabelText;
    private TextBlock? _viewLabelChevron;
    private Border? _viewLabelBorder;

    /// <summary>A click on the fixed view label opens the same view menu as the cogwheel's right click.</summary>
    private void OnViewLabelClick(object sender, MouseButtonEventArgs e)
    {
        if (!ViewLabelPinned) return;
        e.Handled = true;
        if (_dockViewToggle.ShouldConsumeOpen()) return;
        OpenDockViewPopupFrom(_viewLabelBorder!);
    }
    private readonly System.Windows.Threading.DispatcherTimer _viewLabelTimer = new() { Interval = TimeSpan.FromMilliseconds(1100) };

    /// <summary>Un rótulo breve con el nombre de la vista a la que se acaba de llegar, sobre las pestañas; se apaga solo.</summary>
    /// <summary>Whether a label of <paramref name="height"/> fits on the screen at the free end of the strip (see <see cref="ShowViewLabel"/>).</summary>
    private bool ViewLabelFitsOnScreen(double height)
    {
        try
        {
            var top = TabsScroll.TranslatePoint(new Point(0, 0), this);
            double stripTop = Top + top.Y, stripBottom = stripTop + TabsScroll.ActualHeight;
            const double margin = 2;
            return _edge == EdgePosition.Top
                ? stripBottom + ViewLabelAir + height <= _workingArea.Y + _workingArea.Height - margin
                : stripTop + EdgeGeometry.TabShadowHeadroom - ViewLabelAir - height >= _workingArea.Y + margin;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private void ShowViewLabel(string text)
    {
        if (_settings?.DockViewLabel == DockViewLabelMode.Hidden) return;
        // A name that changes while the card is showing (a fixed card, or a refresh with no swap): it leaves, and comes back already with the
        // new size, instead of resizing in view (its window follows the new size a couple of frames late).
        if (_viewLabelPopup is { IsOpen: true } && _viewLabelBorder is { Opacity: > 0.05 } shown && _viewLabelText is { } current
            && current.Text != text && SystemParameters.ClientAreaAnimation)
        {
            int id = ++_viewLabelFade;
            _viewLabelTimer.Stop();
            var leave = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(70))) { EasingFunction = EaseOut() };
            leave.Completed += (_, _) =>
            {
                if (id != _viewLabelFade) return;
                _viewLabelPopup!.IsOpen = false;
                ShowViewLabelCore(text);
            };
            shown.BeginAnimation(OpacityProperty, leave, HandoffBehavior.SnapshotAndReplace);
            return;
        }
        ShowViewLabelCore(text);
    }

    private void ShowViewLabelCore(string text)
    {
        if (_viewLabelPopup is null)
        {
            _viewLabelText = new TextBlock
            {
                FontSize = 13, FontWeight = FontWeights.SemiBold, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                // Measured: the glyphs sat 1 px low (17 px of air above, 15 below); the line box has more room above the baseline than below.
                Margin = new Thickness(0, 0, 0, 2)
            };
            _viewLabelText.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextStrongBrush");
            // The chevron only shows when the label is fixed: that is when it is a control (a click opens the view menu).
            _viewLabelChevron = new TextBlock
            {
                Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 9,
                Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed
            };
            _viewLabelChevron.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextSoftBrush");
            var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(11, 0, 11, 0), VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(_viewLabelText);
            content.Children.Add(_viewLabelChevron);
            // Twin of the button bar (FooterBorder): same pill, same style per look (XP gradient included), same bevel and the same height, so
            // the two read as the top and bottom of one frame. A translucent layer is the hover feedback: a local background would
            // override the style's per-look triggers.
            var hover = new Border { Margin = new Thickness(-5), Opacity = 0.55, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            hover.SetResourceReference(Border.BackgroundProperty, "AlduneHoverBrush");
            hover.SetResourceReference(Border.CornerRadiusProperty, "AlduneRadius21");
            var inner = new Grid();
            inner.Children.Add(hover);
            inner.Children.Add(content);
            inner.Children.Add(new BevelEdge { Margin = new Thickness(-FooterPadding), ShadowOutside = true });
            var border = new Border { Padding = new Thickness(FooterPadding), Child = inner, IsHitTestVisible = false, Style = (Style)FindResource("XpBarBorderStyle") };
            border.SetResourceReference(Border.CornerRadiusProperty, "AlduneRadius21");
            border.MouseEnter += (_, _) => hover.Visibility = Visibility.Visible;
            border.MouseLeave += (_, _) => hover.Visibility = Visibility.Collapsed;
            border.MouseLeftButtonUp += OnViewLabelClick;
            // The pinned label is a popup (its own HWND): a wheel turned over it never reached the dock and the view stopped changing there.
            border.PreviewMouseWheel += OnManageArchiveWheel;
            // The window draws its text in Display mode and the card inherits that only while it is open. Measured closed (as it is just before it
            // opens) it used to be measured in Ideal mode, 1-2 px narrower, and the name came out cut ("#Carrer…") until the first resize.
            TextOptions.SetTextFormattingMode(border, TextFormattingMode.Display);
            border.UseLayoutRounding = true;
            border.SnapsToDevicePixels = true;
            _viewLabelBorder = border;
            // The label only knows its size once it is open and measured: until then the popup would sit at the screen origin.
            border.SizeChanged += (_, _) => ReplaceViewLabelIfOpen();
            _viewLabelPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = border, AllowsTransparency = true, StaysOpen = true, IsHitTestVisible = false
            };
            _viewLabelTimer.Tick += (_, _) => { _viewLabelTimer.Stop(); if (!ViewLabelPinned) FadeViewLabel(show: false); };
        }

        _viewLabelText!.Text = text;
        // Fixed = a control (hand cursor, chevron, clickable); the one-second flash after a change stays inert so it cannot swallow a click.
        bool pinned = ViewLabelPinned;
        _viewLabelChevron!.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
        _viewLabelBorder!.IsHitTestVisible = pinned;
        _viewLabelBorder.Cursor = pinned ? Cursors.Hand : null;
        _viewLabelPopup.IsHitTestVisible = pinned;
        // Always in the same place: centred on the notes column, at the free end of the strip (opposite to the button bar: above the first
        // note on the left, right and bottom docks; below the last one on the top dock, where the bar hugs the screen edge). A custom
        // placement never flips to the other side of its target the way PlacementMode.Left/Right/Bottom did when the label did not fit,
        // which is what made it jump from one side of the gear to the other. It never covers a note either.
        // With no notes there is no strip, so it falls back to being centred under/over the gear.
        // Same height as the button bar and, like it, 8 px of visible air to the notes: on the left, right and bottom docks the first card sits
        // TabShadowHeadroom below the top of the strip (that headroom is transparent), so the label overlaps it; on the top dock the last card
        // is flush with the bottom of the strip. If that does not fit on the screen (dock aligned to a screen end, or a very long list) the
        // label goes beside the first note, on the screen side, instead of covering notes.
        PlaceViewLabel();
        // The card is a window of its own and it takes a couple of frames to take the new size: it is kept invisible until then, or the fade-in
        // showed it with the size of the previous name (too wide or too narrow) and then jumping.
        if (SystemParameters.ClientAreaAnimation && (_viewLabelPopup.IsOpen ? _viewLabelBorder.Opacity < 0.05 : true))
        {
            int pending = ++_viewLabelFade;
            if (!_viewLabelPopup.IsOpen)
            {
                _viewLabelBorder.BeginAnimation(OpacityProperty, null);
                _viewLabelBorder.Opacity = 0;
                _viewLabelPopup.IsOpen = true;
            }
            AfterFrames(2, () =>
            {
                if (pending != _viewLabelFade) return;
                // Measured again now that the card is open and laid out: it is the only measure that is always right (the first one, made while
                // the card is closed, can come out a pixel or two short), and nobody sees it yet.
                PlaceViewLabel();
                FadeViewLabel(show: true);
            });
        }
        else
        {
            FadeViewLabel(show: true);
        }
        _viewLabelTimer.Stop();
        if (!ViewLabelPinned) _viewLabelTimer.Start();
    }

    private int _viewLabelFade;

    /// <summary>
    /// The label eases in and out instead of blinking: quick on the way in (it answers the turn of the wheel), slower on the way out (it just
    /// stops being needed). A label that is already showing only keeps its place when the text changes, and one that is leaving comes
    /// back from where it was; a newer call always wins over an older fade.
    /// </summary>
    private void FadeViewLabel(bool show, double milliseconds = 0)
    {
        if (_viewLabelPopup is not { } popup || _viewLabelBorder is not { } border) return;
        int id = ++_viewLabelFade;
        if (!SystemParameters.ClientAreaAnimation)
        {
            border.BeginAnimation(OpacityProperty, null);
            border.Opacity = 1;
            popup.IsOpen = show;
            return;
        }

        if (show && !popup.IsOpen)
        {
            border.Opacity = 0;
            popup.IsOpen = true;
        }
        if (!show && !popup.IsOpen) return;

        var animation = new DoubleAnimation(show ? 1 : 0, new Duration(TimeSpan.FromMilliseconds(milliseconds > 0 ? milliseconds : show ? 140 : 280))) { EasingFunction = EaseOut() };
        if (!show)
            animation.Completed += (_, _) => { if (id == _viewLabelFade) popup.IsOpen = false; };
        border.BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Computes where the label goes from the sizes the strip has NOW and nudges the popup so WPF asks for that placement again. It is called
    /// when the label is shown and again whenever the strip, the window or the label itself changes size: a popup does not follow its target
    /// on its own, and <see cref="ShowViewLabel"/> runs right after the view changes, before the list has been measured, so the first placement
    /// is made with the old size (the label hung in the old spot for a frame or two).
    /// </summary>
    private void PlaceViewLabel()
    {
        if (_viewLabelPopup is null || _viewLabelBorder is null) return;
        _viewLabelBorder.Height = FooterBorder.ActualHeight > 0 ? FooterBorder.ActualHeight : 42;
        // An even width, so that centred on the (even) notes column it sits on whole pixels: with an odd one it ended half a pixel to one side.
        _viewLabelBorder.ClearValue(FrameworkElement.WidthProperty);
        // A card that is closed is not hooked to the layout manager: the new name does not mark its measure as stale there, and the measure
        // below used to answer with the size of the previous name (the card came out too narrow, "#Carrer…", and widened later).
        _viewLabelText?.InvalidateMeasure();
        _viewLabelBorder.InvalidateMeasure();
        _viewLabelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _viewLabelBorder.Width = Math.Ceiling(_viewLabelBorder.DesiredSize.Width / 2) * 2;
        // With no notes there is no strip: the label sits over (or under) the whole button bar, centred on it. It used to be centred on the
        // first button, and a label wider than that button hung off the bar to the left.
        _viewLabelPopup.PlacementTarget = _noteCount > 0 ? TabsScroll : FooterBorder;
        _viewLabelPopup.Placement = PlacementMode.Custom;
        bool onStrip = _noteCount > 0;
        bool fits = !onStrip || ViewLabelFitsOnScreen(_viewLabelBorder.Height);
        _viewLabelPopup.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
        {
            bool below = _edge == EdgePosition.Top;
            if (onStrip && !fits)
            {
                // Beside the first note (or the last, on the top dock), towards the inside of the screen.
                double sideX = _edge == EdgePosition.Left ? targetSize.Width + ViewLabelSideGap : -popupSize.Width - ViewLabelSideGap;
                double sideY = below ? targetSize.Height - popupSize.Height : EdgeGeometry.TabShadowHeadroom;
                return [new CustomPopupPlacement(new Point(sideX, sideY), PopupPrimaryAxis.Vertical)];
            }
            double x = (targetSize.Width - popupSize.Width) / 2;
            double y = !onStrip
                ? (below ? targetSize.Height + ViewLabelAir : -ViewLabelAir - popupSize.Height)
                : (below ? targetSize.Height + ViewLabelAir : EdgeGeometry.TabShadowHeadroom - ViewLabelAir - popupSize.Height);
            return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Horizontal)];
        };
        // A popup does not follow its target when the strip changes size: nudging the offset makes it place itself again.
        _viewLabelPopup.HorizontalOffset = _viewLabelPopup.HorizontalOffset == 0 ? 0.01 : 0;
    }

    private void ReplaceViewLabelIfOpen()
    {
        if (_viewLabelPopup is { IsOpen: true } && !_viewLabelHold) PlaceViewLabel();
    }

    /// <summary>
    /// The cogwheel says which view the dock shows (tooltip). While the view is not «Active» and the fan is open, the view label stays up
    /// above the notes (see <see cref="RefreshViewLabel"/>): that is the cue that the dock is filtered, so notes that are not shown are not
    /// mistaken for lost ones. (A dot, and then an accent ring, on the cogwheel were tried and dropped.)
    /// </summary>
    private void UpdateViewIndicator()
    {
        UpdateTagAwareTooltips();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshViewLabel));
    }

    /// <summary>
    /// Whether the view label is fixed above the notes right now: depends on the user's choice (<see cref="DockViewLabelMode"/>), on the view
    /// («Active» only counts in the «always» mode) and on the fan being open, so nothing floats over the desktop while the dock is folded.
    /// </summary>
    private bool ViewLabelPinned
    {
        get
        {
            var mode = _settings?.DockViewLabel ?? DockViewLabelMode.WhenFiltered;
            bool filtered = (_settings?.DockView ?? DockViewKind.Active) != DockViewKind.Active;
            bool wanted = mode == DockViewLabelMode.Always || (mode == DockViewLabelMode.WhenFiltered && filtered);
            return wanted && _noteCount > 0 && _fanState.IsExpanded && IsVisible && !_hiddenByFullscreenApp;
        }
    }

    /// <summary>Keeps the view label up while it should be pinned (filtered view, fan open) and takes it down when it should not.</summary>
    private void RefreshViewLabel()
    {
        if (ViewLabelPinned) ShowViewLabel(CurrentViewLabel());
        else if (_viewLabelPopup is { IsOpen: true } && !_viewLabelTimer.IsEnabled) FadeViewLabel(show: false);
    }

    private void OnDockViewPopupOpened(object sender, EventArgs e)
    {
        DockViewKeepDockButton.Content = _settings?.KeepDockOpen == true
            ? Strings.KeepDockOpenOn
            : Strings.KeepDockOpenOff;
        BuildDockLabelModeChoices();

        _coordinator.SuspendNotesAboveDockMenu();
        _openAllMenuTopmostTimer.Start();
        RaiseDockViewPopup();
    }

    private void OnDockViewPopupClosed(object sender, EventArgs e)
    {
        _hoverLayoutHold = false;
        if (!OpenAllMenuPopup.IsOpen) _openAllMenuTopmostTimer.Stop();
        _coordinator.RestoreNotesAboveDockMenu();
        PollHoverState();
    }

    private void OpenDockViewPopup(bool labelOptions = false) => OpenDockViewPopupFrom(ManageArchiveButton, labelOptions);

    private void OpenDockViewPopupFrom(UIElement trigger, bool labelOptions = false)
    {
        // The list of views and tags is always there: with the wheel off and the name never shown, this menu is the only way to change view.
        DockViewMenuPanel.Visibility = Visibility.Visible;
        DockLabelMenuPanel.Visibility = labelOptions ? Visibility.Visible : Visibility.Collapsed;
        // Opened from the fixed label, the menu appears next to the label (the thing that was clicked), not down at the button bar.
        DockViewPopup.PlacementTarget = (ReferenceEquals(trigger, _viewLabelBorder) || ReferenceEquals(trigger, ViewButton)) && trigger is FrameworkElement label ? label : ManageArchiveButton;
        DockViewPopup.Placement = _edge switch
        {
            EdgePosition.Top => PlacementMode.Bottom,
            EdgePosition.Bottom => PlacementMode.Top,
            EdgePosition.Left => PlacementMode.Right,
            _ => PlacementMode.Left
        };
        BuildDockTagChoices();
        _dockViewToggle.SetTrigger(trigger);
        DockViewPopup.IsOpen = true;
    }

    private void BuildDockTagChoices()
    {
        DockTagItems.Children.Clear();
        var tags = _repository.GetAllTags();
        DockNoTagsText.Visibility = tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tag in tags)
        {
            var button = new Button
            {
                Content = tag,
                Tag = tag,
                Style = (Style)FindResource("NoteMenuItemStyle")
            };
            button.Click += OnDockTagChoiceClick;
            DockTagItems.Children.Add(button);
        }
    }

    private void OnDockViewChoiceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !Enum.TryParse<DockViewKind>(value, out var view))
            return;

        _coordinator.SetDockView(view);
        HoldOpenForNextInteraction();
        DockViewPopup.IsOpen = false;
    }

    private void OnDockTagChoiceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        _coordinator.SetDockView(DockViewKind.Tag, tag);
        HoldOpenForNextInteraction();
        DockViewPopup.IsOpen = false;
    }

    /// <summary>
    /// Cuándo se ve el nombre de la vista, elegible desde el mismo menú que cambia de vista (clic derecho o izquierdo en el botón de vistas): así
    /// «nunca» o «solo al cambiar» no obligan a ir a Ajustes, y «nunca» tampoco deja sin salida a quien no ve ya la etiqueta para pulsarla.
    /// </summary>
    private void BuildDockLabelModeChoices()
    {
        DockLabelModeItems.Children.Clear();
        var current = _settings?.DockViewLabel ?? DockViewLabelMode.WhenFiltered;
        foreach (var (mode, text) in new[]
                 {
                     (DockViewLabelMode.Hidden, Strings.ViewLabelHidden),
                     (DockViewLabelMode.WhenChanging, Strings.ViewLabelWhenChanging),
                     (DockViewLabelMode.WhenFiltered, Strings.ViewLabelWhenFiltered),
                     (DockViewLabelMode.Always, Strings.ViewLabelAlways),
                 })
        {
            var button = new Button
            {
                Content = (mode == current ? "✓  " : "    ") + text,
                Tag = mode,
                Style = (Style)FindResource("NoteMenuItemStyle")
            };
            button.Click += OnDockLabelModeClick;
            DockLabelModeItems.Children.Add(button);
        }
    }

    private void OnDockLabelModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DockViewLabelMode mode }) return;
        DockViewPopup.IsOpen = false;
        _coordinator.SetDockViewLabel(mode);
    }

    private void OnDockViewKeepDockToggleClick(object sender, RoutedEventArgs e)
    {
        bool keepOpen = _settings?.KeepDockOpen != true;
        DockViewPopup.IsOpen = false;
        _coordinator.SetKeepDockOpen(keepOpen);

        if (!keepOpen)
        {
            _interactionGraceUntil = DateTime.MinValue;
            _hoverLayoutHold = false;
            PollHoverState();
        }
    }

    /// <summary>
    /// Feedback del botón de sincronizar: mientras corre, la flecha gira y el botón se desactiva (una
    /// segunda sincronización a la vez no aporta nada); al terminar, un ✓ verde o un aviso rojo con el
    /// resultado en el tooltip durante unos segundos. Antes solo había aviso cuando fallaba: un clic
    /// sin respuesta visible no dice si sincronizó de verdad.
    ///
    /// Mientras está ocupado, el sondeo de hover tampoco pliega el dock (ver PollHoverState): la
    /// sincronización puede tardar, y el resultado hay que poder verlo aunque el cursor ya no esté
    /// encima.
    /// </summary>
    private void SetSyncBusy(bool busy)
    {
        _syncBusy = busy;
        SyncButton.IsEnabled = !busy;

        if (SyncButtonGlyph.RenderTransform is RotateTransform spinning)
        {
            spinning.BeginAnimation(RotateTransform.AngleProperty, null);
            SyncButtonGlyph.RenderTransform = null;
        }

        if (!busy) return;

        // A click on the ✓ of the last result starts a new sync: what spins is the arrow, not the tick (the glyph still held the ✓ for the
        // 2.6 s of feedback, so the check mark itself went round).
        _syncFeedbackTimer.Stop();
        ResetSyncGlyph();
        SyncButton.ToolTip = Strings.SyncInProgressStatus;

        var spin = new RotateTransform();
        SyncButtonGlyph.RenderTransform = spin;
        SyncButtonGlyph.RenderTransformOrigin = new Point(0.5, 0.5);
        spin.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            });
    }

    private void ShowSyncResult(bool ok, string tooltip)
    {
        SyncButtonGlyph.Text = ok ? "\uE73E" : "\uE783";
        SyncButtonGlyph.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        SyncButtonGlyph.FontSize = 17;
        SyncButtonGlyph.SetResourceReference(ForegroundProperty, ok ? "AlduneSuccessBrush" : "AlduneDangerTextBrush");
        SyncButton.ToolTip = tooltip;

        // Que el resultado se pueda leer: el dock aguanta otro margen aunque el cursor ya no esté
        // encima, y después decide el sondeo normal.
        _interactionGraceUntil = DateTime.UtcNow + InteractionGrace;

        _syncFeedbackTimer.Stop();
        _syncFeedbackTimer.Start();
    }

    private void ResetSyncGlyph()
    {
        SyncButtonGlyph.Text = "";
        SyncButtonGlyph.FontSize = 17;
        SyncButtonGlyph.ClearValue(TextBlock.ForegroundProperty);
    }

    private void RestoreSyncButton()
    {
        if (_syncBusy) return; // el feedback caducado no debe reactivar un botón ocupado

        ResetSyncGlyph();
        SyncButton.ToolTip = Strings.WithHint(Strings.SyncNowButton, Strings.SyncRightClickHint);
    }

    private async void OnSyncClick(object sender, RoutedEventArgs e)
    {
        SetSyncBusy(true);
        try
        {
            var result = await Task.Run(_coordinator.Synchronize);
            SetSyncBusy(false);

            if (result.Succeeded)
            {
                ShowSyncResult(ok: true, Strings.SyncCompletedStatus(result.Uploaded, result.Downloaded));
                return;
            }

            ShowSyncResult(ok: false, Strings.SyncErrorStatus(result.Error ?? "Unknown error"));

            var message = result.Error?.Contains("401", StringComparison.Ordinal) == true
                ? Strings.SyncUnauthorizedStatus
                : Strings.SyncErrorStatus(result.Error ?? "Unknown error");
            AppDialog.Show(message, Strings.SyncErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            SetSyncBusy(false);
            ShowSyncResult(ok: false, Strings.SyncErrorStatus(ex.Message));
            AppDialog.Show(Strings.SyncErrorStatus(ex.Message), Strings.SyncErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnSyncRightClick(object sender, MouseButtonEventArgs e)
    {
        HoldOpenForWindow(ownWindow: true);
        _coordinator.OpenSettings("Sync");
        e.Handled = true;
    }

    private void OnNewNoteRightClick(object sender, MouseButtonEventArgs e)
    {
        // El descarte de StaysOpen=False ocurre en el mouse-down (con la captura del popup), antes de
        // que este handler vea el mouse-up: un segundo clic derecho no debe reabrirlo.
        if (_newNoteToggle.ShouldConsumeOpen())
        {
            e.Handled = true;
            return;
        }

        NewNoteMenuPopup.PlacementTarget = NewNoteButton;
        NewNoteMenuPopup.Placement = _edge switch
        {
            EdgePosition.Top => PlacementMode.Bottom,
            EdgePosition.Bottom => PlacementMode.Top,
            EdgePosition.Left => PlacementMode.Right,
            _ => PlacementMode.Left
        };
        _newNoteToggle.SetTrigger(NewNoteButton);
        NewNoteMenuPopup.IsOpen = true;
        e.Handled = true;
    }

    private void OnNewNoteMenuPopupOpened(object sender, EventArgs e)
    {
        NewNoteMenuClipboardButton.Content = Strings.NewNoteFromClipboard;
        _coordinator.SuspendNotesAboveDockMenu();
        _openAllMenuTopmostTimer.Start();
        RaiseNewNoteMenu();
    }

    private void OnNewNoteMenuClosed(object sender, EventArgs e)
    {
        _openAllMenuTopmostTimer.Stop();
        _coordinator.RestoreNotesAboveDockMenu();
    }

    private void OnNewNoteMenuCreateClick(object sender, RoutedEventArgs e) =>
        CreateNoteFromMenu(string.Empty);

    private void OnNewNoteMenuClipboardClick(object sender, RoutedEventArgs e)
    {
        var text = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        CreateNoteFromMenu(text);
    }

    private void OnNewNoteMenuOpenFileClick(object sender, RoutedEventArgs e)
    {
        NewNoteMenuPopup.IsOpen = false;
        HoldOpenForWindow();
        _coordinator.ShowOpenLinkedFileDialog(this);
    }

    private void OnNewNoteMenuImportMarkdownClick(object sender, RoutedEventArgs e)
    {
        NewNoteMenuPopup.IsOpen = false;
        HoldOpenForWindow();
        _coordinator.ShowImportMarkdownDialog(this, CurrentTagFilter);
    }

    // Arrastrar archivos desde el Explorador abre cada uno como nota vinculada (spec, decisión 6).
    private void OnDockDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDockDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        e.Handled = true;
        _coordinator.OpenLinkedFiles(files, this);
    }

    private void RaiseNewNoteMenu()
    {
        if (!NewNoteMenuPopup.IsOpen
            || NewNoteMenuPopup.Child is not Visual child
            || PresentationSource.FromVisual(child) is not HwndSource source)
        {
            return;
        }

        NativeMethods.EnsureTopmost(source.Handle);
    }

    private void OnOpenAllClick(object sender, RoutedEventArgs e)
    {
        HoldHoverDuringLayout();
        _coordinator.ToggleAllNotes(this);
        e.Handled = true;
    }

    private void OnOpenAllRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_openAllToggle.ShouldConsumeOpen())
        {
            e.Handled = true;
            return;
        }

        OpenAllMenuPopup.PlacementTarget = OpenAllButton;
        OpenAllMenuPopup.Placement = _edge switch
        {
            EdgePosition.Top => PlacementMode.Bottom,
            EdgePosition.Bottom => PlacementMode.Top,
            EdgePosition.Left => PlacementMode.Right,
            _ => PlacementMode.Left
        };
        _openAllToggle.SetTrigger(OpenAllButton);
        OpenAllMenuPopup.IsOpen = true;
        e.Handled = true;
    }

    private void OnOpenAllNormalClick(object sender, RoutedEventArgs e) =>
        SelectOpenAllLayout(NoteLayoutTemplate.Normal);

    private void OnOpenAllMenuOpened(object sender, EventArgs e)
    {
        if (sender is not Popup menu) return;

        _layoutMonitorKey = _monitorKey;
        PopulateOpenAllMonitors();

        _coordinator.SuspendNotesAboveDockMenu();
        _openAllMenuTopmostTimer.Start();
        RaiseOpenAllMenu();

        // Las notas son ventanas Topmost porque ese es el comportamiento de los post-its. El
        // ContextMenu vive en otro HWND, así que un post-it colocado justo debajo podría pintarse
        // por encima de este selector. Se eleva solo mientras está abierto y no roba el foco.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (menu.IsOpen) RaiseOpenAllMenu();
        }));
    }

    private void OnOpenAllMenuClosed(object sender, EventArgs e)
    {
        _openAllMenuTopmostTimer.Stop();
        _coordinator.RestoreNotesAboveDockMenu();
    }

    private void RaiseOpenAllMenu()
    {
        if (!OpenAllMenuPopup.IsOpen
            || OpenAllMenuPopup.Child is not Visual child
            || PresentationSource.FromVisual(child) is not HwndSource source)
        {
            return;
        }

        NativeMethods.EnsureTopmost(source.Handle);
    }

    private void RaiseDockViewPopup()
    {
        if (!DockViewPopup.IsOpen
            || DockViewPopup.Child is not Visual child
            || PresentationSource.FromVisual(child) is not HwndSource source)
        {
            return;
        }

        NativeMethods.EnsureTopmost(source.Handle);
    }

    private void OnOpenAllGridClick(object sender, RoutedEventArgs e) =>
        SelectOpenAllLayout(NoteLayoutTemplate.Grid);

    private void OnOpenAllColumnsClick(object sender, RoutedEventArgs e) =>
        SelectOpenAllLayout(NoteLayoutTemplate.Columns);

    private void OnCloseAllClick(object sender, RoutedEventArgs e) =>
        CloseOpenAllMenuAfter(_coordinator.CloseAllNoteWindows);

    private void OnCascadeNearDockClick(object sender, RoutedEventArgs e)
    {
        SelectOpenAllLayout(NoteLayoutTemplate.DockCascade);
    }

    /// <summary>
    /// Ofrece elegir a qué pantalla mandar la disposición. Solo aparece cuando hace falta: con un dock
    /// en cada pantalla la elige el propio dock que se pulsa, y en una pantalla sin dock no habría
    /// forma de pedirlo (ver <see cref="AppCoordinator.DockCount"/>). La marca ✓ señala la elegida y el
    /// botón no cierra el menú, para poder elegir pantalla primero y disposición después.
    /// </summary>
    private void PopulateOpenAllMonitors()
    {
        OpenAllMonitorContainer.Children.Clear();

        var monitors = MonitorEnumerator.EnumerateMonitors();
        bool canChoose = monitors.Count > 1 && _coordinator.DockCount < monitors.Count;
        var visibility = canChoose ? Visibility.Visible : Visibility.Collapsed;
        OpenAllMonitorSeparator.Visibility = visibility;
        OpenAllMonitorTitle.Visibility = visibility;
        OpenAllMonitorContainer.Visibility = visibility;

        if (!canChoose)
        {
            // Sin nada que elegir, el destino es la pantalla de este dock y no queda apuntada una
            // pantalla antigua para la próxima acción.
            _layoutMonitorKey = _monitorKey;
            return;
        }

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            string label = Strings.ScreenLabel(
                i + 1, monitor.IsPrimary, (int)monitor.WorkArea.Width, (int)monitor.WorkArea.Height);

            var button = new Button
            {
                Style = (Style)FindResource("NoteMenuItemStyle"),
                Content = monitor.DeviceName == _layoutMonitorKey ? "✓  " + label : label,
                Tag = monitor.DeviceName
            };
            button.Click += OnLayoutMonitorClick;
            OpenAllMonitorContainer.Children.Add(button);
        }
    }

    private void OnLayoutMonitorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string monitorKey }) return;

        _layoutMonitorKey = monitorKey;
        PopulateOpenAllMonitors();
        e.Handled = true;
    }

    private void SelectOpenAllLayout(NoteLayoutTemplate layout)
    {
        _coordinator.SetDefaultNoteLayout(layout);
        _coordinator.OpenAllNotes(this, layout, _layoutMonitorKey);
        OpenAllMenuPopup.IsOpen = false;
    }

    /// <summary>
    /// Si este dock vive en el mismo monitor físico que <paramref name="hMonitor"/>. Lo usa
    /// <c>AppCoordinator</c> para encontrar el dock del monitor donde está el cursor, al abrir una
    /// ventana (Ajustes, el gestor, una nota por atajo) que no tiene "su" monitor propio.
    /// </summary>
    internal bool IsOnMonitor(IntPtr hMonitor) =>
        _hwnd != IntPtr.Zero && NativeMethods.MonitorFromHwnd(_hwnd) == hMonitor;

    /// <summary>
    /// Identificador (<c>MonitorInfo.DeviceName</c>) del monitor de este dock. La posición
    /// recordada de una nota se guarda por pantalla (ver <see cref="AppCoordinator.TryRestorePlacement"/>):
    /// abrirla desde el dock de la pantalla vertical no debe traerla desde donde se dejó en la
    /// horizontal, y viceversa.
    /// </summary>
    internal string MonitorKey => _monitorKey;

    internal WorkingArea WorkArea => _workingArea;

    /// <summary>
    /// Centra <paramref name="window"/> en el monitor de este dock. Una ventana sin posición fijada
    /// acaba en (0,0), o sea siempre en el monitor principal, aunque la hayas abierto desde el otro.
    ///
    /// También acota su alto contra ESTE monitor, no el que tuviera puesto por su cuenta (p. ej.
    /// <c>SettingsWindow</c> se pone un <c>MaxHeight</c> de reserva contra
    /// <c>SystemParameters.WorkArea</c> en su constructor — que en WPF es SIEMPRE el monitor
    /// PRIMARIO del sistema, nunca el monitor real donde la ventana se va a mostrar). Con portátil
    /// + monitor externo, si el externo es el primario, esa reserva se queda corta y la ventana se
    /// sale por abajo en la pantalla pequeña del portátil (reportado por el usuario, 2026-09-11).
    /// Aquí sí se conoce el monitor de verdad, así que se corrige antes de que la ventana se
    /// muestre por primera vez.
    /// </summary>
    /// <summary>
    /// Oculta el dock sin cerrarlo, o lo devuelve. Lo usa el interruptor de la bandeja y su atajo
    /// global para el caso que la detección de pantalla completa no cubre (vídeo a pantalla completa
    /// que Windows no reporta como tal).
    ///
    /// Al ocultar se pliega el abanico y se cierran los popups primero: una ventana oculta con un
    /// menú abierto dejaría el menú flotando solo, y al volver aparecería desplegado sin motivo.
    /// Al mostrar se reafirman la geometría y la capa superior, porque ocultar y mostrar una ventana
    /// puede devolverla por debajo de las notas.
    /// </summary>
    internal void SetUserHidden(bool hidden)
    {
        if (hidden)
        {
            CloseDockPopups();
            _autoScroll?.Stop();
            _hover.Collapsed();
            if (_fanState.IsExpanded)
            {
                _fanState.PointerLeft();
                _fanState.CollapseTimerElapsed();
            }

            Hide();
            return;
        }

        Show();
        ApplyWindowRect();
        if (_hwnd != IntPtr.Zero) NativeMethods.EnsureTopmost(_hwnd);
        Refresh();
    }

    internal void CenterOnThisMonitor(Window window)
    {
        // Alto fijo (Ajustes, Gestionar notas): empieza con holgura (90 %) pero su tope es el área entera, para que Win+flecha pueda ajustarla a
        // media pantalla sin dejar franja. Las que se ajustan al contenido (diálogos) conservan el 90 % como tope.
        bool fixedHeight = !double.IsNaN(window.Height);
        window.MaxHeight = fixedHeight ? _workingArea.Height : _workingArea.Height * 0.9;
        if (fixedHeight) window.Height = Math.Min(window.Height, _workingArea.Height * 0.9);
        // Y el ancho: Ajustes (900 px) en una pantalla pequeña o muy escalada (1280 px a 150 % son 853) se salía por la derecha. Los
        // mínimos bajan con los máximos, para que un mínimo mayor que la pantalla no gane. (Maximizar quita estos topes: MaximizeHook.)
        window.MaxWidth = _workingArea.Width;
        window.MinWidth = Math.Min(window.MinWidth, window.MaxWidth);
        window.MinHeight = Math.Min(window.MinHeight, window.MaxHeight);

        if (window.IsVisible) window.UpdateLayout();

        double width = window.ActualWidth > 0
            ? window.ActualWidth
            : (double.IsNaN(window.Width) ? 0 : window.Width);
        double height = window.ActualHeight > 0
            ? window.ActualHeight
            : (double.IsNaN(window.Height) ? 0 : window.Height);

        if (width <= 0 || height <= 0)
        {
            // SizeToContent puede no haber medido todavía una ventana nueva. Aun así hay que
            // darle una posición perteneciente a este monitor antes de Show(): si se deja Left y
            // Top sin valor, WPF usa el monitor primario para el primer fotograma. Es visible
            // especialmente al abrir Ajustes desde una pantalla horizontal cuando la primaria es
            // vertical. El centrado exacto se repite después de Loaded, cuando ya existe el alto.
            double provisionalWidth = width > 0 ? width : Math.Max(window.MinWidth, 1);
            double provisionalHeight = height > 0 ? height : _workingArea.Height * 0.9;
            window.Left = Math.Clamp(
                _workingArea.X + (_workingArea.Width - provisionalWidth) / 2,
                _workingArea.X,
                Math.Max(_workingArea.X, _workingArea.X + _workingArea.Width - provisionalWidth));
            window.Top = Math.Clamp(
                _workingArea.Y + (_workingArea.Height - provisionalHeight) / 2,
                _workingArea.Y,
                Math.Max(_workingArea.Y, _workingArea.Y + _workingArea.Height - provisionalHeight));
            return;
        }

        window.Left = Math.Clamp(
            _workingArea.X + (_workingArea.Width - width) / 2,
            _workingArea.X,
            Math.Max(_workingArea.X, _workingArea.X + _workingArea.Width - width));
        window.Top = Math.Clamp(
            _workingArea.Y + (_workingArea.Height - height) / 2,
            _workingArea.Y,
            Math.Max(_workingArea.Y, _workingArea.Y + _workingArea.Height - height));
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        // Vengo de un arrastre: el clic es el final de ese gesto, no una petición de abrir la nota.
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        if (sender is FrameworkElement { Tag: Note note } element)
        {
            var screenPositionPixels = element.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(element);
            var tabRect = new System.Windows.Rect(
                screenPositionPixels.X / dpi.DpiScaleX,
                screenPositionPixels.Y / dpi.DpiScaleY,
                element.ActualWidth,
                element.ActualHeight);
            _coordinator.OpenOrActivateNote(note, this, tabRect);
        }
    }

    // --- Reordenar arrastrando ------------------------------------------------------------------
    //
    // Solo se mueve la pestaña arrastrada; las demás no se apartan en vivo. Es a propósito: con el
    // solape del abanico, animar huecos exigiría recolocar todas en cada frame, y el orden real solo
    // se conoce al soltar. Al soltar, la lista se refresca ya ordenada.

    private Button? _dragButton;
    private Note? _dragNote;
    private Point _dragStart;
    private bool _dragging;
    private bool _suppressNextClick;
    private bool _endingDrag;

    private void OnTabDragStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { Tag: Note note } button) return;

        // Dragging only reorders within the note's group (pinned or not): alone in it (the only note, in particular) there is nothing to move,
        // so the tab stays put and the press remains a plain click.
        if ((TabsList.ItemsSource as IEnumerable<Note>)?.Count(n => n.IsPinnedToTop == note.IsPinnedToTop) <= 1) return;

        // No se marca como manejado ni se captura todavía: hasta que no se mueva de verdad, esto
        // tiene que seguir siendo un clic normal que abre la nota.
        _dragButton = button;
        _dragNote = note;
        _dragStart = e.GetPosition(TabsList);
        _dragging = false;
    }

    private void OnTabDragMove(object sender, MouseEventArgs e)
    {
        if (_dragButton is null || e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(TabsList);

        if (!_dragging)
        {
            // El umbral del sistema, no uno inventado: por debajo de eso, para Windows sigue siendo
            // un clic, y la gente mueve el ratón unos píxeles al pulsar sin querer arrastrar nada.
            if (Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance
                && Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance)
            {
                return;
            }

            _dragging = true;
            _dragButton.CaptureMouse();
            SetDragZIndex(_dragButton, 1000); // por encima de las demás mientras viaja
        }

        if (_dragButton.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            translate = new TranslateTransform();
            _dragButton.RenderTransform = translate;
        }
        translate.BeginAnimation(TranslateTransform.YProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        if (IsTopBottomEdge)
        {
            translate.X = position.X - _dragStart.X;
            translate.Y = 0;
        }
        else
        {
            translate.X = 0;
            translate.Y = position.Y - _dragStart.Y;
        }
    }

    private void OnTabDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (_dragButton is null) { ResetDrag(); return; }
        if (!_dragging) { ResetDrag(); return; }

        var note = _dragNote;
        // Cuánto se ha arrastrado, no dónde ha caído: el destino se calcula desde el sitio que
        // ocupaba (ver NoteOrdering.TargetIndex), así que no depende del origen de la lista ni de si
        // el abanico está scrolleado.
        var dropPosition = e.GetPosition(TabsList);
        double delta = IsTopBottomEdge
            ? dropPosition.X - _dragStart.X
            : dropPosition.Y - _dragStart.Y;

        _endingDrag = true;
        _dragButton.ReleaseMouseCapture();
        _endingDrag = false;
        // El Click del botón llega justo después de esto: sin la bandera, soltar tras arrastrar
        // abriría además la nota.
        _suppressNextClick = true;
        ResetDrag();

        if (note is null) return;

        // Solo el grupo de la nota (fijadas o no): arrastrar no cruza la frontera, y las vecinas que
        // se miran para calcular la posición son del mismo grupo.
        var current = (TabsList.ItemsSource as IEnumerable<Note>)?
            .Where(n => n.IsPinnedToTop == note.IsPinnedToTop).Select(n => n.Id).ToList();
        if (current is null || current.Count == 0) return;

        int originalIndex = current.IndexOf(note.Id);
        if (originalIndex < 0) return;

        int target = NoteOrdering.TargetIndex(
            originalIndex,
            delta,
            IsTopBottomEdge
                ? EdgeGeometry.TabWidth + EdgeGeometry.TabGap
                : EdgeGeometry.PitchFor(_workingArea, _edge, _noteCount),
            current.Count);

        if (originalIndex == target) return; // no se arrastró lo suficiente para cambiar de sitio

        _repository.MoveNote(note.Id, target, current);
        _coordinator.RefreshAll();
    }

    /// <summary>
    /// Perder la captura a mitad de arrastre (Alt+Tab, otra ventana que la toma, Esc) cancela el
    /// gesto: la pestaña vuelve a su sitio y el orden no cambia. Sin esto <c>_dragging</c> se quedaba
    /// activo sin nadie que lo terminara, y el dock no volvía a plegarse.
    /// </summary>
    private void OnTabLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_dragging || _endingDrag) return;
        ResetDrag();
    }

    /// <summary>Esc cancela un arrastre en curso. Lo llama el gancho de teclado: el dock nunca tiene el foco.</summary>
    private void CancelDrag()
    {
        if (_dragging) _dragButton?.ReleaseMouseCapture();
    }

    private void ResetDrag()
    {
        if (_dragButton is not null)
        {
            SetDragZIndex(_dragButton, 0);
            if (_dragButton.RenderTransform is TranslateTransform { IsFrozen: false } translate)
            {
                translate.X = 0;
                translate.Y = 0;
            }
        }

        _dragButton = null;
        _dragNote = null;
        _dragging = false;
    }

    /// <summary>
    /// Eleva la pestaña que se arrastra por encima de sus hermanas. El botón vive dentro de un
    /// <see cref="ContentPresenter"/> generado por <see cref="ItemsControl"/>; ese presenter es el
    /// hijo real del <c>StackPanel</c> que decide qué pestaña pinta delante cuando se solapan. El
    /// <c>ZIndex</c> del botón solo ordena hijos dentro de su propia plantilla, así que se fija también
    /// en el contenedor que devuelve el generator y en el presenter del árbol visual.
    /// </summary>
    private void SetDragZIndex(Button button, int zIndex)
    {
        Panel.SetZIndex(button, zIndex);

        if (button.DataContext is not null
            && TabsList.ItemContainerGenerator.ContainerFromItem(button.DataContext) is UIElement itemContainer)
        {
            Panel.SetZIndex(itemContainer, zIndex);
        }

        for (DependencyObject? current = button; current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is ContentPresenter presenter)
            {
                Panel.SetZIndex(presenter, zIndex);
                return;
            }
        }
    }

    /// <summary>
    /// Pestaña de cara oscura: sin el reflejo del lomo ni el borde completo, que están pensados
    /// para caras claras y sobre una oscura parecían plástico o un botón desactivado. En su lugar,
    /// una línea de 1 px arriba con el filo (RimFor, más claro que la cara), como el canto de un
    /// papel grueso. Va después de ApplyLeftEdgeTabShape/ApplyTopBottomTabShape porque pisa el
    /// grosor del borde que fijan ellos. El hover aclara en vez de oscurecer.
    /// </summary>
    private static void ApplyFaceFinish(Button button)
    {
        if (button.DataContext is not Note note) return;
        // Se decide por la cara que de verdad se pinta (NoteFace.Face), no por el color guardado: con
        // Stripe/Tinted la cara es el fondo del chrome o el tinte del canal. Con Filled coincide.
        var face = NoteFace.For(ThemeManager.Skin.Card, note.Color, NoteColorDisplay.Uniform,
            NoteChannelDisplay.Of(note.Id), ThemeManager.Palette);
        if (!NoteColorDerivation.IsDark(face.Face)) return;

        if (button.Template.FindName("CardBorder", button) is Border card)
            card.BorderThickness = new Thickness(0, 1, 0, 0);

        if (button.Template.FindName("SheenBorder", button) is Border sheen)
            sheen.Visibility = Visibility.Collapsed;

        if (button.Template.FindName("HoverOverlay", button) is Border hover)
            hover.Background = (Brush)new BrushConverter().ConvertFromString("#F4F1EC")!;
    }

    private void ApplyTopBottomTabShape(Button button)
    {
        button.HorizontalAlignment = HorizontalAlignment.Center;

        // Arriba y abajo usan tarjetas separadas por un hueco real. Todas conservan sus cuatro
        // esquinas curvas, en vez de tapar la inferior de una tarjeta con la siguiente.
        var radius = ThemeManager.Radius(10);
        var border = new Thickness(1);

        if (button.Template.FindName("CardBorder", button) is Border card)
        {
            card.CornerRadius = radius;
            card.BorderThickness = border;
        }

        if (button.Template.FindName("SheenBorder", button) is Border sheen)
        {
            sheen.CornerRadius = radius;
        }

        if (button.Template.FindName("HoverOverlay", button) is Border hover)
        {
            hover.CornerRadius = radius;
        }

        // Franja horizontal en el lado interior: abajo con el dock arriba, arriba con el dock abajo.
        if (button.Template.FindName("StripeBar", button) is Border stripe)
        {
            stripe.BorderThickness = _edge == EdgePosition.Top ? new Thickness(0, 0, 0, 3) : new Thickness(0, 3, 0, 0);
        }

        if (button.Template.FindName("CardShadowLayer", button) is Border shadowLayer)
        {
            shadowLayer.Effect = (Effect)FindResource(_edge == EdgePosition.Top
                ? "CardShadowTop"
                : "CardShadowBottom");
        }
    }

    // --- Menú contextual de una pestaña ---------------------------------------------------------

    private Note? _tabMenuNote;
    private FrameworkElement? _tabMenuOwner;

    private void OnTabRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Note note }) return;
        if (_tabMenuToggle.ShouldConsumeOpen())
        {
            _tabMenuNote = null;
            e.Handled = true;
            return;
        }

        _tabMenuOwner = (FrameworkElement)sender;
        _tabMenuNote = note;
        TabMenuArchiveButton.Content = note.State == NoteState.Active ? Strings.Archive : Strings.Restore;
        TabMenuTrashButton.Content = note.State == NoteState.Trashed ? Strings.Restore : Strings.MoveToTrash;
        TabMenuPinToTopButton.Content = note.IsPinnedToTop ? Strings.UnpinFromTop : Strings.PinToTop;
        TabMenuPinToTopButton.Visibility = note.State == NoteState.Active ? Visibility.Visible : Visibility.Collapsed;
        TabMenuDoneTasksButton.Content = Strings.DoneTasksPolicyLabel(note.CompletedTasks);
        TabMenuDoneTasksButton.Visibility = note.State == NoteState.Active && !note.IsProtected ? Visibility.Visible : Visibility.Collapsed;
        TabMenuProtectionButton.Content = note.IsProtected ? Strings.RemoveProtection : Strings.ProtectNote;
        TabMenuVisibleNameButton.Visibility = note.IsProtected ? Visibility.Visible : Visibility.Collapsed;
        // Una nota vinculada es el archivo en claro: no se protege (spec, decisión 7).
        bool linked = LinkedNoteDisplay.IsLinked(note.Id);
        TabMenuProtectionButton.IsEnabled = !linked;
        TabMenuProtectionButton.ToolTip = linked ? Strings.LinkedProtectDisabled : null;
        // El archivo de una nota vinculada, a un clic desde la pestaña (sin abrir la nota).
        TabMenuCopyPathButton.Visibility = TabMenuShowInExplorerButton.Visibility = TabMenuOpenInEditorButton.Visibility =
            linked ? Visibility.Visible : Visibility.Collapsed;
        BuildTabMenuSwatches(note);
        _tabMenuToggle.SetTrigger(_tabMenuOwner);
        TabMenuPopup.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// Las pastillas del tema activo, con la de la nota marcada. Se reconstruyen en cada apertura
    /// porque el menú sirve a la pestaña que se acaba de pulsar, no a una fija.
    /// </summary>
    private void BuildTabMenuSwatches(Note note) =>
        NoteSwatchPanel.Fill(TabMenuSwatches, _coordinator.ActiveTheme, note.Color, OnTabMenuColorClick);

    private void OnTabMenuColorClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;

        var color = (string)((FrameworkElement)sender).Tag;
        // The swatch of the colour in use is also a way to pick another one (the same picker as «Choose color…»).
        if (color == note.Color) { OnTabMenuCustomColorClick(sender, e); return; }
        if (color != note.Color)
        {
            _repository.SetColor(note.Id, color);
            _coordinator.NotifyNoteColorChanged(note.Id, color);
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    private void OnTabMenuCustomColorClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;

        HoldOpenForWindow();
        var color = CustomColorWindow.Show(this, note.Color);
        if (color is null) return;

        _repository.SetColor(note.Id, color);
        _coordinator.NotifyNoteColorChanged(note.Id, color);
        _coordinator.RefreshAll();
        CloseTabMenu();
    }

    private void OnTabMenuCopyPathClick(object sender, RoutedEventArgs e) => LinkedFileFromTabMenu(LinkedFileActions.CopyPath);

    private void OnTabMenuShowInExplorerClick(object sender, RoutedEventArgs e) => LinkedFileFromTabMenu(LinkedFileActions.ShowInExplorer);

    private void OnTabMenuOpenInEditorClick(object sender, RoutedEventArgs e) => LinkedFileFromTabMenu(LinkedFileActions.OpenInEditor);

    private void LinkedFileFromTabMenu(Action<string> action)
    {
        var path = _tabMenuNote is { } note ? LinkedNoteDisplay.PathOf(note.Id) : null;
        CloseTabMenu();
        if (path is not null) action(path);
    }

    private void OnTabMenuOpenClick(object sender, RoutedEventArgs e)
    {
        var note = _tabMenuNote;
        CloseTabMenu();
        if (note is not null) _coordinator.OpenOrActivateNote(note, this);
    }

    private void OnTabMenuProtectionClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;
        CloseTabMenu();
        HoldOpenForWindow();

        if (note.IsProtected)
        {
            // Quitar la protección ocurre dentro del diálogo: con la contraseña mal se queda abierto.
            var password = PasswordPromptWindow.Show(this, PasswordPromptMode.RemoveProtection,
                candidate => _repository.RemoveProtection(note.Id, candidate));
            if (password is null) return;
        }
        else
        {
            var password = PasswordPromptWindow.ShowProtect(this, out var visibleName);
            if (password is null) return;
            _repository.Protect(note.Id, password);
            if (visibleName is not null) _repository.SetProtectedTitle(note.Id, visibleName);
        }

        _coordinator.RefreshAll();
    }

    private void OnTabMenuVisibleNameClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;
        CloseTabMenu();
        HoldOpenForWindow();

        if (!PasswordPromptWindow.ShowVisibleName(this, note.ProtectedTitle, out var visibleName)) return;
        _repository.SetProtectedTitle(note.Id, visibleName);
        _coordinator.RefreshAll();
    }

    private void OnTabMenuPinToTopClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is { } note)
        {
            _repository.SetPinnedToTop(note.Id, !note.IsPinnedToTop);
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    // Cicla la política y deja el menú abierto con el texto nuevo, igual que el menú de la propia nota: son tres estados y se ve el cambio.
    private void OnTabMenuDoneTasksClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;
        note.CompletedTasks = CompletedTasksPolicyRules.Next(note.CompletedTasks);
        _repository.SetCompletedTasksPolicy(note.Id, note.CompletedTasks);
        TabMenuDoneTasksButton.Content = Strings.DoneTasksPolicyLabel(note.CompletedTasks);
        _coordinator.RefreshAll();
    }

    private void OnTabMenuArchiveClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is { } note)
        {
            _repository.SetState(note.Id,
                note.State == NoteState.Active ? NoteState.Archived : NoteState.Active);
            if (note.State == NoteState.Active)
            {
                _coordinator.NotifyLinkedFilesKept([note.Id], AppCoordinator.LinkedLeave.Archived);
                _coordinator.PlaySound(SoundEvent.NoteArchived);
            }
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    private void OnTabMenuTagsClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;

        DockTagPanel.Bind(_repository, note);

        // El panel tiene un campo para escribir etiquetas nuevas, y el dock no se deja activar
        // (WS_EX_NOACTIVATE, para no robar el foco al pasar el ratón): sin esto, lo que se tecleaba
        // iba a la aplicación que estuviera delante. Se permite solo mientras el panel está abierto.
        NativeMethods.AllowActivation(_hwnd);
        NativeMethods.ForceActivate(this);

        TagEditorPopup.PlacementTarget = _tabMenuOwner;
        TagEditorPopup.Placement = PlacementMode.Bottom;
        TagEditorPopup.IsOpen = true;
        TabMenuPopup.IsOpen = false;
        TagEditorPopup.Focus();
    }

    /// <summary>El panel guarda al marcar; el dock se refresca una sola vez, al cerrarlo: hacerlo con
    /// el popup abierto regeneraba la pestaña a la que está anclado.</summary>
    private void OnTagEditorClosed(object? sender, EventArgs e)
    {
        if (DockTagPanel.Changed) _coordinator.RefreshAll();
        _tabMenuNote = null;
        NativeMethods.MakeNonActivating(_hwnd);
    }

    private void OnTabMenuTrashClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is { } note)
        {
            var toTrash = note.State != NoteState.Trashed;
            _repository.SetState(note.Id, toTrash ? NoteState.Trashed : NoteState.Active);
            if (toTrash)
            {
                _coordinator.NotifyLinkedFilesKept([note.Id], AppCoordinator.LinkedLeave.Trashed);
                _coordinator.PlaySound(SoundEvent.NoteDeleted);
            }
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    private void CloseTabMenu()
    {
        TabMenuPopup.IsOpen = false;
        _tabMenuNote = null;
        _tabMenuOwner = null;
    }

    private IEnumerable<Popup> DockPopups => PopupsWithTriggers().Select(p => p.Popup);

    // --- Cerrar los menús al clicar fuera ---------------------------------------------------------
    //
    // Un Popup con StaysOpen=False se cierra cuando pierde la captura del ratón. El dock nunca toma el
    // foco (WS_EX_NOACTIVATE), así que clicar en la aplicación que ya estaba activa —lo normal: la que
    // se usaba antes de ir al dock— no cambia nada para Windows y el menú se quedaba abierto. Medido
    // con clics físicos: se cerraba al clicar en Aldune o en una aplicación inactiva, y nunca en la
    // activa. Mientras hay un menú abierto, un gancho global mira (sin comérselos) los clics y cierra
    // los menús si el clic cae fuera de ellos.

    private void WatchClicksOutside()
    {
        if (_mouseHook == IntPtr.Zero) _mouseHook = NativeMethods.InstallMouseHook(_mouseHookProc);
    }

    private void StopWatchingClicksOutside()
    {
        NativeMethods.UninstallHook(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    private IntPtr OnGlobalMouseHook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && NativeMethods.IsMouseButtonDown(message))
        {
            var point = NativeMethods.MouseHookPoint(data);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => CloseDockPopupsIfOutside(point)));
        }

        return NativeMethods.ContinueHook(_mouseHook, code, message, data);
    }

    /// <summary>
    /// Cierra los menús si el clic cae fuera de todos ellos, también sobre el propio dock (medido: con
    /// un menú abierto, clicar otra pestaña no hacía nada). La excepción es el disparador del menú
    /// abierto: volver a pulsarlo lo cierra por su cuenta (<see cref="PopupToggle"/>), y cerrarlo aquí
    /// antes cambiaría ese gesto.
    /// </summary>
    private void CloseDockPopupsIfOutside(Point screenPixels)
    {
        foreach (var (popup, trigger) in PopupsWithTriggers())
        {
            if (!popup.IsOpen) continue;
            if (popup.Child is FrameworkElement child && ContainsScreenPixel(child, screenPixels)) return;
            if (trigger is not null && ContainsScreenPixel(trigger, screenPixels)) return;
        }

        CloseDockPopups();
    }

    private IEnumerable<(Popup Popup, FrameworkElement? Trigger)> PopupsWithTriggers()
    {
        yield return (TabMenuPopup, _tabMenuOwner);
        yield return (OpenAllMenuPopup, OpenAllButton);
        yield return (DockViewPopup, ManageArchiveButton);
        yield return (NewNoteMenuPopup, NewNoteButton);
        yield return (TagEditorPopup, null);
    }

    private static bool ContainsScreenPixel(FrameworkElement element, Point screenPixels)
    {
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null) return false;
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new System.Windows.Rect(topLeft, bottomRight).Contains(screenPixels);
    }

    private void CloseDockPopups()
    {
        _openAllMenuTopmostTimer.Stop();
        if (NewNoteMenuPopup.IsOpen) NewNoteMenuPopup.IsOpen = false;
        if (OpenAllMenuPopup.IsOpen) OpenAllMenuPopup.IsOpen = false;
        if (DockViewPopup.IsOpen) DockViewPopup.IsOpen = false;
        if (TagEditorPopup.IsOpen) TagEditorPopup.IsOpen = false;
        if (TabMenuPopup.IsOpen || _tabMenuNote is not null) CloseTabMenu();
    }

    /// <summary>
    /// A rebuilt strip (a sync that changed what a tab draws) creates every tab visible, and <see cref="RefreshOpenState"/> only hides the ones whose
    /// note is open at Loaded priority, after a frame has been drawn: the open note's tab flashed in the dock. The first layout pass runs before
    /// the first render (DataContextChanged is not raised for the first context of a template), so the tab is hidden from its very first frame.
    /// </summary>
    private void OnTabFirstLayout(object sender, SizeChangedEventArgs e)
    {
        if (e.PreviousSize.Width == 0 && sender is Button { DataContext: Note note } button && IsOnDesk(note))
            button.Visibility = Visibility.Hidden;
    }

    private bool IsOnDesk(Note note) =>
        _coordinator.IsNoteOpen(note.Id) && !_coordinator.IsNoteMinimized(note.Id) && !_coordinator.IsNoteClosing(note.Id);

    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        int index = TabsList.Items.IndexOf(button.DataContext);
        if (index < 0) return;

        _tabButtons[index] = button;

        // Mismo espejado que ApplyEdgeAlignment, pero por botón: el XAML declara cada pestaña
        // alineada "a la derecha" (correcto para EdgePosition.Right) y ItemsControl las regenera
        // enteras en cada SetNotes, así que no basta con corregirlo una vez en el constructor.
        if (_edge == EdgePosition.Left) ApplyLeftEdgeTabShape(button);
        else if (IsTopBottomEdge) ApplyTopBottomTabShape(button);
        ApplyFaceFinish(button);
        // Sombra apagada en Ajustes: la capa de sombra se queda sin efecto y la tarjeta queda plana.
        if (_settings is { DockCardShadow: false } && button.Template.FindName("CardShadowLayer", button) is Border flat)
            flat.Effect = null;

        // La última no lleva el margen del solape. En esta disposición el margen es inferior y no
        // hace falta después de la última tarjeta porque no hay otra que solapar.
        bool isLast = index == _noteCount - 1;
        button.Margin = isLast
            ? new Thickness(0)
            : IsTopBottomEdge
                ? new Thickness(0, 0, 0, EdgeGeometry.TabGap)
                : _tabMargin;

        button.Opacity = _fanState.IsExpanded ? 1 : 0;
        ApplyPreviewVisibility(button);
        ApplyReminderBadge(button);
    }

    /// <summary>
    /// Coloca la ventana de una nota recién abierta que no tiene posición recordada (ver
    /// <see cref="AppCoordinator.TryRestorePlacement"/>): la primera sale siempre en el mismo sitio, junto al
    /// dock y centrada en la pantalla, y cada nota que se abre con otras ya abiertas ocupa el siguiente escalón
    /// de la misma cascada que "Cascada junto al dock" (<see cref="NoteCascade"/>). Antes salía alineada con su
    /// pestaña, así que el sitio dependía de qué pestaña se pulsara.
    /// </summary>
    internal void PositionNoteWindow(NoteWindow noteWindow)
    {
        var (left, top) = NoteCascade.Position(
            _workingArea, _edge, Math.Max(1, _noteCount), noteWindow.Width, noteWindow.Height,
            _coordinator.OpenNoteWindowCount, Alignment);

        noteWindow.Left = Math.Clamp(
            left, _workingArea.X, Math.Max(_workingArea.X, _workingArea.X + _workingArea.Width - noteWindow.Width));
        noteWindow.Top = Math.Clamp(
            top, _workingArea.Y, Math.Max(_workingArea.Y, _workingArea.Y + _workingArea.Height - noteWindow.Height));
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        CreateNote(string.Empty);
    }

    private void CreateNoteFromMenu(string content)
    {
        NewNoteMenuPopup.IsOpen = false;
        CreateNote(content);
    }

    private void CloseOpenAllMenuAfter(Action action)
    {
        OpenAllMenuPopup.IsOpen = false;
        action();
    }

    private void CreateNote(string content)
    {
        HoldHoverDuringLayout();
        var note = _repository.Create(content, _coordinator.NextNoteColor(), screenOrigin: "primary");
        _coordinator.PlaySound(SoundEvent.NoteCreated);

        // En la vista de una etiqueta la nota nace ya con ella: si no, el filtro la escondería nada
        // más crearla y habría que ir a buscarla a "todas" para etiquetarla a mano.
        if (CurrentTagFilter is { } tag) _repository.SetTags(note.Id, new[] { tag });

        _coordinator.RefreshAll();
        HoldOpenForNextInteraction();
    }

    /// <summary>
    /// Mantiene el dock desplegado un margen (<see cref="InteractionGrace"/>) tras una acción que
    /// invita a otra consecutiva — cambiar de vista o de etiqueta es ponerse a buscar una nota, no
    /// cerrar el dock. Mientras el selector está abierto el sondeo no decide nada, pero al cerrarse
    /// ve el cursor fuera de la zona sensible, que además acaba de cambiar de tamaño con el nuevo
    /// recuento de notas, y lo pliega justo cuando el usuario va a usarlo.
    ///
    /// El margen caduca solo: pasado, manda la comprobación normal de dentro/fuera, y si el cursor
    /// volvió a pasar por encima tampoco cambia nada. No es el ajuste "mantener el dock abierto"
    /// (<c>AppSettings.KeepDockOpen</c>): esto es una cortesía puntual, no un modo permanente.
    /// </summary>
    private void HoldOpenForNextInteraction(bool grace = true)
    {
        // The wheel changes the view with the cursor on the dock: there is nothing to wait for, and the 4 s grace made the dock stay open that long
        // after the cursor left. Without it the usual delay applies (the same as after opening the dock and leaving).
        if (grace) _interactionGraceUntil = DateTime.UtcNow + InteractionGrace;
        _hoverLayoutHold = false;
        _hover.Hold();
        if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
    }

    /// <summary>
    /// Margen de cortesía al abrir otra ventana desde este dock. Fija el estado de hover para que el
    /// sondeo no lo cierre bajo los pies mientras la nueva ventana roba el foco.
    /// </summary>
    private void HoldOpenForWindow(bool ownWindow = false)
    {
        _hoverGraceUntil = DateTime.UtcNow.Add(ownWindow ? OwnWindowGrace : HoverOpenGrace);
        // A window the dock opens for the work (the notes manager, Settings): when the cursor goes over to it, the dock folds sooner than usual.
        if (ownWindow) _hover.OwnWindowOpened();
        _hover.Hold();
        if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
    }

    private void HoldHoverDuringLayout()
    {
        if (!_hover.IsPointerInside && (_hwnd == IntPtr.Zero || !NativeMethods.IsCursorOverWindow(_hwnd))) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursor = NativeMethods.GetCursorScreenPosition();
        _hoverLayoutAnchorX = cursor.X / dpi.DpiScaleX;
        _hoverLayoutAnchorY = cursor.Y / dpi.DpiScaleY;
        _hoverLayoutHold = true;
        _hover.Hold();
    }

    /// <summary>
    /// Para todo lo que este dock tiene en marcha antes de cerrarlo, al reconstruir por un cambio
    /// de pantallas. Sin esto sus timers seguirían vivos sobre una ventana ya cerrada.
    /// </summary>
    internal void PrepareForClose()
    {
        CloseDockPopups();
        _hoverLayoutHold = false;
        _hoverPollTimer.Stop();
        _wheelSessionTimer.Stop();
        UninstallWheelHook();
        _viewLabelTimer.Stop();
        if (_viewLabelPopup is not null) _viewLabelPopup.IsOpen = false;
        _fullscreenPollTimer.Stop();
        _arrowScrollTimer.Stop();
        _syncFeedbackTimer.Stop();
        StopSmoothScroll();
        _openAllMenuTopmostTimer.Stop();
        NativeMethods.UninstallHook(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
        StopWatchingClicksOutside();
    }
}

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
    private readonly DispatcherTimer _collapseTimer;
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
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly string _monitorKey;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly AppSettings? _settings;
    private int _noteCount;
    private DockViewKind? _lastView;
    private string? _lastTagFilter;

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
    /// pies y, con <c>_hoverReentryBlocked</c> armado, deja de responder hasta sacar el ratón lejos.
    /// </summary>
    private static readonly TimeSpan HoverOpenGrace = TimeSpan.FromSeconds(3);

    /// <summary>Hasta cuándo vale <see cref="HoverOpenGrace"/>; <c>MinValue</c> si no hay ninguno.</summary>
    private DateTime _hoverGraceUntil = DateTime.MinValue;

    private bool _pointerInside;
    private bool _hoverReentryBlocked;
    private bool _hoverLayoutHold;
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

    private const double NoteWindowCascadeStep = 26;
    private const int NoteWindowMaxCascadeSteps = 6;

    // Antes esta distancia era cero a propósito: la nota tenía que arrancar pegada a su pestaña
    // para venderse como "deslizarse hacia fuera de ahí". Sin esa animación (ver
    // NoteWindow.PlayOpenAnimation), quedarse pegada al canto del dock ya no vende nada — solo se
    // ve encimada con el panel desplegado. 24px de aire, igual que el margen que ya usa el resto
    // de la ventana de la nota.
    private const double NoteWindowGapFromDock = 24;

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
        _workingArea = monitor.WorkArea;
        _monitorKey = monitor.DeviceName;
        _repository = repository;
        _coordinator = coordinator;
        _settings = settings;
        _keyboardHookProc = OnGlobalKeyboardHook;

        TabsScroll.ScrollChanged += OnTabsScrollChanged;
        TabsScroll.SizeChanged += (_, _) => QueueScrollIndicatorUpdate();
        ScrollOverlay.SizeChanged += (_, _) => QueueScrollIndicatorUpdate();

        _collapseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(IsTopBottomEdge ? 60 : 90)
        };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            _fanState.CollapseTimerElapsed();
        };

        _fanState.ExpansionChanged += (_, _) => ApplyState(animate: true);

        // Deliberadamente no MouseEnter/MouseLeave de WPF: sondear la posición real del cursor no
        // depende de que Win32 acierte con su seguimiento, y la ventana es mayormente transparente,
        // así que sus propios eventos de ratón no coinciden con lo que se ve. Ver docs/STATUS.md.
        _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _hoverPollTimer.Tick += (_, _) => PollHoverState();
        _hoverPollTimer.Start();

        // Aparte y mucho más lento: pasar a pantalla completa no hay que detectarlo en 50ms, y
        // quien tiene un juego delante agradece que no le sondeen el primer plano 20 veces/s.
        _fullscreenPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _fullscreenPollTimer.Tick += (_, _) => PollFullscreenApp();
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

        _autoScroll = new AutoScrollManager(NotesColumn, TabsScroll, null);
        _newNoteToggle = new PopupToggle(NewNoteMenuPopup);
        _openAllToggle = new PopupToggle(OpenAllMenuPopup);
        _dockViewToggle = new PopupToggle(DockViewPopup);
        _tabMenuToggle = new PopupToggle(TabMenuPopup);
        Closed += (_, _) => _autoScroll.Stop();

        ApplyWindowRect();
        ApplyState(animate: false);
    }

    /// <summary>
    /// Ajusta el layout interno al borde físico. Derecha/izquierda usan una columna vertical;
    /// arriba/abajo mantienen una tira horizontal en reposo, pero despliegan las tarjetas en una
    /// columna vertical ancha. El HWND ya tiene el rectángulo correcto gracias a
    /// <see cref="EdgeGeometry.WindowRect"/>; aquí solo se cambia la disposición interna.
    /// </summary>
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

        FooterBorder.HorizontalAlignment = HorizontalAlignment.Left;
        FooterBorder.Margin = new Thickness(FooterBorder.Margin.Right, FooterBorder.Margin.Top, 0, 0);

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
        ScrollArrowOverlay.Margin = new Thickness(0);
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
        FooterBorder.Padding = new Thickness(6);
        FooterBorder.Margin = top
            ? new Thickness(0, 0, 0, 8)
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
    }

    private static CornerRadius Mirror(CornerRadius r) => new(r.TopRight, r.TopLeft, r.BottomLeft, r.BottomRight);

    private static Thickness MirrorHorizontal(Thickness t) => new(t.Right, t.Top, t.Left, t.Bottom);

    /// <summary>
    /// Fija el rectángulo de la ventana. Se llama al construir y cuando cambia el número de notas
    /// (que cambia su longitud), nunca como parte de una animación.
    /// </summary>
    private void ApplyWindowRect()
    {
        var rect = EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount);
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
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
            _pointerInside = true;
            _hoverReentryBlocked = false;
            _collapseTimer.Stop();
            if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
            return;
        }

        // Con el menú de una pestaña abierto, el abanico se queda como está: el menú sale fuera de
        // la zona sensible del dock, así que mover el ratón hacia él contaría como salir y lo
        // cerraría justo cuando el usuario va a pulsarlo.
        if (TabMenuPopup.IsOpen
            || OpenAllMenuPopup.IsOpen
            || DockViewPopup.IsOpen
            || NewNoteMenuPopup.IsOpen
            || TagEditorPopup.IsOpen
            || _autoScroll is { IsActive: true })
            return;

        // Lo mismo durante el margen de cortesía de una acción recién hecha (ver
        // HoldOpenForNextInteraction), y mientras haya una sincronización en curso (ver
        // SetSyncBusy): ni sondeo ni plegado hasta que termine. _pointerInside se mantiene en true
        // a propósito: al terminar, la rama de salida de más abajo es la que decide.
        if (_syncBusy || DateTime.UtcNow < _interactionGraceUntil)
        {
            _pointerInside = true;
            _hoverReentryBlocked = false;
            _collapseTimer.Stop();
            if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
            return;
        }

        // Lo mismo mientras se arrastra una pestaña: el gesto puede salirse de la zona sensible, y
        // colapsar el abanico a mitad de arrastre dejaría la nota en el aire.
        if (_dragging) return;

        // Ignora que el cursor pase por encima mientras se arrastra otra cosa que comparta el mismo
        // borde de pantalla (p. ej. la barra de scroll de un navegador).
        if (NativeMethods.IsLeftButtonDown()) return;

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
                _pointerInside = true;
                _collapseTimer.Stop();
                return;
            }
        }

        // Cortesía al abrir otra ventana desde el dock: mientras corre, el sondeo no decide nada y
        // el abanico se queda abierto aunque el cursor esté fuera. En cuanto caduca, el sondeo
        // normal vuelve a mandar y, si el cursor sigue fuera, lo pliega como siempre.
        if (DateTime.UtcNow < _hoverGraceUntil)
        {
            _pointerInside = true;
            _collapseTimer.Stop();
            return;
        }

        var windowRect = EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount);
        var restingRect = EdgeGeometry.RestingVisibleRect(_workingArea, _edge, _noteCount);

        // Contra la zona realmente visible, no contra la ventana entera: casi toda es transparente,
        // y desplegarse al entrar ahí sería desplegarse por pasar el ratón sobre nada.
        // Sin notas la ventana entera es zona sensible: no hay tira que sobrevolar, y los botones
        // tienen que poder pulsarse sin desplegar nada primero.
        bool isInside = _noteCount == 0
            ? Contains(windowRect, cursorX, cursorY)
            : _fanState.IsExpanded
                ? IsInsideExpandedSurface(cursorX, cursorY)
                : Contains(restingRect, cursorX, cursorY);

        // Al salir del abanico, el HWND transparente sigue cubriendo la zona donde estaban las
        // tarjetas. Si el cursor se queda quieto ahí no debe volver a interpretarse como una nueva
        // entrada cuando el abanico termina de ocultarse. Exigimos cruzar el límite completo de la
        // ventana antes de armar otra entrada; así el usuario puede volver a abrirlo moviéndose fuera
        // y regresando a la tira de reposo, sin ciclos de apertura/cierre bajo el cursor.
        if (_hoverReentryBlocked)
        {
            if (Contains(windowRect, cursorX, cursorY) && !Contains(restingRect, cursorX, cursorY)) return;

            _hoverReentryBlocked = false;
        }

        if (isInside && !_pointerInside)
        {
            _pointerInside = true;
            _fanState.PointerEntered();
            NativeMethods.EnsureTopmost(_hwnd);
        }
        else if (!isInside && _pointerInside)
        {
            _pointerInside = false;
            _hoverReentryBlocked = true;
            _fanState.PointerLeft();
            _collapseTimer.Start();
        }
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
            _pointerInside = false;
            _collapseTimer.Stop();
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

    private static readonly Duration SideCrossfadeDuration = new(TimeSpan.FromMilliseconds(150));
    private static readonly Duration TopBottomCrossfadeDuration = new(TimeSpan.FromMilliseconds(120));

    private static IEasingFunction EaseOut() =>
        new QuinticEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// Cruza entre la tira de reposo y el abanico. Sin región que mantener sincronizada, esto es
    /// una animación WPF normal sobre dos capas superpuestas.
    /// </summary>
    private void ApplyState(bool animate)
    {
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
        var animation = new DoubleAnimation(to, duration) { EasingFunction = EaseOut() };
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
    private void PlayEntrance(Button button, double delayMs, bool slideIn)
    {
        var delay = TimeSpan.FromMilliseconds(delayMs);
        var duration = new Duration(TimeSpan.FromMilliseconds(FanTiming.TabSweepMs));

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

    public void Refresh()
    {
        var view = _settings?.DockView ?? DockViewKind.Active;
        var tagFilter = _settings?.DockTagFilter;
        bool viewChanged = _lastView != view
            || !string.Equals(_lastTagFilter, tagFilter, StringComparison.OrdinalIgnoreCase);
        _lastView = view;
        _lastTagFilter = tagFilter;
        var notes = view switch
        {
            DockViewKind.Archived => _repository.GetByState(NoteState.Archived),
            DockViewKind.Trashed => _repository.GetByState(NoteState.Trashed),
            DockViewKind.Tag when !string.IsNullOrWhiteSpace(_settings?.DockTagFilter) =>
                _repository.GetByTag(_settings!.DockTagFilter!, NoteState.Active),
            _ => _repository.GetByState(NoteState.Active)
        };
        SetNotes(notes, animateArrivals: !viewChanged);
        if (viewChanged)
        {
            HoldHoverDuringLayout();
            UpdateTagAwareTooltips();
        }
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
        NewNoteButton.ToolTip = tag is null ? Strings.NewNoteTooltip : Strings.NewNoteTaggedTooltip(tag);
        ManageArchiveButton.ToolTip = tag is null
            ? Strings.ManageNotesTooltip
            : Strings.ManageNotesTaggedTooltip(tag);
        OpenAllButton.ToolTip = tag is null
            ? Strings.OpenAllNotesTooltip
            : Strings.OpenAllNotesTaggedTooltip(tag);
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
            bool onDesk = button.Tag is Note note
                && _coordinator.IsNoteOpen(note.Id)
                && !_coordinator.IsNoteMinimized(note.Id);
            // Hidden y no Collapsed: conserva su hueco, y el mazo enseña el sitio vacío de donde se
            // sacó la ficha.
            button.Visibility = onDesk ? Visibility.Hidden : Visibility.Visible;
        }
    }

    public void SetNotes(IReadOnlyList<Note> notes, bool animateArrivals = true)
    {
        var previousIds = _knownNoteIds;
        _knownNoteIds = notes.Select(n => n.Id).ToHashSet();
        _pendingReminders = _repository.GetPendingReminders();

        _tabButtons.Clear();
        TabsScroll.ScrollToHome();
        TabsList.ItemsSource = notes;

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

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            RefreshOpenState();
            UpdateScrollIndicator();

            if (!_fanState.IsExpanded)
            {
                ResetTabEntrance(settled: false);
                return;
            }

            foreach (var button in _tabButtons.Values)
            {
                if (button.Tag is Note note && arrived.Contains(note.Id))
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

        // La rueda de un ratón llega en muescas de 120; un trackpad de precisión (gesto de dos dedos)
        // manda muchos eventos pequeños y seguidos. Con el paso fijo de una muesca por evento, un
        // solo gesto de trackpad saltaría media lista: con el ajuste de gestos de trackpad encendido
        // el paso pasa a ser proporcional al delta, y un ratón sigue comportándose igual (sus deltas
        // son muescas completas). Ver AppSettings.TrackpadGestures.
        if (_settings is { TrackpadGestures: true } && !IsFullWheelNotch(e.Delta))
        {
            TabsScroll.ScrollToVerticalOffset(
                TabsScroll.VerticalOffset - e.Delta / 120.0 * ArrowScrollStep);
            e.Handled = true;
            return;
        }

        ScrollByArrow(e.Delta < 0 ? 1 : -1);
        e.Handled = true;
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

    private void OnDockPreviewKeyDown(object sender, KeyEventArgs e)
    {
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
        if (code >= 0
            && _fanState.IsExpanded
            && _pointerInside
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

        return NativeMethods.ContinueKeyboardHook(_keyboardHook, code, message, data);
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

    private bool IsInsideExpandedSurface(double cursorX, double cursorY)
    {
        // La entrada empieza en una tira horizontal, pero las tarjetas de arriba/abajo crecen hacia
        // dentro y no ocupan necesariamente la misma coordenada X que el cursor. El rectángulo del
        // HWND es el corredor natural del gesto: mantiene abierto el dock mientras el cursor recorre
        // el espacio entre el borde y las tarjetas. _hoverReentryBlocked impide que ese mismo espacio
        // vuelva a abrirlo después de que ya se haya ocultado.
        // FanPanel incluye el corredor entre las notas y la botonera. Al crear una nota o al
        // recorrer la lista, el cursor puede cruzar ese hueco sin estar sobre una tarjeta concreta;
        // sigue siendo parte del gesto del dock y no debe iniciar el cierre.
        if (ContainsElementSurface(FanPanel, cursorX, cursorY, padding: 0))
            return true;

        if (ContainsElementSurface(FooterBorder, cursorX, cursorY, padding: 5))
            return true;

        if (ScrollArrowOverlay.Visibility == Visibility.Visible
            && ContainsElementSurface(ScrollArrowOverlay, cursorX, cursorY, padding: 5))
            return true;

        if (ScrollOverlay.Visibility == Visibility.Visible
            && ContainsElementSurface(ScrollOverlay, cursorX, cursorY, padding: 5))
            return true;

        // La tira sigue siendo la puerta de entrada del dock. Aunque su capa visual se oculte al
        // desplegarse, mantenerla dentro de la superficie sensible evita iniciar un cierre cuando
        // el usuario vuelve hacia el borde con un movimiento corto.
        if (Contains(EdgeGeometry.RestingVisibleRect(_workingArea, _edge, _noteCount), cursorX, cursorY))
            return true;

        if (!ContainsElementSurface(TabsScroll, cursorX, cursorY, padding: 0))
            return false;

        foreach (var button in _tabButtons.Values)
        {
            if (ContainsElementSurface(button, cursorX, cursorY, padding: 5))
                return true;
        }

        // Antes de que WPF genere los primeros contenedores, el viewport sirve como reserva para
        // que la primera apertura no se cierre durante el layout. Después solo las tarjetas reales
        // mantienen abierto el dock.
        return _tabButtons.Count == 0;
    }

    private bool ContainsElementSurface(UIElement element, double cursorX, double cursorY, double padding)
    {
        if (element is not FrameworkElement framework
            || element.Visibility != Visibility.Visible
            || framework.ActualWidth <= 0
            || framework.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            Point origin = element.TranslatePoint(new Point(0, 0), this);
            return cursorX >= Left + origin.X - padding
                && cursorX <= Left + origin.X + framework.ActualWidth + padding
                && cursorY >= Top + origin.Y - padding
                && cursorY <= Top + origin.Y + framework.ActualHeight + padding;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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
        HoldOpenForWindow();
        _coordinator.OpenOrActivateNotesManager(this, CurrentTagFilter);
        e.Handled = true;
    }

    private void OnManageArchiveRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_dockViewToggle.ShouldConsumeOpen())
        {
            e.Handled = true;
            return;
        }

        OpenDockViewPopup();
        e.Handled = true;
    }

    private void OnDockViewPopupOpened(object sender, EventArgs e)
    {
        DockViewKeepDockButton.Content = _settings?.KeepDockOpen == true
            ? Strings.KeepDockOpenOn
            : Strings.KeepDockOpenOff;

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

    private void OpenDockViewPopup()
    {
        DockViewPopup.PlacementTarget = ManageArchiveButton;
        DockViewPopup.Placement = _edge switch
        {
            EdgePosition.Top => PlacementMode.Bottom,
            EdgePosition.Bottom => PlacementMode.Top,
            EdgePosition.Left => PlacementMode.Right,
            _ => PlacementMode.Left
        };
        BuildDockTagChoices();
        _dockViewToggle.SetTrigger(ManageArchiveButton);
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

    private static readonly Brush SyncOkBrush = CreateSyncBrush(0xA9, 0xC9, 0xA4);
    private static readonly Brush SyncErrorBrush = CreateSyncBrush(0xE8, 0xA0, 0xA0);

    private static Brush CreateSyncBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
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
        SyncButtonGlyph.FontSize = 15;
        SyncButtonGlyph.Foreground = ok ? SyncOkBrush : SyncErrorBrush;
        SyncButton.ToolTip = tooltip;

        // Que el resultado se pueda leer: el dock aguanta otro margen aunque el cursor ya no esté
        // encima, y después decide el sondeo normal.
        _interactionGraceUntil = DateTime.UtcNow + InteractionGrace;

        _syncFeedbackTimer.Stop();
        _syncFeedbackTimer.Start();
    }

    private void RestoreSyncButton()
    {
        if (_syncBusy) return; // el feedback caducado no debe reactivar un botón ocupado

        SyncButtonGlyph.Text = "↻";
        SyncButtonGlyph.FontSize = 17;
        SyncButtonGlyph.ClearValue(TextBlock.FontFamilyProperty);
        SyncButtonGlyph.ClearValue(TextBlock.ForegroundProperty);
        SyncButton.ToolTip = Strings.SyncNowButton;
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
            MessageBox.Show(message, Strings.SyncErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            SetSyncBusy(false);
            ShowSyncResult(ok: false, Strings.SyncErrorStatus(ex.Message));
            MessageBox.Show(Strings.SyncErrorStatus(ex.Message), Strings.SyncErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnSyncRightClick(object sender, MouseButtonEventArgs e)
    {
        HoldOpenForWindow();
        _coordinator.OpenSettings();
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
        window.MaxHeight = _workingArea.Height * 0.9;

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

    private void OnTabDragStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { Tag: Note note } button) return;

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

        _dragButton.ReleaseMouseCapture();
        // El Click del botón llega justo después de esto: sin la bandera, soltar tras arrastrar
        // abriría además la nota.
        _suppressNextClick = true;
        ResetDrag();

        if (note is null) return;

        var current = (TabsList.ItemsSource as IEnumerable<Note>)?.Select(n => n.Id).ToList();
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
        if (button.DataContext is not Note note || !NoteColorDerivation.IsDark(note.Color)) return;

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
        var radius = new CornerRadius(10);
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
        TabMenuProtectionButton.Content = note.IsProtected ? Strings.RemoveProtection : Strings.ProtectNote;
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

    private void OnTabMenuColorClick(object sender, MouseButtonEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;

        var color = (string)((Border)sender).Tag;
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
            var password = PasswordPromptWindow.Show(this, Strings.RemoveProtection,
                Strings.ProtectedNoteHint, confirm: false);
            if (password is null) return;
            if (!_repository.RemoveProtection(note.Id, password))
            {
                MessageBox.Show(this, Strings.WrongPassword, Strings.AppName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            var password = PasswordPromptWindow.Show(this, Strings.ProtectNote,
                Strings.ProtectNoteHint, confirm: true);
            if (password is null) return;
            _repository.Protect(note.Id, password);
        }

        _coordinator.RefreshAll();
    }

    private void OnTabMenuArchiveClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is { } note)
        {
            _repository.SetState(note.Id,
                note.State == NoteState.Active ? NoteState.Archived : NoteState.Active);
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
            _repository.SetState(note.Id,
                note.State == NoteState.Trashed ? NoteState.Active : NoteState.Trashed);
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

    private void CloseDockPopups()
    {
        _openAllMenuTopmostTimer.Stop();
        if (NewNoteMenuPopup.IsOpen) NewNoteMenuPopup.IsOpen = false;
        if (OpenAllMenuPopup.IsOpen) OpenAllMenuPopup.IsOpen = false;
        if (DockViewPopup.IsOpen) DockViewPopup.IsOpen = false;
        if (TagEditorPopup.IsOpen) TagEditorPopup.IsOpen = false;
        if (TabMenuPopup.IsOpen || _tabMenuNote is not null) CloseTabMenu();
    }

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
    /// Coloca la ventana de una nota recién abierta, alineada con la altura de su propia pestaña.
    /// Se usa solo cuando la nota no tiene una posición recordada para este monitor (ver
    /// <see cref="AppCoordinator.TryRestorePlacement"/>) — con ella, la animación de apertura ya no
    /// depende de dónde caiga esta posición inicial (ver <see cref="NoteWindow.PlayOpenAnimation"/>).
    /// </summary>
    internal void PositionNoteWindow(NoteWindow noteWindow, System.Windows.Rect? tabRect = null)
    {
        int step = _coordinator.OpenNoteWindowCount % NoteWindowMaxCascadeSteps;

        if (_edge is EdgePosition.Top or EdgePosition.Bottom)
        {
            double horizontalLeft = tabRect?.X ?? Left;
            double maxHorizontalLeft = Math.Max(_workingArea.X, _workingArea.X + _workingArea.Width - noteWindow.Width);
            noteWindow.Left = Math.Clamp(horizontalLeft, _workingArea.X, maxHorizontalLeft);

            double horizontalTop = _edge == EdgePosition.Top
                ? Top + EdgeGeometry.WindowThickness + NoteWindowGapFromDock
                    + step * NoteWindowCascadeStep
                : Top - NoteWindowGapFromDock - noteWindow.Height
                    - step * NoteWindowCascadeStep;
            double maxHorizontalTop = Math.Max(_workingArea.Y, _workingArea.Y + _workingArea.Height - noteWindow.Height);
            noteWindow.Top = Math.Clamp(horizontalTop, _workingArea.Y, maxHorizontalTop);
            return;
        }

        // Con el dock a la derecha la nota cae a la izquierda del canto interior de la pestaña; con
        // el dock a la izquierda es al revés: a la derecha de SU canto interior, que en ese lado es
        // Left(dock) + TabWidth (ver EdgeGeometry: ShadowMargin vive siempre en el lado interior de
        // la pestaña, opuesto al canto físico de la pantalla que esa pestaña toca). En los dos casos
        // se aparta NoteWindowGapFromDock más, para no quedar pegada al dock.
        double left = _edge == EdgePosition.Left
            ? Left + EdgeGeometry.TabWidth + NoteWindowGapFromDock + step * NoteWindowCascadeStep
            : Left + EdgeGeometry.ShadowMargin - NoteWindowGapFromDock - noteWindow.Width - step * NoteWindowCascadeStep;
        var top = tabRect?.Y ?? Top;

        noteWindow.Left = _edge == EdgePosition.Left
            ? Math.Min(left, _workingArea.X + _workingArea.Width - noteWindow.Width)
            : Math.Max(left, _workingArea.X);
        noteWindow.Top = Math.Clamp(
            top,
            _workingArea.Y,
            Math.Max(_workingArea.Y, _workingArea.Y + _workingArea.Height - noteWindow.Height));
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
    private void HoldOpenForNextInteraction()
    {
        _interactionGraceUntil = DateTime.UtcNow + InteractionGrace;
        _hoverLayoutHold = false;
        _pointerInside = true;
        _hoverReentryBlocked = false;
        _collapseTimer.Stop();
        if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
    }

    /// <summary>
    /// Margen de cortesía al abrir otra ventana desde este dock. Fija el estado de hover para que el
    /// sondeo no lo cierre bajo los pies mientras la nueva ventana roba el foco, y limpia
    /// <c>_hoverReentryBlocked</c> para que el siguiente pase del ratón lo reabra sin exigir primero
    /// una salida completa de la ventana.
    /// </summary>
    private void HoldOpenForWindow()
    {
        _hoverGraceUntil = DateTime.UtcNow.Add(HoverOpenGrace);
        _pointerInside = true;
        _collapseTimer.Stop();
        _hoverReentryBlocked = false;
        if (_noteCount > 0 && !_fanState.IsExpanded) _fanState.PointerEntered();
    }

    private void HoldHoverDuringLayout()
    {
        if (!_pointerInside && (_hwnd == IntPtr.Zero || !NativeMethods.IsCursorOverWindow(_hwnd))) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursor = NativeMethods.GetCursorScreenPosition();
        _hoverLayoutAnchorX = cursor.X / dpi.DpiScaleX;
        _hoverLayoutAnchorY = cursor.Y / dpi.DpiScaleY;
        _hoverLayoutHold = true;
        _collapseTimer.Stop();
        _hoverReentryBlocked = false;
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
        _collapseTimer.Stop();
        _fullscreenPollTimer.Stop();
        _arrowScrollTimer.Stop();
        _syncFeedbackTimer.Stop();
        NativeMethods.UninstallKeyboardHook(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }
}

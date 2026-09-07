using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _hoverPollTimer;
    private readonly DispatcherTimer _fullscreenPollTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly string _monitorKey;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly AppSettings? _settings;
    private int _noteCount;
    private bool _pointerInside;
    private bool _hiddenByFullscreenApp;

    private IntPtr _hwnd;

    // La ventana no se redimensiona nunca (ver EdgeGeometry): siempre ocupa WindowRect. Animar
    // Left/Top/Width/Height de un HWND obliga a WPF a rehacer el layout en cada frame intermedio, y
    // de ahí salía toda la familia de fallos que documenta docs/STATUS.md.
    //
    // Lo que se anima es puro WPF sobre el contenido. Ya no hay región que recalcular por frame ni
    // bucle de CompositionTarget.Rendering: con AllowsTransparency la forma la dibuja WPF.
    private readonly Dictionary<int, Button> _tabButtons = new();

    // Para animar solo las pestañas nuevas al crear una nota, en vez de rehacer la entrada entera.
    private HashSet<Guid> _knownNoteIds = new();

    private Thickness _tabMargin = new(0, 0, 0, EdgeGeometry.TabGap);

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
        _workingArea = monitor.WorkArea;
        _monitorKey = monitor.DeviceName;
        _repository = repository;
        _coordinator = coordinator;
        _settings = settings;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
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

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNonActivating(_hwnd);
            ApplyWindowRect();
        };

        ApplyWindowRect();
        ApplyState(animate: false);
    }

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

        // Con el menú de una pestaña abierto, el abanico se queda como está: el menú sale fuera de
        // la zona sensible del dock, así que mover el ratón hacia él contaría como salir y lo
        // cerraría justo cuando el usuario va a pulsarlo.
        if (TabMenuPopup.IsOpen) return;

        // Lo mismo mientras se arrastra una pestaña: el gesto puede salirse de la zona sensible, y
        // colapsar el abanico a mitad de arrastre dejaría la nota en el aire.
        if (_dragging) return;

        // Ignora que el cursor pase por encima mientras se arrastra otra cosa que comparta el mismo
        // borde de pantalla (p. ej. la barra de scroll de un navegador).
        if (NativeMethods.IsLeftButtonDown()) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorScreen = NativeMethods.GetCursorScreenPosition();
        double cursorX = cursorScreen.X / dpi.DpiScaleX;
        double cursorY = cursorScreen.Y / dpi.DpiScaleY;

        // Contra la zona realmente visible, no contra la ventana entera: casi toda es transparente,
        // y desplegarse al entrar ahí sería desplegarse por pasar el ratón sobre nada.
        // Sin notas la ventana entera es zona sensible: no hay tira que sobrevolar, y los botones
        // tienen que poder pulsarse sin desplegar nada primero.
        var hitRect = _fanState.IsExpanded || _noteCount == 0
            ? EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount)
            : EdgeGeometry.RestingVisibleRect(_workingArea, _edge, _noteCount);

        bool isInside = cursorX >= hitRect.X && cursorX <= hitRect.X + hitRect.Width
            && cursorY >= hitRect.Y && cursorY <= hitRect.Y + hitRect.Height;

        if (isInside && !_pointerInside)
        {
            _pointerInside = true;
            _fanState.PointerEntered();
            NativeMethods.EnsureTopmost(_hwnd);
        }
        else if (!isInside && _pointerInside)
        {
            _pointerInside = false;
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

    private static readonly Duration CrossfadeDuration = new(TimeSpan.FromMilliseconds(180));

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
            return;
        }

        Fade(FanPanel, expanded ? 1 : 0);
        Fade(RestStrip, expanded ? 0 : 1);

        if (expanded) ReplayTabEntrance();
    }

    private static void Fade(UIElement element, double to)
    {
        var animation = new DoubleAnimation(to, CrossfadeDuration) { EasingFunction = EaseOut() };
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
            PlayEntrance(button, FanTiming.StaggerDelayMs(index));
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
                translate.X = 0;
            }
        }
    }

    /// <summary>Una pestaña entra deslizándose desde el canto de la pantalla y apareciendo.</summary>
    private static void PlayEntrance(Button button, double delayMs)
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

        // Instancia nueva por pestaña: un TranslateTransform declarado en XAML dentro de un
        // DataTemplate acaba congelado y compartido entre contenedores (Freezable), y animarlo
        // lanza "Cannot animate ... because the object is sealed or frozen" — ya pasó una vez, ver
        // docs/STATUS.md.
        if (button.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            translate = new TranslateTransform();
            button.RenderTransform = translate;
        }

        double from = EdgeGeometry.TabWidth * 0.55;
        var slide = new DoubleAnimation(from, 0, duration)
        {
            BeginTime = delay,
            EasingFunction = EaseOut()
        };
        slide.Completed += (_, _) =>
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;
        };
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.X = from; // mismo motivo que la opacidad de arriba: el retardo pinta el valor base
        translate.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    // --- Contenido ------------------------------------------------------------------------------

    public void Refresh()
    {
        SetNotes(_repository.GetByState(NoteState.Active));
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
            bool isOpen = button.Tag is Note note && _coordinator.IsNoteOpen(note.Id);
            // Hidden y no Collapsed: conserva su hueco, y el mazo enseña el sitio vacío de donde se
            // sacó la ficha.
            button.Visibility = isOpen ? Visibility.Hidden : Visibility.Visible;
        }
    }

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        var previousIds = _knownNoteIds;
        _knownNoteIds = notes.Select(n => n.Id).ToHashSet();

        _tabButtons.Clear();
        TabsList.ItemsSource = notes;

        // La tira de reposo no hace scroll, así que solo se dibujan los guiones que caben: el resto
        // se recortarían contra el borde de la ventana (ver EdgeGeometry.VisibleRestDashes).
        RestList.ItemsSource = notes
            .Take(EdgeGeometry.VisibleRestDashes(_workingArea, _edge, notes.Count))
            .ToList();

        bool countChanged = notes.Count != _noteCount;
        _noteCount = notes.Count;

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
        var arrived = _knownNoteIds.Except(previousIds).ToHashSet();

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            RefreshOpenState();

            if (!_fanState.IsExpanded)
            {
                ResetTabEntrance(settled: false);
                return;
            }

            foreach (var button in _tabButtons.Values)
            {
                if (button.Tag is Note note && arrived.Contains(note.Id))
                {
                    PlayEntrance(button, 0);
                }
                else
                {
                    button.Opacity = 1;
                }
            }
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

    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenOrActivateNotesManager(this);
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

    /// <summary>
    /// Centra <paramref name="window"/> en el monitor de este dock. Una ventana sin posición fijada
    /// acaba en (0,0), o sea siempre en el monitor principal, aunque la hayas abierto desde el otro.
    /// </summary>
    internal void CenterOnThisMonitor(Window window)
    {
        window.Left = _workingArea.X + (_workingArea.Width - window.Width) / 2;
        window.Top = _workingArea.Y + (_workingArea.Height - window.Height) / 2;
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
            Panel.SetZIndex(_dragButton, 1000); // por encima de las demás mientras viaja
        }

        if (_dragButton.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            translate = new TranslateTransform();
            _dragButton.RenderTransform = translate;
        }
        translate.BeginAnimation(TranslateTransform.YProperty, null);
        translate.Y = position.Y - _dragStart.Y;
    }

    private void OnTabDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (_dragButton is null) { ResetDrag(); return; }
        if (!_dragging) { ResetDrag(); return; }

        var note = _dragNote;
        // Cuánto se ha arrastrado, no dónde ha caído: el destino se calcula desde el sitio que
        // ocupaba (ver NoteOrdering.TargetIndex), así que no depende del origen de la lista ni de si
        // el abanico está scrolleado.
        double delta = e.GetPosition(TabsList).Y - _dragStart.Y;

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
            originalIndex, delta, EdgeGeometry.PitchFor(_workingArea, _edge, _noteCount), current.Count);

        if (originalIndex == target) return; // no se arrastró lo suficiente para cambiar de sitio

        _repository.MoveNote(note.Id, target, current);
        _coordinator.RefreshAll();
    }

    private void ResetDrag()
    {
        if (_dragButton is not null)
        {
            Panel.SetZIndex(_dragButton, 0);
            if (_dragButton.RenderTransform is TranslateTransform { IsFrozen: false } translate)
            {
                translate.Y = 0;
            }
        }

        _dragButton = null;
        _dragNote = null;
        _dragging = false;
    }

    // --- Menú contextual de una pestaña ---------------------------------------------------------

    private Note? _tabMenuNote;

    private void OnTabRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Note note }) return;

        _tabMenuNote = note;
        BuildTabMenuSwatches(note);
        TabMenuPopup.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// Las seis pastillas de color del menú, con la de la nota marcada. Se reconstruyen en cada
    /// apertura porque el menú sirve a la pestaña que se acaba de pulsar, no a una fija.
    /// </summary>
    private void BuildTabMenuSwatches(Note note)
    {
        TabMenuSwatches.Children.Clear();

        foreach (var color in NoteColorPalette.Colors)
        {
            bool selected = color == note.Color;
            var swatch = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(color)!,
                Width = 22,
                Height = 22,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(5),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.Ink)!,
                BorderThickness = new Thickness(selected ? 2 : 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = color,
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)new BrushConverter().ConvertFromString(
                        NoteColorPalette.LabelFor(color))!,
                    Visibility = selected ? Visibility.Visible : Visibility.Collapsed,
                    IsHitTestVisible = false
                }
            };
            swatch.MouseLeftButtonUp += OnTabMenuColorClick;
            TabMenuSwatches.Children.Add(swatch);
        }
    }

    private void OnTabMenuColorClick(object sender, MouseButtonEventArgs e)
    {
        if (_tabMenuNote is not { } note) return;

        var color = (string)((Border)sender).Tag;
        if (color != note.Color)
        {
            _repository.SetColor(note.Id, color);
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    private void OnTabMenuOpenClick(object sender, RoutedEventArgs e)
    {
        var note = _tabMenuNote;
        CloseTabMenu();
        if (note is not null) _coordinator.OpenOrActivateNote(note, this);
    }

    private void OnTabMenuArchiveClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is { } note)
        {
            _repository.SetState(note.Id, NoteState.Archived);
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    private void OnTabMenuTrashClick(object sender, RoutedEventArgs e)
    {
        if (_tabMenuNote is { } note)
        {
            _repository.SetState(note.Id, NoteState.Trashed);
            _coordinator.RefreshAll();
        }
        CloseTabMenu();
    }

    private void CloseTabMenu()
    {
        TabMenuPopup.IsOpen = false;
        _tabMenuNote = null;
    }

    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        int index = TabsList.Items.IndexOf(button.DataContext);
        if (index < 0) return;

        _tabButtons[index] = button;

        // La última no lleva el margen negativo del solape. El solape se consigue con un
        // Margin.Bottom negativo (paso 26 con pestañas de 52 → -26), y en la última eso no solapa
        // con nada: solo hace que el StackPanel se mida 26px más corto de lo que esa pestaña ocupa
        // de verdad, así que el ScrollViewer la recortaba justo por ahí. Con pocas notas no se veía
        // porque el paso natural (60) es mayor que el alto (52) y el margen sale positivo.
        bool isLast = index == _noteCount - 1;
        button.Margin = isLast ? new Thickness(0) : _tabMargin;

        button.Opacity = _fanState.IsExpanded ? 1 : 0;
        ApplyPreviewVisibility(button);
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
        var existingCount = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorPalette.Colors[existingCount % NoteColorPalette.Colors.Length];
        _repository.Create(string.Empty, color, screenOrigin: "primary");
        _coordinator.RefreshAll();
    }

    /// <summary>
    /// Para todo lo que este dock tiene en marcha antes de cerrarlo, al reconstruir por un cambio
    /// de pantallas. Sin esto sus timers seguirían vivos sobre una ventana ya cerrada.
    /// </summary>
    internal void PrepareForClose()
    {
        _hoverPollTimer.Stop();
        _collapseTimer.Stop();
        _fullscreenPollTimer.Stop();
    }
}

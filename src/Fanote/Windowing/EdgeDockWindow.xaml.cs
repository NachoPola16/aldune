using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
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

    public EdgeDockWindow(EdgePosition edge, MonitorInfo monitor, NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = monitor.WorkArea;
        _repository = repository;
        _coordinator = coordinator;

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

        // Ignora que el cursor pase por encima mientras se arrastra otra cosa que comparta el mismo
        // borde de pantalla (p. ej. la barra de scroll de un navegador).
        if (NativeMethods.IsLeftButtonDown()) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorScreen = NativeMethods.GetCursorScreenPosition();
        double cursorX = cursorScreen.X / dpi.DpiScaleX;
        double cursorY = cursorScreen.Y / dpi.DpiScaleY;

        // Contra la zona realmente visible, no contra la ventana entera: casi toda es transparente,
        // y desplegarse al entrar ahí sería desplegarse por pasar el ratón sobre nada.
        var hitRect = _fanState.IsExpanded
            ? EdgeGeometry.WindowRect(_workingArea, _edge, _noteCount)
            : EdgeGeometry.RestingVisibleRect(_workingArea, _edge, _noteCount);

        bool isInside = cursorX >= hitRect.X && cursorX <= hitRect.X + hitRect.Width
            && cursorY >= hitRect.Y && cursorY <= hitRect.Y + hitRect.Height;

        if (isInside && !_pointerInside)
        {
            _pointerInside = true;
            _fanState.PointerEntered();
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
        bool expanded = _fanState.IsExpanded;

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

        button.BeginAnimation(OpacityProperty, null);
        button.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, duration) { BeginTime = delay });

        // Instancia nueva por pestaña: un TranslateTransform declarado en XAML dentro de un
        // DataTemplate acaba congelado y compartido entre contenedores (Freezable), y animarlo
        // lanza "Cannot animate ... because the object is sealed or frozen" — ya pasó una vez, ver
        // docs/STATUS.md.
        if (button.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            translate = new TranslateTransform();
            button.RenderTransform = translate;
        }

        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(EdgeGeometry.TabWidth * 0.55, 0, duration)
            {
                BeginTime = delay,
                EasingFunction = EaseOut()
            });
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

        bool countChanged = notes.Count != _noteCount;
        _noteCount = notes.Count;

        // El solape depende del número de notas y va en Margin (layout). Solo cambia aquí, nunca
        // durante una transición de hover, así que no reintroduce el medir-a-tamaños-intermedios.
        double pitch = EdgeGeometry.PitchFor(_workingArea, _edge, _noteCount);
        _tabMargin = new Thickness(0, 0, 0, pitch - EdgeGeometry.TabHeight);

        if (countChanged) ApplyWindowRect();

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

    private void OnManageArchiveClick(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenOrActivateNotesManager();
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
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

    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        int index = TabsList.Items.IndexOf(button.DataContext);
        if (index < 0) return;

        _tabButtons[index] = button;
        button.Margin = _tabMargin;
        button.Opacity = _fanState.IsExpanded ? 1 : 0;
    }

    /// <summary>
    /// Coloca la ventana de una nota recién abierta, alineada con la altura de su propia pestaña, y
    /// devuelve la X desde la que debe deslizarse (acotada al monitor de este dock).
    /// </summary>
    internal double PositionNoteWindow(NoteWindow noteWindow, System.Windows.Rect? tabRect = null)
    {
        int step = _coordinator.OpenNoteWindowCount % NoteWindowMaxCascadeSteps;

        var left = Left + EdgeGeometry.ShadowMargin - noteWindow.Width - step * NoteWindowCascadeStep;
        var top = tabRect?.Y ?? Top;

        noteWindow.Left = Math.Max(left, _workingArea.X);
        noteWindow.Top = Math.Clamp(
            top,
            _workingArea.Y,
            Math.Max(_workingArea.Y, _workingArea.Y + _workingArea.Height - noteWindow.Height));

        return EdgeGeometry.SlideOriginFor(
            _workingArea, tabRect?.X ?? noteWindow.Left, noteWindow.Left, noteWindow.Width);
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

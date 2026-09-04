using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Interop;

namespace Fanote.Windowing;

public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _hoverPollTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private int _noteCount;
    private bool _pointerInside;

    private IntPtr _hwnd;

    // --- Estado de la transición ---------------------------------------------------------------
    //
    // La ventana no se redimensiona nunca (ver EdgeGeometry): siempre ocupa WindowRect, y lo único
    // que se anima es la región recortada más un RenderTransform por pestaña. Ninguna de las dos
    // cosas toca el layout, así que WPF mide una sola vez a tamaño final y jamás en un tamaño
    // intermedio — que era la causa raíz de toda la familia de fallos de docs/STATUS.md.
    //
    // El precio es recalcular la región por frame mientras dura la transición. SetWindowRgn emite
    // WM_WINDOWPOSCHANGING/CHANGED en cada llamada, así que está acotado a propósito: solo durante
    // los ~350ms de la transición, nunca en reposo ni desplegado quieto, y saltándose la llamada si
    // los rects redondeados a entero no han cambiado respecto al frame anterior.
    private readonly Stopwatch _transitionClock = new();
    private bool _transitionRunning;
    private bool _transitionExpanding;
    private List<(int Left, int Top, int Right, int Bottom)>? _lastRegionKey;

    // ItemsControl.ItemContainerGenerator.ContainerFromIndex devuelve un ContentPresenter, no el
    // Button del ItemTemplate — así genera sus contenedores un ItemsControl normal; solo los
    // derivados de Selector devuelven el elemento plantillado directamente. Un `is Button` sobre
    // ContainerFromIndex por tanto no casa nunca (comprobado empíricamente). Guardar aquí las
    // referencias reales, pobladas desde el propio Loaded de cada Button, esquiva la cuestión del
    // tipo de contenedor por completo.
    private readonly Dictionary<int, Button> _tabButtons = new();
    private readonly Dictionary<int, TranslateTransform> _tabOffsets = new();

    private const double NoteWindowCascadeStep = 26;
    private const int NoteWindowMaxCascadeSteps = 6;
    private const double TabCornerRadius = 10;

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

        _fanState.ExpansionChanged += (_, _) => StartTransition(_fanState.IsExpanded);

        // Deliberadamente no MouseEnter/MouseLeave de WPF: la región recortada cambia qué parte de
        // la ventana recibe ratón, y sondear la posición real del cursor no depende de que Win32
        // acierte con su seguimiento. Ver el historial en docs/STATUS.md.
        _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _hoverPollTimer.Tick += (_, _) => PollHoverState();
        _hoverPollTimer.Start();

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNonActivating(_hwnd);
            // Ni esquinas redondeadas ni sombra vía DWM: la forma la define la región, y la sombra
            // DWM sigue el RECT completo de la ventana, así que pintaría una caja translúcida
            // sobre los huecos que la región existe para quitar (ver NativeMethods).
            ApplyWindowRect();
            ApplyRegion();
        };

        ApplyWindowRect();
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
        // Ignora que el cursor pase por encima mientras se arrastra otra cosa que comparta el mismo
        // borde de pantalla (p. ej. la barra de scroll de un navegador): congela el estado mientras
        // dure el arrastre. Un clic en una pestaña no se ve afectado — eso lo gestiona Button.Click.
        if (NativeMethods.IsLeftButtonDown()) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorScreen = NativeMethods.GetCursorScreenPosition();
        double cursorX = cursorScreen.X / dpi.DpiScaleX;
        double cursorY = cursorScreen.Y / dpi.DpiScaleY;

        // Contra la zona realmente visible, no contra la ventana entera: lo recortado por la región
        // es transparente al ratón, así que desplegarse al entrar ahí sería desplegarse por pasar
        // el ratón sobre nada.
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

    // --- Transición ----------------------------------------------------------------------------

    private void StartTransition(bool expanding)
    {
        _transitionExpanding = expanding;

        if (!SystemParameters.ClientAreaAnimation)
        {
            StopTransition();
            ApplyRegion();
            return;
        }

        _transitionClock.Restart();
        if (!_transitionRunning)
        {
            _transitionRunning = true;
            CompositionTarget.Rendering += OnRenderingFrame;
        }
    }

    private void StopTransition()
    {
        if (!_transitionRunning) return;
        _transitionRunning = false;
        CompositionTarget.Rendering -= OnRenderingFrame;
        _transitionClock.Stop();
    }

    private void OnRenderingFrame(object? sender, EventArgs e)
    {
        double elapsed = _transitionClock.Elapsed.TotalMilliseconds;
        ApplyRegion(elapsed);

        if (elapsed >= TabRegionShape.TotalDurationMs(_noteCount))
        {
            StopTransition();
            ApplyRegion(); // estado final exacto, sin depender del último frame que llegara
        }
    }

    private double ProgressFor(int index, double? elapsedMs)
    {
        if (elapsedMs is null) return _fanState.IsExpanded ? 1 : 0;
        return _transitionExpanding
            ? TabRegionShape.TabProgress(index, elapsedMs.Value)
            : TabRegionShape.TabCollapseProgress(index, _noteCount, elapsedMs.Value);
    }

    /// <summary>
    /// Recalcula y aplica la región, y con ella el desplazamiento vertical de cada pestaña. Sin
    /// <paramref name="elapsedMs"/> aplica el estado asentado.
    /// </summary>
    private void ApplyRegion(double? elapsedMs = null)
    {
        if (_hwnd == IntPtr.Zero) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var tabRects = new List<Fanote.Core.Rect>(_tabButtons.Count);
        double footerProgress = 0;

        // El ItemsControl no está virtualizado, así que con más notas de las que caben existen
        // Buttons colocados por debajo del viewport del ScrollViewer. TranslatePoint devuelve su
        // posición igualmente, y sin acotarlos la región abriría un agujero justo donde el
        // ScrollViewer ya no dibuja la pestaña.
        var scrollOrigin = TabsScroll.TranslatePoint(new Point(0, 0), this);
        double viewportTop = scrollOrigin.Y;
        double viewportBottom = scrollOrigin.Y + TabsScroll.ActualHeight;

        foreach (var (index, button) in _tabButtons)
        {
            double progress = ProgressFor(index, elapsedMs);

            if (index == _noteCount - 1) footerProgress = progress;

            // El desplazamiento va en RenderTransform, no en Margin ni en layout: en reposo los
            // guiones van con un paso de 30px y las pestañas desplegadas con uno de 88, así que
            // sin mover la pestaña la banda que la región deja ver para la nota 2 caería sobre
            // píxeles de la nota 1 y los colores saldrían cambiados.
            if (_tabOffsets.TryGetValue(index, out var offset))
            {
                offset.Y = TabRegionShape.Sweep(EdgeGeometry.RestOffsetFor(index, _noteCount), 0, progress);
            }

            // Una nota abierta se saca del mazo: su pestaña viaja con la ventana como lomo (ver
            // NoteWindow), así que dejarla también aquí mostraría la misma etiqueta dos veces.
            // Hidden y no Collapsed a propósito — conserva su hueco, y el mazo enseña el sitio
            // vacío de donde se sacó la ficha.
            bool isOpen = button.Tag is Note note && _coordinator.IsNoteOpen(note.Id);
            button.Visibility = isOpen ? Visibility.Hidden : Visibility.Visible;
            if (isOpen) continue;

            // TranslatePoint recorre la cadena de transformaciones del visual, así que esto ya
            // incluye el RenderTransform recién fijado arriba.
            var origin = button.TranslatePoint(new Point(0, 0), this);
            double fullWidth = button.ActualWidth;
            double fullHeight = button.ActualHeight;

            double sweptWidth = TabRegionShape.Sweep(EdgeGeometry.RestSliverWidth, fullWidth, progress);
            double sweptHeight = TabRegionShape.Sweep(EdgeGeometry.RestDashLength, fullHeight, progress);

            // Anclada por la derecha (el borde físico de pantalla) y centrada sobre la pestaña ya
            // desplazada: lo que se mueve es el borde izquierdo, barriendo hacia fuera, mientras
            // el guión crece a lo alto hasta ser la pestaña entera.
            double right = origin.X + fullWidth;
            double centerY = origin.Y + fullHeight / 2;
            double top = Math.Max(centerY - sweptHeight / 2, viewportTop);
            double bottom = Math.Min(centerY + sweptHeight / 2, viewportBottom);
            if (bottom <= top) continue; // scrolleada del todo fuera de la vista

            tabRects.Add(new Fanote.Core.Rect(
                (right - sweptWidth) * dpi.DpiScaleX,
                top * dpi.DpiScaleY,
                sweptWidth * dpi.DpiScaleX,
                (bottom - top) * dpi.DpiScaleY));
        }

        if (elapsedMs is null) footerProgress = _fanState.IsExpanded ? 1 : 0;

        var footerOrigin = FooterPanel.TranslatePoint(new Point(0, 0), this);
        double footerFull = FooterPanel.ActualWidth;
        // Barre desde 0, no desde RestSliverWidth: en reposo no debe asomar nada del footer, o
        // parecería una nota más de color gris al final de la tira.
        double footerSwept = TabRegionShape.Sweep(0, footerFull, footerProgress);
        var footerRect = new Fanote.Core.Rect(
            (footerOrigin.X + footerFull - footerSwept) * dpi.DpiScaleX,
            footerOrigin.Y * dpi.DpiScaleY,
            footerSwept * dpi.DpiScaleX,
            FooterPanel.ActualHeight * dpi.DpiScaleY);

        var pieces = TabRegionShape.BuildRegion(tabRects, footerRect, TabCornerRadius * dpi.DpiScaleX);

        // SetWindowRgn emite dos mensajes de ventana por llamada; saltarse los frames en los que la
        // forma redondeada a entero no ha cambiado quita bastantes llamadas de la transición, sobre
        // todo al final de la curva, donde la ease-out apenas avanza.
        var key = new List<(int, int, int, int)>(pieces.Count);
        foreach (var piece in pieces)
        {
            key.Add(((int)piece.Bounds.X, (int)piece.Bounds.Y,
                     (int)(piece.Bounds.X + piece.Bounds.Width),
                     (int)(piece.Bounds.Y + piece.Bounds.Height)));
        }
        if (_lastRegionKey is not null && key.Count == _lastRegionKey.Count)
        {
            bool same = true;
            for (int i = 0; i < key.Count && same; i++)
            {
                if (!key[i].Equals(_lastRegionKey[i])) same = false;
            }
            if (same) return;
        }
        _lastRegionKey = key;

        NativeMethods.SetTabFanRegion(_hwnd, pieces);
    }

    public void Refresh()
    {
        SetNotes(_repository.GetByState(NoteState.Active));
    }

    /// <summary>
    /// Recalcula solo qué pestañas están ocultas por tener su nota abierta, sin reconstruir la
    /// lista. Separado de <see cref="Refresh"/> a propósito: abrir o cerrar una nota no cambia qué
    /// notas hay, y pasar por SetNotes reiniciaría la animación de entrada por nada.
    /// </summary>
    public void RefreshOpenState()
    {
        _lastRegionKey = null;
        ApplyRegion();
    }

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        _tabButtons.Clear();
        _tabOffsets.Clear();
        _lastRegionKey = null;
        TabsList.ItemsSource = notes;
        _noteCount = notes.Count;

        // La longitud de la ventana depende del número de notas. Se fija aquí, de una vez, en vez
        // de animarse: crear o archivar una nota cambia el tamaño del dock, y eso es un cambio de
        // contenido, no una transición de hover.
        ApplyWindowRect();

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ApplyRegion()));
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

        // Una instancia nueva por pestaña, creada en código. Un TranslateTransform declarado en
        // XAML dentro de un DataTemplate acaba congelado y compartido entre todos los contenedores
        // generados (Freezable), y animarlo lanza "Cannot animate ... because the object is sealed
        // or frozen" — ya pasó una vez en este mismo fichero, ver docs/STATUS.md.
        var offset = new TranslateTransform();
        button.RenderTransform = offset;
        _tabOffsets[index] = offset;

        // En arranque en frío, SetNotes puede correr antes de que ningún Loaded se dispare, así que
        // la región quedaría calculada sin pestañas. Recalcular aquí es la red de seguridad.
        _lastRegionKey = null;
        ApplyRegion();
    }

    /// <summary>
    /// Coloca la ventana de una nota recién abierta: alineada con la altura de su propia pestaña,
    /// que es de donde el usuario acaba de "tirar" para sacarla del mazo. La cascada solo se aplica
    /// en horizontal y solo cuando ya hay otras notas abiertas, para que no se tapen entre ellas.
    /// </summary>
    internal void PositionNoteWindow(NoteWindow noteWindow, System.Windows.Rect? tabRect = null)
    {
        int step = _coordinator.OpenNoteWindowCount % NoteWindowMaxCascadeSteps;

        var left = Left - noteWindow.Width + EdgeGeometry.PerforationInset - step * NoteWindowCascadeStep;
        var top = tabRect?.Y ?? Top;

        // Acotado al área de trabajo visible para que un escalón alto de la cascada (o un dock
        // anclado a la izquierda) no deje la ventana parcial o totalmente fuera de pantalla.
        noteWindow.Left = Math.Max(left, _workingArea.X);
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
}

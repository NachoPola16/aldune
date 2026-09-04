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

    // --- Estado de la transición -----------------------------------------------------------
    //
    // La ventana ya no se redimensiona nunca (ver EdgeGeometry): siempre ocupa WindowRect, y lo
    // único que se anima es la región recortada. Eso quita de en medio la causa raíz de todos los
    // fallos visuales que documenta docs/STATUS.md — animar Left/Top/Width/Height obligaba a WPF a
    // rehacer el layout en cada frame intermedio, y las pestañas pasaban la mayor parte de la
    // animación encajadas en anchos donde no cabían. Ahora el layout se mide una sola vez, a
    // tamaño final, y jamás en un tamaño intermedio.
    //
    // El precio es recalcular la región por frame mientras dura la transición. SetWindowRgn emite
    // WM_WINDOWPOSCHANGING/CHANGED en cada llamada, así que esto está acotado a propósito: solo
    // durante los ~280ms de la transición (≈17 frames), nunca en reposo ni desplegado quieto, y
    // saltándose la llamada si los rects enteros no han cambiado respecto al frame anterior. El
    // WM_MOUSELEAVE espurio que eso podría provocar ya no importa: el hover se sondea contra la
    // posición real del cursor desde af6b569, no contra eventos de ratón de WPF.
    private readonly Stopwatch _transitionClock = new();
    private bool _transitionRunning;
    private bool _transitionExpanding;
    private List<(int Left, int Top, int Right, int Bottom)>? _lastRegionKey;

    // ItemsControl.ItemContainerGenerator.ContainerFromIndex devuelve un ContentPresenter, no el
    // Button del ItemTemplate — así genera sus contenedores un ItemsControl normal; solo los
    // derivados de Selector devuelven el elemento plantillado directamente. Un `is Button` sobre
    // ContainerFromIndex por tanto no casa nunca (comprobado empíricamente al montar
    // SetTabFanRegion). Guardar aquí las referencias reales, pobladas desde el propio Loaded de
    // cada Button, esquiva la cuestión del tipo de contenedor por completo. Se limpia en cada
    // SetNotes para que archivar/añadir una nota no deje un índice apuntando a un contenedor
    // reciclado.
    private readonly Dictionary<int, Button> _tabButtons = new();

    private const double NoteWindowCascadeStep = 30;
    private const int NoteWindowMaxCascadeSteps = 8;
    private const double TabCornerRadius = 9;

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

        // Deliberadamente no MouseEnter/MouseLeave de WPF. Aunque la ventana ya no se mueva ni se
        // redimensione (que era lo que disparaba el WM_MOUSELEAVE falso), sigue habiendo un motivo
        // para sondear: la región recortada cambia qué parte de la ventana recibe ratón, y el
        // sondeo contra la posición real del cursor no depende de que Win32 acierte con el
        // seguimiento. Ver el comentario histórico en docs/STATUS.md.
        _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _hoverPollTimer.Tick += (_, _) => PollHoverState();
        _hoverPollTimer.Start();

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNonActivating(_hwnd);
            // Ni esquinas redondeadas ni sombra vía DWM: la forma la define ahora la región, y la
            // sombra DWM sigue el RECT completo de la ventana (no la región), así que pintaría una
            // caja translúcida justo sobre los huecos que la región existe para quitar — está
            // confirmado en una ejecución real, ver NativeMethods.ApplyShadow.
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
        // Ignora que el cursor pase por encima mientras se arrastra otra cosa que comparta el
        // mismo borde de pantalla (p. ej. la barra de scroll vertical de un navegador): congela el
        // estado en el que esté el dock mientras dure el arrastre. Un clic normal en una pestaña
        // no se ve afectado — eso es un press-and-release corto que gestiona Button.Click.
        if (NativeMethods.IsLeftButtonDown()) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cursorScreen = NativeMethods.GetCursorScreenPosition();
        double cursorX = cursorScreen.X / dpi.DpiScaleX;
        double cursorY = cursorScreen.Y / dpi.DpiScaleY;

        // Contra la zona realmente visible, no contra la ventana entera. La ventana ahora ocupa
        // siempre los 140px de grosor completo, pero en reposo la región recorta todo menos la
        // tira de 20px del borde — y lo recortado es transparente al ratón, así que desplegarse
        // al entrar ahí sería desplegarse por pasar el ratón sobre nada.
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

    // --- Transición ---------------------------------------------------------------------------

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
        double total = TabRegionShape.TotalDurationMs(_noteCount);

        ApplyRegion(elapsed);

        if (elapsed >= total)
        {
            StopTransition();
            ApplyRegion(); // estado final exacto, sin depender del último frame que llegara
        }
    }

    /// <summary>
    /// Recalcula y aplica la región. Sin <paramref name="elapsedMs"/> aplica el estado asentado
    /// (todo fuera si está desplegado, todo en su tira si está en reposo).
    /// </summary>
    private void ApplyRegion(double? elapsedMs = null)
    {
        if (_hwnd == IntPtr.Zero) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var tabRects = new List<Fanote.Core.Rect>(_tabButtons.Count);
        double footerProgress = _transitionExpanding ? 1 : 0;

        // El ItemsControl no está virtualizado, así que con más notas de las que caben existen
        // Buttons colocados por debajo del viewport del ScrollViewer. TranslatePoint devuelve su
        // posición igualmente, y sin acotarlos aquí la región abriría un agujero justo donde el
        // ScrollViewer ya no dibuja la pestaña: se vería el fondo de la ventana como una franja
        // suelta, sin nada dentro. El recorte es vertical porque el dock solo se instancia en
        // EdgePosition.Right (ver App.xaml.cs) y su contenido es una columna.
        var scrollOrigin = TabsScroll.TranslatePoint(new Point(0, 0), this);
        double viewportTop = scrollOrigin.Y;
        double viewportBottom = scrollOrigin.Y + TabsScroll.ActualHeight;

        foreach (var (index, button) in _tabButtons)
        {
            double progress;
            if (elapsedMs is null)
            {
                progress = _fanState.IsExpanded ? 1 : 0;
            }
            else if (_transitionExpanding)
            {
                progress = TabRegionShape.TabProgress(index, elapsedMs.Value);
            }
            else
            {
                progress = TabRegionShape.TabCollapseProgress(index, _noteCount, elapsedMs.Value);
            }

            var origin = button.TranslatePoint(new Point(0, 0), this);
            double fullWidth = button.ActualWidth;
            double swept = TabRegionShape.SweptWidth(EdgeGeometry.RestSliverWidth, fullWidth, progress);

            // Anclada por la derecha (el borde físico de pantalla): lo que se mueve es el borde
            // izquierdo, barriendo hacia fuera. El contenido de la pestaña está quieto en su sitio
            // final todo el rato — es la región la que lo va destapando.
            double right = origin.X + fullWidth;
            double left = right - swept;

            double top = Math.Max(origin.Y, viewportTop);
            double bottom = Math.Min(origin.Y + button.ActualHeight, viewportBottom);

            // El footer llega con la última pestaña, no antes. Se calcula aunque la pestaña esté
            // fuera del viewport: si no, con la lista scrolleada el footer no aparecería nunca.
            if (elapsedMs is not null && index == _noteCount - 1) footerProgress = progress;

            if (bottom <= top) continue; // scrolleada del todo fuera de la vista

            tabRects.Add(new Fanote.Core.Rect(
                left * dpi.DpiScaleX,
                top * dpi.DpiScaleY,
                swept * dpi.DpiScaleX,
                (bottom - top) * dpi.DpiScaleY));
        }

        if (elapsedMs is null) footerProgress = _fanState.IsExpanded ? 1 : 0;

        var footerOrigin = FooterPanel.TranslatePoint(new Point(0, 0), this);
        double footerFull = FooterPanel.ActualWidth;
        // Barre desde 0, no desde RestSliverWidth: en reposo no debe asomar nada del footer, o
        // parecería una nota más de color gris al final de la fila de tiras de color.
        double footerSwept = TabRegionShape.SweptWidth(0, footerFull, footerProgress);
        var footerRect = new Fanote.Core.Rect(
            (footerOrigin.X + footerFull - footerSwept) * dpi.DpiScaleX,
            footerOrigin.Y * dpi.DpiScaleY,
            footerSwept * dpi.DpiScaleX,
            FooterPanel.ActualHeight * dpi.DpiScaleY);

        var pieces = TabRegionShape.BuildRegion(tabRects, footerRect, TabCornerRadius * dpi.DpiScaleX);

        // SetWindowRgn emite dos mensajes de ventana por llamada; saltarse los frames en los que
        // la forma redondeada a entero no ha cambiado quita bastantes llamadas de la transición
        // (sobre todo al principio y al final de la curva, donde la ease-out apenas avanza).
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

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        _tabButtons.Clear();
        _lastRegionKey = null;
        TabsList.ItemsSource = notes;
        _noteCount = notes.Count;

        // La longitud de la ventana depende del número de notas. Se fija aquí, de una vez, en vez
        // de animarse: crear o archivar una nota cambia el tamaño del dock, y eso es un cambio de
        // contenido, no una transición de hover.
        ApplyWindowRect();

        // Los anchos y posiciones de las pestañas no existen hasta que WPF haya medido; la región
        // se recalcula tras el layout (OnTabLoaded también la refresca, como red de seguridad).
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

        // Ancho del abanico: interpolado sobre la fracción índice/(total-1), así que la pestaña
        // más ancha es siempre TabMaxWidth haya 3 notas o 30. La fórmula anterior (32 + i*14) no
        // estaba acotada y con 14 notas ya era más ancha que la propia ventana.
        button.Width = EdgeGeometry.TabWidth(index, _noteCount);

        // En arranque en frío, SetNotes puede haber corrido antes de que ningún Loaded se
        // disparase, dejando la región calculada sin pestañas. Recalcular aquí es la red de
        // seguridad: inofensivo si ya estaba bien, y lo único que lo arregla si no.
        _lastRegionKey = null;
        ApplyRegion();
    }

    internal void PositionNoteWindow(NoteWindow noteWindow)
    {
        int step = _coordinator.OpenNoteWindowCount % NoteWindowMaxCascadeSteps;

        var left = Left - noteWindow.Width - 12 - step * NoteWindowCascadeStep;
        var top = Top + step * NoteWindowCascadeStep;

        // Acotado al área de trabajo visible para que un escalón alto de la cascada (o un dock
        // anclado a la izquierda) no deje la ventana de nota parcial o totalmente fuera de
        // pantalla en una pantalla estrecha o baja.
        noteWindow.Left = Math.Max(left, _workingArea.X);
        noteWindow.Top = Math.Min(top, _workingArea.Y + _workingArea.Height - noteWindow.Height);
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        var existingCount = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorPalette.Colors[existingCount % NoteColorPalette.Colors.Length];
        _repository.Create(string.Empty, color, screenOrigin: "primary");
        _coordinator.RefreshAll();
    }
}

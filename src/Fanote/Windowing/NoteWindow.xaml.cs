using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Fanote.Core;
using Fanote.Interop;
using Fanote.Resources;
using Microsoft.Win32;

namespace Fanote.Windowing;

public partial class NoteWindow : Window
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>El tamaño inicial real de esta ventana, capturado desde el XAML. Es el tamaño al que
    /// vuelve "Restaurar tamaño" y el mínimo al que puede encogerse el ajuste automático.</summary>
    private readonly double _initialWidth;
    private readonly double _initialHeight;

    /// <summary>Tope absoluto de alto para el ajuste automático — no tiene sentido que una nota crezca
    /// hasta ocupar la pantalla entera. El monitor real de la nota (no <c>SystemParameters.WorkArea</c>,
    /// que siempre da el principal) actúa como límite aparte para pantallas pequeñas, por si este valor
    /// fuera mayor que el propio monitor.</summary>
    private const double MaxAutoFitHeight = 700;

    private readonly Note _note;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly AppSettings? _settings;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    /// <summary>Si esta nota se ha redimensionado a mano (arrastrando el borde) durante esta apertura
    /// concreta — a partir de ahí, <see cref="FitHeightToContent"/> deja de tocar el alto hasta que se
    /// pulse "Restaurar tamaño". No se guarda en ningún sitio: cada apertura empieza en modo ajuste
    /// automático de nuevo, sea cual sea el alto con el que se guardó la última vez (ver
    /// <see cref="SavePlacementOnce"/> — ese alto puede venir tanto de un ajuste automático anterior
    /// como de un arrastre real, y no hay forma barata de distinguirlos entre sesiones sin tocar el
    /// esquema; distinguirlo solo dentro de esta sesión es lo que de verdad importa en la práctica).</summary>
    private bool _hasManualSize;

    /// <summary>Evita que el propio cambio de tamaño de <see cref="FitHeightToContent"/> o
    /// <see cref="OnRestoreSizeClick"/> se malinterprete como un arrastre manual en el
    /// <c>SizeChanged</c> de más abajo.</summary>
    private bool _isAutoResizing;
    private bool _isConstrainingToMonitor;
    private string? _placementMonitorKey;

    public NoteWindow(Note note, NotesRepository repository, AppCoordinator coordinator, AppSettings? settings = null)
    {
        InitializeComponent();
        _initialWidth = Width;
        _initialHeight = Height;
        _note = note;
        _repository = repository;
        _coordinator = coordinator;
        _settings = settings;

        ApplyColor(note.Color);
        PopulateColorSwatches();

        // Una nota que no está activa se abre desde "Gestionar notas": lo único que se le puede
        // hacer es devolverla, así que archivar y papelera no tienen sentido ahí.
        if (note.State != NoteState.Active)
        {
            ArchiveButton.Visibility = Visibility.Collapsed;
            TrashButton.Visibility = Visibility.Collapsed;
            RestoreButton.Visibility = Visibility.Visible;
        }

        UpdatePinButton();
        UpdateReminderButton();

        // La nota es un solo texto; la cabecera edita su primera línea y el cuerpo el resto.
        var (title, body) = NoteText.Split(note.Text);
        TitleBox.Text = title;
        TextBody.Text = body;
        Title = NoteTitleHelper.GetTitle(note.Text); // el de la ventana: barra de tareas, Alt+Tab
        Header.Background = Brushes.Transparent; // la cabecera comparte el fondo de la nota

        _autosaveTimer = new DispatcherTimer { Interval = AutosaveDelay };
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer.Stop();
            Flush();
            // Barato y aprovecha un temporizador que ya existe, en vez de uno nuevo solo para esto:
            // no cubre el caso de dejar la nota abierta sin tocarla durante todo el plazo (el
            // autoguardado no se dispara sin editar), pero sí el caso normal de seguir trabajando
            // en la nota mientras una tarea de antes va venciendo.
            PruneExpiredTasks();
        };

        Loaded += (_, _) =>
        {
            // Una nota vacía empieza por el título, que es lo primero que se escribe; una que ya
            // tiene algo, por una línea nueva en blanco debajo del cuerpo, no encima del último
            // carácter — así reabrir una nota para añadir algo no continúa sin querer la última
            // palabra que se había dejado escrita.
            if (string.IsNullOrEmpty(note.Text))
            {
                TitleBox.Focus();
                return;
            }

            // Si la última línea ya es una tarea vacía ("☐ " sin nada detrás), ya es en sí misma
            // una línea en blanco esperando texto — igual que pedía el usuario: si se dejó una
            // casilla puesta y sin rellenar, el cursor tiene que ir justo detrás de ella, no una
            // línea más abajo, que sería un hueco de más antes de poder escribir la tarea.
            var lastLine = TaskLines.LineContaining(TextBody.Text, TextBody.Text.Length);

            // La línea en blanco es solo de trabajo, para que el cursor tenga dónde ir: si se
            // cierra la nota sin escribir nada más, no debe guardarse de más. Por eso se deshace
            // el "hay cambios sin guardar" que el propio TextChanged dispara al añadirla — Flush
            // no tiene nada real que guardar todavía.
            if (TextBody.Text.Length > 0 && !TextBody.Text.EndsWith('\n') && !TaskLines.IsEmptyTaskLine(lastLine))
            {
                TextBody.Text += "\r\n";
                _hasPendingEdit = false;
                _autosaveTimer.Stop();
            }

            TextBody.Focus();
            TextBody.CaretIndex = TextBody.Text.Length;
            TextBody.ScrollToEnd();
        };

        // Al abrir es el único momento garantizado en el que se revisa esta nota concreta aparte
        // del arranque de la app (que solo barre todas las notas una vez, ver App.xaml.cs) — si el
        // plazo venció mientras la nota estaba cerrada, aquí es donde se nota.
        Loaded += (_, _) => PruneExpiredTasks();

        // Una nota que ya traía más o menos texto del que le corresponde a su alto guardado (escrito
        // antes de que existiera el ajuste automático, o el alto de la última sesión ya no encaja)
        // se ajusta también al abrirla, no solo al seguir escribiendo -- así "el alto refleja lo que
        // hay escrito" es cierto siempre, no solo a partir de ahora.
        Loaded += (_, _) =>
        {
        ConstrainToCurrentMonitor(fitContent: true);
            _placementMonitorKey = GetCurrentMonitorKey();

            // Enganchado aquí y no antes en el constructor: AppCoordinator.TryRestorePlacement fija
            // Width/Height (para restaurar la posición guardada) antes de Show(), y eso dispararía
            // SizeChanged con _isAutoResizing todavía en false -- marcaría la nota como "tamaño
            // manual" nada más abrirla, antes incluso de que se viera. Un arrastre real del borde
            // (el único SizeChanged que puede llegar a partir de aquí sin pasar por _isAutoResizing)
            // fija el tamaño para el resto de esta apertura hasta "Restaurar tamaño".
            SizeChanged += (_, _) =>
            {
                if (!_isAutoResizing && !_isConstrainingToMonitor) _hasManualSize = true;
            };
        };

        LocationChanged += (_, _) => OnLocationChanged();

        TextBody.TextChanged += (_, _) =>
        {
            OnEdited();
            // El texto puede cambiar sin que el ratón se mueva -- por ejemplo, una tarea que se
            // autoborra sola (ver PruneExpiredTasks) mientras el cursor sigue quieto encima de la
            // casilla que acaba de desaparecer. Sin esto, el resaltado se quedaba "flotando" en su
            // última posición conocida hasta el siguiente movimiento real del ratón, en vez de
            // recolocarse o desaparecer al momento. UpdateLayout fuerza el nuevo layout antes de
            // preguntar por rectángulos de caracteres, que si no reflejarían el texto anterior.
            TextBody.UpdateLayout();
            RefreshCheckboxHoverAfterTextChange();
            FitHeightToContent();
            ClampToWorkArea();
        };
        TitleBox.TextChanged += (_, _) => OnEdited();

        Closing += SavePlacementOnce;

        // Antes del handler de abajo: si este cancela, el guardado del otro corre igual en la
        // segunda pasada, y Flush es idempotente.
        Closing += OnClosingWithAnimation;

        Closing += (_, _) =>
        {
            Flush();
            _coordinator.RefreshAll();
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };

        TextBody.PreviewKeyDown += OnBodyKeyDown;
        TextBody.PreviewMouseLeftButtonDown += OnBodyMouseDown;
        TextBody.MouseMove += OnBodyMouseMove;
        TextBody.MouseLeave += OnBodyMouseLeave;
        TitleBox.PreviewKeyDown += OnTitleKeyDown;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };
    }

    /// <summary>
    /// Guarda dónde y de qué tamaño está la nota justo antes de cerrarla, para que la próxima vez
    /// reaparezca ahí en esa misma pantalla (ver <see cref="AppCoordinator.OpenOrActivateNote"/>).
    /// Se guarda contra el monitor donde está el <b>centro</b> de la nota ahora mismo
    /// (<see cref="MonitorLookup.DeviceNameAt"/>), no el monitor desde el que se abrió — el usuario
    /// puede haberla arrastrado a otra pantalla mientras estaba abierta. Ya no hace falta protegerse
    /// de que <c>Closing</c> se dispare dos veces: desde que abrir y cerrar dejaron de animar
    /// <c>Left</c>/<c>Top</c> (ver <see cref="PlayOpenAnimation"/> y <see cref="OnClosingWithAnimation"/>),
    /// esos valores no cambian entre la primera y la segunda pasada — guardar dos veces sería
    /// idéntico.
    /// </summary>
    private void SavePlacementOnce(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SavePlacementForCurrentMonitor();
    }

    private void OnLocationChanged()
    {
        if (!IsLoaded || _isConstrainingToMonitor || WindowState != WindowState.Normal) return;

        if (NativeMethods.IsLeftButtonDown())
        {
            ClampDuringMonitorDrag();
            return;
        }

        ConstrainToCurrentMonitor(fitContent: !_hasManualSize);
        var monitorKey = GetCurrentMonitorKey();
        if (monitorKey is null || monitorKey == _placementMonitorKey) return;

        // La pertenencia cambia cuando el centro cruza al otro monitor. Guardarlo en ese momento
        // hace que la nota ya pertenezca a la segunda pantalla aunque siga abierta o Fanote se
        // cierre de forma inesperada antes del siguiente cierre normal.
        _placementMonitorKey = monitorKey;
        SavePlacementForCurrentMonitor();
    }

    private void ClampDuringMonitorDrag()
    {
        var position = MonitorBounds.ClampIntoMonitorUnion(
            Left,
            Top,
            Width,
            Height,
            MonitorEnumerator.EnumerateMonitors());

        if (Math.Abs(position.Left - Left) < 0.5 && Math.Abs(position.Top - Top) < 0.5) return;

        bool wasConstraining = _isConstrainingToMonitor;
        _isConstrainingToMonitor = true;
        Left = position.Left;
        Top = position.Top;
        _isConstrainingToMonitor = wasConstraining;
    }

    private string? GetCurrentMonitorKey() =>
        MonitorLookup.DeviceNameAt(Left, Top, Width, Height, MonitorEnumerator.EnumerateMonitors());

    private void SavePlacementForCurrentMonitor()
    {
        if (_settings is not { RememberNotePositions: true }) return;

        var monitorKey = GetCurrentMonitorKey();
        if (monitorKey is null) return;

        _repository.SavePlacement(_note.Id, monitorKey, Left, Top, Width, Height);
        _placementMonitorKey = monitorKey;
    }

    /// <summary>
    /// Tope real de alto para el ajuste automático: el menor entre <see cref="MaxAutoFitHeight"/> y
    /// el <see cref="MonitorInfo.WorkArea"/> real del monitor donde está el centro de la nota ahora
    /// mismo (no <c>SystemParameters.WorkArea</c>, que siempre da el monitor principal del sistema —
    /// mismo aviso que ya deja <c>SettingsWindow</c>). Si el centro no cae en ningún monitor conocido,
    /// se queda solo con <see cref="MaxAutoFitHeight"/>.
    /// </summary>
    private void ConstrainToCurrentMonitor(bool fitContent)
    {
        if (_isConstrainingToMonitor || !IsLoaded || WindowState != WindowState.Normal) return;

        var monitors = MonitorEnumerator.EnumerateMonitors();
        var monitor = MonitorLookup.MonitorAt(Left, Top, Width, Height, monitors);
        if (monitor is not { } m) return;

        _isConstrainingToMonitor = true;
        MaxWidth = Math.Max(MinWidth, m.WorkArea.Width);
        // MaxHeight must describe the real resize/snap limit, not the smaller limit used only by
        // automatic content fitting. FancyZones and Win+Arrow read this WPF property and would
        // otherwise refuse to give the note the full height of a zone.
        MaxHeight = Math.Max(MinHeight, m.WorkArea.Height);

        if (fitContent) FitHeightToContent();
        ClampToMonitorUnion(monitors);
        _isConstrainingToMonitor = false;
    }

    private void ClampToWorkArea()
    {
        var monitors = MonitorEnumerator.EnumerateMonitors();
        var monitor = MonitorLookup.MonitorAt(Left, Top, Width, Height, monitors);
        if (monitor is not null) ClampToMonitorUnion(monitors);
    }

    private void ClampToMonitorUnion(IReadOnlyList<MonitorInfo> monitors)
    {
        if (WindowState != WindowState.Normal) return;

        var position = MonitorBounds.ClampIntoMonitorUnion(Left, Top, Width, Height, monitors);

        if (Math.Abs(position.Left - Left) < 0.5 && Math.Abs(position.Top - Top) < 0.5) return;

        bool wasConstraining = _isConstrainingToMonitor;
        _isConstrainingToMonitor = true;
        Left = position.Left;
        Top = position.Top;
        _isConstrainingToMonitor = wasConstraining;
    }

    /// <summary>
    /// Ajusta el alto al contenido actual — crece si no cabe, encoge si sobra sitio, nunca por debajo
    /// de <see cref="_initialHeight"/> ni por encima de <c>MaxHeight</c> (puesto por
    /// <see cref="ConstrainToCurrentMonitor"/>). Si <see cref="_hasManualSize"/>, no toca el alto
    /// (un arrastre real del usuario en esta apertura tiene la última palabra hasta "Restaurar
    /// tamaño"), pero sí deja la barra de scroll visible de verdad: en modo manual el ajuste
    /// automático no va a corregir nada, así que hace falta poder desplazarse.
    ///
    /// <c>Height - TextBody.ViewportHeight</c> es todo lo que NO es el propio cuerpo de texto
    /// (cabecera, márgenes) — sumado al alto real del contenido (<c>ExtentHeight</c>) da el alto de
    /// ventana que hace falta, sin tener que conocer esos números a mano.
    ///
    /// La barra de scroll de <c>TextBody</c> se mantiene oculta (<c>Hidden</c>, no <c>Auto</c>)
    /// mientras el modo automático pueda seguir corrigiendo el alto: cualquier desbordamiento en ese
    /// caso es transitorio por definición (el propio ajuste de aquí lo resuelve en el acto, cada vez
    /// que el texto cambia), así que mostrarla sería solo un parpadeo de un fotograma mientras el
    /// nuevo alto de la ventana termina de aplicarse a nivel de Windows (un <c>UpdateLayout()</c> del
    /// lado de WPF no basta para forzar ese redimensionado real a tiempo). Solo se deja <c>Auto</c>
    /// cuando el desbordamiento es real y permanente: ya en modo manual, o ya en el tope de
    /// <c>MaxHeight</c> sin que el contenido quepa ahí tampoco.
    /// </summary>
    private void FitHeightToContent()
    {
        if (_hasManualSize)
        {
            TextBody.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            return;
        }

        double chromeHeight = Height - TextBody.ViewportHeight;
        double neededHeight = TextBody.ExtentHeight + chromeHeight;
        double autoFitMaxHeight = Math.Max(MinHeight, Math.Min(
            MaxAutoFitHeight,
            MonitorLookup.MonitorAt(Left, Top, Width, Height, MonitorEnumerator.EnumerateMonitors())
                ?.WorkArea.Height * 0.9 ?? MaxAutoFitHeight));
        double desired = Math.Clamp(neededHeight, _initialHeight, autoFitMaxHeight);

        TextBody.VerticalScrollBarVisibility = neededHeight > autoFitMaxHeight + 0.5
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Hidden;

        if (Math.Abs(desired - Height) < 0.5) return;

        _isAutoResizing = true;
        Height = desired;
        _isAutoResizing = false;

        TextBody.UpdateLayout();
        ClampToWorkArea();
    }

    /// <summary>
    /// Guarda el texto pendiente y la posición de esta nota YA, sin pasar por el ciclo normal de
    /// cierre (que cancela el primer intento para reproducir la animación de salida y solo cierra
    /// de verdad al terminar, ver <see cref="OnClosingWithAnimation"/>). Windows no espera a que esa
    /// animación termine al apagar o reiniciar el equipo — <c>App.OnSessionEnding</c> llama aquí
    /// para cada nota abierta en cuanto llega el aviso de cierre de sesión, en vez de arriesgarse a
    /// que el proceso se corte antes de que la animación complete y dispare el guardado real.
    /// </summary>
    internal void FlushForShutdown()
    {
        Flush();
        SavePlacementOnce(this, new System.ComponentModel.CancelEventArgs());
    }

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(140);

    private bool _closingAnimationDone;

    /// <summary>
    /// Fundido + un ligero crecimiento desde el 95% del tamaño final, siempre ya en su posición
    /// definitiva. Toda nota se abre igual, tanto si es la primera vez (recién sacada del dock)
    /// como si recuerda su última posición (ver <see cref="AppCoordinator.TryRestorePlacement"/>):
    /// antes esta última aparecía de golpe mientras que una nota nueva se deslizaba desde su
    /// pestaña, y esa inconsistencia — más el propio deslizamiento largo por la pantalla, un
    /// candidato más a verse brusco — es lo que esta animación reemplaza. <c>RenderTransformOrigin</c>
    /// centrado para que crezca hacia dentro/fuera de su propio centro, no desde una esquina.
    /// </summary>
    internal void PlayOpenAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var content = (UIElement)Content;
        content.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.95, 0.95);
        content.RenderTransform = scale;

        var duration = new Duration(OpenDuration);
        IEasingFunction Ease() => new QuinticEase { EasingMode = EasingMode.EaseOut };

        content.Opacity = 0;
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = Ease() });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.95, 1, duration) { EasingFunction = Ease() });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.95, 1, duration) { EasingFunction = Ease() });
    }

    /// <summary>Desplaza la nota suavemente al elegir otra plantilla de disposiciÃ³n.</summary>
    internal void MoveToLayoutPosition(double left, double top)
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);

        if (!SystemParameters.ClientAreaAnimation || !IsVisible ||
            (Math.Abs(Left - left) < 0.5 && Math.Abs(Top - top) < 0.5))
        {
            Left = left;
            Top = top;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(220));
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var leftAnimation = new DoubleAnimation(Left, left, duration) { EasingFunction = ease };
        var topAnimation = new DoubleAnimation(Top, top, duration) { EasingFunction = ease };
        topAnimation.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = left;
            Top = top;
        };
        BeginAnimation(LeftProperty, leftAnimation);
        BeginAnimation(TopProperty, topAnimation);
    }

    private void StopLayoutPositionAnimation()
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
    }

    /// <summary>
    /// Al cerrar, fundido + encogimiento simétrico a <see cref="PlayOpenAnimation"/> en vez de
    /// desaparecer de golpe.
    ///
    /// Cerrar hay que cancelarlo y repetirlo al terminar la animación, porque no existe forma de
    /// aplazar un Close en WPF. <see cref="_closingAnimationDone"/> corta el bucle. Flush corre en
    /// las dos pasadas y es idempotente (mira <c>_hasPendingEdit</c>), así que no guarda dos veces.
    /// </summary>
    private void OnClosingWithAnimation(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closingAnimationDone || !SystemParameters.ClientAreaAnimation) return;

        _closingAnimationDone = true;
        e.Cancel = true;

        var content = (UIElement)Content;
        content.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = content.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        content.RenderTransform = scale;

        var duration = new Duration(CloseDuration);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };

        var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => Close();

        content.BeginAnimation(OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.95, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.95, duration) { EasingFunction = ease });
    }

    // --- Casillas de tarea ----------------------------------------------------------------------
    //
    // Las casillas son texto, no controles (ver Fanote.Core.TaskLines para el porqué). Aquí solo se
    // traducen gestos: un clic encima del glifo lo marca, Ctrl+L convierte la línea en tarea, y
    // Enter continúa la lista. Todo el cálculo vive en TaskLines, que es puro y está probado.

    /// <summary>
    /// Aplica un texto nuevo conservando la posición del cursor. Asignar <c>Text</c> lo manda al
    /// principio, y en un editor eso se nota como un salto: hay que reponerlo a mano.
    /// </summary>
    private void ReplaceBody(string text, int caret)
    {
        TextBody.Text = text;
        TextBody.CaretIndex = Math.Clamp(caret, 0, TextBody.Text.Length);
    }

    /// <summary>El texto completo de la nota: la cabecera y el cuerpo vueltos a unir.</summary>
    private string CurrentText => NoteText.Join(TitleBox.Text, TextBody.Text);

    private void OnEdited()
    {
        Title = NoteTitleHelper.GetTitle(CurrentText);
        _hasPendingEdit = true;
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    // El cursor tiene que poder cruzar entre los dos cuadros, o se sentirían dos cosas distintas en
    // vez de una nota. Enter y Abajo bajan al cuerpo; Arriba desde la primera línea del cuerpo sube
    // al título. **No** se implementa unir con Retroceso al principio del cuerpo: hacerlo bien exige
    // fusionar líneas, y hacerlo a medias (borrar sin más) se siente roto — al principio de un
    // cuadro de texto, que Retroceso no haga nada es lo que hace cualquier control de Windows.
    private void OnTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Down)
        {
            TextBody.Focus();
            TextBody.CaretIndex = 0;
            e.Handled = true;
        }
    }

    private void OnBodyKeyDown(object sender, KeyEventArgs e)
    {
        // Alt+Arriba/Alt+Abajo: sube o baja la línea del cursor, intercambiándola con la vecina
        // (ver Fanote.Core.LineMovement para el porqué de un atajo en vez de arrastrar dentro del
        // TextBox). Va antes que el resto de gestos de Arriba/Abajo para que tenga prioridad sobre
        // "cruzar al título" en la primera línea. Se marca Handled aunque no haya vecina (ya es la
        // primera o última línea): Alt+flecha no hace nada por defecto en un TextBox, así que no hay
        // comportamiento nativo al que dejar paso.
        if (e.Key is Key.Up or Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            var direction = e.Key == Key.Up ? LineDirection.Up : LineDirection.Down;
            if (LineMovement.Move(TextBody.Text, TextBody.CaretIndex, direction) is { } moved)
            {
                ReplaceBody(moved.Text, moved.Caret);
            }
            e.Handled = true;
            return;
        }

        // Arriba en la primera línea del cuerpo: el sitio de encima es el título.
        if (e.Key == Key.Up && TextBody.GetLineIndexFromCharacterIndex(TextBody.CaretIndex) == 0)
        {
            TitleBox.Focus();
            TitleBox.CaretIndex = TitleBox.Text.Length;
            e.Handled = true;
            return;
        }

        // Ctrl+L: convierte la línea en tarea, o le quita la casilla si ya lo era. Sobre una lista con
        // viñeta, la convierte en tarea en vez de apilar los dos prefijos (ver TaskLines.ToggleTaskLineAt).
        if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var (text, caret) = TaskLines.ToggleTaskLineAt(TextBody.Text, TextBody.CaretIndex);
            ReplaceBody(text, caret);
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+L: lo mismo que Ctrl+L pero para una lista con viñeta (→) en vez de una tarea —
        // atajo nuevo a propósito, no un ciclo sobre Ctrl+L, para no mezclar los dos significados.
        if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            var (text, caret) = BulletLines.ToggleBulletLineAt(TextBody.Text, TextBody.CaretIndex);
            ReplaceBody(text, caret);
            e.Handled = true;
            return;
        }

        // Tab: sobre una tarea o viñeta, sube un nivel de sangría. En una línea normal deja pasar el
        // Tab nativo (AcceptsTab="True" en el XAML), que inserta una tabulación literal — antes sacaba
        // el foco de TextBody sin ningún beneficio real en esta ventana.
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (ListIndent.Indent(TextBody.Text, TextBody.CaretIndex) is { } indented)
            {
                ReplaceBody(indented.Text, indented.Caret);
                e.Handled = true;
            }
        }

        // Mayús+Tab: baja un nivel. En una línea normal no se toca (queda la navegación de foco hacia
        // atrás de siempre, gesto raro mientras se escribe prosa).
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            if (ListIndent.Outdent(TextBody.Text, TextBody.CaretIndex) is { } outdented)
            {
                ReplaceBody(outdented.Text, outdented.Caret);
                e.Handled = true;
            }
        }

        // Enter al final de una tarea o una viñeta: la lista sigue sola. Cada clase decide si toca
        // continuar, terminar la lista (línea vacía) o no hacer nada — en ese último caso, Enter normal.
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (TaskLines.EnterContinuation(TextBody.Text, TextBody.CaretIndex) is { } taskResult)
            {
                ReplaceBody(taskResult.Text, taskResult.Caret);
                e.Handled = true;
            }
            else if (BulletLines.EnterContinuation(TextBody.Text, TextBody.CaretIndex) is { } bulletResult)
            {
                ReplaceBody(bulletResult.Text, bulletResult.Caret);
                e.Handled = true;
            }
        }
    }

    /// <summary>Una casilla de tarea localizada por un punto del ratón: dónde está el glifo (para
    /// marcarla) y el rectángulo que hay que resaltar (para el hover).</summary>
    private readonly record struct CheckboxZone(int GlyphIndex, System.Windows.Rect VisualRect);

    /// <summary>
    /// Busca la casilla de tarea "bajo" un punto, con una zona de clic más generosa que el propio
    /// glifo: toda la indentación de la línea más el glifo y el espacio que lo sigue cuentan como
    /// "la casilla", no solo el carácter exacto — el usuario pidió poder marcarla sin acertar el
    /// glifo a pixel. El rectángulo visual, en cambio, se ciñe al glifo + su espacio (lo que se ve),
    /// no a la indentación en blanco, para que el resaltado del hover no incluya hueco vacío.
    /// </summary>
    private CheckboxZone? FindCheckboxZoneAt(Point position)
    {
        int index = TextBody.GetCharacterIndexFromPoint(position, snapToText: true);
        if (index < 0) return null;

        var text = TextBody.Text;
        int lineStart = TaskLines.LineStart(text, index);
        var line = TaskLines.LineContaining(text, index);
        int glyph = TaskLines.GlyphIndex(line);
        if (glyph < 0) return null;

        int glyphIndex = lineStart + glyph;
        // El espacio detrás del glifo es opcional (ver TaskLines.PrefixLength): si el texto de la
        // tarea le va pegado sin espacio, la zona de clic no debe comerse su primera letra.
        int zoneEnd = glyphIndex + TaskLines.PrefixLength(line, glyph);
        if (index < lineStart || index > zoneEnd) return null;

        // GetRectFromCharacterIndex da rectángulos de CARET (la línea entera de alto/ancho de
        // avance, incluido el interlineado y el hueco lateral que el tipo de letra reserva
        // alrededor del glifo), no la caja visual de la tinta del propio carácter — un primer
        // intento que centraba un cuadrado de FontSize*1.3 dentro de ese rect de línea seguía
        // saliendo casi tan alto como la línea entera (18.2 de 18.62px), el doble de la tinta real
        // (9.9px), y el usuario lo vio "más grande... y desplazado" en cuanto lo probó.
        //
        // Los números de abajo (0.093/0.82 de ancho, 0.282/0.53 de alto) salen de medir la tinta
        // real del glifo ☐ en Segoe UI Variable Text a 14px píxel a píxel (renderizado en
        // aislamiento y escaneado por color, mismo tipo de técnica que ya se usó para decidir ☒
        // frente a ☑ — ver docs/STATUS.md), no de una fórmula general: si el glifo o el tamaño de
        // fuente cambiaran alguna vez, habría que volver a medir. Con FontSize=14 esto da una caja
        // de ~13x13px, ajustada al cuadrado visible más 3px de aire alrededor.
        var glyphRect = TextBody.GetRectFromCharacterIndex(glyphIndex);

        if (position.Y < glyphRect.Top || position.Y > glyphRect.Top + glyphRect.Height)
        {
            // GetCharacterIndexFromPoint con snapToText:true ignora la distancia vertical: un clic
            // muy por debajo de la última línea (en el hueco vacío del TextBox) igualmente "cae" en
            // el carácter más cercano de esa línea, así que sin esto se podía marcar una tarea desde
            // bastante más abajo, sin estar encima de verdad — reportado por el usuario tras
            // probarlo. Se descarta cualquier punto que no caiga dentro del alto real de esa línea.
            return null;
        }

        var afterGlyphRect = TextBody.GetRectFromCharacterIndex(glyphIndex + 1);
        double caretWidth = afterGlyphRect.Left - glyphRect.Left;

        const double padding = 3;
        double tightLeft = glyphRect.Left + caretWidth * 0.093;
        double tightWidth = caretWidth * 0.82;
        double tightTop = glyphRect.Top + glyphRect.Height * 0.282;
        double tightHeight = glyphRect.Height * 0.53;

        var visualRect = new System.Windows.Rect(
            tightLeft - padding / 2, tightTop - padding / 2,
            tightWidth + padding, tightHeight + padding);

        return new CheckboxZone(glyphIndex, visualRect);
    }

    private void OnBodyMouseDown(object sender, MouseButtonEventArgs e)
    {
        var zone = FindCheckboxZoneAt(e.GetPosition(TextBody));
        if (zone is not { } z) return;

        var toggled = TaskLines.ToggleCheckboxAt(TextBody.Text, z.GlyphIndex);
        if (toggled is null) return;

        RecordTaskToggle(toggled, z.GlyphIndex);

        int caret = TextBody.CaretIndex;
        ReplaceBody(toggled, caret);

        // Handled: el clic ya ha hecho su trabajo, y dejarlo pasar movería además el cursor al
        // sitio donde se pulsó, que no es lo que se pretendía al marcar una casilla.
        TextBody.Focus();
        e.Handled = true;
    }

    /// <summary>Resalta la casilla bajo un punto (ver <see cref="FindCheckboxZoneAt"/>) y cambia el
    /// cursor a una mano, para que se note que es clicable — antes solo se veía el cursor de texto
    /// normal, indistinguible de pasar por cualquier otra parte de la nota. Recibe el punto en vez
    /// de leerlo de un evento de ratón porque también se llama cuando el texto cambia sin que el
    /// ratón se haya movido (ver el <c>TextChanged</c> del constructor).</summary>
    private void UpdateCheckboxHover(Point position)
    {
        var zone = FindCheckboxZoneAt(position);
        if (zone is { } z)
        {
            TaskHoverHighlight.Visibility = Visibility.Visible;
            Canvas.SetLeft(TaskHoverHighlight, z.VisualRect.Left);
            Canvas.SetTop(TaskHoverHighlight, z.VisualRect.Top);
            TaskHoverHighlight.Width = z.VisualRect.Width;
            TaskHoverHighlight.Height = z.VisualRect.Height;
            TextBody.Cursor = Cursors.Hand;
        }
        else
        {
            TaskHoverHighlight.Visibility = Visibility.Collapsed;
            TextBody.Cursor = Cursors.IBeam;
        }
    }

    private void OnBodyMouseMove(object sender, MouseEventArgs e) => UpdateCheckboxHover(e.GetPosition(TextBody));

    /// <summary>
    /// Se llama tras cualquier cambio de texto (ver el <c>TextChanged</c> del constructor), pero
    /// solo para corregir o esconder un resaltado que YA estaba visible — nunca para encenderlo de
    /// la nada. Sin esta distinción, convertir una línea en tarea con Ctrl+L (o cualquier otro
    /// cambio por teclado) podía hacer aparecer el resaltado de golpe si el ratón, quieto en
    /// cualquier sitio, resultaba estar sobre la casilla recién creada — pareciendo una casilla ya
    /// "seleccionada" sin que nadie la hubiera tocado con el ratón (reportado por el usuario,
    /// 2026-09-11). Solo un movimiento real del ratón (<see cref="OnBodyMouseMove"/>) puede pasar el
    /// resaltado de oculto a visible; un cambio de texto únicamente puede apagarlo o recolocarlo si
    /// ya estaba encendido.
    /// </summary>
    private void RefreshCheckboxHoverAfterTextChange()
    {
        if (TaskHoverHighlight.Visibility != Visibility.Visible) return;
        UpdateCheckboxHover(Mouse.GetPosition(TextBody));
    }

    private void OnBodyMouseLeave(object sender, MouseEventArgs e)
    {
        TaskHoverHighlight.Visibility = Visibility.Collapsed;
        TextBody.Cursor = Cursors.IBeam;
    }

    /// <summary>
    /// Si el ajuste de borrar tareas completadas está activo, guarda o borra cuándo se marcó esta
    /// línea concreta como hecha (ver <see cref="Fanote.Core.TaskCompletion"/>). El hash se calcula
    /// sobre el texto sin el glifo, así que desmarcar y volver a marcar la misma tarea más tarde
    /// reinicia el reloj en vez de arrastrar el momento en que se marcó la primera vez.
    /// </summary>
    private void RecordTaskToggle(string afterText, int glyphIndex)
    {
        if (_settings is not { AutoHideCompletedTasks: true }) return;

        var line = TaskLines.LineContaining(afterText, glyphIndex);
        var hash = TaskCompletion.HashLine(line);
        bool nowChecked = afterText[glyphIndex] == TaskLines.Checked || afterText[glyphIndex] == TaskLines.CheckedAlternate;

        if (nowChecked) _repository.RecordTaskCompletion(_note.Id, hash, DateTimeOffset.UtcNow);
        else _repository.ClearTaskCompletion(_note.Id, hash);
    }

    /// <summary>
    /// Borra del cuerpo las tareas marcadas cuyo plazo ya venció (ver <see cref="Fanote.Core.TaskCompletion"/>).
    /// Solo opera sobre <c>TextBody</c>, no sobre el texto completo de la nota: igual que Ctrl+L o el
    /// clic en la casilla, las tareas solo importan en el cuerpo — el título casi nunca lo es.
    /// </summary>
    private void PruneExpiredTasks()
    {
        if (_settings is not { AutoHideCompletedTasks: true } settings) return;

        var completions = _repository.GetTaskCompletions(_note.Id);
        if (completions.Count == 0) return;

        var result = TaskCompletion.Prune(TextBody.Text, completions, DateTimeOffset.UtcNow, settings.AutoHideCompletedTasksDelay);
        foreach (var hash in result.HashesToClear)
        {
            _repository.ClearTaskCompletion(_note.Id, hash);
        }

        if (result.Changed)
        {
            ReplaceBody(result.Text, Math.Min(TextBody.CaretIndex, result.Text.Length));
            OnEdited();
        }
    }

    /// <summary>El asa de la cabecera es un elemento normal (no zona de "caption" de WindowChrome) para
    /// que el cursor pueda cambiar a mano encima — así que el arrastre en sí hay que dispararlo aquí,
    /// en vez de dejar que WindowChrome lo maneje solo como con el resto de la cabecera.</summary>
    private void OnGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            StopLayoutPositionAnimation();
            try
            {
                DragMove();
            }
            finally
            {
                // Durante DragMove no se limita la posición, para que la nota pueda cruzar el
                // borde entre monitores. Al soltarla se recalculan el monitor propietario, los
                // límites de tamaño y la posición visible, y se guarda el nuevo monitor.
                OnLocationChanged();
            }
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        ToggleMaximized();

    private void ToggleMaximized()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            ConstrainToCurrentMonitor(fitContent: !_hasManualSize);
            return;
        }

        // El tope de ajuste automÃ¡tico protege el tamaÃ±o normal de una nota, pero no debe impedir
        // que el usuario elija ocupar toda la pantalla de forma explÃ­cita.
        MaxHeight = double.PositiveInfinity;
        MaxWidth = double.PositiveInfinity;
        WindowState = WindowState.Maximized;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeGlyph is not null)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaxHeight = double.PositiveInfinity;
                MaxWidth = double.PositiveInfinity;
            }
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
        if (MaximizeButton is not null)
        {
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized
                ? Strings.RestoreWindowTooltip
                : Strings.MaximizeWindowTooltip;
        }
    }

    private void OnMenuPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Popup.StaysOpen=False closes the popup during the button's mouse-down routed event. Handle
        // the second click before that outside-click logic runs, otherwise it immediately reopens
        // in OnMenuClick and the ellipsis appears to be broken.
        if (!ActionsPopup.IsOpen) return;

        ActionsPopup.IsOpen = false;
        e.Handled = true;
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (!e.Handled) ActionsPopup.IsOpen = true;
    }

    /// <summary>Lo mismo que Ctrl+L, para quien no conoce el atajo (que es casi todo el mundo).</summary>
    private void OnTaskClick(object sender, RoutedEventArgs e)
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt(TextBody.Text, TextBody.CaretIndex);
        ReplaceBody(text, caret);
        ActionsPopup.IsOpen = false;
        TextBody.Focus();
    }

    /// <summary>Lo mismo que Ctrl+Shift+L, para quien no conoce el atajo.</summary>
    private void OnBulletClick(object sender, RoutedEventArgs e)
    {
        var (text, caret) = BulletLines.ToggleBulletLineAt(TextBody.Text, TextBody.CaretIndex);
        ReplaceBody(text, caret);
        ActionsPopup.IsOpen = false;
        TextBody.Focus();
    }

    /// <summary>Devuelve la ventana al tamaño inicial con el que se creó, sin autoagrandarla en ese
    /// mismo clic. El ajuste automático queda reactivado para los siguientes cambios de texto.</summary>
    private void OnRestoreSizeClick(object sender, RoutedEventArgs e)
    {
        _hasManualSize = false;

        _isAutoResizing = true;
        Width = _initialWidth;
        Height = _initialHeight;
        _isAutoResizing = false;

        // Al volver al tamaño original el contenido puede dejar de caber. Medimos con la barra
        // oculta para no contar su propio ancho y la mostramos solo si el desbordamiento es real.
        TextBody.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        TextBody.UpdateLayout();
        TextBody.VerticalScrollBarVisibility = TextBody.ExtentHeight > TextBody.ViewportHeight + 0.5
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Hidden;
        ActionsPopup.IsOpen = false;
    }

    /// <summary>
    /// Quita o devuelve el "siempre encima" de esta nota.
    ///
    /// Es por ventana y no se guarda: al volver a abrirla vuelve a estar fijada, que es el
    /// comportamiento de siempre y lo que se espera de un post-it. El interruptor existe para el
    /// caso concreto de dejar una nota abierta mientras trabajas en otra cosa, donde tenerla encima
    /// de todo estorba. Persistirlo exigiría una columna nueva en la tabla Note — la que sí tiene
    /// datos de verdad del usuario — y no compensa hasta saber si alguien lo usa así (ver
    /// docs/ROADMAP.md).
    /// </summary>
    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinButton();
        ActionsPopup.IsOpen = false;
    }

    /// <summary>
    /// El botón dice qué está pasando ahora, no qué hará al pulsarlo, y debajo se explica en una
    /// línea: "siempre encima" a secas no dice nada a quien no conoce el concepto — el usuario
    /// preguntó literalmente qué hacía.
    /// </summary>
    private void UpdatePinButton()
    {
        PinButton.Content = Topmost ? Strings.PinnedOn : Strings.PinnedOff;
        PinHint.Text = Topmost ? Strings.PinnedOnHint : Strings.PinnedOffHint;
    }

    private void Flush()
    {
        if (!_hasPendingEdit) return;
        _hasPendingEdit = false;
        _repository.UpdateText(_note.Id, CurrentText);
    }

    private void ApplyColor(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        var rim = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.RimFor(color))!;

        Background = brush;
        TextBody.Background = brush;
        WindowRim.BorderBrush = rim;

        // El troquelado va en el tono oscuro del propio hue — igual que la pestaña del dock de la
        // que viene, que es lo que sigue diciendo que esta cabecera es esa pestaña.
        Perforation.Stroke = rim;
    }

    private void PopulateColorSwatches()
    {
        foreach (var color in NoteColorPalette.Colors)
        {
            var swatch = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(color)!,
                Width = 22,
                Height = 22,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(5),
                // Tinta, no negro puro: es el mismo anillo sobre seis pasteles que comparten
                // claridad, y el negro absoluto sería el único valor de la ventana sin relación de
                // hue con el resto.
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.Ink)!,
                Cursor = Cursors.Hand,
                Tag = color,
                // La marca de selección vive dentro, no en el borde. El color seleccionado es por
                // definición el de la nota, así que su pastilla se funde con el fondo y el anillo
                // solo dibujaba un cuadrado vacío: parecía un hueco, no la opción activa. Un tick
                // se ve igual coincida o no el color.
                Child = new TextBlock
                {
                    Text = "\u2713",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)new BrushConverter().ConvertFromString(
                        NoteColorPalette.LabelFor(color))!,
                    IsHitTestVisible = false
                }
            };
            ApplySwatchSelection(swatch, color == _note.Color);
            swatch.MouseLeftButtonUp += OnColorSwatchClick;
            ColorSwatches.Children.Add(swatch);
        }
    }

    private static void ApplySwatchSelection(Border swatch, bool selected)
    {
        swatch.BorderThickness = new Thickness(selected ? 2 : 0);
        if (swatch.Child is UIElement tick)
        {
            tick.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnColorSwatchClick(object sender, MouseButtonEventArgs e)
    {
        var color = (string)((Border)sender).Tag;
        if (color == _note.Color) return;

        _note.Color = color;
        _repository.SetColor(_note.Id, color);
        ApplyColor(color);

        foreach (Border swatch in ColorSwatches.Children)
        {
            ApplySwatchSelection(swatch, (string)swatch.Tag == color);
        }

        _coordinator.RefreshAll();
        ActionsPopup.IsOpen = false;
    }

    private void OnCustomColorClick(object sender, RoutedEventArgs e)
    {
        var color = CustomColorWindow.Show(this, _note.Color);
        if (color is null) return;

        _note.Color = color;
        _repository.SetColor(_note.Id, color);
        ApplyColor(color);
        _coordinator.RefreshAll();
        ActionsPopup.IsOpen = false;
    }

    private void OnReminderMenuClick(object sender, RoutedEventArgs e)
    {
        ReminderPanel.Visibility = ReminderPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnReminderPresetClick(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        var dueAt = sender == ReminderInOneHourButton ? ReminderPresets.InOneHour(now)
            : sender == ReminderTonightButton ? ReminderPresets.Tonight(now)
            : ReminderPresets.TomorrowMorning(now);

        SaveReminder(dueAt);
    }

    private void OnReminderSaveClick(object sender, RoutedEventArgs e)
    {
        if (ReminderCalendar.SelectedDate is not { } date) return;
        if (!int.TryParse(ReminderHourBox.Text, out int hour) || hour is < 0 or > 23) return;
        if (!int.TryParse(ReminderMinuteBox.Text, out int minute) || minute is < 0 or > 59) return;

        var local = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeOffset.Now.Offset);
        SaveReminder(local);
    }

    private void SaveReminder(DateTimeOffset dueAt)
    {
        _repository.SetReminder(_note.Id, dueAt);
        ReminderPanel.Visibility = Visibility.Collapsed;
        UpdateReminderButton();
        _coordinator.RefreshAll(); // para que el badge del dock (Task 5) se actualice ya
    }

    private void OnReminderClearClick(object sender, RoutedEventArgs e)
    {
        _repository.ClearReminder(_note.Id);
        ReminderPanel.Visibility = Visibility.Collapsed;
        UpdateReminderButton();
        _coordinator.RefreshAll();
    }

    private void UpdateReminderButton()
    {
        var dueAt = _repository.GetReminder(_note.Id);
        ReminderButton.Content = dueAt is { } due ? Strings.ReminderSet(due.ToLocalTime()) : Strings.ReminderMenuEntry;
        ReminderClearButton.Visibility = dueAt is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Exporta el texto en vivo de la nota (título+cuerpo, tal como están en pantalla ahora mismo,
    /// no lo último guardado) — ver <see cref="MarkdownExport"/> para la conversión.
    /// </summary>
    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        ActionsPopup.IsOpen = false;
        var text = NoteText.Join(TitleBox.Text, TextBody.Text);

        var dialog = new SaveFileDialog
        {
            FileName = MarkdownExport.SuggestedFileName(text),
            Filter = Strings.MarkdownFileFilter,
            DefaultExt = ".md"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, MarkdownExport.ToMarkdown(text));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.UnexpectedErrorMessage(ex.Message), Strings.UnexpectedErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        Flush();
        _repository.SetState(_note.Id, NoteState.Archived);
        _coordinator.RefreshAll();
        Close();
    }

    private void OnTrashClick(object sender, RoutedEventArgs e)
    {
        _hasPendingEdit = false; // discard any pending edit — the note is being trashed, not saved
        _repository.SetState(_note.Id, NoteState.Trashed);
        _coordinator.RefreshAll();
        Close();
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        Flush();
        _repository.SetState(_note.Id, NoteState.Active);
        _coordinator.RefreshAll();
        Close();
    }
}

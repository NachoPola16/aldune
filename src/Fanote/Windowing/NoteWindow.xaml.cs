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

namespace Fanote.Windowing;

public partial class NoteWindow : Window
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly Note _note;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly AppSettings? _settings;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    public NoteWindow(Note note, NotesRepository repository, AppCoordinator coordinator, AppSettings? settings = null)
    {
        InitializeComponent();
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

        // La nota es un solo texto; la cabecera edita su primera línea y el cuerpo el resto.
        var (title, body) = NoteText.Split(note.Text);
        TitleBox.Text = title;
        TextBody.Text = body;
        Title = NoteTitleHelper.GetTitle(note.Text); // el de la ventana: barra de tareas, Alt+Tab
        Header.Background = Brushes.Transparent; // la cabecera comparte el fondo de la nota

        Loaded += (_, _) =>
        {
            // Una nota vacía empieza por el título, que es lo primero que se escribe; una que ya
            // tiene algo, por el final del cuerpo, para seguir escribiendo donde se dejó.
            if (string.IsNullOrEmpty(note.Text))
            {
                TitleBox.Focus();
                return;
            }

            TextBody.Focus();
            TextBody.CaretIndex = TextBody.Text.Length;
            TextBody.ScrollToEnd();
        };

        _autosaveTimer = new DispatcherTimer { Interval = AutosaveDelay };
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer.Stop();
            Flush();
        };

        TextBody.TextChanged += (_, _) => OnEdited();
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
        if (_settings is not { RememberNotePositions: true }) return;

        var monitorKey = MonitorLookup.DeviceNameAt(Left, Top, Width, Height, MonitorEnumerator.EnumerateMonitors());
        if (monitorKey is null) return; // el centro de la nota no cae en ningún monitor conocido

        _repository.SavePlacement(_note.Id, monitorKey, Left, Top, Width, Height);
    }

    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(180);
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
        // Arriba en la primera línea del cuerpo: el sitio de encima es el título.
        if (e.Key == Key.Up && TextBody.GetLineIndexFromCharacterIndex(TextBody.CaretIndex) == 0)
        {
            TitleBox.Focus();
            TitleBox.CaretIndex = TitleBox.Text.Length;
            e.Handled = true;
            return;
        }

        // Ctrl+L: convierte la línea en tarea, o le quita la casilla si ya lo era.
        if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var (text, caret) = TaskLines.ToggleTaskLineAt(TextBody.Text, TextBody.CaretIndex);
            ReplaceBody(text, caret);
            e.Handled = true;
            return;
        }

        // Enter al final de una tarea: la lista sigue sola. TaskLines decide si toca continuar,
        // terminar la lista (tarea vacía) o no hacer nada — en ese último caso, Enter normal.
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (TaskLines.EnterContinuation(TextBody.Text, TextBody.CaretIndex) is { } result)
            {
                ReplaceBody(result.Text, result.Caret);
                e.Handled = true;
            }
        }
    }

    private void OnBodyMouseDown(object sender, MouseButtonEventArgs e)
    {
        int index = TextBody.GetCharacterIndexFromPoint(e.GetPosition(TextBody), snapToText: false);
        if (index < 0) return;

        // Se prueba también el carácter anterior: GetCharacterIndexFromPoint devuelve el límite de
        // carácter más cercano, así que un clic en la mitad derecha del glifo cae ya en el espacio
        // de detrás. Sin esto, media casilla no respondería.
        var toggled = TaskLines.ToggleCheckboxAt(TextBody.Text, index)
                      ?? TaskLines.ToggleCheckboxAt(TextBody.Text, index - 1);
        if (toggled is null) return;

        int caret = TextBody.CaretIndex;
        ReplaceBody(toggled, caret);

        // Handled: el clic ya ha hecho su trabajo, y dejarlo pasar movería además el cursor al
        // sitio donde se pulsó, que no es lo que se pretendía al marcar una casilla.
        TextBody.Focus();
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnMenuClick(object sender, RoutedEventArgs e) =>
        ActionsPopup.IsOpen = !ActionsPopup.IsOpen;

    /// <summary>Lo mismo que Ctrl+L, para quien no conoce el atajo (que es casi todo el mundo).</summary>
    private void OnTaskClick(object sender, RoutedEventArgs e)
    {
        var (text, caret) = TaskLines.ToggleTaskLineAt(TextBody.Text, TextBody.CaretIndex);
        ReplaceBody(text, caret);
        ActionsPopup.IsOpen = false;
        TextBody.Focus();
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

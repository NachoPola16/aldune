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

public partial class NoteWindow : Window
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(500);

    // El mismo conversor que usa la pestaña del dock, para que el lomo diga exactamente lo mismo
    // (mayúsculas, con tracking, recortado igual) que decía la pestaña de la que salió.
    private static readonly NoteTabLabelConverter _spineLabelConverter = new();

    private readonly Note _note;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    public NoteWindow(Note note, NotesRepository repository, AppCoordinator coordinator)
    {
        InitializeComponent();
        _note = note;
        _repository = repository;
        _coordinator = coordinator;

        ApplyColor(note.Color);
        PopulateColorSwatches();

        if (note.State != NoteState.Active)
        {
            ActiveActions.Visibility = Visibility.Collapsed;
            InactiveActions.Visibility = Visibility.Visible;
        }

        TextBody.Text = note.Text;
        Title = NoteTitleHelper.GetTitle(note.Text);
        SpineLabel.Text = (string)_spineLabelConverter.Convert(
            note.Text, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);
        Header.Background = Brushes.Transparent; // la cabecera comparte el fondo de la nota
        Loaded += (_, _) =>
        {
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

        TextBody.TextChanged += (_, _) =>
        {
            Title = NoteTitleHelper.GetTitle(TextBody.Text);
            SpineLabel.Text = (string)_spineLabelConverter.Convert(
                TextBody.Text, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);
            _hasPendingEdit = true;
            _autosaveTimer.Stop();
            _autosaveTimer.Start();
        };

        // Antes del handler de abajo: si este cancela, el guardado del otro corre igual en la
        // segunda pasada, y Flush es idempotente.
        Closing += OnClosingWithAnimation;

        Closing += (_, _) =>
        {
            Flush();
            _coordinator.RefreshAll();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyRoundedCorners(hwnd);
        };
    }

    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(260);

    private bool _closingAnimationDone;

    /// <summary>
    /// La nota sale del mazo deslizándose hacia la izquierda, con su cabecera por delante — como
    /// tirar de una ficha en un fichero. <paramref name="startLeft"/> lo calcula
    /// <see cref="EdgeDockWindow.PositionNoteWindow"/>, ya acotado al monitor de ese dock.
    ///
    /// Se anima <c>Left</c> y, a la vez, el contenido se desliza un poco <b>más</b> y aparece: sin
    /// eso la ventana entraba a plena opacidad de golpe y solo el rectángulo se movía, que se lee
    /// como una ventana que salta, no como algo que se saca de un sitio. Los dos desplazamientos a
    /// distinta velocidad dan la sensación de que la cabecera tira del cuerpo.
    ///
    /// El alto, el ancho y el Top son definitivos desde el primer frame, así que el contenido nunca
    /// se mide a un tamaño intermedio — misma razón por la que el dock dejó de redimensionar su
    /// ventana (ver EdgeDockWindow).
    /// </summary>
    internal void SlideInFrom(double startLeft)
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        double targetLeft = Left;
        if (Math.Abs(startLeft - targetLeft) < 1) return;

        Left = startLeft;

        var duration = new Duration(SlideDuration);
        // Quíntica: las curvas de salida suaves (quart/quint/expo) son lo que se lee como "viene a
        // pararse". La QuadraticEase original era la más débil posible y apenas se distinguía de un
        // desplazamiento lineal.
        IEasingFunction Ease() => new QuinticEase { EasingMode = EasingMode.EaseOut };

        var slide = new DoubleAnimation(startLeft, targetLeft, duration) { EasingFunction = Ease() };

        // FillBehavior.HoldEnd dejaría esta animación enganchada a Left para siempre, por encima de
        // cualquier asignación posterior — y esta ventana se puede arrastrar (ver WindowChrome),
        // así que moverla justo después de abrirla pelearía contra un reloj todavía activo.
        slide.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = targetLeft;
        };
        BeginAnimation(LeftProperty, slide);

        // El contenido llega con un poco de retraso respecto al marco. No se anima Window.Opacity:
        // WPF la implementa con una ventana por capas, que es justo lo que esta ventana evita para
        // conservar ClearType en el texto que escribes.
        var content = (UIElement)Content;
        var lag = new TranslateTransform();
        content.RenderTransform = lag;
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, new Duration(SlideDuration)));
        lag.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(26, 0, duration) { EasingFunction = Ease() });
    }

    /// <summary>
    /// Al cerrar, la nota vuelve al mazo por donde salió en vez de desaparecer de golpe.
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

        var duration = new Duration(TimeSpan.FromMilliseconds(160));
        var slide = new DoubleAnimation(Left, Left + 40, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        slide.Completed += (_, _) => Close();

        ((UIElement)Content).BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, duration));
        BeginAnimation(LeftProperty, slide);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void Flush()
    {
        if (!_hasPendingEdit) return;
        _hasPendingEdit = false;
        _repository.UpdateText(_note.Id, TextBody.Text);
    }

    private void ApplyColor(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        var rim = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.RimFor(color))!;

        Background = brush;
        TextBody.Background = brush;
        WindowRim.BorderBrush = rim;

        // El lomo comparte el fondo de la nota (es la misma ficha), y su etiqueta y su troquelado
        // van en el tono oscuro del propio hue — igual que la pestaña del dock de la que viene.
        SpineLabel.Foreground = (Brush)new BrushConverter().ConvertFromString(
            NoteColorPalette.LabelFor(color))!;
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

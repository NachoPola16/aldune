using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

    /// <summary>
    /// La nota sale del mazo deslizándose hacia la izquierda, con su lomo por delante — como
    /// tirar de una ficha en un fichero. <paramref name="startLeft"/> lo calcula
    /// <see cref="EdgeDockWindow.PositionNoteWindow"/>, ya acotado al monitor de este dock.
    ///
    /// Solo se anima <c>Left</c>. El alto, el ancho y el Top ya son los definitivos desde el
    /// primer frame, así que el contenido nunca se mide a un tamaño intermedio (misma razón por
    /// la que el dock dejó de redimensionar su ventana, ver EdgeDockWindow). Antes se animaban
    /// las cuatro propiedades a la vez y la nota "crecía" desde un rect diminuto, que es una
    /// aparición genérica: esto es un movimiento con dirección y con causa.
    /// </summary>
    internal void SlideInFrom(double startLeft)
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        double targetLeft = Left;
        if (Math.Abs(startLeft - targetLeft) < 1) return;

        Left = startLeft;

        var duration = new Duration(TimeSpan.FromMilliseconds(320));
        var animation = new System.Windows.Media.Animation.DoubleAnimation(startLeft, targetLeft, duration)
        {
            // Quíntica, no cuadrática: las curvas de salida suaves (quart/quint/expo) son lo que
            // se lee como "viene a pararse". La QuadraticEase anterior era la más débil posible y
            // apenas se distinguía de un desplazamiento lineal.
            EasingFunction = new System.Windows.Media.Animation.QuinticEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        };

        // FillBehavior.HoldEnd (el valor por defecto) dejaría esta animación enganchada a Left
        // para siempre, por encima de cualquier asignación posterior — y esta ventana sí se puede
        // arrastrar (ver WindowChrome en el XAML), así que arrastrarla justo después de abrirla
        // pelearía contra un reloj de animación todavía activo.
        animation.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = targetLeft;
        };

        BeginAnimation(LeftProperty, animation);
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
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                // Tinta, no negro puro: es el mismo anillo de selección sobre seis pasteles que
                // comparten claridad, y el negro absoluto sería el único valor de la ventana sin
                // relación de hue con el resto.
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(NoteColorPalette.Ink)!,
                BorderThickness = new Thickness(color == _note.Color ? 2 : 0),
                Cursor = Cursors.Hand,
                Tag = color
            };
            swatch.MouseLeftButtonUp += OnColorSwatchClick;
            ColorSwatches.Children.Add(swatch);
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
            swatch.BorderThickness = new Thickness((string)swatch.Tag == color ? 2 : 0);
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

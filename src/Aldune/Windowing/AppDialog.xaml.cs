using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Aviso y confirmación con el aspecto de Aldune, en lugar de <see cref="MessageBox"/>: el de Windows
/// es claro sobre una app oscura, suena, y pone los botones en el idioma del sistema aunque Aldune
/// esté en otro. Misma firma que <c>MessageBox.Show</c> para sustituirlo sin cambiar a los que llaman.
///
/// Los avisos de arranque y de fallo grave (<c>App.xaml.cs</c>) siguen siendo nativos a propósito:
/// salen cuando la app puede estar rota o aún no tiene interfaz, y el de Windows es el que se
/// muestra seguro en esos casos.
/// </summary>
public partial class AppDialog : Window
{
    private MessageBoxResult _result;
    private readonly MessageBoxResult _escapeResult;

    private AppDialog(string message, string title, MessageBoxButton buttons, MessageBoxImage image,
        MessageBoxResult defaultResult)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        (IconGlyph.Text, IconGlyph.Foreground) = image switch
        {
            MessageBoxImage.Error => ("", Brush("#D98C84")),
            MessageBoxImage.Warning => ("", Brush("#E0B070")),
            MessageBoxImage.Question => ("", Brush("#A79E90")),
            MessageBoxImage.Information => ("", Brush("#A79E90")),
            _ => ("", Brushes.Transparent)
        };
        if (image == MessageBoxImage.None) IconGlyph.Visibility = Visibility.Collapsed;

        // Principal a la derecha, como en el resto de ventanas de la app (Cancelar | Aceptar).
        var choices = buttons switch
        {
            MessageBoxButton.OKCancel => new[] { MessageBoxResult.Cancel, MessageBoxResult.OK },
            MessageBoxButton.YesNo => new[] { MessageBoxResult.No, MessageBoxResult.Yes },
            MessageBoxButton.YesNoCancel => new[] { MessageBoxResult.Cancel, MessageBoxResult.No, MessageBoxResult.Yes },
            _ => new[] { MessageBoxResult.OK }
        };
        var affirmative = choices[^1];
        _escapeResult = choices.Contains(MessageBoxResult.Cancel) ? MessageBoxResult.Cancel
            : choices.Contains(MessageBoxResult.No) ? MessageBoxResult.No
            : MessageBoxResult.OK;
        _result = _escapeResult;

        // Un aviso de advertencia que pide confirmar es una acción destructiva (borrar para siempre,
        // revocar): su botón va en rojo y el foco empieza en el que no hace nada, si quien llama lo pide.
        bool destructive = image == MessageBoxImage.Warning && choices.Length > 1;
        var focus = defaultResult != MessageBoxResult.None && choices.Contains(defaultResult) ? defaultResult : affirmative;

        foreach (var choice in choices)
        {
            var button = new Button
            {
                Content = Label(choice),
                MinWidth = 84,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = choice == focus,
                Style = (Style)FindResource(choice != affirmative ? "ColorDialogButtonStyle"
                    : destructive ? "DangerDialogButtonStyle" : "PrimaryColorDialogButtonStyle")
            };
            button.Click += (_, _) => Close(choice);
            ButtonsPanel.Children.Add(button);
            if (choice == focus) Loaded += (_, _) => button.Focus();
        }
    }

    internal static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
        Show(null, message, title, buttons, image, defaultResult);

    internal static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var dialog = new AppDialog(message, title, buttons, image, defaultResult);
        if (owner is { IsLoaded: true })
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        // Siempre en la capa superior: las notas y el dock lo están, y un aviso tapado por ellas
        // bloquearía la app sin que se viera por qué.
        dialog.Topmost = true;
        dialog.ShowDialog();
        return dialog._result;
    }

    private void Close(MessageBoxResult result)
    {
        _result = result;
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close(_escapeResult);
        e.Handled = true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    private static string Label(MessageBoxResult choice) => choice switch
    {
        MessageBoxResult.Cancel => Strings.DialogCancel,
        MessageBoxResult.Yes => Strings.DialogYes,
        MessageBoxResult.No => Strings.DialogNo,
        _ => Strings.DialogOk
    };

    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
}

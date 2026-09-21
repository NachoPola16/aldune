using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Cortar/copiar/pegar con la estética de la app en cualquier TextBox.
///
/// El menú nativo de un TextBox es una subclase interna de <see cref="ContextMenu"/>, y un estilo
/// implícito de WPF solo casa por tipo EXACTO: el estilo oscuro de App.xaml nunca le llegaba y el
/// menú salía con el tema claro de Windows. Se le da a cada TextBox un ContextMenu propio (del tipo
/// exacto, así que sí recibe el estilo) que enruta los mismos comandos nativos del editor.
/// </summary>
internal static class DarkTextContextMenu
{
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(TextBoxBase),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Respeta el que ya tenga puesto quien lo quiera distinto (p. ej. una caja sin menú).
        if (sender is not TextBoxBase box || box.ContextMenu is not null) return;

        var menu = new ContextMenu();
        menu.Items.Add(Item(Strings.Cut, ApplicationCommands.Cut, box));
        menu.Items.Add(Item(Strings.Copy, ApplicationCommands.Copy, box));
        menu.Items.Add(Item(Strings.Paste, ApplicationCommands.Paste, box));
        box.ContextMenu = menu;
    }

    private static MenuItem Item(string header, RoutedCommand command, TextBoxBase target) => new()
    {
        Header = header,
        Command = command,
        CommandTarget = target
    };
}

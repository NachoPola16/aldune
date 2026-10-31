using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Aldune.Windowing;

/// <summary>
/// Whether the focus ring (<c>AlduneFocusVisual</c>) is drawn. WPF shows its focus visual whenever the last input was the keyboard, and Esc (to
/// close a menu) or leaving the dock's keyboard mode counts as keyboard: the button that got the focus back kept a ring although the user had
/// only used the mouse. Here the ring appears after a navigation key (Tab, arrows, Enter, Space...) and goes away with Esc or any mouse click.
/// </summary>
public sealed class FocusRingState : INotifyPropertyChanged
{
    public static FocusRingState Current { get; } = new();

    private bool _visible;

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static bool _hooked;

    /// <summary>Starts following the input of the whole app (once).</summary>
    internal static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        EventManager.RegisterClassHandler(typeof(UIElement), Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => Current.Visible = false), true);
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) Current.Visible = false;
        else if (key is Key.Tab or Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Space or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            Current.Visible = true;
    }
}

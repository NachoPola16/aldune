using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aldune.Resources;

namespace Aldune.Windowing;

internal sealed class PasswordPromptWindow : Window
{
    private readonly PasswordBox _passwordBox = new();
    private readonly PasswordBox? _confirmationBox;
    private readonly TextBlock _errorText = new();

    private PasswordPromptWindow(Window owner, string title, string description, bool confirm)
    {
        Owner = owner;
        Title = title;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = (Brush)new BrushConverter().ConvertFromString("#2A261F")!;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = (Brush)new BrushConverter().ConvertFromString("#B9AEA0")!,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });

        panel.Children.Add(new TextBlock { Text = Strings.PasswordLabel, Foreground = Brushes.White });
        _passwordBox.Margin = new Thickness(0, 5, 0, 10);
        _passwordBox.Padding = new Thickness(7, 4, 7, 4);
        panel.Children.Add(_passwordBox);

        if (confirm)
        {
            panel.Children.Add(new TextBlock { Text = Strings.ConfirmPasswordLabel, Foreground = Brushes.White });
            _confirmationBox = new PasswordBox { Margin = new Thickness(0, 5, 0, 4), Padding = new Thickness(7, 4, 7, 4) };
            panel.Children.Add(_confirmationBox);
        }

        _errorText.Foreground = (Brush)new BrushConverter().ConvertFromString("#F0A6A6")!;
        _errorText.TextWrapping = TextWrapping.Wrap;
        _errorText.Margin = new Thickness(0, 2, 0, 8);
        panel.Children.Add(_errorText);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = Strings.SyncNotesCancel, MinWidth = 82, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 5, 10, 5) };
        cancel.Click += (_, _) => DialogResult = false;
        var accept = new Button { Content = Strings.Accept, MinWidth = 82, Padding = new Thickness(10, 5, 10, 5), IsDefault = true };
        accept.Click += OnAccept;
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _passwordBox.Focus();
    }

    public static string? Show(Window owner, string title, string description, bool confirm)
    {
        var dialog = new PasswordPromptWindow(owner, title, description, confirm);
        return dialog.ShowDialog() == true ? dialog._passwordBox.Password : null;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var password = _passwordBox.Password;
        if (password.Length < 4)
        {
            _errorText.Text = Strings.PasswordTooShort;
            return;
        }

        if (_confirmationBox is not null && password != _confirmationBox.Password)
        {
            _errorText.Text = Strings.PasswordsDoNotMatch;
            return;
        }

        DialogResult = true;
    }
}

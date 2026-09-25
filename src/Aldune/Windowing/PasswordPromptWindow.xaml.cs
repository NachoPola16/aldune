using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

internal enum PasswordPromptMode { Unlock, Protect, RemoveProtection }

/// <summary>
/// Pide la contraseña de una nota protegida: para abrirla, para protegerla (con confirmación) o para
/// quitarle la protección. La comprobación va dentro del diálogo (<c>verify</c>): con una contraseña
/// equivocada se queda abierto, marca el campo y deja el texto seleccionado para reescribirlo, en vez
/// de cerrarse y soltar un MessageBox que obligaba a empezar otra vez desde la pestaña.
/// </summary>
internal partial class PasswordPromptWindow : Window
{
    private static readonly Brush ErrorBrush = Frozen("#E8A29A");
    private static readonly Brush NoticeBrush = Frozen("#E0CBA8");

    private readonly bool _confirm;
    private readonly Func<string, bool>? _verify;
    private bool _syncingText;

    private PasswordPromptWindow(PasswordPromptMode mode, Func<string, bool>? verify)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        _confirm = mode == PasswordPromptMode.Protect;
        _verify = verify;

        (string title, string hint, string action) = mode switch
        {
            PasswordPromptMode.Protect => (Strings.ProtectNoteTitle, Strings.ProtectNoteHint, Strings.ProtectAction),
            PasswordPromptMode.RemoveProtection =>
                (Strings.RemoveProtectionTitle, Strings.RemoveProtectionHint, Strings.RemoveProtectionAction),
            _ => (Strings.UnlockNote, Strings.ProtectedNoteHint, Strings.UnlockAction),
        };
        Title = title;
        TitleText.Text = title;
        HintText.Text = hint;
        AcceptButton.Content = action;
        ConfirmationPanel.Visibility = _confirm ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) =>
        {
            // El dueño suele ser el dock (WS_EX_NOACTIVATE): sin forzar el primer plano, lo que se
            // teclease iría a la aplicación que estuviera delante.
            NativeMethods.ForceActivate(this);
            PasswordInput.Focus();
            UpdateStatus();
        };
        // Bloq Mayús cambia de estado al soltar la tecla; también puede cambiar con la ventana en
        // segundo plano, de ahí Activated.
        PreviewKeyUp += (_, e) => { if (e.Key == Key.CapsLock) UpdateStatus(); };
        Activated += (_, _) => UpdateStatus();
    }

    /// <summary>
    /// Devuelve la contraseña aceptada, o null si se cancela. Con <paramref name="verify"/> solo se
    /// acepta la que la función dé por buena; lo que haga la función (desbloquear, quitar la
    /// protección) ya ha ocurrido cuando esto vuelve.
    /// </summary>
    public static string? Show(Window owner, PasswordPromptMode mode, Func<string, bool>? verify = null)
    {
        var dialog = new PasswordPromptWindow(mode, verify) { Owner = owner };
        if (owner is EdgeDockWindow dock)
        {
            // Centrado en su dueño quedaría pegado al canto, encima de la tira del dock: mejor en el
            // centro de su pantalla. Se recoloca al cargar, cuando ya se conoce el alto real (sigue
            // oculta por CloakUntilFirstFrame, así que el salto no se ve).
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Loaded += (_, _) => dock.CenterOnThisMonitor(dialog);
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        return dialog.ShowDialog() == true ? dialog.PasswordInput.Password : null;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        string password = PasswordInput.Password;
        var problem = PasswordRules.Check(password, _confirm ? ConfirmationInput.Password : null);
        if (problem != PasswordProblem.None)
        {
            string message = problem switch
            {
                PasswordProblem.Empty => Strings.PasswordEmpty,
                PasswordProblem.TooShort => Strings.PasswordTooShort,
                _ => Strings.PasswordsDoNotMatch,
            };
            ShowError(message, onConfirmation: problem == PasswordProblem.Mismatch);
            return;
        }

        if (_verify is not null && !_verify(password))
        {
            ShowError(Strings.WrongPassword, onConfirmation: false);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message, bool onConfirmation)
    {
        PasswordField.Tag = onConfirmation ? null : "Error";
        ConfirmationField.Tag = onConfirmation ? "Error" : null;
        StatusText.Text = message;
        StatusText.Foreground = ErrorBrush;

        // El foco vuelve al campo que hay que corregir, con el texto seleccionado: se reescribe
        // directamente, sin borrar a mano.
        bool revealed = RevealToggle.IsChecked == true;
        if (revealed)
        {
            var box = onConfirmation ? ConfirmationPlain : PasswordPlain;
            box.Focus();
            box.SelectAll();
        }
        else
        {
            var box = onConfirmation ? ConfirmationInput : PasswordInput;
            box.Focus();
            box.SelectAll();
        }
    }

    /// <summary>
    /// Mostrar la contraseña cambia la PasswordBox por una TextBox con el mismo texto (PasswordBox no
    /// sabe enseñarlo). El foco pasa a la caja visible para seguir escribiendo sin más.
    /// </summary>
    private void OnRevealChanged(object sender, RoutedEventArgs e)
    {
        bool reveal = RevealToggle.IsChecked == true;
        bool onConfirmation = ConfirmationInput.IsKeyboardFocusWithin || ConfirmationPlain.IsKeyboardFocusWithin;

        _syncingText = true;
        PasswordPlain.Text = PasswordInput.Password;
        ConfirmationPlain.Text = ConfirmationInput.Password;
        _syncingText = false;

        PasswordInput.Visibility = ConfirmationInput.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
        PasswordPlain.Visibility = ConfirmationPlain.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;

        string tip = reveal ? Strings.HidePassword : Strings.ShowPassword;
        RevealToggle.ToolTip = tip;
        AutomationProperties.SetName(RevealToggle, tip);

        if (reveal)
        {
            var box = onConfirmation ? ConfirmationPlain : PasswordPlain;
            box.Focus();
            box.CaretIndex = box.Text.Length;
        }
        else
        {
            (onConfirmation ? ConfirmationInput : PasswordInput).Focus();
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingText) return;
        ClearError();
    }

    private void OnPlainChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingText) return;
        // La PasswordBox sigue siendo la fuente de verdad: es la que se lee al aceptar.
        _syncingText = true;
        PasswordInput.Password = PasswordPlain.Text;
        ConfirmationInput.Password = ConfirmationPlain.Text;
        _syncingText = false;
        ClearError();
    }

    private void ClearError()
    {
        if (PasswordField.Tag is null && ConfirmationField.Tag is null) return;
        PasswordField.Tag = ConfirmationField.Tag = null;
        UpdateStatus();
    }

    /// <summary>Sin error a la vista, la línea de estado avisa de Bloq Mayús activado.</summary>
    private void UpdateStatus()
    {
        if (PasswordField.Tag is not null || ConfirmationField.Tag is not null) return;
        StatusText.Text = Keyboard.IsKeyToggled(Key.CapsLock) ? Strings.CapsLockOn : "";
        StatusText.Foreground = NoticeBrush;
    }

    private static Brush Frozen(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}

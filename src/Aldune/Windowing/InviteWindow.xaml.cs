using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Las dos mitades de una invitación al servidor de sync (docs/SYNC.md → «Invitations»): quien aloja crea un código de un solo uso con el token
/// de administrador, y quien recibe lo pega para tener su propio espacio. La red y las reglas están en Core (<see cref="SyncInviteClient"/>,
/// <see cref="SyncService.JoinWithInvitation"/>); esto solo es la ventana.
/// </summary>
internal partial class InviteWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly bool _owner;
    private readonly string _serverUrl;
    private readonly Func<string, string, string>? _join;
    private bool _busy;
    private bool _created;

    /// <summary>Qué se pidió al cerrar: el perfil nuevo en el modo «unirse» (para que la ventana de ajustes lo recargue).</summary>
    public bool Joined { get; private set; }

    private InviteWindow(bool owner, string serverUrl, Func<string, string, string>? join)
    {
        InitializeComponent();
        ThemeManager.ScaleWindowToText(this);
        NativeMethods.CloakUntilFirstFrame(this);
        _owner = owner;
        _serverUrl = serverUrl;
        _join = join;
        CloseButton.Content = Strings.DialogCancel;

        if (owner)
        {
            Title = TitleText.Text = Strings.InviteCreateTitle;
            HintText.Text = Strings.InviteCreateHint;
            FirstLabel.Text = Strings.InviteAdminTokenLabel;
            FirstInput.Visibility = Visibility.Collapsed;
            AdminInput.Visibility = Visibility.Visible;
            SecondLabel.Text = Strings.InviteLabelLabel;
            LabelPanel.Visibility = Visibility.Visible;
            ActionButton.Content = Strings.InviteCreateAction;
        }
        else
        {
            Title = TitleText.Text = Strings.InviteJoinTitle;
            HintText.Text = Strings.InviteJoinHint;
            FirstLabel.Text = Strings.InviteJoinLabel;
            ActionButton.Content = Strings.InviteJoinAction;
        }

        Loaded += (_, _) =>
        {
            NativeMethods.ForceActivate(this);
            if (owner) AdminInput.Focus(); else FirstInput.Focus();
        };
    }

    /// <summary>Quien recibe: pega la invitación. <paramref name="join"/> recibe (dirección, token) y crea el perfil; devuelve su nombre.</summary>
    public static bool ShowJoin(Window owner, Func<string, string, string> join)
    {
        var window = Create(owner, new InviteWindow(false, string.Empty, join));
        window.ShowDialog();
        return window.Joined;
    }

    /// <summary>Quien aloja: crea una invitación para <paramref name="serverUrl"/> (la del perfil activo).</summary>
    public static void ShowCreate(Window owner, string serverUrl) =>
        Create(owner, new InviteWindow(true, serverUrl, null)).ShowDialog();

    private static InviteWindow Create(Window owner, InviteWindow window)
    {
        if (owner is { IsLoaded: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return window;
    }

    private async void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        // Tras crear la invitación, el botón principal copia el texto.
        if (_created)
        {
            Clipboard.SetText(ResultBox.Text);
            ShowStatus(Strings.InviteCopied, error: false);
            return;
        }

        _busy = true;
        ActionButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Collapsed;
        try
        {
            if (_owner) await CreateInvitation(); else await JoinServer();
        }
        catch (InviteException ex)
        {
            ShowStatus(Message(ex.Failure), error: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ShowStatus(Strings.InviteErrServer, error: true);
        }
        finally
        {
            _busy = false;
            ActionButton.IsEnabled = true;
        }
    }

    private async Task CreateInvitation()
    {
        if (string.IsNullOrWhiteSpace(_serverUrl) || AdminInput.Password.Trim().Length == 0)
        {
            ShowStatus(Strings.InviteErrNeedAdmin, error: true);
            return;
        }

        var text = await SyncInviteClient.CreateAsync(Http, _serverUrl, AdminInput.Password, LabelInput.Text);
        ResultBox.Text = text;
        ResultPanel.Visibility = Visibility.Visible;
        FirstLabel.Visibility = AdminInput.Visibility = LabelPanel.Visibility = Visibility.Collapsed;
        HintText.Text = Strings.InviteCreatedHint;
        ActionButton.Content = Strings.Copy;
        CloseButton.Content = Strings.DialogOk;
        _created = true;
        AdminInput.Clear();
    }

    private async Task JoinServer()
    {
        if (!SyncInvite.TryParse(FirstInput.Text, out var url, out var code))
        {
            ShowStatus(Strings.InviteErrPaste, error: true);
            return;
        }

        var token = await SyncInviteClient.RedeemAsync(Http, url, code);
        _join!(url, token);
        Joined = true;
        Close();
    }

    private void ShowStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, error ? "AlduneErrorTextBrush" : "AlduneMutedTextBrush");
        StatusText.Visibility = Visibility.Visible;
    }

    private static string Message(InviteFailure failure) => failure switch
    {
        InviteFailure.InvalidCode => Strings.InviteErrInvalid,
        InviteFailure.Offline => Strings.InviteErrOffline,
        InviteFailure.NotAllowed => Strings.InviteErrNotAllowed,
        InviteFailure.NotSupported => Strings.InviteErrNotSupported,
        InviteFailure.ServerFull => Strings.InviteErrFull,
        InviteFailure.TooManyAttempts => Strings.InviteErrBusy,
        _ => Strings.InviteErrServer,
    };

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);
        NativeMethods.SetFrameColor(this, (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ThemeManager.Color("Ground")));
    }
}

using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// «Personas de este servidor»: los espacios creados con invitaciones y «Revocar» (docs/SYNC.md → «Invitations»). Pide el token de
/// administrador cada vez y no lo guarda: vive solo en el cuadro mientras la ventana está abierta. La red y las reglas están en
/// <see cref="SyncInviteClient"/>; esto solo es la ventana.
/// </summary>
internal partial class PeopleWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _serverUrl;
    private bool _busy;

    private PeopleWindow(string serverUrl)
    {
        InitializeComponent();
        ThemeManager.ScaleWindowToText(this);
        NativeMethods.CloakUntilFirstFrame(this);
        _serverUrl = serverUrl;
        Title = TitleText.Text = Strings.PeopleTitle;
        HintText.Text = Strings.PeopleHint;
        TokenLabel.Text = Strings.InviteAdminTokenLabel;
        CloseButton.Content = Strings.DialogOk;
        ShowButton.Content = Strings.PeopleShow;
        Loaded += (_, _) =>
        {
            NativeMethods.ForceActivate(this);
            AdminInput.Focus();
        };
    }

    /// <summary>Muestra las personas del servidor <paramref name="serverUrl"/> (el del perfil activo).</summary>
    public static void Show(Window owner, string serverUrl)
    {
        var window = new PeopleWindow(serverUrl);
        if (owner is { IsLoaded: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.ShowDialog();
    }

    private async void OnShowClick(object sender, RoutedEventArgs e) => await LoadAsync(notice: null);

    private async Task LoadAsync(string? notice)
    {
        if (_busy) return;
        if (string.IsNullOrWhiteSpace(_serverUrl) || AdminInput.Password.Trim().Length == 0)
        {
            ShowStatus(Strings.InviteErrNeedAdmin, error: true);
            return;
        }

        _busy = true;
        ShowButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Collapsed;
        try
        {
            var spaces = await SyncInviteClient.ListSpacesAsync(Http, _serverUrl, AdminInput.Password);
            Fill(spaces);
            if (notice is not null) ShowStatus(notice, error: false);
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
            ShowButton.IsEnabled = true;
        }
    }

    private void Fill(IReadOnlyList<SyncSpaceInfo> spaces)
    {
        ListPanel.Children.Clear();
        ListScroll.Visibility = Visibility.Visible;
        if (spaces.Count == 0)
        {
            var empty = new TextBlock { Text = Strings.PeopleEmpty, TextWrapping = TextWrapping.Wrap };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "AlduneMutedTextBrush");
            ListPanel.Children.Add(empty);
            return;
        }

        foreach (var space in spaces)
        {
            var name = string.IsNullOrWhiteSpace(space.Label) ? Strings.PeopleUnnamed : space.Label;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis };
            title.SetResourceReference(TextBlock.ForegroundProperty, "AlduneTextStrongBrush");
            var since = new TextBlock { Text = Strings.PeopleJoined.Replace("{0}", space.CreatedAt.ToLocalTime().ToString("d")), FontSize = 12 };
            since.SetResourceReference(TextBlock.ForegroundProperty, "AlduneMutedTextBrush");
            text.Children.Add(title);
            text.Children.Add(since);

            var revoke = new Button { Content = Strings.PeopleRevoke, MinWidth = 84, Margin = new Thickness(12, 0, 0, 0) };
            revoke.SetResourceReference(StyleProperty, "ColorDialogButtonStyle");
            revoke.Click += async (_, _) => await RevokeAsync(space, name);
            Grid.SetColumn(revoke, 1);

            row.Children.Add(text);
            row.Children.Add(revoke);
            ListPanel.Children.Add(row);
        }
    }

    private async Task RevokeAsync(SyncSpaceInfo space, string name)
    {
        if (_busy) return;
        var answer = AppDialog.Show(this, Strings.PeopleRevokeBody.Replace("{0}", name), Strings.PeopleRevokeTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        _busy = true;
        try
        {
            await SyncInviteClient.RevokeSpaceAsync(Http, _serverUrl, AdminInput.Password, space.Name);
        }
        catch (InviteException ex)
        {
            ShowStatus(Message(ex.Failure), error: true);
            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ShowStatus(Strings.InviteErrServer, error: true);
            return;
        }
        finally
        {
            _busy = false;
        }

        await LoadAsync(Strings.PeopleRevoked);
    }

    private void ShowStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, error ? "AlduneErrorTextBrush" : "AlduneMutedTextBrush");
        StatusText.Visibility = Visibility.Visible;
    }

    private static string Message(InviteFailure failure) => failure switch
    {
        InviteFailure.Offline => Strings.InviteErrOffline,
        InviteFailure.NotAllowed => Strings.InviteErrNotAllowed,
        InviteFailure.NotSupported => Strings.InviteErrNotSupported,
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

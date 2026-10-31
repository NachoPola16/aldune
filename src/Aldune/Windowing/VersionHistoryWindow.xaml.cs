using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// «Historial de versiones…» de una nota: lista de copias anteriores con fecha, vista previa y Restaurar. Las copias son locales (no se
/// sincronizan) y restaurar guarda antes el texto actual, así que se puede deshacer. Reglas en <see cref="NoteVersionRules"/>.
/// </summary>
internal sealed partial class VersionHistoryWindow : Window
{
    private readonly NotesRepository _repository;
    private readonly Guid _noteId;
    private readonly Action _restored;
    private IReadOnlyList<NoteVersion> _versions = Array.Empty<NoteVersion>();

    public VersionHistoryWindow(NotesRepository repository, Guid noteId, Window? owner, Action restored)
    {
        _repository = repository;
        _noteId = noteId;
        _restored = restored;
        InitializeComponent();
        if (owner is { IsVisible: true }) Owner = owner;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        Fill();
    }

    private void Fill()
    {
        _versions = _repository.GetVersions(_noteId);
        VersionList.Items.Clear();
        foreach (var version in _versions)
            VersionList.Items.Add(version.SavedAt.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture));
        bool any = _versions.Count > 0;
        EmptyText.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        VersionList.Visibility = any ? Visibility.Visible : Visibility.Hidden;
        Preview.Visibility = any ? Visibility.Visible : Visibility.Hidden;
        if (any) VersionList.SelectedIndex = 0;
    }

    private NoteVersion? Selected => VersionList.SelectedIndex is >= 0 and var i && i < _versions.Count ? _versions[i] : null;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Preview.Text = Selected?.Text ?? string.Empty;
        RestoreButton.IsEnabled = Selected is not null;
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } version) return;
        if (_repository.RestoreVersion(_noteId, version.Id)) _restored();
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeMethods.ApplyRoundedCorners(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        NativeMethods.SetFrameColor(this, (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ThemeManager.Color("Ground")));
    }
}

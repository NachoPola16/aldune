using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aldune.Core;
using Aldune.Resources;
using Microsoft.Data.Sqlite;

namespace Aldune.Windowing;

/// <summary>
/// Manual backup and restore (Settings > About). The restore never touches the live files here: the app
/// holds the database open, so it only stages the chosen backup and <c>App.OnStartup</c> swaps it in at
/// the next start (see <see cref="LocalBackup.ApplyPendingRestore"/>).
/// </summary>
public partial class SettingsWindow
{
    private string AppDataDirectory => Path.GetDirectoryName(Path.GetFullPath(_settingsService.SettingsPath))!;

    private async void OnBackupNowClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Strings.BackupFileFilter,
            FileName = "aldune-backup-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".zip",
            DefaultExt = ".zip",
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        var directory = AppDataDirectory;
        var target = dialog.FileName;
        try
        {
            // Off the UI thread: copying a large database froze the window for a moment.
            var manifest = await Task.Run(() => LocalBackup.CreateManual(directory, target, AppInfo.Version, DateTimeOffset.Now));
            AppDialog.Show(this, Strings.BackupDone(target, manifest.NoteCount), Strings.BackupSectionTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            AppDialog.Show(this, Strings.BackupFailed, Strings.BackupSectionTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnBackupRestoreFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Strings.BackupFileFilter, DefaultExt = ".zip", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        RestoreFrom(dialog.FileName, whenText: null);
    }

    private async void OnBackupRestoreDailyClick(object sender, RoutedEventArgs e)
    {
        var directory = AppDataDirectory;
        // Verifying opens each copy's database, so it is done off the UI thread.
        var copies = await Task.Run(() => LocalBackup.ListNewestFirst(directory)
            .Select(folder => (Folder: folder, Check: LocalBackup.Verify(folder)))
            .Where(copy => copy.Check.IsOk)
            .ToList());

        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        if (copies.Count == 0)
            menu.Items.Add(new MenuItem { Header = Strings.BackupNoDailyCopies, IsEnabled = false });
        foreach (var (folder, check) in copies)
        {
            var day = Path.GetFileName(folder);
            var item = new MenuItem { Header = Strings.BackupDailyItem(day, check.Manifest!.NoteCount) };
            item.Click += (_, _) => RestoreFrom(folder, day);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>Verifies, asks, and stages. <paramref name="whenText"/> is the day of a daily copy; null for a file (its own date is used).</summary>
    private void RestoreFrom(string source, string? whenText)
    {
        var check = LocalBackup.Verify(source);
        if (!check.IsOk)
        {
            AppDialog.Show(this, Strings.BackupProblemText(check.Problem), Strings.BackupSectionTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var manifest = check.Manifest!;
        whenText ??= manifest.CreatedUtc == default
            ? Path.GetFileName(source)
            : manifest.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        if (AppDialog.Show(this, Strings.BackupRestoreConfirm(whenText, manifest.NoteCount), Strings.BackupSectionTitle,
                MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK)
            return;

        BackupCheck staged;
        try
        {
            staged = LocalBackup.StageRestore(AppDataDirectory, source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            AppDialog.Show(this, Strings.BackupFailed, Strings.BackupSectionTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (!staged.IsOk)
        {
            AppDialog.Show(this, Strings.BackupProblemText(staged.Problem), Strings.BackupSectionTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (AppDialog.Show(this, Strings.BackupRestoreStaged + "\n\n" + Strings.RestartNowQuestion, Strings.BackupSectionTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            OnRestartClick(this, new RoutedEventArgs());
    }
}

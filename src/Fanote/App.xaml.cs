using System.IO;
using System.Windows;
using Fanote.Core;
using Fanote.Windowing;

namespace Fanote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fanote");
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        var databasePath = Path.Combine(appDataDir, "notes.db");

        if (DatabaseCorruptionGuard.IsValidSqliteFile(databasePath) is false && File.Exists(databasePath))
        {
            DatabaseCorruptionGuard.BackupAndRemove(databasePath);
        }

        var settingsService = new SettingsService(settingsPath);
        var settings = settingsService.Load();

        byte[] rawKey;
        if (settings.WrappedDatabaseKey is null)
        {
            rawKey = DatabaseKeyProvider.GenerateKey();
            settings.WrappedDatabaseKey = DatabaseKeyProvider.Wrap(rawKey);
            settingsService.Save(settings);
        }
        else
        {
            rawKey = DatabaseKeyProvider.Unwrap(settings.WrappedDatabaseKey);
        }

        var cipher = new ContentCipher(rawKey);
        var database = new NotesDatabase(databasePath);
        var repository = new NotesRepository(database, cipher);

        var area = SystemParameters.WorkArea;
        var workingArea = new WorkingArea(area.Left, area.Top, area.Width, area.Height);

        var dock = new EdgeDockWindow(EdgePosition.Right, workingArea, repository);
        dock.Refresh();
        dock.Show();
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Fanote.Core;
using Fanote.Interop;
using Fanote.Windowing;
using Microsoft.Data.Sqlite;

namespace Fanote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Safety net for exceptions raised during normal operation, AFTER startup has already
        // succeeded (autosave, new-note, refresh, etc. — see NoteWindow.Flush, EdgeDockWindow's
        // OnNewNoteClick/Refresh). This is not a substitute for the explicit bootstrap error
        // handling below: OnStartup runs before the dispatcher message loop is pumping in a way
        // that reliably routes startup-time exceptions here, so the 3 bootstrap failure modes are
        // handled explicitly via try/catch instead of relying on this handler.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Ha ocurrido un error inesperado: {args.Exception.Message}\n\nLa aplicación continuará, pero esta acción concreta puede no haberse completado.",
                "Fanote — error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fanote");
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        var databasePath = Path.Combine(appDataDir, "notes.db");

        if (DatabaseCorruptionGuard.IsValidSqliteFile(databasePath) is false && File.Exists(databasePath))
        {
            DatabaseCorruptionGuard.BackupAndRemove(databasePath);
        }

        NotesRepository repository;
        try
        {
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
            // ContentCipher clones the key into its own array in its constructor, so it's safe to
            // zero this local copy immediately afterwards — it no longer needs to live on.
            CryptographicOperations.ZeroMemory(rawKey);

            repository = BootstrapDatabase(databasePath, cipher, allowRetry: true);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // Case (a): DPAPI can't unwrap the stored key (most likely cause: a Windows admin
            // reset this user's password, which permanently destroys the DPAPI-protected key —
            // no technical recovery is possible), or settings.json itself is malformed. In either
            // case we deliberately do NOT generate a fresh key over a damaged settings file: that
            // would silently orphan every existing encrypted note forever, with no way back.
            MessageBox.Show(
                "No se puede descifrar la base de datos de notas con la clave almacenada.\n\n" +
                "La causa más probable es que se haya restablecido la contraseña de Windows de este " +
                "usuario, lo que destruye de forma permanente la clave protegida. Esto no se puede " +
                "recuperar técnicamente, salvo que exista una copia de seguridad exportada previamente " +
                "(esa función aún no existe en esta versión).",
                "Fanote — no se puede iniciar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        catch (SqliteException)
        {
            // Case (b), retry already attempted and failed inside BootstrapDatabase: the database
            // file is damaged beyond automatic recovery. Fatal.
            MessageBox.Show(
                "No se ha podido abrir ni recrear la base de datos de notas tras un intento de " +
                "recuperación automática. Es posible que el disco esté lleno o que el archivo siga " +
                "dañado.",
                "Fanote — no se puede iniciar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se ha podido iniciar Fanote debido a un error inesperado: {ex.Message}",
                "Fanote — no se puede iniciar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (monitors.Count == 0)
        {
            // Practically impossible on real Windows (there's always at least one display), but
            // treat it as a fourth bootstrap failure mode rather than crashing with no explanation.
            MessageBox.Show(
                "No se ha podido detectar ningún monitor conectado.",
                "Fanote — no se puede iniciar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var coordinator = new AppCoordinator(repository);
        var docks = new List<EdgeDockWindow>();
        foreach (var monitor in monitors)
        {
            var dock = new EdgeDockWindow(EdgePosition.Right, monitor, repository, coordinator);
            coordinator.RegisterDock(dock);
            docks.Add(dock);
        }

        try
        {
            // The first place decryption of existing notes is actually attempted — this is where
            // case (c) (wrong key for this database) surfaces, not earlier in the bootstrap.
            coordinator.RefreshAll();
        }
        catch (AuthenticationTagMismatchException)
        {
            // Case (c): the key doesn't match this database's encrypted content (e.g. settings.json
            // was lost/replaced independently of notes.db, so a NEW key got generated that doesn't
            // match the OLD encrypted data). Critically different from case (b): the database file
            // itself is fine, so we must NOT delete or back it up — doing so would destroy the
            // user's only chance of ever recovering their notes (e.g. if they later find their old
            // settings.json).
            MessageBox.Show(
                "La base de datos de notas no se ha podido descifrar con la clave actual.\n\n" +
                "La causa más probable es que el archivo de configuración que contenía la clave se " +
                "haya perdido, sustituido o proceda de otra instalación. Las notas NO se han eliminado " +
                "y siguen almacenadas de forma segura, pero no se pueden leer en este momento.",
                "Fanote — no se puede iniciar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Cheap sweep so trash doesn't grow forever even for someone who never opens the
        // Archivadas view (which also purges on entry, see EdgeDockWindow.OnToggleArchiveClick).
        repository.PurgeExpiredTrash(TimeSpan.FromDays(NotesRepository.DefaultTrashRetentionDays));

        foreach (var dock in docks) dock.Show();
    }

    /// <summary>
    /// Constructs the <see cref="NotesRepository"/> for <paramref name="databasePath"/>. If the
    /// database file is structurally corrupt (throws <see cref="SqliteException"/> when actually
    /// opened/queried, even though it passed the header check), the corrupt file is backed up via
    /// <see cref="DatabaseCorruptionGuard.BackupAndRemove"/> and construction is retried exactly
    /// once against a fresh, empty database at the same path. The key itself is untouched by this
    /// recovery path. If the retry also fails, the exception propagates.
    /// </summary>
    private static NotesRepository BootstrapDatabase(string databasePath, ContentCipher cipher, bool allowRetry)
    {
        try
        {
            var database = new NotesDatabase(databasePath);
            return new NotesRepository(database, cipher);
        }
        catch (SqliteException) when (allowRetry)
        {
            DatabaseCorruptionGuard.BackupAndRemove(databasePath);
            var repository = BootstrapDatabase(databasePath, cipher, allowRetry: false);

            MessageBox.Show(
                "El archivo de notas existente estaba dañado. Se ha conservado una copia en un archivo " +
                "\".corrupt-<fecha>\" junto al original, y se ha creado una base de datos nueva y vacía.",
                "Fanote — base de datos recuperada",
                MessageBoxButton.OK, MessageBoxImage.Information);

            return repository;
        }
    }
}

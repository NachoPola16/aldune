using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
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

        if (MonitorEnumerator.EnumerateMonitors().Count == 0)
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
        _coordinator = coordinator;
        _repository = repository;
        try
        {
            // BuildDocks termina llamando a RefreshAll, que es el primer sitio donde de verdad se
            // intenta descifrar las notas existentes — aquí es donde aflora el caso (c), clave
            // equivocada para esta base de datos, no antes en el arranque. Por eso la construcción
            // va dentro del try y no fuera: si se dejara fuera, esa excepción escaparía sin que
            // nadie la tradujera al mensaje de abajo.
            BuildDocks();
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

        // Cheap sweep so trash doesn't grow forever. This startup sweep is now the only
        // trigger for it — earlier there was also a dock-side "Archivadas" view that purged
        // on entry, but that view was removed by the fan-tabs redesign (NotesManagerWindow's
        // gear icon is the only way to browse archived/trashed notes now, and it doesn't
        // purge on open).
        repository.PurgeExpiredTrash(TimeSpan.FromDays(NotesRepository.DefaultTrashRetentionDays));

        // Apagar, encender o reconfigurar un monitor con la app corriendo. Sin esto, Windows
        // reubica el dock huérfano del monitor que desaparece sobre el que queda, y acabas con dos
        // docks apilados en la misma pantalla — reportado por el usuario al apagar un monitor.
        //
        // Los docks se posicionan con coordenadas absolutas calculadas una sola vez (ver
        // EdgeGeometry.WindowRect), así que no pueden recolocarse solos: la única salida correcta
        // es reconstruirlos contra la lista de monitores nueva. Se engancha al final del arranque,
        // ya con el descifrado verificado, para no reconstruir nada si la app va a abortar.
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Exit += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
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
    
    private AppCoordinator? _coordinator;
    private NotesRepository? _repository;
    private DispatcherTimer? _rebuildDebounce;

    /// <summary>
    /// Crea un dock por cada monitor conectado ahora mismo. Se llama al arrancar y cada vez que
    /// cambia la configuración de pantallas.
    /// </summary>
    private void BuildDocks()
    {
        if (_coordinator is null || _repository is null) return;

        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (monitors.Count == 0) return; // sin pantallas no hay nada que colocar; ya volverá otra

        // Filtro de monitor por variable de entorno. Existe porque durante el desarrollo hace
        // falta poder lanzar la app sin invadir la otra pantalla (p. ej. si hay algo a pantalla
        // completa en ella), y es la pieza mínima del punto 3 de "Prerrequisitos para la Fase 3":
        // restringir la app a un solo monitor. Cuando ese punto se aborde de verdad, esto debería
        // pasar a ser un ajuste de verdad en AppSettings, no una variable de entorno.
        var onlyMonitor = Environment.GetEnvironmentVariable("FANOTE_MONITOR_INDEX");
        if (int.TryParse(onlyMonitor, out int monitorIndex)
            && monitorIndex >= 0 && monitorIndex < monitors.Count)
        {
            monitors = new[] { monitors[monitorIndex] };
        }

        foreach (var monitor in monitors)
        {
            var dock = new EdgeDockWindow(EdgePosition.Right, monitor, _repository, _coordinator);
            _coordinator.RegisterDock(dock);
            dock.Show();
        }

        _coordinator.RefreshAll();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Con retardo y reiniciable: cambiar de pantallas dispara varios DisplaySettingsChanged
        // seguidos (Windows reconfigura en pasos), y reconstruir los docks en cada uno significa
        // crear y destruir ventanas varias veces por nada. Esperar a que pare deja una sola
        // reconstrucción, ya contra la disposición definitiva.
        _rebuildDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _rebuildDebounce.Tick -= OnRebuildTick;
        _rebuildDebounce.Tick += OnRebuildTick;
        _rebuildDebounce.Stop();
        _rebuildDebounce.Start();
    }

    private void OnRebuildTick(object? sender, EventArgs e)
    {
        _rebuildDebounce?.Stop();
        _coordinator?.CloseAllDocks();
        BuildDocks();
    }
}

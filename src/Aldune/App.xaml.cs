using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Linq;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;
using Aldune.Windowing;
using Microsoft.Data.Sqlite;

namespace Aldune;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Adivinado por el idioma de Windows hasta que se lea AppSettings.Language más abajo (o
        // para siempre, si nunca se llega a leer — p. ej. settings.json corrupto). Así incluso los
        // mensajes de error del arranque más temprano salen en el idioma que toca la mayoría de las
        // veces, en vez de siempre en inglés.
        ApplyLanguage(null);

        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            // Ya hay una Aldune en marcha y se le acaba de avisar: ella abre el gestor de notas.
            Shutdown(0);
            return;
        }

        DarkTextContextMenu.Register();

        // Ver el comentario de DisablePowerThrottling: Aldune vive casi siempre sin foco, y
        // Windows puede reducirle CPU/prioridad tras un rato así — mitigacion contra el reporte de
        // animaciones que se ven peor "tras un rato sin abrir ninguna nota". Sin coste ni efecto
        // secundario si el diagnostico resulta no ser este; se deja siempre activo.
        NativeMethods.DisablePowerThrottling();

        // Safety net for exceptions raised during normal operation, AFTER startup has already
        // succeeded (autosave, new-note, refresh, etc. — see NoteWindow.Flush, EdgeDockWindow's
        // OnNewNoteClick/Refresh). This is not a substitute for the explicit bootstrap error
        // handling below: OnStartup runs before the dispatcher message loop is pumping in a way
        // that reliably routes startup-time exceptions here, so the 3 bootstrap failure modes are
        // handled explicitly via try/catch instead of relying on this handler.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                Strings.UnexpectedErrorMessage(args.Exception.Message),
                Strings.UnexpectedErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var appDataDir = ResolveAppDataDirectory();
        // Registro de diagnóstico del dock y las pantallas (docs/DOCK_DIAGNOSTICS.md): local, acotado
        // a unos cientos de KB y sin contenido de notas.
        DockDiagnostics.Log = new DiagnosticLog(Path.Combine(appDataDir, "logs", "dock.log"));
        DockDiagnostics.Write("app", $"arranque, versión {AppInfo.Version}");
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        var databasePath = Path.Combine(appDataDir, BrandIdentity.DatabaseFileName);

        if (DatabaseCorruptionGuard.IsValidSqliteFile(databasePath) is false && File.Exists(databasePath))
        {
            DatabaseCorruptionGuard.BackupAndRemove(databasePath);
        }

        NotesRepository repository;
        // Fuera del try: los ajustes se siguen usando despues del arranque (la ventana de Ajustes
        // los guarda), asi que no pueden quedarse dentro de este ambito.
        var settingsService = new SettingsService(settingsPath);
        AppSettings settings;
        try
        {
            bool settingsExisted = File.Exists(settingsPath);
            settings = LoadSettingsOrRestoreBackup(settingsService, settingsPath, appDataDir, databasePath);
            ApplyLanguage(settings.Language);

            byte[] rawKey;
            if (settings.WrappedDatabaseKey is null && DatabaseKeyRecovery.DatabaseHasNotes(databasePath))
            {
                // Sin clave pero con notas: una clave nueva las dejaría ilegibles para siempre. Solo
                // vale la de una copia que de verdad las descifre.
                if (DatabaseKeyRecovery.FindSettingsBackup(appDataDir, databasePath) is not { } backup)
                {
                    MessageBox.Show(Strings.MissingKeyMessage(appDataDir), Strings.CannotStartTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                if (settingsExisted)
                {
                    settings.WrappedDatabaseKey = backup.WrappedKey;
                    settingsService.Save(settings);
                }
                else
                {
                    // Falta el fichero entero: se restaura la copia completa, no solo la clave, para
                    // no perder también la sincronización, el atajo y el resto de ajustes.
                    File.Copy(backup.Path, settingsPath, overwrite: true);
                    settings = settingsService.Load();
                    ApplyLanguage(settings.Language);
                }
                MessageBox.Show(Strings.KeyRestoredFromBackupMessage(backup.Day), Strings.DataRecoveredTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                rawKey = DatabaseKeyProvider.Unwrap(settings.WrappedDatabaseKey);
            }
            else if (settings.WrappedDatabaseKey is null)
            {
                // Sin clave y sin notas: es la primera vez que se abre Aldune en este usuario.
                _firstRun = true;
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
                Strings.KeyUnwrapFailedMessage,
                Strings.CannotStartTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        catch (SqliteException)
        {
            // Case (b), retry already attempted and failed inside BootstrapDatabase: the database
            // file is damaged beyond automatic recovery. Fatal.
            MessageBox.Show(
                Strings.DatabaseUnrecoverableMessage,
                Strings.CannotStartTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Strings.UnexpectedStartupFailureMessage(ex.Message),
                Strings.CannotStartTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (MonitorEnumerator.EnumerateMonitors().Count == 0)
        {
            // Practically impossible on real Windows (there's always at least one display), but
            // treat it as a fourth bootstrap failure mode rather than crashing with no explanation.
            MessageBox.Show(
                Strings.NoMonitorsMessage,
                Strings.CannotStartTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var syncService = new SyncService(repository, settings, settingsService);
        var coordinator = new AppCoordinator(repository, settings, syncService, settingsService);
        _coordinator = coordinator;
        _repository = repository;
        _settings = settings;
        _settingsService = settingsService;
        StartDailyBackups(appDataDir);
        _singleInstance.ListenForActivation(() =>
            Dispatcher.BeginInvoke(() => _coordinator?.OpenNotesManager()));
        try
        {
            // Barrido del ajuste opcional "borrar tareas completadas solas" (ver
            // Aldune.Core.TaskCompletion) para toda nota activa — no solo la que esté abierta,
            // que NoteWindow ya cubre por su cuenta al abrirse. Va dentro de este try y no fuera,
            // ni después de BuildDocks: GetByState ya descifra, así que si la clave no encaja
            // (caso (c) de abajo) es aquí donde puede aflorar por primera vez, y tiene que
            // traducirse al mismo mensaje.
            if (settings.AutoHideCompletedTasks)
            {
                var delay = settings.AutoHideCompletedTasksDelay;
                foreach (var note in repository.GetByState(NoteState.Active))
                {
                    repository.PruneExpiredCompletedTasks(note.Id, note.Text, delay);
                }
            }

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
                Strings.KeyMismatchMessage,
                Strings.CannotStartTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Cheap sweep so trash doesn't grow forever. This startup sweep is now the only
        // trigger for it — earlier there was also a dock-side "Archivadas" view that purged
        // on entry, but that view was removed by the fan-tabs redesign (NotesManagerWindow's
        // gear icon is the only way to browse archived/trashed notes now, and it doesn't
        // purge on open). The retention period is user-configurable now (AppSettings.TrashRetentionDays,
        // defaulting to NotesRepository.DefaultTrashRetentionDays), not a fixed constant.
        repository.PurgeExpiredTrash(TimeSpan.FromDays(settings.TrashRetentionDays));

        // Apagar, encender o reconfigurar un monitor con la app corriendo. Sin esto, Windows
        // reubica el dock huérfano del monitor que desaparece sobre el que queda, y acabas con dos
        // docks apilados en la misma pantalla — reportado por el usuario al apagar un monitor.
        //
        // Los docks se posicionan con coordenadas absolutas calculadas una sola vez (ver
        // EdgeGeometry.WindowRect), así que no pueden recolocarse solos: la única salida correcta
        // es reconstruirlos contra la lista de monitores nueva. Se engancha al final del arranque,
        // ya con el descifrado verificado, para no reconstruir nada si la app va a abortar.
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        // Sin esto, apagar o reiniciar el equipo con notas abiertas podía dejarlas sin guardar: el
        // cierre normal de una nota cancela su primer intento para reproducir la animación de salida
        // y solo guarda de verdad cuando esa animación termina (ver NoteWindow.OnClosingWithAnimation)
        // — pero Windows no espera a eso al terminar la sesión, así que el proceso podía cortarse
        // antes de que el guardado real llegara a dispararse. SessionEnding llega con margen antes de
        // que Windows fuerce el cierre, así que aquí se guarda ya, sin pasar por esa animación.
        SystemEvents.SessionEnding += OnSessionEnding;

        // Icono de bandeja: hasta ahora la app no tenia forma de cerrarse ni de configurarse — se
        // lanzaba a mano y se cerraba matando el proceso. Un dock sin ventana propia necesita
        // algun sitio donde vivir, y la bandeja es el sitio convenido en Windows para eso.
        // Atajo global. Se enciende segun lo guardado, y si falla (otra app ya tiene la
        // combinacion) se refleja en los ajustes en vez de fallar en silencio.
        _hotkey = new GlobalHotkey(coordinator.CreateAndOpenNote);
        if (settings.GlobalHotkeyEnabled) _hotkey.Enable(settings.Hotkey);

        // Segundo atajo, fijo (Ctrl+Alt+H): ocultar y devolver el dock. No es configurable a
        // propósito — es una acción de emergencia para cuando el dock estorba encima de un vídeo a
        // pantalla completa, y el sitio para configurar atajos ya está en Ajustes para el de crear
        // nota. Si el registro falla (otra app lo tiene cogido) no pasa nada: queda la bandeja.
        _dockHotkey = new GlobalHotkey(coordinator.ToggleDocksVisible);
        _dockHotkey.Enable(HotkeyBinding.DockToggleDefault);

        var hotkey = _hotkey;
        var loadedSettings = settings;
        coordinator.SettingsWindowFactory = () => new SettingsWindow(
            settingsService, loadedSettings, hotkey, coordinator, syncService,
            () => _updateNotifier?.CheckManually());
        coordinator.ConfigureAutomaticSync();
        coordinator.RebuildDocksAction = () =>
        {
            coordinator.CloseAllDocks();
            BuildDocks();
        };

        _toastCenter = new ToastCenter();
        _updateNotifier = new UpdateNotifier(_toastCenter);
        _trayIcon = new TrayIcon(coordinator, _updateNotifier.CheckManually);

        _reminderScheduler = new ReminderScheduler(repository, coordinator, _toastCenter);
        if (_firstRun) ShowWelcome(settings, coordinator);
        // Catch-up: avisa ya de lo vencido con la app cerrada. Diferido con BeginInvoke en vez de
        // llamado aquí mismo, en línea: este punto de OnStartup queda FUERA del último try/catch de
        // arranque (ver el comentario de DispatcherUnhandledException más arriba — OnStartup no
        // enruta de forma fiable sus excepciones ahí), así que un fallo aquí -por ejemplo un
        // SqliteException por un fichero de BD bloqueado, o un error al mostrar el aviso-
        // tumbaría la app entera al arrancar sin ningún mensaje, en una app que por lo demás explica
        // cualquier fallo de arranque. Diferir la llamada la deja correr ya bajo el bucle de mensajes
        // normal, con DispatcherUnhandledException cubriéndola como a cualquier otro error en
        // steady-state. Efecto colateral bueno: al ejecutarse después de BuildDocks (que ya hizo su
        // propio RefreshAll), y con el RefreshAll que ahora hace CheckDueReminders al final, el dock
        // queda repintado correctamente si esta primera pasada llega a limpiar algún recordatorio.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _reminderScheduler.CheckDueReminders());

        Exit += (_, _) =>
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _updateNotifier?.Dispose();
            _reminderScheduler?.Dispose();
            _toastCenter?.Dispose();
            _coordinator?.Dispose();
            _trayIcon?.Dispose();
            _hotkey?.Dispose();
            _dockHotkey?.Dispose();
            _backupTimer?.Stop();
            _singleInstance?.Dispose();
        };
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        _coordinator?.FlushAllOpenNotes();
    }

    /// <summary>
    /// Fija el idioma de toda la interfaz. <paramref name="explicitLanguage"/> es
    /// <c>AppSettings.Language</c> ("es"/"en"/null); con <c>null</c> se seguirá el idioma de
    /// Windows. Hay que llamarlo antes de construir cualquier ventana: los enlaces
    /// <c>{x:Static}</c> del XAML se resuelven al construir, no cuando cambia <c>Strings.Current</c>
    /// después — por eso <see cref="OnStartup"/> lo llama dos veces (una adivinando, por si el
    /// arranque falla antes de leer los ajustes de verdad).
    /// </summary>
    private static void ApplyLanguage(string? explicitLanguage)
    {
        Strings.Current = explicitLanguage is "es" or "en"
            ? explicitLanguage
            : System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en";

        NoteTitleHelper.PlaceholderTitle = Strings.NewNotePlaceholder;
    }

    /// <summary>
    /// Carga los ajustes. Si settings.json está dañado, restaura la copia más reciente cuya clave
    /// descifra las notas (guardando el dañado aparte) en vez de negarse a arrancar. Si no hay copia
    /// que sirva, deja pasar la JsonException: el arranque la convierte en el mensaje de siempre,
    /// que no genera una clave nueva encima.
    /// </summary>
    private static AppSettings LoadSettingsOrRestoreBackup(
        SettingsService settingsService, string settingsPath, string appDataDir, string databasePath)
    {
        try
        {
            return settingsService.Load();
        }
        catch (JsonException) when (DatabaseKeyRecovery.FindSettingsBackup(appDataDir, databasePath) is { } backup)
        {
            File.Copy(settingsPath, $"{settingsPath}.damaged-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", overwrite: true);
            File.Copy(backup.Path, settingsPath, overwrite: true);
            MessageBox.Show(Strings.SettingsRestoredFromBackupMessage(backup.Day), Strings.DataRecoveredTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return settingsService.Load();
        }
    }

    private DispatcherTimer? _backupTimer;
    private SettingsService? _settingsService;
    private bool _firstRun;

    /// <summary>
    /// La primera vez, una notificación que dice dónde está el dock y cómo crear una nota: sin notas
    /// el dock solo enseña sus botones en un borde, y es fácil no verlo. Una notificación y no una nota
    /// de bienvenida: una nota se sincronizaría, y cada dispositivo nuevo añadiría la suya a todos.
    /// Se va sola a los 20 s; su botón abre el gestor.
    /// </summary>
    private void ShowWelcome(AppSettings settings, AppCoordinator coordinator)
    {
        var hotkey = settings.GlobalHotkeyEnabled ? settings.Hotkey.DisplayName : null;
        _toastCenter?.Show(new ToastContent(Strings.WelcomeTitle, Strings.WelcomeMessage(settings.DockEdge, hotkey),
            [new ToastAction(Strings.WelcomeOpenManager, coordinator.OpenNotesManager, Primary: true)],
            AutoDismiss: TimeSpan.FromSeconds(20)));
    }
    private SingleInstance? _singleInstance;

    /// <summary>
    /// Copia diaria de notas y ajustes (ver <see cref="LocalBackup"/>): al arrancar y, como la app
    /// puede pasar días abierta, cada pocas horas por si ha cambiado el día. Un fallo de disco aquí
    /// no debe molestar: la próxima vuelta lo intenta otra vez.
    /// </summary>
    private void StartDailyBackups(string appDataDir)
    {
        // En un hilo aparte: con una base de datos grande, la copia congelaba la interfaz un momento.
        // La API de copia de SQLite usa sus propias conexiones y convive con las de la app.
        void Run() => Task.Run(() =>
        {
            try { LocalBackup.CreateDaily(appDataDir, DateTimeOffset.Now); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException) { }
        });

        Run();
        _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(3) };
        _backupTimer.Tick += (_, _) => Run();
        _backupTimer.Start();
    }

    private static string ResolveAppDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var current = Path.Combine(localAppData, BrandIdentity.AppDataDirectoryName);
        var legacy = Path.Combine(localAppData, BrandIdentity.LegacyAppDataDirectoryName);

        // El nombre visible cambió a Aldune, pero las instalaciones anteriores guardaban aquí sus
        // notas. Se migra una sola vez antes de abrir settings.json o notes.db; si Windows no deja
        // mover la carpeta, se conserva la ruta antigua para no dejar la app aparentemente vacía.
        if (!Directory.Exists(current) && Directory.Exists(legacy))
        {
            try { Directory.Move(legacy, current); }
            catch (IOException) { return legacy; }
            catch (UnauthorizedAccessException) { return legacy; }
        }

        return current;
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
                Strings.DatabaseRecoveredMessage,
                Strings.DatabaseRecoveredTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);

            return repository;
        }
    }
    
    private AppCoordinator? _coordinator;
    private TrayIcon? _trayIcon;
    private GlobalHotkey? _hotkey;
    private GlobalHotkey? _dockHotkey;
    private NotesRepository? _repository;
    private AppSettings? _settings;
    private DispatcherTimer? _rebuildDebounce;
    private int _displayRebuildAttempts;
    private DateTime _burstStartedAt;
    private string? _lastTickMonitorKey;
    private ReminderScheduler? _reminderScheduler;
    private UpdateNotifier? _updateNotifier;
    private ToastCenter? _toastCenter;

    /// <summary>
    /// Crea un dock por cada monitor conectado ahora mismo. Se llama al arrancar y cada vez que
    /// cambia la configuración de pantallas.
    /// </summary>
    private void BuildDocks()
    {
        if (_coordinator is null || _repository is null) return;

        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (monitors.Count == 0) return; // sin pantallas no hay nada que colocar; ya volverá otra

        // Filtro de monitor: miramos TargetMonitorIndex de los ajustes guardados. Si no esta fijado,
        // se mira la variable de entorno BrandIdentity.MonitorIndexEnvVar (util en pruebas manuales).
        int? targetIndex = _settings?.TargetMonitorIndex;
        if (targetIndex is null)
        {
            var onlyMonitor = Environment.GetEnvironmentVariable(BrandIdentity.MonitorIndexEnvVar);
            if (int.TryParse(onlyMonitor, out int envIndex))
            {
                targetIndex = envIndex;
            }
        }

        // Ajuste antiguo que solo guardaba la posición: se traduce una vez al identificador de esa
        // pantalla, que es lo que sobrevive a apagarla y encenderla.
        if (_settings is { TargetMonitorId: null, TargetMonitorIndex: { } legacy }
            && DockMonitorSelection.IdForLegacyIndex(monitors, legacy) is { } migratedId)
        {
            _settings.TargetMonitorId = migratedId;
            _settingsService?.Save(_settings);
        }

        var available = monitors;
        monitors = DockMonitorSelection.Select(monitors, _settings?.TargetMonitorId, targetIndex);
        DockDiagnostics.Write("pantallas",
            $"docks: pantallas vistas [{DockDiagnostics.Describe(available)}]; elegida {_settings?.TargetMonitorId ?? "todas"}; "
            + $"docks en [{string.Join(", ", monitors.Select(monitor => monitor.DeviceName))}]");

        var edge = _settings?.DockEdge ?? EdgePosition.Right;
        foreach (var monitor in monitors)
        {
            var dock = new EdgeDockWindow(edge, monitor, _repository, _coordinator, _settings);
            _coordinator.RegisterDock(dock);

            // Antes de Show(), no después: un dock recién construido no sabe todavía cuántas notas
            // hay (_noteCount empieza en 0), así que ApplyState lo trata como el caso "vacío" de
            // verdad y enseña los botones directamente — el diseño correcto cuando de verdad no hay
            // notas, pero aquí era temporal y se corregía solo un instante después, al llegar el
            // RefreshAll() de más abajo. Ese instante es justo el parpadeo que reportó el usuario al
            // cambiar de pantalla en Ajustes (se ve el pie de botones fuera de sitio y luego el dock
            // "se coloca"). Refrescar antes de mostrar deja el dock ya con sus datos reales desde el
            // primer frame que se pinta.
            dock.Refresh();
            dock.Show();
        }

        _coordinator.RefreshAll();
    }

    private static void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e) =>
        DockDiagnostics.Write("sistema", "energía: " + e.Mode);

    private static void OnSessionSwitch(object? sender, SessionSwitchEventArgs e) =>
        DockDiagnostics.Write("sistema", "sesión: " + e.Reason);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DockDiagnostics.Write("pantallas",
            "DisplaySettingsChanged: " + DockDiagnostics.Describe(MonitorEnumerator.EnumerateMonitors()));
        // Con retardo y reiniciable: cambiar de pantallas dispara varios DisplaySettingsChanged
        // seguidos (Windows reconfigura en pasos), y reconstruir los docks en cada uno significa
        // crear y destruir ventanas varias veces por nada. Esperar a que pare deja una sola
        // reconstrucción, ya contra la disposición definitiva.
        _rebuildDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _rebuildDebounce.Tick -= OnRebuildTick;
        _rebuildDebounce.Tick += OnRebuildTick;
        _rebuildDebounce.Stop();
        _burstStartedAt = DateTime.UtcNow;
        _lastTickMonitorKey = null;
        _displayRebuildAttempts = 0;
        _rebuildDebounce.Start();
    }

    /// <summary>
    /// Firma del conjunto de pantallas: qué monitores hay y qué tamaño tienen, por nombre de
    /// dispositivo. Solo sirve para comparar "¿sigue todo igual?" entre dos momentos de este mismo
    /// proceso, por eso no hace falta nada estable entre reinicios (ver el aviso de DeviceName en
    /// <see cref="MonitorInfo"/>): basta con que dos conjuntos distintos no firmen nunca igual.
    /// </summary>
    private static string MonitorSignature(IReadOnlyList<MonitorInfo> monitors) =>
        string.Join(";", monitors
            .OrderBy(monitor => monitor.DeviceName, StringComparer.Ordinal)
            .Select(monitor => monitor.DeviceName + "|" + (int)Math.Round(monitor.WorkArea.Width)
                + "x" + (int)Math.Round(monitor.WorkArea.Height)));

    /// <summary>Espera mínima antes de dar el conjunto de pantallas por bueno (ver OnRebuildTick).</summary>
    private const double MinMonitorSettleSeconds = 3;

    private void OnRebuildTick(object? sender, EventArgs e)
    {
        _displayRebuildAttempts++;
        var current = MonitorSignature(MonitorEnumerator.EnumerateMonitors());
        bool settled = current == _lastTickMonitorKey;
        _lastTickMonitorKey = current;

        // Solo se reconstruye cuando el conjunto ha cambiado de verdad: la primera pasada y cada vez
        // que la lista se mueva. Con la pantalla apagada, la segunda pasada ya ve lo mismo y no se
        // vuelve a tocar nada.
        if (!settled)
        {
            _coordinator?.RememberOpenNoteMonitors();
            _coordinator?.CloseAllDocks();
            BuildDocks();
            _coordinator?.RestoreOpenNotesAfterDisplayChange();
        }

        // Lo que fallaba al apagar y encender una pantalla: la lista tarda en volver a completarse
        // (el driver publica la pantalla en pasos), y el último rebuild podía caer en mitad del
        // camino — el dock se reconstruía con la otra pantalla y ahí se quedaba. Por eso no basta con
        // "estable un tick": hay una espera mínima para que una pantalla lenta al encenderse (1-2 s)
        // entre en el recuento, y un tope para que un cambio permanente (un monitor que se va de
        // verdad) no sondee eternamente.
        double elapsed = (DateTime.UtcNow - _burstStartedAt).TotalSeconds;
        if ((settled && elapsed >= MinMonitorSettleSeconds) || _displayRebuildAttempts >= 20)
        {
            _rebuildDebounce?.Stop();
            return;
        }

        _rebuildDebounce?.Stop();
        _rebuildDebounce?.Start();
    }
}

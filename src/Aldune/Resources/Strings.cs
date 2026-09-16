using Aldune.Core;

namespace Aldune.Resources;

/// <summary>
/// Todos los textos de la interfaz, en inglés y español, uno al lado del otro.
///
/// No son ficheros .resx: con dos idiomas y un puñado de cadenas, un diccionario a mano evita la
/// maquinaria de generación de código de recursos (que además depende de herramientas de Visual
/// Studio que no están garantizadas en cualquier máquina donde se compile esto) y sobre todo evita
/// el error más común de .resx — traducir un fichero satélite y olvidarse del otro — porque las dos
/// versiones de cada texto están en la misma línea.
///
/// <c>Current</c> se fija una sola vez al arrancar (<c>App.OnStartup</c>), antes de construir
/// cualquier ventana, a partir de <c>AppSettings.Language</c>. Los enlaces <c>{x:Static}</c> del
/// XAML se resuelven en el momento en que cada ventana se construye — por eso un cambio de idioma
/// en Ajustes no se ve en las ventanas ya abiertas hasta reiniciar la app: no hay nada más simple y
/// consistente que ofrecer sin meter un sistema de notificación de cambios en cada TextBlock.
/// </summary>
public static class Strings
{
    /// <summary>"en" o "es". Por defecto inglés; <c>App.OnStartup</c> lo resuelve de verdad.</summary>
    public static string Current { get; set; } = "en";

    private static string T(string en, string es) => Current == "es" ? es : en;

    // --- General / compartido entre ventanas -----------------------------------------------------

    public static string AppName => BrandIdentity.AppName;
    public static string ReminderManyDue(int count) => T($"{count} reminders pending", $"{count} recordatorios pendientes");
    public static string Archive => T("Archive", "Archivar");
    public static string Restore => T("Restore", "Restaurar");
    public static string MoveToTrash => T("Move to trash", "Mover a la papelera");
    public static string Trash => T("Trash", "Papelera");
    public static string Archived => T("Archived", "Archivada");

    /// <summary>
    /// El título que muestran la pestaña del dock y la barra de tareas para una nota sin texto
    /// todavía. Se copia a <see cref="Aldune.Core.NoteTitleHelper.PlaceholderTitle"/> al arrancar
    /// (Core no depende de idiomas) — ver <c>App.OnStartup</c>.
    /// </summary>
    public static string NewNotePlaceholder => T("Untitled", "Sin título");

    // --- Ventana de nota --------------------------------------------------------------------------

    public static string TitlePlaceholder => T("Title", "Título");
    public static string BodyPlaceholder => T("Write something…  ·  Ctrl+L for a task", "Escribe algo…  ·  Ctrl+L para una tarea");
    public static string MoreActionsTooltip => T("More actions", "Más acciones");
    public static string CloseTooltip => T("Close (Esc)", "Cerrar (Esc)");
    public static string ConvertToTask => T("Convert to task", "Convertir en tarea");
    public static string ConvertToBullet => T("Convert to list", "Convertir en lista");
    public static string CustomColor => T("Choose another color…", "Elegir otro color…");
    public static string CustomColorContrastError => T(
        "Enter a valid HEX color.",
        "Escribe un color HEX válido.");
    public static string CustomColorWindowTitle => T("Custom note color", "Color personalizado de la nota");
    public static string CustomColorWindowHint => T(
        "Text color adapts to keep your note readable.",
        "El color del texto se adapta para que la nota siga siendo legible.");
    public static string CustomColorPreview => T("Preview", "Vista previa");
    public static string CustomColorSpectrumHint => T(
        "Pick a tone visually, then fine-tune it below.",
        "Elige un tono visualmente y ajústalo debajo.");
    public static string CustomColorHexLabel => T("HEX color", "Color HEX");
    public static string CustomColorRgbLabel => T("Fine tune with RGB", "Ajuste fino con RGB");
    public static string CustomColorReadable => T("Readable text", "Texto legible");
    public static string CustomColorInvalidHex => T("Enter a color like #F5E3B3.", "Escribe un color como #F5E3B3.");
    public static string CustomColorCancel => T("Cancel", "Cancelar");
    public static string CustomColorApply => T("Use this color", "Usar este color");
    public static string RestoreSize => T("Restore size", "Restaurar tamaño");
    public static string ExportToMarkdown => T("Export to Markdown", "Exportar a Markdown");
    public static string MarkdownFileFilter => T("Markdown file (*.md)|*.md", "Archivo Markdown (*.md)|*.md");

    // --- Actualizaciones ---------------------------------------------------------------------------

    public static string TrayCheckForUpdates => T("Check for updates…", "Buscar actualizaciones…");
    public static string UpdateChecking => T("Checking for updates…", "Buscando actualizaciones…");
    public static string UpdateCurrentMessage => T("Aldune is up to date.", "Aldune está actualizado.");
    public static string UpdateErrorMessage => T(
        "The update check failed. Try again later.",
        "No se ha podido comprobar si hay actualizaciones. Inténtalo más tarde.");
    public static string UpdateAvailableMessage(string version) =>
        T($"Aldune {version} is available.",
          $"Aldune {version} está disponible.");
    public static string UpdateAvailableTitle => T("Update available", "Actualización disponible");
    public static string UpdateDownload => T("Download", "Descargar");
    public static string UpdateLater => T("Later", "Más tarde");
    public static string UpdateWindowTitle => T("Aldune update", "Actualización de Aldune");
    public static string UpdateClose => T("Close", "Cerrar");
    public static string UpdateOpenErrorMessage => T(
        "The download page couldn't be opened.",
        "No se ha podido abrir la página de descarga.");
    public static string ReminderMenuEntry => T("Reminder", "Recordatorio");
    public static string ReminderSet(DateTimeOffset dueAt) =>
        T($"Reminder: {dueAt:dd/MM HH:mm}", $"Recordatorio: {dueAt:dd/MM HH:mm}");
    public static string ReminderInOneHour => T("In 1 hour", "En 1 hora");
    public static string ReminderTonight => T("Tonight", "Esta noche");
    public static string ReminderTomorrowMorning => T("Tomorrow 9:00", "Mañana 9:00");
    public static string ReminderSave => T("Save", "Guardar");
    public static string ReminderClear => T("Remove reminder", "Quitar recordatorio");
    public static string PinnedOn => T("✓  Always on top", "✓  Siempre encima");
    public static string PinnedOff => T("Always on top", "Siempre encima");
    public static string PinnedOnHint => T("The note stays in front of other windows.", "La nota se queda por delante de las demás ventanas.");
    public static string PinnedOffHint => T("The note goes behind when you click another window.", "La nota se queda detrás al pinchar en otra ventana.");

    // --- Dock (mazo anclado al borde) -------------------------------------------------------------

    public static string ManageNotesTooltip => T("Manage notes", "Gestionar notas");
    public static string ManageNotesTaggedTooltip(string tag) => T(
        $"Manage the notes with the tag \"{tag}\"", $"Gestionar las notas con la etiqueta \"{tag}\"");
    public static string OpenAllNotesTooltip => T("Open all notes (closes them all if they're already open)", "Abrir todas las notas (las cierra todas si ya están abiertas)");
    public static string OpenAllMenuTitle => T("Open all with...", "Abrir todas con...");
    public static string OpenAllModeTitle => T("Opening mode", "Modo de apertura");
    public static string OpenAllMonitorTitle => T("Open notes on", "Abrir las notas en");
    public static string OpenAllNormal => T("Normal cascade", "Cascada normal");
    public static string CloseAllNotes => T("Close all notes", "Cerrar todas las notas");
    public static string RestoreOriginalPositions => T("Restore original positions", "Restaurar posiciones originales");
    public static string OpenAllGrid => T("Grid", "Cuadrícula");
    public static string OpenAllColumns => T("Columns", "Columnas");
    public static string NewNoteTooltip => T("New note", "Nueva nota");
    public static string NewNoteFromClipboard => T("New note from clipboard", "Nueva nota desde el portapapeles");
    public static string KeepDockOpenOn => T("✓  Keep dock open", "✓  Mantener el dock abierto");
    public static string KeepDockOpenOff => T("Keep dock open", "Mantener el dock abierto");
    public static string NewNoteTaggedTooltip(string tag) => T(
        $"New note with the tag \"{tag}\"", $"Nueva nota con la etiqueta \"{tag}\"");
    public static string OpenAllNotesTaggedTooltip(string tag) => T(
        $"Open the notes with the tag \"{tag}\" (closes them if they're already open)",
        $"Abrir las notas con la etiqueta \"{tag}\" (las cierra si ya están abiertas)");
    public static string ExitAppTooltip => T("Exit Aldune", "Salir de Aldune");
    public static string ScrollNotesUpTooltip => T("Show earlier notes", "Ver notas anteriores");
    public static string ScrollNotesDownTooltip => T("Show later notes", "Ver notas siguientes");
    public static string OpenNote => T("Open", "Abrir");
    public static string ProtectNote => T("Protect with password…", "Proteger con contraseña…");
    public static string RemoveProtection => T("Remove password protection", "Quitar protección con contraseña");
    public static string UnlockNote => T("Unlock note", "Desbloquear nota");
    public static string ProtectedNote => T("Protected note", "Nota protegida");
    public static string ProtectedNoteHint => T("Enter the password to open this note. It is not stored by Aldune.", "Escribe la contraseña para abrir esta nota. Aldune no la guarda.");
    public static string ProtectNoteHint => T("Choose a password for this note. If you lose it, the note cannot be recovered.", "Elige una contraseña para esta nota. Si la pierdes, no se podrá recuperar.");
    public static string PasswordLabel => T("Password", "Contraseña");
    public static string ConfirmPasswordLabel => T("Repeat password", "Repite la contraseña");
    public static string PasswordTooShort => T("Use at least 4 characters.", "Usa al menos 4 caracteres.");
    public static string PasswordsDoNotMatch => T("The passwords do not match.", "Las contraseñas no coinciden.");
    public static string WrongPassword => T("That password is not correct.", "Esa contraseña no es correcta.");
    public static string Accept => T("Accept", "Aceptar");
    public static string DockViewTooltip => T("Dock view", "Vista del dock");
    public static string DockViewTitle => T("Show in dock", "Mostrar en el dock");
    public static string DockViewActive => T("Active notes", "Notas activas");
    public static string DockViewArchived => T("Archived notes", "Notas archivadas");
    public static string DockViewTrash => T("Trash", "Papelera");
    public static string DockViewTags => T("By tag", "Por etiqueta");
    public static string DockViewAllTags => T("Choose a tag", "Elegir una etiqueta");
    public static string DockViewNoTags => T("No tags yet", "Aún no hay etiquetas");
    public static string NoteTags => T("Tags…", "Etiquetas…");
    public static string NoteTagsHint => T("Separate tags with commas.", "Separa las etiquetas con comas.");
    public static string SaveTags => T("Save tags", "Guardar etiquetas");
    public static string ManageTags => T("Manage tags", "Gestionar etiquetas");
    public static string NewTag => T("New tag", "Nueva etiqueta");
    public static string CreateTag => T("Create", "Crear");
    public static string DeleteTag => T("Delete", "Eliminar");
    public static string DeleteTagTooltip => T("Delete tag", "Eliminar etiqueta");
    public static string AllTags => T("All tags", "Todas las etiquetas");
    public static string TagFilterTooltip => T("Filter by tag", "Filtrar por etiqueta");
    public static string NoTagResults(string tag) =>
        T($"No notes have the tag “{tag}”.", $"Ninguna nota tiene la etiqueta «{tag}».");

    // --- Gestor de notas ---------------------------------------------------------------------------

    public static string ManageNotesTitle => T("Manage notes", "Gestionar notas");
    public static string SettingsTooltip => T("Settings", "Ajustes");
    public static string FilterActive => T("Active", "Activas");
    public static string FilterArchived => T("Archived", "Archivadas");
    public static string FilterTrashed => T("Trash", "Papelera");
    public static string SearchPlaceholder => T("Search your notes…", "Buscar en tus notas…");
    public static string ClearSearchTooltip => T("Clear search", "Borrar búsqueda");
    public static string SelectAll => T("Select all", "Seleccionar todo");
    public static string Delete => T("Delete", "Eliminar");
    public static string DeletePermanentlyTitle => T("Delete permanently", "Eliminar definitivamente");
    public static string Export => T("Export", "Exportar");
    public static string ExportFolderDialogTitle => T("Choose a folder to export to", "Elige una carpeta donde exportar");
    public static string ExportFormatTitle => T("Export notes", "Exportar notas");
    public static string ExportAsZipPrompt => T(
        "Export as a single .zip file?\n\nChoose “No” to get loose .md files in a folder instead.",
        "¿Exportar como un único archivo .zip?\n\nElige «No» para tener los archivos .md sueltos en una carpeta.");
    public static string ZipFileFilter => T("Zip file (*.zip)|*.zip", "Archivo zip (*.zip)|*.zip");

    public static string OneNote => T("1 note", "1 nota");
    public static string NotesCount(int count) => T($"{count} notes", $"{count} notas");
    public static string OneSelected => T("1 selected", "1 seleccionada");
    public static string SelectedCount(int count) => T($"{count} selected", $"{count} seleccionadas");

    public static string NoSearchResults(string query) =>
        T($"No note contains “{query}”.", $"Ninguna nota contiene «{query}».");
    public static string EmptyActive => T("No active notes. Create one with the + button on the screen edge.", "No hay notas activas. Crea una con el botón + del borde de la pantalla.");
    public static string EmptyArchived => T("You haven't archived any notes yet.", "No has archivado ninguna nota todavía.");
    public static string EmptyTrashed => T("Trash is empty. Anything you send here is deleted after 30 days.", "La papelera está vacía. Lo que envíes aquí se borra solo a los 30 días.");
    public static string EmptyNone => T("There are no notes yet. Create one with the + button on the screen edge.", "Todavía no hay notas. Crea una con el botón + del borde de la pantalla.");

    public static string ConfirmDeleteOne => T("1 note will be permanently deleted. This action cannot be undone.", "Se eliminará 1 nota definitivamente. Esta acción no se puede deshacer.");
    public static string ConfirmDeleteMany(int count) =>
        T($"{count} notes will be permanently deleted. This action cannot be undone.",
          $"Se eliminarán {count} notas definitivamente. Esta acción no se puede deshacer.");

    // --- Bandeja del sistema ------------------------------------------------------------------------

    public static string TrayNewNote => T("New note", "Nueva nota");
    public static string TrayManageNotes => T("Manage notes…", "Gestionar notas…");
    public static string TraySettings => T("Settings…", "Ajustes…");
    public static string TrayExit => T("Exit", "Salir");

    // --- Ventana de Ajustes -------------------------------------------------------------------------

    public static string SettingsWindowTitle => T("Aldune settings", "Ajustes de Aldune");
    public static string SettingsHeader => T("Settings", "Ajustes");
    public static string InterfaceModeSectionTitle => T("Interface mode", "Modo de interfaz");
    public static string InterfaceModeHint => T(
        "The simplified mode keeps the essentials and hides advanced options. You can switch back at any time.",
        "El modo simplificado conserva lo esencial y oculta las opciones avanzadas. Puedes volver al modo completo cuando quieras.");
    public static string SwitchToSimplifiedMode => T("Use simplified mode", "Usar modo simplificado");
    public static string SwitchToCompleteMode => T("Use complete mode", "Usar modo completo");
    public static string ApplicationSectionTitle => T("Application", "Aplicación");
    public static string RestartApplicationButton => T("Restart Aldune", "Reiniciar Aldune");
    public static string RestartAppTooltip => T("Restart Aldune", "Reiniciar Aldune");
    public static string ExitApplicationButton => T("Exit Aldune", "Salir de Aldune");

    public static string StartupCheckbox => T("Open Aldune at sign-in", "Abrir Aldune al iniciar sesión");
    public static string StartupHint => T("You can also remove it from Task Manager, in the Startup tab.", "También puedes quitarlo desde Administrador de tareas, en la pestaña Inicio.");

    public static string HotkeyCheckbox => T("Create a note with a keyboard shortcut", "Crear una nota con un atajo de teclado");
    public static string HotkeyResetButton => T("Reset", "Restablecer");
    public static string HotkeyRecording => T("Press a key combination… (Esc to cancel)", "Pulsa una combinación… (Esc para cancelar)");
    public static string HotkeyNeedsModifier => T("Add at least Ctrl, Alt, Shift or Win to the combination.", "Añade al menos Ctrl, Alt, Shift o Win a la combinación.");
    public static string HotkeyDisabled => T("Disabled.", "Desactivado.");
    public static string HotkeyWorks => T("Works from any application, without going to the screen edge.", "Funciona desde cualquier aplicación, sin tener que ir al borde de la pantalla.");
    public static string HotkeyConflict(string combo) =>
        T($"Couldn't activate it: another application already uses {combo}. Choose a different combination.",
          $"No se ha podido activar: otra aplicación ya usa {combo}. Elige otra combinación.");

    public static string MonitorSectionTitle => T("Screens where Aldune shows", "Pantallas donde mostrar Aldune");
    public static string MonitorSectionHint => T("Choose whether to show Aldune's dock on every screen or restrict it to one.", "Elige si deseas ver el dock de Aldune en todas las pantallas o restringirlo a una específica.");
    public static string AllScreens => T("On every connected screen", "En todas las pantallas conectadas");
    public static string ScreenLabel(int number, bool isPrimary, int width, int height)
    {
        var primary = isPrimary ? T(" · Primary", " · Principal") : "";
        return T($"Screen {number}{primary} ({width}×{height})", $"Pantalla {number}{primary} ({width}×{height})");
    }

    public static string HideOnFullscreenCheckbox => T("Hide dock in fullscreen", "Ocultar dock a pantalla completa");
    public static string HideOnFullscreenHint => T("Hides the dock automatically over any real fullscreen window — games, videos, presentations. Doesn't affect regular maximized windows (like a browser with tabs).", "Oculta el dock automáticamente ante cualquier ventana a pantalla completa de verdad — juegos, vídeos, presentaciones. No afecta a ventanas normales maximizadas (como el navegador con pestañas).");
    public static string KeepDockOpenCheckbox => T("Keep dock open", "Mantener el dock abierto");
    public static string KeepDockOpenHint => T("Keeps the dock expanded until you turn this off.", "Mantiene el dock desplegado hasta que desactives esta opción.");

    public static string EdgeSectionTitle => T("Screen edge", "Lado de la pantalla");
    public static string EdgeSectionHint => T("Which edge the Aldune dock lives on.", "En qué borde vive el dock de Aldune.");
    public static string EdgeRight => T("Right", "Derecha");
    public static string EdgeLeft => T("Left", "Izquierda");
    public static string EdgeTop => T("Top", "Arriba");
    public static string EdgeBottom => T("Bottom", "Abajo");

    public static string RememberPositionsCheckbox => T("Remember note positions", "Recordar la posición de las notas");
    public static string RememberPositionsHint => T("When you close a note, it reopens in the same spot and size on the desktop — like a real sticky note.", "Al cerrar una nota, la próxima vez se abre en el mismo sitio y con el mismo tamaño en el escritorio — como un post-it de verdad.");

    public static string AutoHideCompletedTasksCheckbox => T("Delete completed tasks automatically", "Borrar automáticamente las tareas completadas");
    public static string AutoHideCompletedTasksHint => T("A checked task stays for a while so you can still see it, then its line is removed from the note for good.", "Una tarea marcada se queda un tiempo por si quieres verla, y después su línea se borra de la nota para siempre.");
    public static string AutoHideCompletedTasksDelayLabel => T("After:", "Después de:");
    public static string TaskDelayMinutesUnit => T("minutes", "minutos");
    public static string TaskDelayHoursUnit => T("hours", "horas");
    public static string TaskDelayDaysUnit => T("days", "días");
    public static string TaskDelayWeeksUnit => T("weeks", "semanas");

    public static string TrashRetentionTitle => T("Trash", "Papelera");
    public static string TrashRetentionHint => T("A trashed note is deleted for good after this many days.", "Una nota en la papelera se borra para siempre pasados estos días.");
    public static string TrashRetentionDaysUnit => T("days", "días");

    public static string SyncSectionTitle => T("Sync between devices", "Sincronización entre dispositivos");
    public static string SyncSectionHint => T(
        "Notes stay encrypted. Use a shared folder, your own Aldune server, or WebDAV.",
        "Las notas permanecen cifradas. Usa una carpeta compartida o tu propio servidor de sincronización de Aldune.");
    public static string SyncEnabledCheckbox => T("Enable sync", "Activar sincronización");
    public static string SyncFolderOption => T("Shared folder / NAS", "Carpeta compartida / NAS");
    public static string SyncServerOption => T("Self-hosted server", "Servidor propio");
    public static string SyncWebDavOption => T("WebDAV / Nextcloud", "WebDAV / Nextcloud");
    public static string SyncFolderLabel => T("Folder:", "Carpeta:");
    public static string SyncServerUrlLabel => T("Server URL:", "URL del servidor:");
    public static string SyncServerTokenLabel => T("Access token:", "Token de acceso:");
    public static string SyncWebDavUsernameLabel => T("Username:", "Usuario:");
    public static string SyncWebDavPasswordLabel => T("Password:", "Contraseña:");
    public static string SyncWebDavHint => T(
        "Use an app password when your provider supports it. Aldune stores it protected on this device.",
        "Usa una contraseña de aplicación si tu proveedor la admite. Aldune la protege en este dispositivo.");
    public static string SyncBrowseButton => T("Browse…", "Examinar…");
    public static string SyncCodeLabel => T("Device sync code:", "Código de sincronización:");
    public static string SyncGenerateCodeButton => T("Generate / copy", "Generar / copiar");
    public static string SyncShareProfileButton => T("Share profile", "Compartir perfil");
    public static string SyncRevokeAccessButton => T("Revoke old codes", "Revocar códigos anteriores");
    public static string SyncRevokeAccessHint => T(
        "Replaces this link's key. Devices using an old code will need a new invitation.",
        "Cambia la clave de este vínculo. Los dispositivos con un código antiguo necesitarán una invitación nueva.");
    public static string SyncRevokeAccessConfirm => T(
        "Replace this profile's sync key? Every device using an older code will stop syncing until it imports a new invitation.",
        "¿Cambiar la clave de este perfil? Los dispositivos que usen un código antiguo dejarán de sincronizarse hasta importar una invitación nueva.");
    public static string SyncRevokeAccessCompletedStatus => T(
        "Old profile codes were revoked. Generate and share a new invitation with the devices that should keep syncing.",
        "Los códigos antiguos del perfil han sido revocados. Genera y comparte una invitación nueva con los dispositivos autorizados.");
    public static string SyncShareCodeHint => T(
        "This invitation also carries the profile name and self-hosted server URL. It never contains the access token.",
        "Esta invitación también lleva el nombre del perfil y la URL del servidor propio. Nunca contiene el token de acceso.");
    public static string SyncShareCodeCopiedStatus => T("Profile code copied to the clipboard.", "Código de perfil copiado al portapapeles.");
    public static string SyncImportCodeButton => T("Import code", "Importar código");
    public static string SyncNowButton => T("Sync now", "Sincronizar ahora");
    public static string SyncInProgressStatus => T("Syncing…", "Sincronizando…");
    public static string SyncProfileLabel => T("Sync profile", "Perfil de sincronización");
    public static string SyncProfileNameLabel => T("Name:", "Nombre:");
    public static string SyncNewProfile => T("New", "Nuevo");
    public static string SyncDeleteProfile => T("Delete", "Eliminar");
    public static string SyncProfileNewName(int number) => T($"Sync link {number}", $"Vínculo {number}");
    public static string SyncProfileDeleteConfirm => T(
        "Delete this sync profile? Its local connection settings will be removed.",
        "¿Eliminar este perfil de sincronización? Se borrarán sus ajustes de conexión locales.");
    public static string SyncProfileLastRemaining => T(
        "Keep at least one sync profile.",
        "Debe quedar al menos un perfil de sincronización.");
    public static string SyncScopeAll => T("All notes", "Todas las notas");
    public static string SyncScopeSelected => T("Selected notes", "Notas seleccionadas");
    public static string SyncScopeHint => T(
        "Choose which notes this sync link can exchange. The selection stays local to this device.",
        "Elige qué notas puede intercambiar este vínculo. La selección se guarda solo en este dispositivo.");
    public static string SyncChooseNotes => T("Choose notes…", "Elegir notas…");
    public static string SyncSelectedCount(int count) => T(
        $"{count} selected for sync", $"{count} seleccionadas para sincronizar");
    public static string SyncNotesWindowTitle => T("Choose notes to sync", "Elegir notas para sincronizar");
    public static string SyncNotesWindowHint => T(
        "Only the checked notes and their changes will use this sync link.",
        "Solo las notas marcadas y sus cambios usarán este vínculo de sincronización.");
    public static string SyncNotesSave => T("Save selection", "Guardar selección");
    public static string SyncNotesCancel => T("Cancel", "Cancelar");
    public static string SyncNotesEmpty => T("There are no notes to choose yet.", "Todavía no hay notas para elegir.");
    public static string SyncAutomaticCheckbox => T("Sync periodically", "Sincronizar periódicamente");
    public static string SyncAutomaticHint => T("Aldune checks for changes in the background while it is running.", "Aldune comprueba cambios en segundo plano mientras está abierta.");
    public static string SyncIntervalLabel => T("Every minutes:", "Cada minutos:");
    public static string SyncCodeHint => T(
        "Use the same code on each device. Keep it private: it unlocks your notes.",
        "Usa el mismo código en cada dispositivo. Guárdalo en privado: permite descifrar tus notas.");
    public static string SyncDisabledStatus => T("Sync is disabled.", "La sincronización está desactivada.");
    public static string SyncReadyStatus => T("Ready to sync.", "Lista para sincronizar.");
    public static string SyncCompletedStatus(int uploaded, int downloaded) =>
        T($"Sync complete: {uploaded} uploaded, {downloaded} downloaded.",
          $"Sincronización completada: {uploaded} subidas, {downloaded} descargadas.");
    public static string SyncErrorStatus(string details) => T($"Sync error: {details}", $"Error de sincronización: {details}");
    public static string SyncCodeCopiedStatus => T("Code copied to the clipboard.", "Código copiado al portapapeles.");
    public static string SyncCodeImportedStatus => T(
        "Code imported. Check the connection and add the access token if this is a self-hosted server.",
        "Código importado. Comprueba la conexión y añade el token de acceso si es un servidor propio.");
    public static string SyncInvalidCode => T("That sync code is not valid.", "Ese código de sincronización no es válido.");
    public static string SyncLastSyncNever => T("Last sync: never", "Última sincronización: nunca");
    public static string SyncLastSyncAt(DateTimeOffset at) =>
        T($"Last sync: {at.ToLocalTime():g}", $"Última sincronización: {at.ToLocalTime():g}");
    public static string SyncConflictsButton => T("Review conflicts", "Revisar conflictos");
    public static string SyncConflictsCount(int count) =>
        T($"{count} conflict(s) saved for review", $"{count} conflicto(s) guardado(s) para revisar");
    public static string SyncNoConflicts => T("No saved conflicts.", "No hay conflictos guardados.");
    public static string SyncConflictTitle => T("Sync conflicts", "Conflictos de sincronización");
    public static string SyncConflictHint => T(
        "Aldune kept the winning version active. You can restore the other version or dismiss this record.",
        "Aldune mantiene activa la versión ganadora. Puedes restaurar la otra versión o descartar este registro.");
    public static string SyncConflictRestore => T("Restore this version", "Restaurar esta versión");
    public static string SyncConflictDismiss => T("Dismiss", "Descartar");
    public static string SyncConflictDismissAll => T("Dismiss all", "Descartar todo");
    public static string SyncConflictDismissAllConfirm(int count) => T(
        $"Dismiss all {count} conflicts? The losing versions will be discarded permanently.",
        $"¿Descartar los {count} conflictos? Las versiones perdedoras se descartarán definitivamente.");
    public static string SyncConflictDeleted => T("Deleted version", "Versión eliminada");
    public static string SyncErrorTitle => T("Aldune sync", "Sincronización de Aldune");
    public static string SyncUnauthorizedStatus => T(
        "The server rejected the token. Paste only the value after ALDUNE_SYNC_TOKEN=.",
        "El servidor ha rechazado el token. Pega solo el valor que aparece después de ALDUNE_SYNC_TOKEN=.");

    public static string LanguageSectionTitle => T("Language", "Idioma");
    public static string LanguageSectionHint => T("Restarting Aldune applies the change to every window.", "Reiniciar Aldune aplica el cambio en todas las ventanas.");

    public static string QuickHelpTitle => T("Quick help", "Ayuda rápida");
    public static string QuickHelpHover => T("• Hover over the screen edge to fan out the notes.", "• Pasa el ratón por el borde de la pantalla para desplegar las notas.");
    public static string QuickHelpDrag => T("• Click a tab to open that note; drag it yourself to move it — it will remember the spot.", "• Haz clic en una pestaña para abrir esa nota; arrástrala tú para moverla — recordará el sitio.");
    public static string QuickHelpRightClick => T("• Right-click a tab: change color, archive, or send to trash without opening it.", "• Clic derecho en una pestaña: cambiar color, archivar o tirar a la papelera sin abrirla.");
    public static string QuickHelpTask => T("• Ctrl+L turns a line into a task. Click the checkbox to check it off, and Enter\n   keeps the list going on its own.", "• Ctrl+L convierte una línea en tarea. Haz clic en la casilla para marcarla, y Enter\n   sigue la lista sola.");
    public static string QuickHelpMoveLine => T("• Alt+Up/Down moves the current line up or down — handy for reordering a checklist.", "• Alt+Arriba/Abajo sube o baja la línea del cursor — útil para reordenar una lista de tareas.");
    public static string QuickHelpMenu => T("• The ⋯ button on a note opens color, “always on top,” archive and trash.", "• El botón ⋯ de una nota abre color, «siempre encima», archivar y papelera.");
    public static string QuickHelpEscape => T("• Esc closes the open note without losing what you wrote (it autosaves).", "• Esc cierra la nota abierta sin perder lo escrito (se guarda solo).");
    public static string QuickHelpHotkeyOn(string combo) =>
        T($"• {combo} creates a new note from anywhere.", $"• {combo} crea una nota nueva desde cualquier sitio.");
    public static string QuickHelpHotkeyOff => T("• The keyboard shortcut is off; turn it on above to create notes without going to the edge.", "• El atajo de teclado está desactivado; actívalo arriba para crear notas sin ir al borde.");
    public static string QuickHelpTray => T("• The tray icon opens the notes manager and these settings.", "• El icono de la bandeja abre el gestor de notas y estos ajustes.");
    public static string QuickHelpDockMenus => T(
        "• Right-click the dock buttons for views, tags, notes from the clipboard, settings and Keep dock open.",
        "• Clic derecho en los botones del dock para ver vistas, tags, notas desde el portapapeles, Ajustes y Mantener el dock abierto.");
    public static string QuickHelpAutoHideTasks => T(
        "• Completed tasks can disappear automatically after the delay configured in Settings.",
        "• Las tareas completadas pueden desaparecer automáticamente tras el plazo configurado en Ajustes.");
    public static string QuickHelpConflicts => T(
        "• Sync conflicts can be reviewed in Settings; restore the losing version or dismiss the record.",
        "• Los conflictos de sincronización se revisan en Ajustes; puedes restaurar la versión perdedora o descartar el registro.");
    public static string QuickHelpSync => T("• Sync now is available in the dock; automatic sync can be enabled in Settings.", "• Puedes sincronizar desde el dock y activar la sincronización automática en Ajustes.");
    public static string QuickHelpSearch => T("• In Manage notes, Ctrl+F focuses the search box and selects the current search.", "• En Gestionar notas, Ctrl+F enfoca la búsqueda y selecciona el texto actual.");

    public static string MinimizeWindowTooltip => T("Minimize", "Minimizar");
    public static string MaximizeWindowTooltip => T("Maximize", "Maximizar");
    public static string RestoreWindowTooltip => T("Restore", "Restaurar");

    // --- Arranque / errores de arranque --------------------------------------------------------------

    public static string UnexpectedErrorTitle => T("Aldune — error", "Aldune — error");
    public static string UnexpectedErrorMessage(string details) =>
        T($"An unexpected error occurred: {details}\n\nThe application will continue, but this particular action may not have completed.",
          $"Ha ocurrido un error inesperado: {details}\n\nLa aplicación continuará, pero esta acción concreta puede no haberse completado.");

    public static string CannotStartTitle => T("Aldune — can't start", "Aldune — no se puede iniciar");
    public static string DatabaseRecoveredTitle => T("Aldune — database recovered", "Aldune — base de datos recuperada");

    public static string KeyUnwrapFailedMessage => T(
        "The notes database can't be decrypted with the stored key.\n\n" +
        "The most likely cause is that this Windows user's password was reset, which permanently " +
        "destroys the protected key. This cannot be recovered technically, unless a previously " +
        "exported backup exists (that feature doesn't exist yet in this version).",
        "No se puede descifrar la base de datos de notas con la clave almacenada.\n\n" +
        "La causa más probable es que se haya restablecido la contraseña de Windows de este " +
        "usuario, lo que destruye de forma permanente la clave protegida. Esto no se puede " +
        "recuperar técnicamente, salvo que exista una copia de seguridad exportada previamente " +
        "(esa función aún no existe en esta versión).");

    public static string DatabaseUnrecoverableMessage => T(
        "The notes database couldn't be opened or recreated after an automatic recovery attempt. " +
        "The disk may be full, or the file may still be damaged.",
        "No se ha podido abrir ni recrear la base de datos de notas tras un intento de " +
        "recuperación automática. Es posible que el disco esté lleno o que el archivo siga " +
        "dañado.");

    public static string UnexpectedStartupFailureMessage(string details) =>
        T($"Aldune couldn't start due to an unexpected error: {details}",
          $"No se ha podido iniciar Aldune debido a un error inesperado: {details}");

    public static string NoMonitorsMessage => T("No connected monitor could be detected.", "No se ha podido detectar ningún monitor conectado.");

    public static string KeyMismatchMessage => T(
        "The notes database couldn't be decrypted with the current key.\n\n" +
        "The most likely cause is that the configuration file holding the key was lost, replaced, " +
        "or comes from another install. Your notes have NOT been deleted and remain stored " +
        "securely, but can't be read right now.",
        "La base de datos de notas no se ha podido descifrar con la clave actual.\n\n" +
        "La causa más probable es que el archivo de configuración que contenía la clave se " +
        "haya perdido, sustituido o proceda de otra instalación. Las notas NO se han eliminado " +
        "y siguen almacenadas de forma segura, pero no se pueden leer en este momento.");

    public static string DatabaseRecoveredMessage => T(
        "The existing notes file was damaged. A copy was kept in a \".corrupt-<date>\" file next " +
        "to the original, and a fresh, empty database was created.",
        "El archivo de notas existente estaba dañado. Se ha conservado una copia en un archivo " +
        "\".corrupt-<fecha>\" junto al original, y se ha creado una base de datos nueva y vacía.");
}

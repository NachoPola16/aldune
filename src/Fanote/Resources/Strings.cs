namespace Fanote.Resources;

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

    public static string AppName => "Fanote";
    public static string ReminderManyDue(int count) => T($"{count} reminders pending", $"{count} recordatorios pendientes");
    public static string Archive => T("Archive", "Archivar");
    public static string Restore => T("Restore", "Restaurar");
    public static string MoveToTrash => T("Move to trash", "Mover a la papelera");
    public static string Trash => T("Trash", "Papelera");
    public static string Archived => T("Archived", "Archivada");

    /// <summary>
    /// El título que muestran la pestaña del dock y la barra de tareas para una nota sin texto
    /// todavía. Se copia a <see cref="Fanote.Core.NoteTitleHelper.PlaceholderTitle"/> al arrancar
    /// (Core no depende de idiomas) — ver <c>App.OnStartup</c>.
    /// </summary>
    public static string NewNotePlaceholder => T("New note", "Nueva nota");

    // --- Ventana de nota --------------------------------------------------------------------------

    public static string TitlePlaceholder => T("Title", "Título");
    public static string BodyPlaceholder => T("Write something…  ·  Ctrl+L for a task", "Escribe algo…  ·  Ctrl+L para una tarea");
    public static string MoreActionsTooltip => T("More actions", "Más acciones");
    public static string CloseTooltip => T("Close (Esc)", "Cerrar (Esc)");
    public static string ConvertToTask => T("Convert to task", "Convertir en tarea");
    public static string ExportToMarkdown => T("Export to Markdown", "Exportar a Markdown");
    public static string MarkdownFileFilter => T("Markdown file (*.md)|*.md", "Archivo Markdown (*.md)|*.md");
    public static string PinnedOn => T("✓  Always on top", "✓  Siempre encima");
    public static string PinnedOff => T("Always on top", "Siempre encima");
    public static string PinnedOnHint => T("The note stays in front of other windows.", "La nota se queda por delante de las demás ventanas.");
    public static string PinnedOffHint => T("The note goes behind when you click another window.", "La nota se queda detrás al pinchar en otra ventana.");

    // --- Dock (mazo anclado al borde) -------------------------------------------------------------

    public static string ManageNotesTooltip => T("Manage notes", "Gestionar notas");
    public static string OpenAllNotesTooltip => T("Open all notes (closes them all if they're already open)", "Abrir todas las notas (las cierra todas si ya están abiertas)");
    public static string NewNoteTooltip => T("New note", "Nueva nota");
    public static string OpenNote => T("Open", "Abrir");

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

    public static string SettingsWindowTitle => T("Fanote settings", "Ajustes de Fanote");
    public static string SettingsHeader => T("Settings", "Ajustes");

    public static string StartupCheckbox => T("Open Fanote at sign-in", "Abrir Fanote al iniciar sesión");
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

    public static string MonitorSectionTitle => T("Screens where Fanote shows", "Pantallas donde mostrar Fanote");
    public static string MonitorSectionHint => T("Choose whether to show Fanote's dock on every screen or restrict it to one.", "Elige si deseas ver el dock de Fanote en todas las pantallas o restringirlo a una específica.");
    public static string AllScreens => T("On every connected screen", "En todas las pantallas conectadas");
    public static string ScreenLabel(int number, bool isPrimary, int width, int height)
    {
        var primary = isPrimary ? T(" · Primary", " · Principal") : "";
        return T($"Screen {number}{primary} ({width}×{height})", $"Pantalla {number}{primary} ({width}×{height})");
    }

    public static string HideOnFullscreenCheckbox => T("Hide dock in fullscreen", "Ocultar dock a pantalla completa");
    public static string HideOnFullscreenHint => T("Hides the dock automatically over any real fullscreen window — games, videos, presentations. Doesn't affect regular maximized windows (like a browser with tabs).", "Oculta el dock automáticamente ante cualquier ventana a pantalla completa de verdad — juegos, vídeos, presentaciones. No afecta a ventanas normales maximizadas (como el navegador con pestañas).");

    public static string EdgeSectionTitle => T("Screen edge", "Lado de la pantalla");
    public static string EdgeSectionHint => T("Which edge the Fanote dock lives on.", "En qué borde vive el dock de Fanote.");
    public static string EdgeRight => T("Right", "Derecha");
    public static string EdgeLeft => T("Left", "Izquierda");

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

    public static string LanguageSectionTitle => T("Language", "Idioma");
    public static string LanguageSectionHint => T("Restarting Fanote applies the change to every window.", "Reiniciar Fanote aplica el cambio en todas las ventanas.");

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

    // --- Arranque / errores de arranque --------------------------------------------------------------

    public static string UnexpectedErrorTitle => T("Fanote — error", "Fanote — error");
    public static string UnexpectedErrorMessage(string details) =>
        T($"An unexpected error occurred: {details}\n\nThe application will continue, but this particular action may not have completed.",
          $"Ha ocurrido un error inesperado: {details}\n\nLa aplicación continuará, pero esta acción concreta puede no haberse completado.");

    public static string CannotStartTitle => T("Fanote — can't start", "Fanote — no se puede iniciar");
    public static string DatabaseRecoveredTitle => T("Fanote — database recovered", "Fanote — base de datos recuperada");

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
        T($"Fanote couldn't start due to an unexpected error: {details}",
          $"No se ha podido iniciar Fanote debido a un error inesperado: {details}");

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

namespace Aldune.Core;

public sealed class AppSettings
{
    public byte[]? WrappedDatabaseKey { get; set; }

    /// <summary>Si la sincronización está activada. Desactivada por defecto hasta configurarla.</summary>
    public bool SyncEnabled { get; set; }

    /// <summary>Transporte de sincronización: carpeta local/UNC/NAS o servidor HTTP propio.</summary>
    public SyncTransportKind SyncTransport { get; set; } = SyncTransportKind.Folder;

    /// <summary>Ruta de la carpeta compartida para el transporte de carpeta.</summary>
    public string? SyncFolderPath { get; set; }

    /// <summary>URL base del servidor autohosteable, por ejemplo http://192.168.1.20:8087/.</summary>
    public string? SyncServerUrl { get; set; }

    /// <summary>Token de acceso al servidor, protegido localmente por DPAPI.</summary>
    public byte[]? WrappedSyncServerToken { get; set; }

    /// <summary>Usuario de la cuenta WebDAV, si el perfil usa ese transporte.</summary>
    public string? SyncWebDavUsername { get; set; }

    /// <summary>Contraseña o contraseña de aplicación WebDAV protegida localmente por DPAPI.</summary>
    public byte[]? WrappedSyncWebDavPassword { get; set; }

    /// <summary>Si se debe intentar sincronizar automáticamente de forma periódica.</summary>
    public bool SyncAutomatically { get; set; }

    /// <summary>Intervalo del sondeo automático, en minutos. Quince es el valor de fábrica.</summary>
    public int SyncIntervalMinutes { get; set; } = 15;

    /// <summary>Último intento completado correctamente, guardado en UTC.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Identificador estable de esta instalación, compartido solo como metadato de desempate.</summary>
    public string? SyncDeviceId { get; set; }

    /// <summary>Clave de sincronización protegida para este usuario y equipo, nunca subida al transporte.</summary>
    public byte[]? WrappedSyncKey { get; set; }

    /// <summary>Clave nueva pendiente de publicar durante una revocación segura.</summary>
    public byte[]? WrappedPendingSyncKey { get; set; }

    /// <summary>Indica que hay una rotación de clave que debe poder reanudarse tras un cierre.</summary>
    public bool SyncKeyRotationPending { get; set; }

    /// <summary>Si este vínculo sincroniza todo el cuaderno o solo una selección local.</summary>
    public SyncScopeKind SyncScope { get; set; } = SyncScopeKind.AllNotes;

    /// <summary>Identificadores incluidos cuando <see cref="SyncScope"/> es selectivo.</summary>
    public List<Guid> SyncNoteIds { get; set; } = new();

    /// <summary>Etiqueta incluida cuando <see cref="SyncScope"/> es <see cref="SyncScopeKind.Tag"/>.</summary>
    public string? SyncTag { get; set; }

    /// <summary>Perfiles independientes de sincronización. El perfil activo se refleja en los campos anteriores.</summary>
    public List<SyncProfileSettings> SyncProfiles { get; set; } = new();

    /// <summary>Identificador del perfil de sincronización que usan los botones y la sincronización automática.</summary>
    public string? ActiveSyncProfileId { get; set; }

    /// <summary>
    /// Si el atajo global de crear nota debe registrarse al arrancar.
    ///
    /// Por defecto <c>true</c>, y eso importa para los ficheros de ajustes que ya existen: un
    /// settings.json escrito antes de que este campo existiera no lo trae, System.Text.Json deja
    /// entonces el valor por defecto de la propiedad, y sale activado — que es lo que se quiere.
    /// Si el defecto fuera false, actualizar la app apagaria el atajo en silencio.
    /// </summary>
    public bool GlobalHotkeyEnabled { get; set; } = true;

    /// <summary>
    /// Modificadores y tecla del atajo global, en valores Win32. Nulos significa "el de fabrica":
    /// asi un settings.json anterior a este campo sigue funcionando, y ademas cambiar el atajo por
    /// defecto en una version futura llega a quien nunca lo toco, sin pisar a quien si lo hizo.
    /// </summary>
    public uint? HotkeyModifiers { get; set; }
    public uint? HotkeyKey { get; set; }

    /// <summary>La combinacion efectiva, resolviendo los nulos al valor de fabrica.</summary>
    public HotkeyBinding Hotkey =>
        HotkeyModifiers is { } mods && HotkeyKey is { } key && new HotkeyBinding(mods, key).IsValid
            ? new HotkeyBinding(mods, key)
            : HotkeyBinding.Default;

    /// <summary>
    /// Índice 0-based del monitor al que restringir Aldune, según la enumeración de Win32.
    /// <c>null</c> significa mostrar el dock en todas las pantallas conectadas (por defecto).
    /// </summary>
    public int? TargetMonitorIndex { get; set; }

    /// <summary>
    /// Identificador de hardware de la pantalla elegida (<see cref="MonitorInfo.StableId"/>). Manda
    /// sobre <see cref="TargetMonitorIndex"/>, que se conserva para ajustes antiguos: la posición en la
    /// lista de Windows cambia al apagar y encender pantallas, y el dock se quedaba en la otra. Ver
    /// <see cref="DockMonitorSelection"/>.
    /// </summary>
    public string? TargetMonitorId { get; set; }

    /// <summary>
    /// El dock va a la pantalla donde está el ratón, en vez de a una fija. Es el respaldo para quien
    /// apaga pantallas con su botón y tiene monitores que no dicen por DDC/CI que están apagados (ver
    /// <see cref="MonitorPowerTracker"/>). Falso por defecto: un ajuste antiguo carga igual.
    /// </summary>
    public bool DockFollowsMouse { get; set; }

    /// <summary>
    /// Si el dock debe ocultarse automáticamente cuando una aplicación o juego pase a pantalla
    /// completa real en ese monitor. Por defecto <c>true</c>.
    /// </summary>
    public bool HideOnFullscreen { get; set; } = true;

    /// <summary>Si el dock debe permanecer desplegado aunque el cursor salga de él.</summary>
    public bool KeepDockOpen { get; set; }

    /// <summary>
    /// Si la rueda se interpreta como gesto continuo (paso proporcional al delta) en vez de como
    /// muescas de ratón. Solo tiene sentido con trackpad de precisión: con un ratón los deltas ya son
    /// muescas completas y el resultado es el mismo. Ajustes lo deja deshabilitado y apagado cuando no
    /// detecta trackpad (ver <c>TouchpadDetector</c>), así que un <c>true</c> guardado en un equipo sin
    /// trackpad no supone nada: el ajuste solo se lee cuando hay gestos que interpretar.
    /// </summary>
    public bool TrackpadGestures { get; set; } = true;

    /// <summary>
    /// Borde de la pantalla donde se ancla el dock de Aldune. Por defecto derecha (EdgePosition.Right).
    /// </summary>
    public EdgePosition DockEdge { get; set; } = EdgePosition.Right;

    /// <summary>
    /// Si las notas deben recordar su última posición y tamaño en el escritorio cuando el usuario las mueve
    /// (comportamiento libre tipo post-it). Por defecto <c>true</c>.
    /// </summary>
    public bool RememberNotePositions { get; set; } = true;

    /// <summary>Distribución que usa el botón de abrir todas por defecto.</summary>
    public NoteLayoutTemplate DefaultNoteLayout { get; set; } = NoteLayoutTemplate.Normal;

    /// <summary>
    /// Idioma de la interfaz: "es" o "en". <c>null</c> significa "sigue el idioma de Windows" —
    /// mientras el usuario nunca elija uno explícitamente en Ajustes, la app sigue el idioma del
    /// sistema aunque este cambie entre arranques. Se fija de forma explícita y permanente en
    /// cuanto se elige uno en el selector de Ajustes, igual que <see cref="TargetMonitorIndex"/> o
    /// <see cref="DockEdge"/>.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>Si Ajustes muestra solo las opciones esenciales para una interfaz más sencilla.</summary>
    public bool SimplifiedMode { get; set; }

    /// <summary>
    /// Si las tareas marcadas como hechas (☒) deben borrarse solas de la nota pasado un tiempo. Por
    /// defecto desactivado: es una edición automática del texto de la nota, así que nadie la sufre
    /// sin haberla pedido explícitamente en Ajustes.
    /// </summary>
    public bool AutoHideCompletedTasks { get; set; }

    /// <summary>
    /// Si una tarea recién marcada baja al final de su lista (y vuelve a subir al desmarcarla), como en
    /// una lista de la compra: lo pendiente queda arriba y junto. Es lo más parecido a "ocultar lo
    /// hecho" que permite el texto plano, y no borra nada. Desactivado por defecto: mueve líneas del
    /// texto, así que nadie lo sufre sin pedirlo (ver <see cref="TaskLists.SettleToggled"/>).
    /// </summary>
    public bool MoveCompletedTasksToEnd { get; set; }

    /// <summary>Cuántas unidades de <see cref="AutoHideCompletedTasksDelayUnit"/> esperar. Por defecto 1.</summary>
    public int AutoHideCompletedTasksDelayValue { get; set; } = 1;

    /// <summary>Por defecto días: "1 día" es el plazo de fábrica si se activa el ajuste sin tocar nada más.</summary>
    public TaskDelayUnit AutoHideCompletedTasksDelayUnit { get; set; } = TaskDelayUnit.Days;

    /// <summary>Los dos campos de arriba ya convertidos a <see cref="TimeSpan"/>.</summary>
    public TimeSpan AutoHideCompletedTasksDelay => AutoHideCompletedTasksDelayUnit switch
    {
        TaskDelayUnit.Minutes => TimeSpan.FromMinutes(AutoHideCompletedTasksDelayValue),
        TaskDelayUnit.Hours => TimeSpan.FromHours(AutoHideCompletedTasksDelayValue),
        TaskDelayUnit.Weeks => TimeSpan.FromDays(7 * AutoHideCompletedTasksDelayValue),
        _ => TimeSpan.FromDays(AutoHideCompletedTasksDelayValue),
    };

    /// <summary>
    /// Días que una nota se queda en la papelera antes de que la purga automática la borre para
    /// siempre (ver <see cref="NotesRepository.PurgeExpiredTrash"/>). Era una constante fija
    /// (<see cref="NotesRepository.DefaultTrashRetentionDays"/>, todavía el valor de fábrica aquí) —
    /// pasó a Ajustes tras revisar qué otros valores fijos del código tenía sentido dejar elegir al
    /// usuario, ya que no hay ningún motivo técnico para que 30 sea mejor que otro número para
    /// alguien en concreto.
    /// </summary>
    public int TrashRetentionDays { get; set; } = NotesRepository.DefaultTrashRetentionDays;

    /// <summary>Vista persistente del dock. La papelera nunca entra en la vista normal por accidente.</summary>
    public DockViewKind DockView { get; set; } = DockViewKind.Active;

    /// <summary>Etiqueta seleccionada cuando <see cref="DockView"/> es <see cref="DockViewKind.Tag"/>.</summary>
    public string? DockTagFilter { get; set; }

    /// <summary>
    /// Tema de color de las notas. Nulo = Clásico, la paleta de siempre: un settings.json anterior
    /// a los temas carga así y nada cambia hasta que el usuario elige otro. Un id que ya no existe
    /// también cae a Clásico (ver <see cref="NoteThemes.Resolve"/>).
    /// </summary>
    public string? ActiveThemeId { get; set; }

    /// <summary>De qué tono nacen las notas nuevas. Por defecto claras, como siempre.</summary>
    public NoteTone NewNoteTone { get; set; } = NoteTone.Light;

    /// <summary>Cómo se elige el color de una nota nueva dentro del tema.</summary>
    public NoteColorAssignment ColorAssignment { get; set; } = NoteColorAssignment.RotateAvoidNeighbors;

    /// <summary>Color de <see cref="NoteColorAssignment.Fixed"/>. Nulo = el primero del tono pedido.</summary>
    public string? FixedNoteColor { get; set; }

    /// <summary>Temas creados por el usuario. Locales a este equipo: no se sincronizan en la 1.0.</summary>
    public List<NoteTheme> CustomThemes { get; set; } = new();
}

# Fanote Fase 3a — Multi-monitor real + DPI por monitor (design spec)

Primera sub-entrega de la Fase 3 (multi-monitor + DPI, ver
`docs/STATUS.md`). La Fase 3 completa se troceó porque mezcla piezas
independientes: esta sub-entrega cubre solo la parte técnica dura y
aislada — geometría real por monitor, DPI, y el coordinador de ventanas
a nivel de app. Deliberadamente fuera de esta entrega (ver "Fuera de
alcance" más abajo): el toggle de Ajustes para elegir monitores, IDs
estables de dispositivo, y el comportamiento de desconectar/reconectar
un monitor.

## Contexto y motivación

Hoy Fanote crea un único `EdgeDockWindow`, siempre en el monitor
principal, usando `SystemParameters.WorkArea` para su geometría — una
API de WPF que **solo devuelve el área de trabajo del monitor
principal**, sin importar cuántos monitores haya conectados. Tampoco
existe ningún `app.manifest` que declare a la aplicación consciente de
DPI por monitor (`PerMonitorV2`), así que hoy corre bajo el
comportamiento por defecto de Windows para un proceso sin declarar.

Esto es exactamente el hueco que la spec v1 (`2026-08-30-fanote-v1-design.md`)
señala en su sección "Núcleo": "consciente de DPI por monitor (Per-Monitor
V2)" y "multi-monitor: la app puede mostrarse en todas las pantallas
conectadas". Los prerrequisitos ya identificados en `docs/STATUS.md` tras
la revisión final de la Fase 2 son el punto de partida de este diseño:

1. `_openNoteWindows` (el diccionario que evita abrir la misma nota dos
   veces) y la relación `NoteWindow` → `EdgeDockWindow` propietario viven
   hoy dentro de `EdgeDockWindow`, que pasará a ser una instancia **por
   monitor**. Si esto no se mueve a un coordinador de nivel de aplicación
   antes de crear el segundo dock, se reproduce el bug de "ventanas
   duplicadas"/"etiqueta desactualizada" que costó varias rondas arreglar
   en la Fase 2.
2. `SystemParameters.WorkArea` no sirve para monitores no principales —
   hace falta geometría real vía Win32.
3. `ScreenOrigin` sigue fijo al literal `"primary"` en todas las notas —
   esta entrega **no** lo toca (ver "Fuera de alcance").

## Alcance de esta sub-entrega

**Dentro:**

- Declarar la app consciente de DPI por monitor (`PerMonitorV2`) vía
  `app.manifest`.
- Enumerar los monitores conectados (Win32) con su área de trabajo real
  en píxeles y su DPI real.
- Crear **un `EdgeDockWindow` por cada monitor conectado**, siempre —
  sin toggle de configuración todavía (eso es de la siguiente
  sub-entrega). Esto ya es uno de los dos comportamientos que la spec v1
  permite ("todas las pantallas conectadas").
- Cada dock usa la geometría y DPI reales de *su* monitor, no las del
  monitor principal.
- Coordinador a nivel de aplicación (`AppCoordinator`) que centraliza:
  las notas ya abiertas (evita duplicados entre docks), refrescar todos
  los docks a la vez, y la ventana única de "Gestionar notas".
- Detección de monitores **solo al arrancar** — no se maneja conectar o
  desconectar un monitor con la app ya corriendo (ver "Fuera de
  alcance").

**Fuera de alcance** (para una sub-entrega posterior de la Fase 3):

- Selección de qué monitores usar desde Ajustes (hoy no existe ninguna
  UI de Ajustes; `AppSettings` solo guarda la clave de cifrado).
- Posición de borde distinta por pantalla (la spec v1 ya la aplaza a
  v1.1; sigue habiendo una sola posición de borde global).
- IDs estables de dispositivo para `ScreenOrigin` (sigue siendo el
  literal `"primary"` para todas las notas).
- Conectar/desconectar un monitor con la app ya corriendo
  (`WM_DISPLAYCHANGE`), y el comportamiento de "mostrar temporalmente en
  la pantalla principal sin reescribir el origen" que depende de tener
  IDs estables primero.
- Cambiar la escala de DPI en Windows con la app ya corriendo: WPF con
  `PerMonitorV2` reescala solo las ventanas existentes, así que no
  debería romperse, pero el reposicionamiento fino específico del
  docking al borde no se ha probado para ese caso — límite conocido.
- Con varios docks pero `ScreenOrigin` todavía sin diferenciar por
  monitor: **todos los docks muestran exactamente el mismo listado
  completo de notas activas** (son espejo unos de otros). Crear o
  archivar una nota desde cualquier dock actualiza todos. Cuando una
  sub-entrega posterior añada IDs estables por monitor, cada dock podrá
  empezar a filtrar por su propio origen.

## Componentes

### `Fanote.Core.MonitorInfo` (nuevo)

Tipo de dato puro, sin dependencias de Win32/WPF — fácil de testear:

```csharp
public readonly record struct MonitorInfo(
    string DeviceName,      // ej. "\\.\DISPLAY1" — NO es el id estable
                            // futuro, solo lo que da Win32 hoy; ver
                            // "Fuera de alcance"
    WorkingArea WorkArea,   // ya convertida a DIPs de ESE monitor
    double DpiScale,        // 1.0 = 96 DPI, 1.5 = 150%, etc.
    bool IsPrimary);
```

### Conversión píxeles → DIP (nuevo, `Fanote.Core`)

Win32 da rectángulos en píxeles físicos; WPF interpreta
`Window.Left/Top/Width/Height` en DIPs relativas a la escala de DPI de
ese monitor concreto (una vez declarado `PerMonitorV2`). Una función
pura y testeable hace la conversión:

```csharp
public static class DpiConversion
{
    public static WorkingArea ToWorkingArea(Rect pixelBounds, double dpiScale) =>
        new(pixelBounds.X / dpiScale, pixelBounds.Y / dpiScale,
            pixelBounds.Width / dpiScale, pixelBounds.Height / dpiScale);
}
```

Con esto, **`EdgeGeometry.cs` no cambia nada** — sus cálculos de
`PillRect`/`ExpandedRect` son aritmética de ancho/alto pura, no les
importa la unidad mientras sea consistente. Cada dock recibe ya las
DIPs correctas de su propio monitor.

### Enumeración de monitores (nuevo, `Fanote.Interop`)

Nuevos métodos junto a `NativeMethods.cs` (o un fichero nuevo
`MonitorEnumerator.cs` en el mismo namespace si `NativeMethods.cs` se
queda demasiado grande): `EnumDisplayMonitors` + `GetMonitorInfoW` (área
de trabajo en píxeles + si es el principal) + `GetDpiForMonitor` (DPI
real). Expone `IReadOnlyList<MonitorInfo> EnumerateMonitors()`, usando
`DpiConversion.ToWorkingArea` internamente. Se llama una vez, al
arrancar (`App.xaml.cs`).

Se eligió Win32 puro (no `System.Windows.Forms.Screen`) para no meter
una dependencia a WinForms solo para esto, y porque coincide con el
patrón ya establecido en el proyecto (`NativeMethods.cs`) y con la
justificación de stack tecnológico de la spec v1 ("acceso completo a
Win32 vía P/Invoke cuando hace falta bajar de nivel").

### `app.manifest` (nuevo, `src/Fanote/`)

Declara `<dpiAwareness>PerMonitorV2</dpiAwareness>` (namespace
`http://schemas.microsoft.com/SMI/2016/WindowsSettings`). Referenciado
desde `Fanote.csproj`. Sin esto, nada de lo anterior tiene efecto:
Windows seguiría tratando a la app como no consciente de DPI por
monitor y escalaría un mapa de bits ya renderizado en vez de dejar que
cada ventana se renderice nítida en su propio monitor.

### `AppCoordinator` (nuevo, `Fanote.Windowing`)

Una sola instancia para toda la app (no una por dock). Sustituye lo que
hoy vive dentro de `EdgeDockWindow`:

```csharp
public sealed class AppCoordinator
{
    private readonly NotesRepository _repository;
    private readonly Dictionary<Guid, NoteWindow> _openNoteWindows = new();
    private readonly List<EdgeDockWindow> _docks = new();
    private NotesManagerWindow? _notesManagerWindow;

    public AppCoordinator(NotesRepository repository) => _repository = repository;

    public void RegisterDock(EdgeDockWindow dock) => _docks.Add(dock);

    public int OpenNoteWindowCount => _openNoteWindows.Count;

    public void OpenOrActivateNote(Note note, EdgeDockWindow requestingDock)
    {
        if (_openNoteWindows.TryGetValue(note.Id, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            NativeMethods.ForceActivate(existing);
            return;
        }

        var noteWindow = new NoteWindow(note, _repository, this);
        requestingDock.PositionNoteWindow(noteWindow);
        _openNoteWindows[note.Id] = noteWindow;
        noteWindow.Closed += (_, _) => _openNoteWindows.Remove(note.Id);
        noteWindow.Show();
        NativeMethods.ForceActivate(noteWindow);
    }

    public void OpenOrActivateNotesManager()
    {
        if (_notesManagerWindow is not null)
        {
            _notesManagerWindow.Activate();
            NativeMethods.ForceActivate(_notesManagerWindow);
            return;
        }

        _notesManagerWindow = new NotesManagerWindow(_repository, this);
        _notesManagerWindow.Closed += (_, _) => _notesManagerWindow = null;
        _notesManagerWindow.Show();
        NativeMethods.ForceActivate(_notesManagerWindow);
    }

    public void RefreshAll()
    {
        foreach (var dock in _docks) dock.Refresh();
    }
}
```

`requestingDock.PositionNoteWindow(...)` se queda en `EdgeDockWindow`
(necesita la posición/límites de **ese** monitor concreto), pero el
contador de notas abiertas para calcular el escalón de la cascada pasa
a leerse de `coordinator.OpenNoteWindowCount` en vez de un diccionario
por dock, para que la cascada sea consistente aunque las notas estén
repartidas entre monitores.

### Cambios en clases existentes

- **`EdgeDockWindow`**: constructor pasa a recibir `MonitorInfo` en vez
  de `WorkingArea` suelta (internamente sigue guardando el
  `WorkingArea` de `MonitorInfo.WorkArea` para `EdgeGeometry`), y recibe
  el `AppCoordinator`. `OnTabClick` delega en
  `_coordinator.OpenOrActivateNote(note, this)` en vez de gestionar
  `_openNoteWindows` él mismo. `OnManageArchiveClick` delega en
  `_coordinator.OpenOrActivateNotesManager()`. Pierde el campo
  `_openNoteWindows` y `_notesManagerWindow` (se van al coordinador).
  `PositionNoteWindow` pasa de `private` a `internal` — el coordinador
  (misma librería, distinta clase) necesita poder llamarlo.
- **`NoteWindow`**: el constructor recibe `AppCoordinator coordinator`
  en vez de `EdgeDockWindow owner`; todas las llamadas a
  `_owner.Refresh()` pasan a ser `_coordinator.RefreshAll()`.
- **`NotesManagerWindow`**: mismo cambio — recibe el coordinador, sus
  acciones en bloque llaman a `_coordinator.RefreshAll()`.
- **`App.xaml.cs`**: en vez de crear un único `EdgeDockWindow` con
  `SystemParameters.WorkArea`, enumera monitores
  (`MonitorEnumerator.EnumerateMonitors()`), crea un `AppCoordinator`,
  crea un `EdgeDockWindow` por cada `MonitorInfo` (todos con
  `EdgePosition.Right` — la posición de borde sigue siendo global, sin
  cambios), registra cada uno en el coordinador, llama a
  `coordinator.RefreshAll()` una vez (sustituye el actual
  `dock.Refresh()` suelto — el primer intento real de descifrado sigue
  ocurriendo aquí, el `try/catch` de `AuthenticationTagMismatchException`
  no cambia), y muestra todos los docks.

## Manejo de errores

- **Cero monitores detectados** (extremadamente improbable en Windows
  real, pero hay que cubrirlo): se trata como un cuarto caso de fallo de
  arranque, con el mismo patrón que los tres ya existentes en
  `App.xaml.cs` (mensaje claro + `Shutdown(1)`).
- **`GetDpiForMonitor` falla para un monitor concreto**: se asume DPI 96
  (escala 1.0) para ese monitor en vez de propagar la excepción — mismo
  espíritu que `NativeMethods.ApplyRoundedCorners`, que no rompe nada en
  sistemas donde la API subyacente no está disponible.
- **Cambio de escala de DPI en caliente** (sin desconectar el monitor):
  no se maneja de forma especial en esta entrega; queda como límite
  conocido documentado arriba.

## Testing

- `DpiConversion.ToWorkingArea`: tests unitarios normales en
  `Fanote.Core.Tests`, con varias escalas (100%, 150%, 200%) — pura
  aritmética, sin Win32 de por medio.
- `MonitorInfo`: tipo de dato, sin lógica propia que testear más allá de
  la conversión anterior.
- Enumeración real de monitores, creación de varios `EdgeDockWindow`, y
  el `AppCoordinator` en conjunto: Win32/WPF puro, se verifica a mano
  (igual que el resto de la mecánica de ventana de esta app). El usuario
  tiene un segundo monitor disponible (en vertical) para probar esto en
  persona. Checklist manual sugerido:
  1. Un solo monitor conectado — regresión: debe verse exactamente como
     hoy.
  2. Dos monitores — aparece un dock en cada uno, ambos en el borde
     derecho, cada uno con su geometría/tamaño correctos para su propio
     monitor (el vertical incluido — `EdgeGeometry` es agnóstico a
     orientación, pero es la primera vez que se prueba con un monitor en
     vertical).
  3. Crear una nota desde el dock del monitor A → aparece también en la
     pestaña del dock del monitor B (mismo listado, ver "Fuera de
     alcance").
  4. Abrir la misma nota haciendo clic desde los dos docks (si la nota
     aparece en ambos) → se activa la misma ventana, nunca se duplica.
  5. Abrir "Gestionar notas" desde el dock del monitor A y luego desde
     el B → es la misma ventana (se activa, no se abre una segunda).
  6. Archivar una nota desde `NotesManagerWindow` → los dos docks
     actualizan su pestaña.

## Orden de implementación sugerido

1. `Fanote.Core.MonitorInfo` + `DpiConversion` (TDD, sin dependencias).
2. `app.manifest` + enumeración Win32 de monitores
   (`Fanote.Interop`) — verificable con un `Console.WriteLine` temporal
   o test manual antes de tocar las ventanas.
3. `AppCoordinator` (aún sin usar).
4. Refactor de `EdgeDockWindow`/`NoteWindow`/`NotesManagerWindow` para
   usar el coordinador — comportamiento debe seguir idéntico con un solo
   monitor (regresión).
5. `App.xaml.cs`: crear un dock por monitor real.
6. Verificación manual completa con el segundo monitor.

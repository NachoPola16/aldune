# Aldune — macOS, Linux y web: ¿hay port? Estudio y propuesta

> **Estado: macOS, Linux y web siguen sin decidir; el cliente móvil ya tiene plan decidido**
> (Android primero, en tableta: `MOBILE_PORT.md` §0.1). En ninguno de los casos hay código escrito.
> Sesión del 2026-09-28. Documento hermano de
> [`MOBILE_PORT.md`](MOBILE_PORT.md) (Android e iOS). Ese contesta "¿y en el teléfono?"; este contesta
> "¿y en el Mac, y en Linux, y en el navegador?".
>
> Regla de lectura: aquí se separa a propósito **lo verificado** (con enlace) de **lo que hay que
> comprobar en un spike**. No se ha escrito ni una línea de código para ninguno de estos sistemas.

## 0. Resumen para quien tenga prisa

| Sistema | ¿Lo hacemos? | Por qué |
|---|---|---|
| **Windows** | Ya está | Es el producto: 1.4.1, dock al canto, instalador y MSIX en camino |
| **Windows en ARM64** | **Sí, y es barato** (1 sesión) | Hoy se publica solo `win-x64`; el instalador **sí** acepta ARM64 (`x64compatible` incluye Windows 11 ARM), pero corre **emulado**. Un RID más no añade mantenimiento: §9.1 |
| **Servidor en ARM** (Raspberry Pi, NAS) | Ya funciona | El `Dockerfile` parte de una imagen multi-arquitectura y compila en destino: §9.2 |
| **Android** | **Sí, primero: decidido el 2026-09-28** | Es donde el port tiene sentido y se reutiliza Core entero (ver `MOBILE_PORT.md` §0.1: sin Mac, sin licencias, tableta como referencia) |
| **iOS** | Sí, después de Android | Mismo código, otro toolchain y una extensión en Swift |
| **macOS** | **No, por ahora** | Es el único sistema donde **el concepto ya existe y está ocupado** (Hold My Notes, noty, SideNotes) y encima es donde la mecánica de Aldune (ventanas recortadas al canto) hay que rehacerla de otra manera |
| **Linux** | **No como app**; sí como servidor | El dock al canto no es portable a Wayland sin ser una extensión del escritorio (GNOME no soporta `wlr-layer-shell`); y el `SyncServer` ya corre en Linux, que es lo que un usuario de Linux necesita para sincronizar |
| **Web / WASM** | **No** | Sin widgets, sin notificaciones fiables, y el texto acabaría en el navegador. Ya descartado en `MOBILE_PORT.md` §11 |
| **ChromeOS** | Cubierto por Android | ChromeOS ejecuta aplicaciones de Play Store, así que el cliente Android (§`MOBILE_PORT.md`) vale; y su Linux (Crostini) es el caso de Linux de §3 |

**Lo que sí deberíamos hacer con los sistemas que faltan, y es barato:** dejar el **Core listo para
cualquier cliente** (los llamados "seams", `MOBILE_PORT.md` §5). Con eso, el día que macOS o Linux
interese, no hay que tocar la lógica, solo escribir interfaz. Esa es la parte que se recomienda hacer
**una sola vez y pronto**, porque el port móvil la necesita igual.

## 1. Lo que ya es multiplataforma hoy, sin tocar nada

| Pieza | Estado | Nota |
|---|---|---|
| `Aldune.Core` (69 ficheros, 6.663 líneas, `net10.0`) | Portable salvo **un** fichero | El único Windows es DPAPI (`DatabaseKeyProvider`) |
| Formato de sync (JSON + AES-256-GCM, formato 4) | Portable por diseño | `SYNC.md` lo dice explícitamente: "no dependen de Windows, WPF, SQLite ni DPAPI" |
| `Aldune.SyncServer` (ASP.NET Core minimal API) | **Ya corre en Linux** | Es el `docker-compose.sync.yml` que se documenta en `SYNC.md` |
| `tests/Aldune.Core.Tests` (850) | Portables | Valen para cualquier cliente que enlace Core |
| UI: `src/Aldune` (WPF, 15.829 líneas) | **No portable** | Es lo único que habría que reescribir por sistema |

Es decir: **Aldune ya tiene el cerebro y el servidor multiplataforma; lo que no tiene es cuerpo.** Todo
este documento va de cuánto cuesta ponerle cuerpo a macOS, Linux o web, y de si merece la pena.

## 2. macOS: el sistema donde el concepto **ya existe**

### 2.1 El problema no es técnico, es de mercado

Lo dice la investigación del propio proyecto (`ROADMAP.md` §1, 2026-09-06): el concepto de Aldune nació
en macOS y allí sigue: **Hold My Notes** (6,99-14,99 $ pago único, cifrado local, sync opcional por
iCloud), **noty** (open source, AES-GCM, gratis) y **SideNotes** (19,99 $). Aldune se hizo precisamente
para llevar ese concepto a Windows, donde no existía ("si Aldune sale al público, no entra en un mercado
saturado — ocupa un hueco vacío").

Portar Aldune a macOS invierte ese argumento: entraría como **cuarto** en un mercado con tres jugadores,
uno de ellos gratis y con el código a la vista, y con la app que inspiró el diseño. En Windows el hueco
está vacío; en macOS está lleno.

### 2.2 Lo técnico: la mecánica del dock hay que rehacerla entera

La pieza central de la 1.0 de Windows es que **la ventana del dock nunca se redimensiona**: se mide una
vez y lo único que cambia por frame es la **región recortada** (`SetWindowRgn` + `CombineRgn`). En macOS
no existe ese concepto: `NSWindow` es rectangular. Cada pieza tiene su equivalente, pero es otro código:

| En Windows | En macOS | Estado |
|---|---|---|
| `SetWindowRgn` + `CombineRgn` (recorte por frame) | Ventana sin borde (`borderless`) + `isOpaque = false` + **máscara de `CALayer`** sobre la vista de contenido | Técnica estándar, verificada; **a comprobar**: parpadeo por frame y que la sombra del sistema (que sigue la forma rectangular) no se vea mal |
| `WS_EX_TOPMOST` / "siempre encima" | `NSWindow.level = .floating` / `.statusBar`, más `collectionBehavior` (`.canJoinAllSpaces`, `.fullScreenAuxiliary`) para no desaparecer en pantalla completa y en otros espacios | Verificado; el equivalente de la detección de juegos de Windows es esto |
| Abrir el dock sin robar el foco | `NSPanel` con `nonactivatingPanel` + app de agente (`LSUIElement`, sin icono en el Dock) | Verificado como patrón habitual (apps de panel y utilidades de barra de menús) |
| Bandeja (`NotifyIcon`) | `NSStatusItem` en la barra de menús | Verificado |
| Arranque al iniciar sesión (clave `Run`) | `SMAppService` (macOS 13+) | Verificado |
| Atajo global `Ctrl+Alt+H` | **`RegisterEventHotKey`** (Carbon, obsoleto pero funcional, **sin permisos**); el camino "moderno", `NSEvent.addGlobalMonitorForEvents`, **exige permiso de Accesibilidad** | Verificado; es un detalle que decide la experiencia |
| Sondeo del cursor para el hover | `NSTrackingArea` | Verificado; el sondeo de Windows habría que reescribirlo |
| Varias pantallas y `MonitorInfo.StableId` | `NSScreen` + `CGDisplayCreateUUIDFromDisplayID` (el UUID es el id estable natural) | Verificado |
| Notificaciones *toast* propias | `UNUserNotificationCenter` | **Ya se escribirá** si se hace el port móvil de iOS |
| Instalador Inno Setup / MSIX | DMG + **notarización obligatoria** para distribuir fuera de la App Store; o sandbox + Mac App Store | Verificado ([notarización](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)) |
| `Microsoft.Data.Sqlite`, AES-GCM, PBKDF2 | Soportados en macOS | Verificado (`cross-platform-cryptography`); macOS lleva AES-GCM desde antes que iOS |

Dos consecuencias prácticas:

- **El dinero y el Mac ya están pagados** si se hace el port móvil de iOS: la cuenta de Apple (99 $/año),
  el Mac y Xcode sirven para macOS. El coste de macOS es **trabajo, no licencias**.
- **Lo caro es justo lo que ya está hecho por otros.** El dock recortado al canto, con animación por
  frame y varios monitores, es un trabajo de semanas en Windows; replicarlo en AppKit es empezar de cero
  en la parte más frágil del producto para entrar en el mercado más competido.

### 2.3 La única vía realista de tener *el* Aldune en macOS

**XPF** (el producto comercial de Avalonia) ejecuta aplicaciones WPF sin modificar en macOS y Linux. Es
la única forma de llevar *el mismo* programa con su dock, en lugar de escribir otro. Dos reservas, y
son grandes: es de pago y hay que **comprobar qué hace XPF con los P/Invoke a `user32`/`gdi32`/`dwmapi`**
que son el corazón de la mecánica del dock (regiones, sombras, `WS_EX_NOACTIVATE`, ganchos de ratón). Si
XPF no mapea esas llamadas, el valor cae y queda solo "una app WPF que arranca en macOS", que para este
producto es poco. **Antes de considerarlo hay que probar ese punto concreto.**

## 3. Linux: se puede hacer **el servidor**, no **el dock**

### 3.1 Qué es portable y qué no

- **Servidor de sync: ya funciona en Linux.** Es el `docker-compose.sync.yml` de `SYNC.md`. Para un
  usuario de Linux, la mitad de Aldune (el almacén cifrado, sin cuenta y autohospedado) **ya está hecha**.
- **El dock al canto: no es portable sin dejar de ser una aplicación normal.** Es el problema del
  capítulo anterior, agravado por Wayland.

### 3.2 Wayland, en una tabla (esto es lo que decide)

Una superficie pegada al borde con reserva de espacio (lo que en Windows se consigue con la región de la
ventana y en X11 con `_NET_WM_STRUT_PARTIAL`) en Wayland se hace con el protocolo **`wlr-layer-shell`**.
Y ahí está el problema: no lo implementan todos los compositores.

| Compositor | `wlr-layer-shell` | Consecuencia |
|---|---|---|
| **KWin / KDE Plasma 6.7** | **Sí** (versión 5) | Un panel al canto es posible, pero como *capa de escritorio*: se comporta y se pinta como un panel de Plasma, no como una app |
| **Mutter / GNOME 51** | **No** | En GNOME (el escritorio más usado) una app normal **no puede** reservar el borde ni colocarse en coordenadas absolutas. Solo se puede siendo **extensión de GNOME Shell** (JavaScript) |
| Sway, Hyprland, niri, COSMIC, Muffin, Mir, Labwc, Wayfire… | Sí | Tantas variantes como compositores, cada una con sus reglas |
| Weston, Cage | No | — |

Fuente: tabla de soporte de [`wlr-layer-shell-unstable-v1`](https://wayland.app/protocols/wlr-layer-shell-unstable-v1)
(Wayland Explorer, 2026-09-28). Además, bajo **XWayland** (una app X11 en un escritorio Wayland) no se
puede ni colocar la ventana ni reservar el borde: el `_NET_WM_STRUT_PARTIAL` clásico queda ignorado.

**Traducción:** en Windows, Aldune es *una aplicación*; en Linux, para hacer lo mismo, habría que ser
**una extensión del escritorio por cada escritorio** (GNOME Shell en JavaScript, un plasmoid en QML para
KDE…), sin margen para las otras variantes. Es otro producto, con otro lenguaje y otra distribución.

### 3.3 Lo demás que cambia

- **Tres o cuatro formas de distribuir** (`.deb`, `.rpm`, AppImage, Flatpak, Snap) y ninguna "la" forma.
  Con **Flatpak**, además, el sandbox complica dos cosas que Aldune usa: un atajo global (portal
  `GlobalShortcuts`, soporte irregular) y el acceso permanente a una carpeta de sync.
- **Bandeja**: `StatusNotifierItem`; en GNOME hace falta una extensión para verla.
- **Escalado fraccionario por monitor** en Wayland: la geometría al píxel, que en Windows ya costó lo
  suyo (`DpiConversion`, `EdgeGeometry`), vuelve a ser un problema con otras reglas.
- **Mercado**: los usuarios de Linux esperan software libre y gratis. Lo que valoran de Aldune —local,
  cifrado, sin cuenta, autohospedado— encaja perfectamente con su cultura, pero **no con un precio**.

### 3.4 Qué sí se les puede dar, sin escribir una app

1. **El servidor**: ya está. Mantenerlo documentado y con la imagen actualizada en cada release (es lo
   que ya se hace).
2. **El formato**: ya está documentado como legible por cualquier cliente futuro (`SYNC.md`,
   "Compatibilidad futura"). Cualquiera puede escribir un cliente.
3. **Un cliente de escritorio sin dock**, si algún día se hace: el mismo que serviría para macOS
   (§4.1), y con el Core compartido.

## 4. Web / WASM: descartado, y con un argumento que no admite vuelta

- Un cliente web necesitaría la **clave de descifrado en el navegador**: o se sirve desde una web de
  terceros (rompe la promesa "sin nube, sin cuenta") o se autohospeda (un servicio más que mantener,
  que sirve la aplicación y ve el texto en la pestaña).
- **No hay "visor" posible sin clave**: el almacén de sync solo contiene sobres cifrados, así que una
  vista web del servidor no mostraría nada. La única utilidad real sería un cliente completo, con todo lo
  anterior.
- En móvil, una PWA no tiene widgets ni notificaciones fiables: justo las dos cosas que hacen útil al
  cliente de bolsillo.
- Y en el escritorio, el navegador no puede ser un panel al canto de manera fiable.

Queda descartado también como herramienta de soporte ("enséñame mi almacén"): sin clave no enseña nada,
y con clave es un cliente.

## 5. Lo que propongo hacer (y lo que no)

| Prioridad | Qué | Por qué | Cuándo |
|---|---|---|---|
| **1** | **Core sin Windows** (los "seams": `ISecretProtector`, sync `async`, `JsonSerializerContext`) | Es el mismo trabajo que necesita el móvil, y deja el cerebro listo para cualquier cliente | Con el móvil, `MOBILE_PORT.md` §5 fase 1 |
| **2** | **Cliente móvil Android y después iOS** | Es donde hay uso real y hueco de mercado | `MOBILE_PORT.md` |
| **3** | **Cliente de escritorio *sin dock* para macOS y Linux** (Avalonia), compartiendo Core y la capa de presentación (`Aldune.Mobile.Core`) | Da "el cuaderno también en el Mac" sin reescribir la mecánica del dock y sin entrar de frente en el mercado ocupado de macOS | Solo si hay alguien que lo pida y lo use |
| 4 | Dock al canto en macOS (AppKit, o XPF) | Carísimo y en el mercado más competido; XPF tiene los P/Invoke por verificar (§2.3) | **No, por ahora** |
| 5 | Dock al canto en Linux (extensión de GNOME/KDE) | Otro producto, otro lenguaje, y hay que mantener una extensión publicada que se rompe con cada versión del escritorio | **No** |
| 6 | Cliente web | §4 | **No** |

Tres cosas que **no** hay que hacer por el camino: separar Core en un paquete NuGet (el monorepo basta),
duplicar los textos (`Strings.cs` se enlaza, no se copia) y portar el editor de temas de nota.

## 6. Estimación (sesiones, como las de este proyecto)

| Trabajo | Sesiones | Nota |
|---|---|---|
| Core sin Windows | 3 | Ya contadas en el plan móvil; no se suman |
| Cliente de escritorio sin dock en Avalonia, macOS + Linux, reutilizando Core y la presentación | 5-7 | Es el camino barato a macOS/Linux |
| Empaquetado: DMG + firma + notarización (macOS); AppImage/Flatpak + `.deb` (Linux) | 1-2 | La cuenta de Apple y el Mac ya se necesitan para iOS |
| Dock al canto en macOS con AppKit | 6-10 | Incertidumbre alta: regiones, sombras, hover, multi-monitor. **No recomendado** |
| Dock al canto en Linux como extensión de escritorio | 3-5 **por escritorio** | Más el mantenimiento permanente de la extensión. **No recomendado** |

## 7. Decisiones abiertas

1. **¿macOS y Linux se quieren de verdad o son curiosidad?** De la respuesta depende todo: el cliente sin
   dock solo tiene sentido si alguien va a usarlo.
2. **Avalonia o Mac Catalyst.** Si el móvil se hace con MAUI, Mac Catalyst "sale casi gratis", pero es
   iOS adaptado a escritorio: ventanas múltiples, paneles de agente y atajos globales encajan mal.
   Recomendación: **MAUI para el móvil, Avalonia para el escritorio**.
3. **¿XPF?** Solo si se prueba antes qué hace con los P/Invoke del dock (§2.3).
4. **¿El cliente sin dock también en Windows** (una "vista lista" para tablet y pantallas pequeñas)?
   Sería una función nueva de Windows: va a `ROADMAP.md` §2, no aquí.

## 8. Cómo se retoma y fuentes

Antes de trabajar en esto, **revalidar**: la tabla de Wayland (§3.2, los compositores cambian cada
versión), el estado de XPF y los requisitos de Apple. Todo lo de este documento es del **2026-09-28**.

- Mercado de macOS y concepto original: `ROADMAP.md` §1 (investigación del 2026-09-06), con las fuentes
  de Hold My Notes, noty y SideNotes.
- [Soporte de `wlr-layer-shell` por compositor](https://wayland.app/protocols/wlr-layer-shell-unstable-v1)
- [Notarización de software para macOS](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
- [Requisitos próximos de Apple](https://developer.apple.com/news/upcoming-requirements/)
- [Avalonia](https://avaloniaui.net/) (y su producto XPF, que ejecuta apps WPF sin modificar en macOS y
  Linux)
- [Criptografía multiplataforma en .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography)
  (SQLite, AES-GCM y PBKDF2 también en macOS y Linux)
- Los enlaces de plataforma del cliente móvil están en [`MOBILE_PORT.md`](MOBILE_PORT.md) → "Fuentes".

## 9. Windows en ARM64 y el servidor en ARM: los dos "sistemas que faltan" de verdad

Son los únicos sistemas que hoy existen y no están atendidos, y son **baratos**: no añaden superficie de
mantenimiento porque no hay código nuevo, solo otro destino de compilación.

### 9.1 Windows en ARM64: hoy funciona, pero emulado

Lo que hay, verificado:

- El publish es **solo `win-x64`**: `src/Aldune/Properties/PublishProfiles/portable.pubxml` (línea 23),
  `scripts/build-installer.ps1` (línea 17 y el nombre del zip, línea 52) y
  `.github/workflows/release.yml` (líneas 34 y 45).
- El instalador declara `ArchitecturesAllowed=x64compatible` y
  `ArchitecturesInstallIn64BitMode=x64compatible` (`installer/Aldune.iss`, líneas 21-22). Según la
  definición oficial de Inno Setup, **`x64compatible` incluye los sistemas ARM64 con Windows 11**, que
  pueden ejecutar binarios x64 por emulación ([identificadores de arquitectura de Inno
  Setup](https://jrsoftware.org/ishelp/topic_archidentifiers.htm)). Es decir: en un Copilot+ o un
  Snapdragon X, **Aldune ya se instala y funciona**, pero emulado: más batería y más coste en la parte de
  dibujo, que es justo la que más se nota en un dock animado.
- El aviso de versiones (`ReleaseUpdateChecker`) **no elige fichero por nombre**: solo lee el `tag_name`
  del release, así que por ese lado no hay nada que cambiar.

Un ARM64 nativo sería, en corto:

1. Publicar también `-r win-arm64`. WPF, el runtime de escritorio de .NET y `Microsoft.Data.Sqlite`
   (por SQLitePCLRaw) tienen ARM64; no hay dependencias propias que lo impidan.
2. Añadir `arm64` a `ArchitecturesAllowed`/`ArchitecturesInstallIn64BitMode` y meter los dos juegos de
   ficheros en el mismo instalador decidiendo por arquitectura (`IsArm64`), o publicar dos instaladores.
   El zip portable, con el nombre por arquitectura (`aldune-portable-win-arm64.zip`), y el `release.yml`
   con una matriz de dos RID.
3. Verificar en un equipo ARM real o en una VM de Windows 11 ARM. Para compilar en CI hay runners
   **`windows-11-arm`** (GitHub Actions los ofrece para **repositorios públicos**; la imagen con Windows
   11 ARM y Visual Studio 2026 llegó en septiembre de 2026
   ([changelog](https://github.blog/changelog/2025-04-02-arm64-windows-runners-for-public-repositories-in-public-preview/),
   [issue de la imagen](https://github.com/actions/partner-runner-images/issues/14602))), pero el smoke
   test de UI se hace igual en un dispositivo.

**Recomendación:** hacerlo **cuando alguien lo pida** o cuando haya un equipo ARM para probarlo. Es de las
pocas ampliaciones que no añaden deuda, y evita que un usuario de Copilot+ piense que "la app va rara".

### 9.2 El servidor de sync en ARM (Raspberry Pi, NAS)

**Ya funciona y no hay que hacer nada**: el `Dockerfile` parte de
`mcr.microsoft.com/dotnet/aspnet:10.0`, que es una **imagen multi-arquitectura** (linux/amd64 y
linux/arm64), y `docker compose ... up --build` compila en la máquina donde se ejecuta. Es decir, el
almacén cifrado de Aldune se puede autohospedar hoy en un Raspberry Pi o un NAS ARM.

Lo único que **no** está hecho es **publicar una imagen ya construida** para las dos arquitecturas
(`docker buildx build --platform linux/amd64,linux/arm64`). Solo importa si algún día se reparte la imagen
en vez del código (por ejemplo, para quien no quiera compilar). Es una decisión de distribución, no de
compatibilidad.
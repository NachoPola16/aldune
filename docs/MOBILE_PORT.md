# Aldune — Cliente móvil (Android e iOS): estudio y plan

> **Estado: plan decidido el 2026-09-28 —Android primero, en tableta, ver §0.1— y sin una sola línea de
> código de móvil en el repositorio.**
> Sesión del 2026-09-28. Este documento existe para que el port se pueda retomar en otra sesión, con
> otra IA o dentro de meses, sin repetir la investigación que ya está hecha. Si algo de aquí se
> implementa, manda lo implementado: hay que actualizar `STATUS.md` (lo hecho y por qué) y
> `ROADMAP.md` (lo decidido y lo descartado) y, si hay diseño nuevo de producto,
> `docs/superpowers/specs/`.

## 0. El encargo y la respuesta corta

Pedido por el usuario el 2026-09-28, en sus palabras: hacer **el mejor port posible a Android e iOS**;
no está convencido de que deba funcionar igual ("para móvil debe ser más bien una app que me permita
**gestionar las notas que tengo en el ordenador**, por si desde el móvil se me ocurre algo que quiera
apuntar"); pide ver **cómo hacer el port, qué funcionalidades, algo diferente**.

Respuesta corta: **no debe ser igual, y no por gusto, sino porque la metáfora del escritorio no existe
en un móvil.** Aldune en Windows es un objeto de escritorio: un mazo pegado al canto de la pantalla que
se abre en abanico al pasar el ratón, notas como ventanas, varias pantallas, atajo global. En el móvil
no hay ratón, ni canto, ni ventanas, ni varios monitores: el "escritorio" es la pantalla de inicio y lo
"siempre visible" son las notificaciones y los widgets. Copiar el abanico sería plantar un gesto de
ratón donde no hay ratón.

El cliente móvil tiene que hacer tres cosas, y ya:

1. **Capturar en dos segundos** (el caso de uso que da el usuario: se me ocurre algo y quiero apuntarlo).
2. **Consultar y gestionar** lo que ya está en el ordenador (buscar, leer, editar, etiquetar, archivar,
   papelera, reordenar el mazo).
3. **Recordar** (los recordatorios, con acciones desde la notificación).

Todo lo demás (dock, abanico, pill, ventanas, posición por pantalla, bandeja, atajos, editor de temas,
gestión de monitores) **se queda en Windows**.

### 0.1 Decisión tomada (2026-09-28): Android primero, probado en una tableta

El usuario tiene **iPhone y una tableta Android**. Se decide **Android primero**, por economía y por
alcance:

- **Coste de hacerlo: 0 €.** Sin Mac, sin cuenta de Apple, sin Xcode: Android se compila y se depura
  desde Windows con lo que ya hay (el SDK y el JDK los instala Visual Studio).
- **Ya hay dispositivo real para probar**: la tableta. Importa más de lo que parece, porque las sondas de
  este proyecto se hacen con ventanas y dispositivos reales, no con simulaciones.
- **La tableta es el mejor banco de pruebas del caso de uso principal**: gestionar y editar las notas que
  ya existen, que es lo que pidió el usuario. Y acepta teclado y ratón, así que se puede probar también la
  interacción de escritorio.
- **Para el producto**: Android llega a más usuarios por euro invertido y publicar cuesta **25 $ una vez**
  (Play) frente a **99 $/año** (Apple).
- **Para uso propio no hace falta publicar**: un APK firmado instalado a mano en la tableta cuesta 0 €.
  Google Play solo hace falta para que lo use otra gente o para tener actualizaciones automáticas.

**Consecuencia de diseño: la tableta es el dispositivo de referencia y el teléfono es el subconjunto.**
Lista y nota pueden convivir en dos paneles, el widget grande es el principal y hay que contar con teclado
físico; el diseño de teléfono sale de comprimir eso, no al revés.

**El iPhone se queda fuera hasta que exista el cliente iOS**, y conviene ser claro sobre por qué: iOS
exige Mac con Xcode 26 y cuenta de Apple de pago, incluso para instalarlo en un teléfono propio. Lo que
decide la prioridad no es el dinero, es el caso de uso:

- **Leer, buscar, editar y anotar** lo que ya está en el ordenador → la tableta cubre el caso completo.
- **Apuntar algo desde el bolsillo** con el teléfono que se lleva encima → eso solo lo hace iOS, y
  entonces la conversación empieza por el Mac y los 99 $/año, no por el código. Mientras tanto, en la
  tableta el camino corto es abrir la app directamente en "nota nueva".

La decisión no cierra iOS: el cliente se diseña como **un proyecto MAUI y dos extensiones Swift**, así que
añadirlo después es sumar el toolchain, no rehacer nada (ver §7, fase 3).

## 1. Lo que hay hoy, verificado en el repositorio (2026-09-28)

| Dato | Valor real |
|---|---|
| `src/Aldune.Core` | **69** ficheros `.cs`, **6.663** líneas, `net10.0` puro, sin WPF |
| Dependencias de Core | `Microsoft.Data.Sqlite` 10.0.11 y `System.Security.Cryptography.ProtectedData` 10.0.11 |
| `src/Aldune` (WPF) | **60** ficheros `.cs`/`.xaml`, **15.829** líneas, `net10.0-windows`, `UseWPF` + `UseWindowsForms` (bandeja), versión 1.4.1 |
| `tests/Aldune.Core.Tests` | **60** ficheros, **6.260** líneas, **850/850** tests en verde (son tests *de Core*, no de WPF) |
| `src/Aldune.SyncServer` | ASP.NET Core minimal API, ~150 líneas útiles (`/health`, `GET /api/v1/objects`, `GET`/`PUT`/`DELETE /api/v1/objects/{id}`), `Bearer` con rotación de tokens, tope de 5 MB por objeto |
| Textos de interfaz | `src/Aldune/Resources/Strings.cs`, **1.268** líneas, `T(en, es, de, fr, pt)`; idiomas en `Aldune.Core.UiLanguages` |
| CI | `.github/workflows/ci.yml`: `windows-latest`, `dotnet build` + `dotnet test` de `Aldune.slnx` |

**El único código realmente Windows de Core es el DPAPI.** Búsqueda exhaustiva de
`ProtectedData|OperatingSystem.|Registry|DllImport|win-x64|Environment.` en `src/Aldune.Core/*.cs`:
fuera de `DatabaseKeyProvider.cs` todo lo demás son comentarios sobre Windows, no API. Las llamadas
están en:

- `SyncService.cs`: líneas 69, 86, 110, 132, 144, 163, 254, 257, 428, 670-679.
- `LocalBackup.cs`: 153 (`DatabaseKeyRecovery.Opens`).
- `src/Aldune/App.xaml.cs`: 115-127 (arranque: generar, envolver, desenvolver la clave de la base).
- Tests: `DatabaseKeyProviderTests`, `EnvelopeEncryptionTests`, `LocalBackupTests`.

**Lo que viaja en la sincronización (formato 4)**: texto, color, etiquetas, estado (`Activa`,
`Archivada`, `Papelera`), `ScreenOrigin`, notas protegidas (sal + contenido cifrado) y
`DockPosition` (el orden del mazo). El sobre es JSON con `CipherText`/`Nonce`/`Tag` en base64.

**Lo que NO viaja hoy** (es local a cada instalación y hay que decirlo en voz alta antes de diseñar el
móvil): los **recordatorios** (`NoteReminder`), las **tareas marcadas** (`TaskCompletion`), las
**posiciones de ventana** (`NotePlacement`), los **conflictos** (`SyncConflict`), los **temas de nota
propios** (`CustomThemes`) y los ajustes de aspecto e idioma.

**Notas protegidas**: `ProtectedNoteContent` — PBKDF2-SHA256, **600.000 iteraciones**, sal de 16 B,
AES-256-GCM, `Version = 1`. El número de iteraciones **está en el código, no en el dato**, así que es
una constante de compatibilidad: el cliente móvil tiene que usar exactamente la misma (usar la clase de
Core lo garantiza).

**Transportes existentes** (todos en Core, todos portables en principio): `FolderSyncTransport`
(`objects/` en una carpeta), `HttpSyncTransport` (servidor propio, `Bearer`) y `WebDavSyncTransport`
(PROPFIND + Basic, para Nextcloud/ownCloud). El código de invitación (`SyncShareCodeCodec`) lleva
clave, alcance, nombre del perfil, transporte y URL del servidor, **nunca el token**.

## 2. Qué se reutiliza y qué no

| Se reutiliza tal cual (Core, con sus 850 tests) | Por qué |
|---|---|
| `Note`, `NoteState`, `NotesDatabase`, `NotesRepository` | SQLite + texto cifrado; el esquema no depende de Windows |
| `ContentCipher` (AES-256-GCM), `PasswordRules`, `ProtectedNoteContent` | Cripto de biblioteca estándar; mismas constantes = misma compatibilidad |
| `SyncService`, `SyncModels`/`SyncEnvelopeCodec`, `ISyncTransport`, `HttpSyncTransport`, `WebDavSyncTransport`, `SyncProfileStore`, `SyncShareCodeCodec` | El formato y la resolución de conflictos son la parte cara de duplicar |
| `SettingsService`, `AppSettings` (con sus valores por defecto compatibles) | Mismo fichero, mismos enums numéricos |
| `NoteSearch`, `NoteText`/`NoteTitleHelper`, `TaskLines`, `TaskLists`/`TaskCompletion`, `ListIndent`, `LineMovement`, `BulletLines`, `TextEdit`, `MarkdownExport` | Es el "formato de texto" del producto: casillas, listas, títulos |
| `NoteOrdering`, `NoteListing` | El orden del mazo, que ya se sincroniza |
| `NoteThemes`, `NoteColorDerivation`, `OklchColor`, `NoteColorContrast`, `AppPalette` | Continuidad visual PC↔móvil y contraste ya testeado |
| `ReminderPresets`, `LocalBackup`, `DiagnosticLog`, `BrandIdentity` | Utilidades portables (con matices de rutas) |

| Se queda fuera en móvil | Por qué |
|---|---|
| Dock, abanico, pill, `EdgeGeometry`, `DockPlacement`, `DockHover*`, `FanStateMachine`/`FanTiming`, `DpiConversion`, `MonitorInfo`/`MonitorLookup`/`MonitorPowerTracker`, `FullscreenDetection` | Geometría y hover de escritorio |
| `HotkeyBinding`, `TrayIcon`, `StartupRegistration`, notificaciones *toast* propias | Conceptos de Windows (o reemplazados por notificaciones del sistema) |
| `ReleaseUpdateChecker` | En móvil actualiza la tienda, no GitHub |

## 3. El producto móvil propuesto

### 3.1 Traducción de conceptos

| Escritorio (WPF) | Móvil (propuesta) |
|---|---|
| Pill en reposo = tiras de color al canto | **Widget** de pantalla de inicio (notas con su color). En iOS, además, widget de pantalla de bloqueo / StandBy |
| Abanico al pasar el ratón | **Lista** con búsqueda arriba. Se toca, no se hace hover: el abanico no se imita |
| Atajo global `Ctrl+Alt+H` | **Hoja de compartir**; **mosaico de Ajustes rápidos** (Android); **acción "Nueva nota"** del icono; **notificación persistente con "+"**; dictado del teclado |
| Ventana de nota (título = 1ª línea + cuerpo) | Pantalla de nota a pantalla completa, con el mismo texto (`☐`/`☒`, listas, sangría) |
| Recordatorio con notificación de Windows | Notificación local con botones: *Hecho*, *Posponer 10 min / 1 h / mañana*, *Abrir* |
| Gestor de notas (buscar, ordenar, filtrar) | Es la pantalla principal |
| Tema de color de nota | Igual (misma paleta: la nota se ve del mismo color en el PC y en el teléfono) |
| Notas protegidas con contraseña | Igual, más desbloqueo **biométrico** (la clave derivada se guarda en Keychain/Keystore) |
| Aspecto Oscuro/Claro/Pastel/Medianoche | Oscuro/Claro/según el sistema (Pastel y Medianoche se derivan solos, como en Windows) |
| Copia local diaria (`LocalBackup`) | Lo cubre la copia del sistema; decidir qué se excluye (ver riesgos) |

### 3.2 Qué NO se porta, ni se imita

Dock, abanico, pill, ventanas flotantes de nota, posición por pantalla, varias pantallas, bandeja,
atajos globales, detección de juegos a pantalla completa, editor de temas, "Gestionar notas" como
ventana aparte. En móvil **no hay pantalla donde pegar un mazo**.

### 3.3 El widget, y la primera decisión de privacidad del port

Un widget no puede pedir contraseña ni desbloquear nada: se pinta solo. Propuesta:

- La app escribe una **instantánea** pequeña (color, título = primera línea y, opcionalmente, el
  primer renglón) en un JSON del contenedor compartido (App Group en iOS, fichero propio en Android),
  y el widget pinta eso. Se refresca al cambiar la nota y en el refresco de fondo.
- **Por defecto, solo títulos.** Incluir más texto es un ajuste explícito del usuario, porque es una
  excepción consciente a "el texto va cifrado en reposo": la instantánea va en claro (protegida por el
  cifrado del sistema de ficheros, no por la clave de Aldune).
- Las **notas protegidas nunca entran** en la instantánea.

Ventaja de este diseño: no hay que reimplementar AES-GCM en Swift para el widget, y Android e iOS usan
el mismo mecanismo (un solo camino que probar).

### 3.4 Ideas móviles que sí son "algo diferente" (y baratas una vez existe el MVP)

- **Notificación persistente con acción "+"** en Android: es el equivalente exacto del atajo global.
- **Widget del mazo**: la tira de colores del dock, en la pantalla de inicio. Es la continuidad visual
  del producto, no un widget de lista genérico.
- **Compartir cualquier cosa al teléfono** (un enlace, una frase de un artículo) → nota nueva con esa
  semilla. En Android es un `intent-filter` de `ACTION_SEND`; en iOS, extensión de compartir (Swift) o,
  sin escribir Swift, un atajo del usuario que abra `aldune://nueva?texto=...`.
- **"Hecho" desde la notificación** del recordatorio, sin abrir la app.
- **Dictado del teclado** en el editor: gratis, del sistema, y es la captura más rápida que existe.

## 4. Tecnología: opciones y recomendación

| Opción | Reutiliza Core | Coste | Riesgo | Veredicto |
|---|---|---|---|---|
| **A. .NET MAUI (C#/XAML)** | **Sí, casi entero** (los 850 tests siguen valiendo) | Medio-alto: XAML nuevo + toolchain Android/iOS | Calidad/perf de MAUI, AOT/trimming en iOS | **Recomendada** |
| B. Avalonia (Skia) | Sí, igual | Medio | Móvil menos maduro; los widgets siguen necesitando código nativo | Sin ventaja clara frente a A |
| C. Flutter | **No**: habría que reescribir cripto, sync y texto en Dart | Alto | Dos implementaciones de un **formato cifrado** = divergencia silenciosa | No |
| D. Nativo (Kotlin + Swift) | No (dos reescrituras) | El doble | El más alto | No, salvo que se busque el máximo pulido y haya presupuesto de tiempo |
| E. Web/PWA | No | Medio | Sin widgets, notificaciones poco fiables, texto en el navegador | No |

**La razón de fondo para MAUI no es el framework, es el formato.** El sobre de sincronización y el
cifrado se implementan **una sola vez**; duplicarlos en Dart/Kotlin/Swift es la forma más fácil de que
un dispositivo escriba algo que los demás no sepan leer. Y MAUI permite mantener la cultura del
proyecto: lógica en una librería con tests, UI verificada aparte.

### 4.1 Hechos de plataforma verificados (2026-09-28), con fuentes

- **AES-GCM**: en iOS **no existía**; se añadió en **.NET 9** con mínimo iOS 13
  ([dotnet/runtime#91523](https://github.com/dotnet/runtime/issues/91523), cerrado con hito 9.0.0;
  la API aparece marcada `SupportedOSPlatform("ios13.0")`). Android no figura como no soportado y el
  runtime tiene implementación nativa de plataforma. **Con .NET 10, `ContentCipher` es portable tal
  cual**; aun así, comprobar `AesGcm.IsSupported` al arrancar y dejarlo probado en el spike.
  ([Criptografía multiplataforma en .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography))
- **SQLite**: `Microsoft.Data.Sqlite` está soportado en Android/iOS (la propia documentación de MAUI
  lo lista como alternativa a `sqlite-net-pcl`), pero en **iOS Release (AOT + recorte)** es donde
  aparecen los problemas clásicos: `DllNotFoundException: e_sqlite3` y enlaces que revientan en
  AOT-only. Hay que resolverlo **en el spike**, no en producción:
  ([SQLite en MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite),
  [Resolving SQLite issues for .NET MAUI](https://www.banditoth.net/resolving-sqlite-issues-for-net-maui/)).
- **Secretos (el sustituto de DPAPI)**: `SecureStorage` de MAUI usa **Keystore +
  EncryptedSharedPreferences** en Android y **Keychain** en iOS. Dos avisos que ya están en la
  documentación y que afectan al diseño:
  [Secure storage](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage)
  - **Android Auto Backup** restaura preferencias cifradas cuyas claves **no** viajan: MAUI borra el
    valor y hay que envolver toda lectura en `try/catch` (o excluir esas preferencias de la copia).
  - En iOS, el **Keychain sobrevive a la desinstalación**; y si el usuario tiene **iCloud Keychain**
    activado, los valores pueden sincronizarse a sus otros dispositivos Apple sin control desde la app.
- **Widgets**: en **Android se pueden hacer en C# puro** con `AppWidgetProvider` + `RemoteViews`
  (incluso widget con lista desplazable), **sin Kotlin ni Android Studio**
  ([blog de .NET](https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/)).
  En **iOS exige una extensión Swift/SwiftUI + Xcode + App Group** y no se puede escribir en C#: es la
  parte más manual del port ([blog de .NET](https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/)).
- **Toolchain y tiendas**:
  - iOS: **Mac con Xcode 26** y cuenta de Apple Developer de pago, **99 $/año**. Desde Windows **no**
    se puede compilar iOS ([instalación de MAUI](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation)).
    Apple exige desde el **28-abr-2026** compilar con Xcode 26 y el SDK de iOS 26
    ([requisitos próximos](https://developer.apple.com/news/upcoming-requirements/)).
  - Android: los SDK/JDK son gratuitos y se instalan desde Visual Studio; Google Play exige
    **target API 36 (Android 16) desde el 31-ago-2026** para apps nuevas y actualizaciones
    ([requisitos de target API](https://support.google.com/googleplay/android-developer/answer/11926878)),
    y la cuenta son **25 $ una sola vez**.
- **Segundo plano (esto condiciona lo que se puede prometer)**: en iOS `BGAppRefreshTask` es
  **oportunista** — `earliestBeginDate` es un suelo, no una promesa, y el sistema decide si te deja
  correr, con qué frecuencia y si te corta a mitad
  ([resumen práctico 2026](https://vburojevic.dev/blog/ios-background-tasks-2026/)). En Android,
  `WorkManager` tiene un mínimo de **15 minutos** y los avisos exactos necesitan permiso de alarmas
  (Android 12+), que la política de Play restringe para apps que no sean de reloj/calendario
  ([alarmas en Android](https://developer.android.com/develop/background-work/services/alarms/schedule)).
  **Conclusión de diseño: nunca prometer "sincroniza cada 15 minutos".** Hay que sincronizar siempre
  **al abrir, al volver a primer plano y antes de editar**, y además "cuando el sistema quiera" en
  segundo plano. El motor de sync ya es idempotente (sube sobres completos y desempata por fecha), así
  que sincronizar de más no rompe nada.
- **Recordatorios**: en iOS las notificaciones locales las entrega el sistema aunque la app esté
  cerrada (fiables, previo permiso); en Android conviene asumir que sin permiso de alarmas exactas el
  aviso puede llegar con minutos de retraso. Hay que probarlo **con la app cerrada** en Android 16 e
  iOS 26 (spike C).

## 5. Lo que hay que cambiar antes, en Core y en Windows

Nada de esto rompe compatibilidad; es cirugía acotada y con test primero. **Es la única parte que toca
el repositorio actual**, así que se puede hacer (y probar) sin ningún proyecto móvil todavía.

1. **Sacar DPAPI de Core.** Introducir
   `ISecretProtector { byte[] Protect(byte[]); byte[] Unprotect(byte[]); }` con una implementación
   `DpapiSecretProtector` (lo que hoy hace `DatabaseKeyProvider`, más `GenerateKey`) e inyectarla en
   `SyncService`, `LocalBackup`/`DatabaseKeyRecovery` y el arranque. En móvil se implementa con
   Keystore/Keychain. Los tests inyectan la de Windows y **siguen pasando tal cual** (los ficheros de
   test que la usan: `DatabaseKeyProviderTests`, `EnvelopeEncryptionTests`, `LocalBackupTests`).
2. **Sincronización asíncrona.** Hoy `ISyncTransport` y `SyncService` son síncronos con
   `GetAwaiter().GetResult()` (ver `HttpSyncTransport`, `WebDavSyncTransport`). En móvil eso bloquea
   hilos del pool y en iOS es directamente mala idea. Añadir API `async` **conservando la síncrona**,
   con los tests de conflictos, tombstones y formato como red de seguridad.
3. **Serialización con generador de código (`JsonSerializerContext`).** iOS Release compila AOT y
   recorta: `System.Text.Json` por reflexión da avisos IL2026/IL3050 y puede fallar en ejecución.
   Puntos afectados: `SyncModels.cs` (líneas 243, 255, 330, 337, 382, 415, 425, 429, 446),
   `SettingsService.cs` (19, 30, 44) y `NotesRepository.cs` (341, 363).
   **Antes de tocar nada: un test que fije el JSON exacto de un sobre** (golden file) para garantizar
   que el formato 4 sigue siendo byte a byte el mismo.
4. **Rutas.** Ya se pasan por parámetro (`SettingsService(path)`, `NotesDatabase(path)`,
   `LocalBackup.CreateDaily(appDataDir, ...)`), así que basta con que la app móvil use
   `FileSystem.AppDataDirectory`. No hace falta un `IAppPaths` nuevo.
5. **Windows, para que el móvil sea usable sin fricción** (dos funciones nuevas, ambas reutilizan lo
   que ya existe):
   - **Emparejamiento con QR.** Hoy la invitación no lleva el token (bien), pero teclearlo en un móvil
     es horrible. Un QR generado por el PC con perfil + URL + token de un solo uso, y escaneo desde la
     app móvil. *Ojo: toca el modelo de seguridad de la sync (`docs/SYNC.md` → "Modelo de seguridad"):
     hay que decidirlo con el usuario y documentarlo.*
   - **Servidor de sync dentro del propio PC** (opcional y barato): arrancar en el proceso WPF el mismo
     minimal API de `Aldune.SyncServer` sobre una carpeta de la LAN (el servidor son ~150 líneas). El
     escenario sin Docker queda: instalar la app → escanear el QR → sincronizado, sin nube y sin cuenta.
6. **Nada de esto es necesario para el MVP móvil, pero condiciona la v1**: los recordatorios no se
   sincronizan. Si algún día deben viajar PC↔móvil, hace falta **subir el formato a 5**, y por la regla
   del proyecto (una versión antigua rechaza lo que no entiende, con error claro, sin aplicar nada a
   medias) **hay que publicar primero Windows que acepte el 5 y solo después el móvil que lo escribe**.
   Recomendación: en la v1 móvil los recordatorios son locales al teléfono, y se dice claramente en la
   interfaz. El formato 5 se decide con uso real en la mano.

### 5.1 Formatos y compatibilidad, en corto

- El cliente móvil implementa **el formato 4 tal cual** (mismos sobres, misma validación, mismos
  desempates por `UpdatedAt` y `DeviceId`). No se añade ningún campo nuevo en la v1.
- El `DeviceId` del móvil se genera una vez por instalación y no cambia (participa en el desempate).
- Las **posiciones de ventana siguen siendo locales**: en móvil no se usan, pero la columna
  `ScreenOrigin` existe y no molesta (se guarda un valor sintético y punto).
- **Temas propios y conflictos quedan fuera del MVP móvil** (son locales hoy): el móvil lee los colores
  que vengan en cada nota y ya. La pantalla de conflictos puede ser una lista de solo lectura más
  adelante.

## 6. Estructura de repositorio propuesta

```
Aldune.slnx                       (Windows + Core; sin cambios de fondo)
src/Aldune.Core                   net10.0 puro (se le quita DPAPI)
src/Aldune.Core.Windows           nuevo: DPAPI y lo que solo existe en Windows
src/Aldune                        WPF (referencia Core + Core.Windows)
src/Aldune.Mobile.Core            nuevo, net10.0: ViewModels, servicios de presentación y navegación
                                  abstracta → testeable sin UI (aquí vive la cultura de tests)
src/Aldune.Mobile                 nuevo, MAUI: net10.0-android (+ net10.0-ios)
src/Aldune.Mobile.iOS.Widget      extensión Swift (WidgetKit + hoja de compartir), fuera de la solución .NET
tests/Aldune.Core.Tests           los 850 de siempre
tests/Aldune.Mobile.Tests         xUnit sobre Aldune.Mobile.Core
docs/MOBILE_PROBES.md             "sondas" en móvil: pantalla de diagnóstico + checklist por dispositivo
```

Convenciones que se mantienen: textos con `Strings.T(en, es, de, fr, pt)` (para no duplicar las cinco
traducciones, el proyecto móvil enlaza `src/Aldune/Resources/Strings.cs` con un `<Compile Include>`
en vez de copiarlo), comentarios en español, BOM UTF-8 en `.cs`/`.xaml`, documentación sin BOM, y
**test primero** para todo lo que sea lógica.

## 7. Fases (estimación en sesiones de trabajo como las de este proyecto)

| # | Fase | Entregable | Verificación / puerta | Sesiones |
|---|---|---|---|---|
| 0 | **Spec y spikes** | `docs/superpowers/specs/<fecha>-aldune-movil-design.md` (producto, qué no se porta, privacidad del widget) + cuatro sondas desechables | A: abrir `notes.db` y descifrar en Android. B: sync real contra el servidor Docker, con conflicto y borrado. C: notificación con la app cerrada, Android 16 e iOS 26. D: widget Swift mínimo leyendo del App Group. **Si A o D fallan, se revisa el stack antes de escribir producto** | 2 |
| 1 | **Seams en Core** | `ISecretProtector`, API `async` de sync, `JsonSerializerContext`, `Aldune.Core.Windows` | Los 850 tests + el smoke test de UI de Windows, **sin cambios de comportamiento** | 3 |
| 2 | **MVP Android** | Capturar (nota nueva, compartir, acceso directo, notificación "+"), lista con búsqueda y filtros, leer/editar con el formato de texto real, color/tema, etiquetar/archivar/papelera, ajustes de sync (código de perfil + token), sync automática al abrir y al volver a primer plano, recordatorios locales, notas protegidas | Tests de Core + tests de `Aldune.Mobile.Core` + **sonda móvil** con informe desde el dispositivo + checklist manual | 6 |
| 3 | **MVP iOS** | El mismo alcance sobre el pipeline del Mac (Xcode 26), TestFlight y la extensión Swift de compartir | Igual que la fase 2, con checklist propia de iOS | 4 |
| 4 | **Lo que hace que valga la pena** | Widget del mazo (Android en C#; iOS en Swift + App Group), widget de pantalla de bloqueo, mosaico de Ajustes rápidos, acciones en la notificación (*Hecho*, *Posponer*, *Abrir*), desbloqueo biométrico, diseño de tablet | Checklist por dispositivo + capturas; y un test de que la instantánea del widget **nunca** incluye notas protegidas | 4-5 |
| 5 | **Emparejamiento sin fricción** | QR en Windows y, si se quiere, servidor de sync dentro del PC | Sonda: emparejar desde cero y ver llegar la nota al PC; y un intento con token inválido | 2-3 |
| 6 | **Tiendas (solo si se publica)** | Android: Play (AAB, target API 36, seguridad de datos, política de privacidad). iOS, cuando toque: App Store (manifiesto de privacidad, declaración de cifrado, App Privacy). CI: job de Android en `windows-latest` | Para uso propio basta con un **APK firmado instalado a mano** (§0.1); si se publica, pista interna primero y comprobar **disponibilidad del nombre "Aldune"** | 1 (Android) + 2 (iOS) |

**Total: ~23-26 sesiones.** Es un proyecto del tamaño del original, no un añadido.

## 8. Costes y requisitos

- **Apple Developer Program: 99 $/año** (obligatorio para instalar en un iPhone y para publicar).
- **Google Play: 25 $** una sola vez.
- **Un Mac con Xcode 26** para iOS, o un runner macOS en la nube (GitHub Actions en repositorio público,
  o un servicio tipo MacinCloud, ~25-40 $/mes). **Desde Windows no se compila iOS.**
- Nada nuevo para el servidor: el `SyncServer` y las composiciones Docker que ya existen sirven igual.

## 9. Verificación: cómo se mantiene la cultura de sondas

Los tests de Core (850) valen tal cual, porque **el Core es el mismo ensamblado**. Encima:

1. `tests/Aldune.Mobile.Tests`: xUnit sobre `Aldune.Mobile.Core` (ViewModels, filtros, formateo, reglas
   de captura). Sin UI.
2. **Sonda móvil**: una pantalla de diagnóstico (solo en compilación de depuración) que ejecuta un guion
   de comprobaciones sobre datos temporales — abrir la base, descifrar N notas, sincronizar contra un
   servidor local, disparar una notificación, escribir la instantánea del widget, medir el PBKDF2 de una
   nota protegida — y guarda un informe. Se documenta en `docs/MOBILE_PROBES.md`, en la línea de
   `docs/WPF_PROBES.md`.
3. **Checklist manual por dispositivo** en cada fase: widget tras reiniciar el teléfono, notificación con
   la app cerrada, sync con la pantalla apagada y con cambio de red, rotación y modo oscuro.
4. CI: el job actual no cambia; se añade uno de Android (compila `-f net10.0-android`) y, cuando haya
   cuenta de Apple, otro de iOS en runner macOS.

## 10. Riesgos y decisiones abiertas

1. **iOS depende de un Mac.** Si no hay Mac, iOS queda atado a CI + un iPhone físico para probar. Es el
   mayor bloqueo práctico del plan.
2. **Widget y privacidad**: la instantánea de texto es una excepción deliberada a "cifrado en reposo".
   Propuesta: solo títulos por defecto, con aviso claro al activar el texto.
3. **Recordatorios**: locales en el móvil (v1) o formato 5 (después, con orden de publicación
   obligatorio: primero Windows que lo entienda).
4. **"Carpeta compartida" en móvil: descartada.** En iOS depende del proveedor (descargas bajo demanda)
   y en Android obliga a SAF. La apuesta es **servidor propio o WebDAV**, que ya funcionan.
5. **Mantenimiento**: tres aplicaciones × cinco idiomas × los tests. El recorte de funciones en móvil es
   deliberado, no un descuido.
6. **Modelo de pago (si algún día se cobra)**: Apple exige IAP para bienes digitales; el pago único de
   escritorio y el de tienda no se mezclan sin pensarlo. Hay que decidir si el móvil es gratis
   (compañero del escritorio) o de pago único en cada tienda.
7. **Marca**: revisar que "Aldune" esté libre también en Play y App Store (el `ROADMAP.md` §6 ya avisa
   de *FanNote* como nombre parecido).
8. **Decidido el 2026-09-28** (§0.1): Android primero, en la tableta y sin publicar al principio. Lo único
   que queda por elegir es el arranque fino: hacer la fase 0 (spec + spikes) o ir directo a un MVP de
   Android.

## 11. Descartes, con su razón (para no volver a proponerlos)

- **Imitar el abanico/dock en táctil**: es un gesto de ratón sin ratón. Descartado como diseño.
- **Reescribir Core en otro lenguaje** (Flutter, React Native, nativo): duplica el cifrado y el formato de
  sync, que es justo la parte donde una divergencia se paga con datos ilegibles.
- **PWA como cliente principal**: sin widgets, notificaciones poco fiables en iOS, y el texto acabaría en
  el navegador.
- **"Carpeta compartida" en el móvil**: ver riesgo 4.
- **Sincronizar cada N minutos en segundo plano**: no es algo que iOS ni Android garanticen.
- **Guardar la base de datos del PC en la nube y abrirla desde el móvil**: la clave está envuelta para el
  usuario de Windows (DPAPI), así que ese fichero no se puede abrir desde otro sistema. Lo que se
  comparte entre dispositivos es la **sync**, no `notes.db`.

## 12. Cómo se retoma esto

1. Leer este documento entero y, si han pasado meses, **revalidar §4.1**: las versiones de Xcode, el
   target API de Play y las versiones de MAUI cambian cada temporada. Todo lo de arriba es del 2026-09-28.
2. Si lo que se quiere es otro sistema de escritorio (macOS, Linux), leer antes
   [`DESKTOP_PORT.md`](DESKTOP_PORT.md): la conclusión allí es que **compensa el Core, no el dock**.
3. Antes de empezar, que el plan lo revise otra IA con ojos frescos: el encargo completo está en
   [`MOBILE_PORT_REVIEW.md`](MOBILE_PORT_REVIEW.md), con los comandos para comprobar cada afirmación,
   los ocho puntos que hay que atacar y el prompt listo para pegar.
4. Escribir la spec de producto de la fase 0 y hacer los cuatro spikes. Sin el resultado del spike A
   (SQLite + AES-GCM en un dispositivo real) y del D (widget iOS), no escribir producto.
5. Antes de tocar `src/Aldune.Core`, comprobar que siguen **todos los tests en verde** (850 cuando se escribió esto; hoy ~1350) y que existe el
   golden file del sobre (formato 4).
6. Plataforma: **ya decidida** el 2026-09-28 (§0.1): Android primero, en la tableta, sin publicar. Lo que
   queda por elegir es el arranque fino: fase 0 (spec + spikes) o MVP de Android directo.

## Fuentes

- [AES-GCM en iOS (dotnet/runtime#91523)](https://github.com/dotnet/runtime/issues/91523) ·
  [Criptografía multiplataforma en .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography)
- [Qué es .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui) ·
  [Novedades de MAUI en .NET 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10) ·
  [Instalación: el Mac es obligatorio para iOS](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation)
- [Widgets de Android con .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/) ·
  [Widgets de iOS con .NET MAUI](https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/)
- [SecureStorage de MAUI](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage) ·
  [SQLite en MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite) ·
  [Problemas de SQLite en MAUI](https://www.banditoth.net/resolving-sqlite-issues-for-net-maui/)
- [Requisitos próximos de Apple](https://developer.apple.com/news/upcoming-requirements/) ·
  [Target API de Google Play](https://support.google.com/googleplay/android-developer/answer/11926878) ·
  [Alarmas en Android](https://developer.android.com/develop/background-work/services/alarms/schedule) ·
  [Segundo plano en iOS, 2026](https://vburojevic.dev/blog/ios-background-tasks-2026/)
- [Avalonia](https://avaloniaui.net/) (comparación de opciones)

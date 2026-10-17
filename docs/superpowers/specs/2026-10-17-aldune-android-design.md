# Aldune para Android — diseño de producto, arquitectura y fases

> 2026-10-17. Sustituye a la «spec de fase 0» que pedía `docs/MOBILE_PORT.md` §7 (era para MAUI). Manda sobre §3 y §7 de
> ese documento; la revisión de tecnología y distribución está en su §13. Las decisiones de producto marcadas **(usuario)**
> las tomó el usuario en esta sesión; el resto son defaults razonados que se pueden discutir.

## 0. En una frase

Una sola app **Kotlin + Jetpack Compose**, adaptativa (Pixel 10a con Android 16 y Lenovo Tab P11 con Android 14), `minSdk` 26,
`targetSdk` 36, GPL-3.0, publicable en F-Droid, que hace tres cosas: **capturar en dos segundos, consultar y gestionar las notas del
PC, y recordar**. Habla el formato de sync 4 sin cambiarlo. Móvil y tableta tienen **las mismas funciones**; cambia la disposición.

## 1. Lo que hace hoy Core y condiciona el diseño (leído del código el 2026-10-17)

- **Nota** (`Note.cs`): texto, color (hex), creación/modificación, estado (activa, archivada, papelera), etiquetas (máx. 20, sin
  duplicados ignorando mayúsculas), posición en el mazo (`DockPosition`, `double?`), `ScreenOrigin` (Android no lo usa pero **lo
  conserva y lo devuelve tal cual**: si lo vaciara, el PC perdería en qué pantalla va la nota), protegida + contenido protegido.
  **Viaja todo eso**; no viajan recordatorios, tareas marcadas aparte (`TaskCompletion`), vínculo a archivo, posiciones de ventana,
  conflictos ni temas propios.
- **Formato del texto**: texto plano. Título = primera línea (se le quitan de 1 a 6 `#` al mostrarlo; el texto guardado los conserva).
  Tarea = sangría + `☐`/`☒` (o `☑`, solo lectura) **seguido de espacio**; al marcar se escribe `☒`. Viñeta = sangría + `→ `.
  Enter continúa la lista y en una línea vacía la termina; Retroceso quita el prefijo entero; pegar `- [ ]`/`- [x]` da casillas.
  No hay negrita, enlaces ni Markdown renderizado. Windows une líneas con CRLF.
- **Búsqueda** (`NoteSearch.cs`): subcadena en el texto completo, ignora mayúsculas. **Hallazgo:** el comentario dice que ignora
  acentos y el código no (`CurrentCultureIgnoreCase`): «cafe» no encuentra «café». Las etiquetas se filtran aparte.
- **Notas vinculadas a archivos**: el vínculo es local al PC. Las que tienen la sync desactivada (por defecto) **no llegan al almacén**;
  las que la tienen activa llegan como una nota normal y, si Android las edita, el PC escribe el cambio en el archivo con su regla de huella.
- **Recordatorios**: uno por nota, puntual, local (`NoteReminder`). Atajos: +1 h, esta noche 20:00, mañana 9:00 (`ReminderPresets`).
- **Papelera**: es un estado que viaja. Se purga a los 30 días por defecto. Borrar del todo solo desde la papelera. Borrar o purgar
  deja una lápida **local** (`SyncTombstone`); el sobre firmado lo crea la sync en la pasada siguiente.
- **Autor de la versión**: toda edición local (texto, color, estado, orden, etiquetas) pone `SyncAuthorDeviceId = NULL`, para que la
  versión suba con el `deviceId` de este equipo (`NotesRepository.cs`, p. ej. :206, :594). Android tiene que hacer lo mismo.
- **Etiquetas en la comparación**: `NoteEquivalence.SameContent` las compara ordenadas y **distinguiendo mayúsculas** (ordinal). Android
  guarda las etiquetas tal como llegan (deduplicando sin distinguir mayúsculas al editar, como `SetTags`) para no crear falsos conflictos.
- **Motor de sync** (`SyncService.cs:300-451`): lee **todo** el almacén en cada pasada (sin cursor), clasifica (descifra / borrado
  auténtico / borrado sin firma), se queda con la versión mayor por nota, aplica el alcance (todas, elegidas, etiqueta), trata los
  borrados sin firma (papelera, nunca borrar), compara con lo local usando **bases por nota** para distinguir conflicto real de cambio
  de un solo lado, guarda la versión perdedora como `SyncConflict` y fija las bases al final. Si hay al menos una nota (no borrado) en
  el almacén y **ninguna** descifra, aborta sin escribir (la clave cambió); si el almacén solo tiene borrados, no aborta
  (`SyncService.cs:556-584`). Con la **misma fecha** en los dos lados nunca hay conflicto: desempata el `deviceId` y punto (:462).
  Un conflicto también puede ser **edición frente a borrado firmado**: si gana el borrado, la nota desaparece y el conflicto queda sin
  nota a la que colgarse. Decisión y E/S están mezcladas; no hay reloj inyectable ni forma de inyectar el transporte.

## 2. Pantallas: qué hace cada una en móvil y en tableta

La regla: **mismas funciones, distinta disposición**. Se usa `NavigableListDetailPaneScaffold` de Material 3 Adaptive (estable 1.3.0):
en ventana compacta un panel cada vez; en mediana/expandida, lista y nota lado a lado. No se decide por «es tableta», sino por
**clase de tamaño de ventana** (un Pixel en horizontal o en pantalla dividida se comporta como toca).

### 2.1 Lista (pantalla de inicio) **(usuario: lista + botón grande)**

| | Móvil (compacta) | Tableta (expandida) |
|---|---|---|
| Disposición | Lista a pantalla completa, búsqueda arriba, botón «+» grande abajo | Lista a la izquierda (~360 dp), nota abierta a la derecha |
| Filas | Tira de color de la nota + título + vista previa de una línea + contador de tareas (`hechas/total`) + iconos (protegida, recordatorio, conflicto) | Igual |
| Filtros | Pestañas o chips: Activas · Archivadas · Papelera; chip de etiqueta | Igual, con espacio para la lista de etiquetas fija |
| Orden | El del mazo (`DockPosition`); reordenar con pulsación larga y arrastre | Arrastre con dedo o ratón; teclado (Alt+↑/↓) |
| Acciones por nota | Deslizar: archivar / papelera; menú: color, etiquetas, recordatorio, proteger | Menú contextual (clic derecho) además del deslizar |

- **Búsqueda**: subcadena sobre texto **y etiquetas**, sin distinguir **mayúsculas ni acentos** (en Android se pliegan acentos con
  `Normalizer`; es mejor que Windows hoy y es lo que el comentario de Core promete). Las protegidas no salen en la búsqueda (su texto no
  se conoce sin contraseña). El hallazgo de Windows se apunta en §9, no se arregla aquí.
- **Indicador de sync** discreto en la barra: última sync, error con texto claro y botón «Sincronizar ahora». Tirar hacia abajo sincroniza.

### 2.2 Nota (editor)

| | Móvil | Tableta |
|---|---|---|
| Disposición | Pantalla completa; atrás vuelve a la lista | Panel derecho; la lista sigue visible |
| Barra | Color, etiquetas, recordatorio, proteger, archivar, papelera | Igual, en la barra superior del panel |
| Barra de edición | Encima del teclado: casilla, viñeta, sangría −/+, subir/bajar línea | Igual; y atajos de teclado físico |

- El editor trabaja sobre el **texto tal cual** con las reglas de §1 (portadas de `TaskLines`, `BulletLines`, `ListPrefix`,
  `TaskLists`, `NoteTitleHelper`). Tocar un `☐` lo marca. Se puede dictar con el teclado del sistema.
- **Saltos de línea**: internamente `\n`; al guardar se vuelve a unir con el estilo que traía la nota (CRLF si venía de Windows). Y
  **solo se cambia `UpdatedAt` si el usuario editó de verdad**: abrir y cerrar una nota no puede generar una versión nueva, porque
  provocaría subidas y falsos conflictos (`NoteEquivalence.SameContent` compara el texto exacto).
- Guardado automático (al pausar la escritura y al salir); no hay botón Guardar.
- **Color**: paleta de temas de nota de Core (`NoteThemes`); el borde y la etiqueta se derivan (`NoteColorDerivation`). Un color que
  venga de un tema propio de Windows se respeta tal cual (se pinta su hex y se deriva lo demás).
- **Atajos de tableta con teclado**: Ctrl+N nota nueva, Ctrl+F buscar, Esc cerrar la nota; los de edición de líneas, los mismos que en
  Windows (se listan en la fase F4 leyendo `src/Aldune`).

### 2.3 Notas protegidas **(usuario: abrir y editar con contraseña; biometría después)**

- En la lista: icono de candado, título de relleno («Nota protegida»), sin vista previa.
- Abrir pide la contraseña; la derivación (PBKDF2 600.000 it.) va **fuera del hilo de interfaz** con un indicador. Desbloqueada solo
  en memoria; se vuelve a bloquear al salir de la nota y al pasar la app a segundo plano.
- Se puede proteger una nota (contraseña ≥ 4 caracteres, dos veces) y quitar la protección (pide la contraseña).
- Nunca entran en el widget, en la búsqueda ni en el texto de un recordatorio.
- **Fuera de la v1:** desbloqueo biométrico (guardar la clave derivada en el Keystore con autenticación es un diseño de seguridad aparte).

### 2.4 Conflictos **(usuario: aviso + restaurar/descartar)**

- La nota muestra una franja «Esta nota cambió en dos sitios a la vez» con la otra versión visible (solo lectura) y dos botones:
  **Quedarme con esta** (descarta el conflicto) y **Usar la otra** (restaura la perdedora como versión nueva, como `RestoreConflict`).
- «Combinar» se queda en Windows (o en una fase posterior). Icono de conflicto en la fila de la lista.
- **Conflictos sin nota** (ganó un borrado sobre una edición): la lista muestra un aviso «N conflictos» que abre una lista breve con
  esos casos y los mismos dos botones («Recuperar la nota» restaura la versión editada; «Descartar»). Sin esto serían inalcanzables.

### 2.5 Papelera y archivo

- Archivadas y Papelera son filtros de la lista. En la papelera: Restaurar y Borrar para siempre (pide confirmación; deja borrado firmado).
- Purga automática a los 30 días, misma regla que Windows (ajuste en Ajustes → Notas).

### 2.6 Ajustes

- **Sincronización** **(usuario: pegar código + token; servidor y WebDAV)**: un perfil en la v1. Pegar el código de perfil
  (`aldune-profile-v2:`, o los antiguos), y según su transporte: **servidor** (URL del código + token) o **WebDAV** (URL + usuario +
  contraseña de aplicación). La carpeta compartida no se ofrece (si el código es de carpeta, mensaje claro). Botón «Probar conexión».
  Muestra alcance (todas / elegidas / etiqueta), última sync y el último error. «Desconectar» borra clave y token del dispositivo
  (no toca el almacén).
- **Cuándo se sincroniza** (no se promete periodicidad): al abrir, al volver a primer plano, unos segundos después de dejar de editar,
  al tirar de la lista, y en segundo plano «cuando el sistema quiera» (WorkManager, periódico de ≥15 min con red).
- **Aspecto** **(usuario: paleta de Aldune)**: Oscuro / Claro / Según el sistema, con la paleta de `AppPalette` (Pastel y Medianoche,
  si se ofrecen, se derivan igual que en Windows). Sin Material You.
- **Idioma** **(usuario: los cinco)**: en, es, de, fr, pt-BR. Por defecto el del sistema; en Android 13+ se usa el selector de idioma por app.
- **Notas**: color por defecto de la nota nueva, días de papelera.
- **Captura**: notificación persistente con «+» (desactivada por defecto); widget: «mostrar más que el título» (desactivado, con aviso).
- **Recordatorios**: «Avisar a la hora exacta» (pide el permiso de alarmas exactas; ver §2.9).
- Acerca de: versión, licencia GPL-3.0, enlace al código, diagnóstico (copiar el registro de sync, sin contenido de notas).

### 2.7 Captura rápida

Todas abren el editor con una nota nueva y el teclado fuera; la nota se crea al escribir el primer carácter (si se sale sin escribir,
no queda nota vacía):

1. Botón «+» de la lista.
2. **Acceso directo del icono** (pulsación larga en el icono: «Nota nueva»; shortcut estático).
3. **Compartir hacia Aldune** (`ACTION_SEND` de texto): nota nueva con el texto compartido; si es un enlace con asunto, el asunto va de título.
4. **Mosaico de Ajustes rápidos** «Nota nueva» (`TileService`).
5. **Notificación persistente con «+»** (opcional, desactivada por defecto **(usuario)**; pide permiso de notificaciones en Android 13+).
6. **Widget** (siguiente apartado).

### 2.8 Widget **(usuario: títulos; texto opcional)**

- **Widget del mazo**: tiras de color con el título de cada nota activa en el orden del mazo y un botón «+». Pequeño en móvil (unas
  pocas tiras), grande en tableta (cuadrícula). Tocar una tira abre esa nota; «+» abre nota nueva.
- **Por defecto solo títulos**; el ajuste «mostrar más texto» añade la vista previa, con aviso de que se verá en la pantalla de inicio.
  Las protegidas **nunca** salen (ni su título: tira con candado).
- **Corrección respecto a `MOBILE_PORT.md` §3.3**: en Android el widget (Glance) se pinta desde el **proceso de la app**, que puede
  descifrar con la clave del Keystore igual que la propia app. **No hace falta ninguna instantánea en claro en disco.** El texto que se
  pinta sí llega al lanzador (es inevitable: se ve), por eso el ajuste de títulos sigue teniendo sentido.

### 2.9 Recordatorios (locales en la v1) y notificaciones

- Uno por nota, como en Windows, con los mismos atajos (+1 h, esta noche 20:00, mañana 9:00) y fecha/hora a elegir.
- **Locales al dispositivo** y la interfaz lo dice («Este recordatorio solo avisa en este dispositivo»). No viajan al PC (formato 4).
- Notificación con: título (o «Nota protegida»), hasta 3 tareas pendientes si la nota no está protegida, y acciones **Hecho**
  (quita el recordatorio), **Posponer** (+1 h / mañana 9:00) y **Abrir**.
- **Hora exacta** **(usuario: exacta si se permite)**: por defecto alarma no exacta (puede llegar minutos tarde con el móvil en reposo);
  el ajuste pide `SCHEDULE_EXACT_ALARM` (en Android 14+ viene denegado y se concede en Ajustes del sistema). No se usa
  `USE_EXACT_ALARM` (Play lo reserva a apps de reloj/calendario; F-Droid no pone pegas, pero no conviene cerrar la puerta de Play).
- Se reprograman al reiniciar (`BOOT_COMPLETED`) y al cambiar la hora/zona. Si la nota pasa a archivo o papelera, no avisa (como Windows).

### 2.10 Lo que se queda fuera de la v1

Dock/abanico/ventanas/pantallas (nunca), carpeta compartida como transporte, más de un perfil de sync, **iniciar** una rotación de clave
o revocar acceso (Android solo la **sufre**: si nada descifra, mensaje «la clave de este perfil cambió: pega el código nuevo»), combinar
conflictos, desbloqueo biométrico, bloqueo de la app entera **(usuario)**, QR de emparejamiento **(usuario: pegar en la v1)**, temas de
nota propios, editor de temas, exportar a Markdown, notas vinculadas a archivos del móvil, recordatorios sincronizados (formato 5),
iOS. Copia de seguridad del sistema: **excluida** (ver §4.3).

## 3. Arquitectura y módulos

```
android/
  settings.gradle.kts        (raíz Gradle cuando exista la app: include :core, :sync, :text, :app)
  core/                      HECHO. Kotlin/JVM puro: formato 4, cifrado, protegidas, códigos de perfil, orden de versiones
  sync/                      Kotlin/JVM puro: motor de sync (plan + ejecución), interfaces LocalStore y SyncTransport,
                             transportes HTTP y WebDAV (OkHttp). Tests JUnit contra tests/vectors/sync-scenarios.json
  text/                      Kotlin/JVM puro: formato de texto (tareas, viñetas, listas, título), color (temas, OKLCH,
                             derivación), orden del mazo, búsqueda. Tests contra tests/vectors/text-*.json
  app/                       Android: Compose + M3 Adaptive, Room, Keystore, WorkManager, Glance, notificaciones
  fastlane/metadata/android/<locale>/   metadatos de F-Droid (título ≤50, corta ≤80, larga ≤4000, changelogs ≤500)
```

- **Por qué `sync` y `text` son JVM puro**: se prueban con `gradle test` sin SDK de Android (sesiones de nube, CI de Linux) y son el
  sitio donde vive la cultura de tests del proyecto. `app` solo pega piezas y pinta.
- **Hasta que exista la app**, `android/sync` es su propia compilación con `includeBuild("../core")` (coordenadas
  `app.aldune:aldune-core`): no hace falta tocar `android/core`. Al crear `app` se pasa a una raíz común.
- **Dependencias** (todas libres y en Maven Central o Google Maven, que F-Droid admite): AndroidX/Compose/M3 Adaptive, Room,
  WorkManager, Glance, OkHttp (+ MockWebServer en test), kotlinx-serialization. **Prohibido**: Google Play Services, Firebase,
  ML Kit, Crashlytics o cualquier SDK de analítica (F-Droid los rechaza). Si algún día hay QR, ZXing.
- **Textos**: una tabla única (p. ej. `android/strings.tsv` con clave + cinco idiomas) de la que un script genera
  `values*/strings.xml`; el script falla si falta un idioma, igual que la regla de `Strings.T` en Windows.

### 3.1 El motor de sync en Kotlin

Mismo comportamiento que `SyncService.SynchronizeCore`, pero **separando decisión y efectos**, que es lo que permite probarlo con vectores:

1. `SyncInput` = estado local (notas, borrados locales, bases, autores, ids excluidos) + objetos del almacén + ajustes de alcance + `deviceId`.
2. `planSync(input, key): SyncPlan` — función pura: lista de acciones por nota (subir nota, subir borrado, aplicar nota, aplicar borrado,
   papelera por borrado sin firma, borrar del almacén, guardar conflicto) + bases nuevas, o un error (nada descifra, formato desconocido).
3. `execute(plan, local: LocalStore, transport: SyncTransport)` — aplica en orden. En la app, `LocalStore` lo implementa Room dentro de una
   transacción; las bases se guardan al final, como en Core.

Se replica Core **incluidas sus rarezas** (las fijan los vectores; no se «arreglan» en Kotlin): si falta `format` vale 4; la asimetría
de alcance de los borrados locales (§9); que un objeto ilegible en HTTP aborta la pasada y en WebDAV se salta. **El formato desconocido
se trata en el transporte/deserialización**, no en `planSync`, que ya recibe sobres válidos.

Reglas que no se pueden olvidar al construir los sobres locales:
- Una nota protegida sube con `text: ""` **siempre**, aunque esté desbloqueada en memoria (en Core es seguro porque el repositorio
  devuelve el texto vacío; en Android hay que forzarlo, o el texto en claro viajaría dentro del sobre).
- Autor: `syncAuthorDeviceId` si la versión vino de fuera y no se ha tocado; si no, el `deviceId` propio (§1).
- Varias versiones del mismo id en el almacén: gana la mayor según `SyncVersion.compare` (`SyncService.cs:325-329`). No sale de un
  vector (la carpeta guarda un fichero por id); va en un test propio de Kotlin.

## 4. Almacenamiento

### 4.1 Room, con el texto cifrado en reposo

- **Room** (SQLite del sistema). El esquema de Android **no** tiene que ser el de `notes.db` de Windows: el contrato entre dispositivos es
  la sync, no la base. Room sí tiene migraciones (a diferencia de la regla de Windows): se exporta el esquema y cada versión lleva su migración probada.
- Tablas: `note` (id, `textCipher`/`nonce`/`tag`, color, createdAt, updatedAt, state, tags, dockPosition, screenOrigin, isProtected,
  `protectedContentJson`, `syncAuthorDeviceId`, `lineEnding`), `tombstone`, `sync_base`, `sync_conflict` (versión perdedora cifrada),
  `reminder` (noteId, dueAt).
- **Texto cifrado por fila** con AES-256-GCM y una **clave de datos** aleatoria guardada envuelta por una clave del **Android Keystore**
  (sin autenticación de usuario, para que el widget y la sync en segundo plano funcionen). Mismo modelo que Windows con DPAPI. No hace falta SQLCipher.
- Búsqueda: las notas se descifran a una caché en memoria y se busca ahí (son cientos, no millones; Windows hace lo mismo).

### 4.2 Secretos

Clave de sync, token del servidor y contraseña de WebDAV: envueltos por el Keystore, en almacenamiento privado de la app. Nunca en
`SharedPreferences` en claro. `EncryptedSharedPreferences` está obsoleta: no se usa.

### 4.3 Copia de seguridad del sistema

**Excluido todo** (`dataExtractionRules` y `fullBackupContent`): las claves del Keystore no viajan en la copia, así que restaurar la base
daría datos ilegibles. La recuperación es **volver a pegar el código de perfil** y sincronizar. Se dice en el asistente de sync.

## 5. Fases y criterio de verificación

| # | Fase | Dónde | Entregable | Verificación (puerta) | Sesiones |
|---|---|---|---|---|---|
| F0 | **Vectores de escenario** | Este PC (Windows: `SyncService` usa DPAPI y en Linux no corre) | `tests/vectors/sync-scenarios.json` + generador en `Aldune.Core.Tests` (§6) | `dotnet test` verde; cada escenario cita la línea de `SyncService.cs` que cubre; README de vectores actualizado | 1 |
| F1 | **Motor de sync Kotlin** | Nube (Sonnet) | `android/sync`: `planSync`, `execute`, dobles en memoria | `gradle test` verde con **todos** los escenarios; `android/core` sin tocar | 1 (nube) |
| F2 | Transportes | Nube o local | HTTP y WebDAV con OkHttp | MockWebServer con las respuestas reales del servidor (401, 404, 413, 5 MB); y una sync real contra `Aldune.SyncServer` en local | 1 |
| F3 | Texto, color, orden, búsqueda | Nube | `android/text` + vectores de texto generados desde Core (entrada, cursor, operación → salida) | `gradle test` verde contra los vectores de texto | 1-2 |
| F4 | App: esqueleto | Local (Android Studio) | Room + Keystore + lista/nota adaptativas + editor, **sin sync** | Pruebas de Compose en dispositivo + capturas por `adb` en Pixel y Tab (sonda móvil, §7) | 3-4 |
| F5 | App: sync | Local | Ajustes de sync, disparadores, WorkManager | Sonda: nota creada en Android aparece en el PC y al revés; conflicto provocado; borrado firmado y sin firma; servidor real del CT 103 con un perfil de prueba | 2 |
| F6 | Captura | Local | Compartir, acceso directo, mosaico, notificación «+», widget Glance | Checklist en los dos dispositivos, con el móvil reiniciado; test de que el widget nunca pinta una protegida | 2 |
| F7 | Recordatorios, protegidas, conflictos | Local | §2.3, §2.4, §2.9 | Aviso con la app cerrada y el móvil en reposo (exacto y no exacto); PBKDF2 medido en la tableta | 2 |
| F8 | Publicar | Local | Firma, build reproducible, fastlane en 5 idiomas, IzzyOnDroid y F-Droid | `fdroid build` reproduce el APK firmado; confirmar con el usuario antes de publicar | 1-2 |

**Total ≈ 14-17 sesiones** (F1 hecha en la nube cuenta como una). Coincide con la horquilla de `MOBILE_PORT.md` §13.2.

## 6. Cómo se generan desde Aldune.Core los vectores de escenario (F0; no implementado)

**Principio:** no se toca el comportamiento de Core. Se observa el motor de verdad, con un repositorio real y un almacén real, y se anota
lo que pasa. Nada de reimplementar la lógica en el test.

1. **Dónde**: `tests/Aldune.Core.Tests/SyncScenarioVectorsTests.cs`, con el mismo mecanismo que `SyncVectorsTests`: cada ejecución
   **genera y compara** con `tests/vectors/sync-scenarios.json`; con `ALDUNE_REGEN_VECTORS=1` lo reescribe.
2. **Montaje de cada escenario**: base de datos temporal (`NotesDatabase` en `%TEMP%`, nunca la del usuario), `NotesRepository`,
   `SettingsService` temporal con `deviceId` fijo (`"android-pc-a"`…), clave fija `00…1f` (la de los vectores de formato) y
   transporte **carpeta** sobre una carpeta temporal. `SyncService` no deja inyectar el transporte (`CreateTransport` es privado,
   :728), así que **no se envuelve**: se fotografía la carpeta `objects/` antes y después. Cada `Write` vuelve a cifrar con nonce nuevo,
   así que un fichero cambiado = subida, uno nuevo = subida, uno que falta = borrado del almacén. Así no hace falta ningún seam en Core.
3. **Estado inicial** descrito con datos, no con código: notas locales (con autor y base), borrados locales, ids excluidos (vinculada sin
   sync) y objetos del almacén (cabecera + contenido en claro + `signed` + qué clave: la de los vectores u «otra»). El generador escribe
   esos objetos con `SyncEnvelopeCodec` y guarda también su `envelopeJson`, para que Kotlin lea **exactamente** los mismos bytes.
4. **Ejecución**: una pasada de `Synchronize`. **Resultado observado**: acciones (el diff de la carpeta + el diff de la base antes y
   después por nota: aplicada, a papelera, borrada, sin cambios), conflictos guardados (nota, ganador, versión perdedora), bases finales,
   estado final del almacén (cabeceras + contenido descifrado) y error si lo hubo.
5. **Reloj**: `UtcNow` no es inyectable y **no se añade un seam**. Las fechas del escenario son fijas y del pasado; la regla de «borrado
   sin firma con fecha futura» usa 2099; lo que dependa de «ahora» (fecha del conflicto, `LastSyncAt`) **no entra en el vector**.
6. **Formato del fichero** (por escenario): `name`, `covers` (líneas de `SyncService.cs`), `settings` (`deviceId`, `scope`, `tag`,
   `noteIds`), `local` (`notes`, `tombstones`, `bases`, `excludedIds`), `remote` (`envelopeJson` + `plain` para leer a ojo),
   `expected` (`error`, `actions` ordenadas por `noteId`, `localAfter`, `remoteAfter`, `conflicts`, `basesAfter`). Fechas: comparar como instantes.
7. **Escenarios mínimos** (cada uno, una línea del motor):
   **E1** Solo local → subir. **E2** Solo remoto → aplicar. **E3** Iguales → nada. **E4** Local cambió, remoto = base → subir sin conflicto.
   **E5** Remoto cambió, local = base → aplicar sin conflicto. **E6** Los dos cambiaron con contenido distinto → conflicto, gana la versión mayor.
   **E7** Los dos cambiaron con el mismo contenido → sin conflicto. **E8** Sin base y distintos → conflicto (prudencia, instalación antigua).
   **E9** Empate de fecha con contenido distinto → desempata `deviceId` ordinal y **no** hay conflicto. **E10** Base + 1 ms (`OnlyMigrated`) → sin conflicto.
   **E11** Borrado remoto firmado → aplicar. **E12** Borrado remoto sin firma → papelera (E12b: nota que no existe aquí → nada; E12c: ya en la
   papelera → nada, sin tocar `UpdatedAt`). **E13** Sin firma con fecha futura → se borra del almacén.
   **E14** Sin firma con un objeto firmado del mismo id → se ignora. **E15** Borrado local → subir borrado firmado. **E16** Papelera viaja como estado.
   **E17** Alcance por etiqueta: nota sin la etiqueta → no sube. **E18** Nota que salió de la etiqueta aquí y es más reciente → no se aplica la
   remota (E18b: la remota es igual o más reciente → se aplica y se fija la base).
   **E19** Con etiqueta, un borrado remoto pasa igual. **E20** Alcance «notas elegidas». **E21** Vinculada sin sync → ni sube ni la toca un borrado sin firma.
   **E22** Protegida: sube y baja con `protectedContent`; misma protegida en los dos lados → sin conflicto. **E23** Ninguna nota descifra (otra clave) → error, cero escrituras
   (E23b: el almacén solo tiene borrados de otra clave → no aborta; van por el camino de los borrados sin firma).
   **E24** Un objeto con `format` 5: con carpeta **se salta** (es lo que fija el vector); el «error claro» de HTTP se prueba en el transporte (F2).
   **E25** Solo cambia `dockPosition` → sube. **E26** Asimetría de borrados locales con alcance por etiqueta (§9): se fija lo que hace hoy.
   **E27** Edición local frente a borrado remoto firmado más reciente → conflicto guardado con la edición como perdedora y nota borrada (y al revés).
   **E28** `ScreenOrigin` y etiquetas con mayúsculas distintas viajan y vuelven sin cambios (no generan subida ni conflicto).
   Rotación de clave: **fuera** (Android no la inicia en la v1; el caso «nada descifra» la cubre del lado receptor).

## 7. Verificación de la interfaz en Android (la «sonda móvil»)

El equivalente de `docs/WPF_PROBES.md`: pruebas de Compose (`createAndroidComposeRule`) ejecutadas **en los dos dispositivos reales**
por USB, con base de datos temporal, que hacen toques reales y guardan capturas (`adb exec-out screencap`). Más una pantalla de
diagnóstico (solo en depuración) que corre el guion de §9 de `MOBILE_PORT.md` (descifrar, sync contra servidor local, notificación,
widget, PBKDF2 medido). Se documenta en `docs/ANDROID_PROBES.md` en la fase F4.

## 8. Distribución (resumen; detalle en `MOBILE_PORT.md` §13.6)

Una sola clave de firma del desarrollador. **Build reproducible desde el primer día**, para que F-Droid publique el APK firmado por el
desarrollador (y no uno con su clave): así GitHub, IzzyOnDroid y F-Droid son intercambiables sin desinstalar, y solo hay una clave que
registrar en la verificación de desarrolladores de Google (2027 en todo el mundo).

## 8.1 Decisiones del usuario sobre distribución (2026-10-17)

- **`applicationId`: `io.github.nachopola16.aldune`** (permanente). El paquete Kotlin del código sigue siendo `app.aldune.*`
  (`namespace` y `applicationId` son independientes).
- **Nombre visible: «Aldune».** Comprobado el 2026-10-17: F-Droid no tiene ninguna app con ese nombre y una búsqueda web no encuentra
  ninguna app Android llamada así. En IzzyOnDroid la búsqueda web no fue concluyente: mirarlo a mano al pedir la inclusión.
- **Clave de firma:** una sola (RSA 4096, validez 30 años), generada en F8 con `keytool`; el almacén y su contraseña en KeePass y una
  copia cifrada fuera del PC. Nunca en el repositorio ni en una sesión de nube. Perderla = no poder actualizar la app.
- **Cuenta de Google:** nada por ahora; cuenta de distribución limitada (gratis, 20 dispositivos) cuando haga falta para los dos
  dispositivos propios, y verificación completa solo antes de publicar para otros en 2027.
- **Publicar:** primero APK firmado en las *releases* de GitHub + IzzyOnDroid; F-Droid cuando el build reproducible funcione.
- **Herramientas:** Android Studio en este PC antes de F4 (trae JDK 21, que también resuelve que Gradle no corra con el Java 26 del sistema).

## 9. Hallazgos en Windows/Core que salieron al diseñar (no se tocan en esta fase)

1. `NoteSearch` dice ignorar acentos y no lo hace (`CurrentCultureIgnoreCase`). Arreglarlo en Windows es un cambio aparte, con test.
2. Los borrados **locales** se filtran por ids del alcance pero no por etiqueta (`SyncService.cs:368-370`), a diferencia de las notas.
   Puede ser intencionado; el escenario E26 lo fija tal cual hasta decidirlo.
3. **Probable pérdida de datos en Windows, ya hoy (encontrado por la revisión de agy, comprobado leyendo el código; falta un test que
   lo reproduzca):** si llega por sync una versión **protegida** de una nota que en ese PC está **vinculada** a un archivo,
   `ApplySyncNote` no mira el vínculo y guarda el texto vacío; después `LinkedFileService.Reconcile` (:102-126) ve texto pendiente
   (`""` ≠ huella conocida), no comprueba `IsProtected` y **escribe el archivo vacío**. No hace falta Android: basta otro PC con Windows
   que proteja esa nota (allí no está vinculada). Arreglarlo en Windows con test primero (p. ej. que `Reconcile` no escriba nunca desde
   una nota protegida y que la protección remota desvincule o se rechace) **antes de publicar el cliente Android**. **Arreglado (2026-10-17):** `LinkedFileService.Reconcile` no hace nada con una nota protegida (ni escribe ni recarga el archivo; el vínculo se conserva), con
   dos tests en `LinkedFileServiceTests`. Android puede ofrecer «proteger» sin esa restricción.
4. Etiquetas: SQLite `NOCASE` solo pliega ASCII, `OrdinalIgnoreCase` pliega más: «Ñ»/«ñ» pueden contar como dos etiquetas en un lado y una en otro.

# Aldune para Android — F4: esqueleto de la app (sin sync)

> 2026-10-17. Detalla la fila F4 de [`2026-10-17-aldune-android-design.md`](../../specs/2026-10-17-aldune-android-design.md) §5. Manda esa spec en todo lo que
> no se diga aquí. Estado de partida: F0-F3 hechas (`android/core`, `android/sync`, `android/text`), Android Studio Rabbit 1 instalado,
> SDK con plataforma 37 (falta la 36), sin dispositivo conectado todavía.

## 0. Objetivo y límite

Una app que **abre, crea, edita, ordena, archiva y borra notas en el móvil y la tableta**, con el texto cifrado en reposo, el formato de texto
de Aldune (tareas, viñetas, sangría) y la paleta de colores de nota, **sin sync, sin recordatorios, sin protegidas, sin widget**. Es la base sobre la
que F5-F7 cuelgan lo demás, así que lo caro de cambiar después (esquema de Room, cifrado, límites entre módulos) se decide bien aquí.

Fuera de F4 aunque parezca cerca: sync (F5), compartir/mosaico/widget (F6), recordatorios, protegidas y conflictos (F7), idiomas distintos al inglés y
español **de cadenas nuevas** (las cinco lenguas se hacen con la tabla de §3.1 de la spec madre; en F4 se crea la tabla con los cinco desde el principio).

## 1. Decisión de reparto: una pieza pura nueva, `android/domain`

El riesgo de F4 no es pintar pantallas, es la lógica de «qué pasa cuando el usuario edita» (cuándo cambia `UpdatedAt`, cómo se conservan los saltos
de línea, cómo se reordena, qué hace papelera/purga). Si vive dentro de Room y de Compose solo se prueba en un dispositivo. Por eso:

```
android/
  domain/   NUEVO. Kotlin/JVM puro. Modelo de nota, NoteStore (interfaz), NoteService (reglas), NoteStore en memoria para tests,
            NoteCipher (interfaz). Depende de :text. Tests JUnit sin Android.
  app/      Android. Room (implementa NoteStore), Keystore (implementa NoteCipher), Compose, ViewModels. Casi sin reglas propias.
```

`domain` se puede construir aislado (no necesita SDK ni Android Studio) con el mismo método que F1-F3. `app` se hace aquí, en local.

### 1.1 `domain`: contenido

- `Note` (inmutable): `id` (UUID), `text` (siempre con `\n`), `lineEnding` (`LF` | `CRLF`, el que traía), `color` (hex tal cual llega), `createdAt`,
  `updatedAt`, `state` (`ACTIVE`, `ARCHIVED`, `TRASHED`; en sync viajan como 0, 1, 2), `tags`, `dockPosition: Double?`, `screenOrigin: String`
  (se conserva y se devuelve intacto), `syncAuthorDeviceId: String?`. Sin campos de protegida en F4 (llegan en F7; el esquema ya los reserva, §2.1).
- `NoteStore`: `observeAll()`, `get(id)`, `put(note)`, `delete(id)` (deja lápida local), `tombstones()`. Todo síncrono sobre un hilo de E/S; el
  `Flow` lo añade `app`. `InMemoryNoteStore` para tests.
- `NoteService(store, clock, deviceId, idFactory)` — **el reloj y los ids se inyectan** (a diferencia de `SyncService` de Windows) para poder probar:
  - `create(text?, color?, themeSettings)` → color por `NoteColorAssigner` mirando los colores del mazo activo; **no crea la nota hasta el primer carácter**
    (spec §2.7): `create` solo se llama en esa primera edición.
  - `edit(id, newText)` → normaliza `\r\n`→`\n` **solo para trabajar** y guarda con el `lineEnding` original (para una nota nueva, `LF`; se decide en §1.2).
    **Si el texto resultante es igual al guardado, no toca `UpdatedAt` ni escribe nada** (abrir y cerrar no genera versión). Si cambia, `UpdatedAt = clock.now`
    y `syncAuthorDeviceId = null` (la versión es de este equipo; spec madre §1).
  - `setColor`, `setTags` (máx. 20, sin duplicados ignorando mayúsculas, **conserva la forma en que llegaron**; igual que `SetTags` de Core: léelo), `archive`,
    `trash`, `restore`, `deleteForever` (solo desde la papelera; deja lápida local), `purgeTrashed(days)` (por defecto 30), `move(id, beforeId?, afterId?)` usando
    `NoteOrdering.between`/`spaced` (renumera si devuelve `null`).
  - Cada cambio de estado, color, etiquetas u orden también es edición local: pone `UpdatedAt` y `syncAuthorDeviceId = null`.
  - `list(filter, query, tag)` → notas ordenadas como el mazo (`dockPosition`, luego `createdAt`; las que no tienen posición, al final: **leer `NotesRepository.cs`
    para copiar el orden exacto**), con `NoteSearch` (texto y etiquetas).
- Reglas de texto que **no** se reimplementan: todo sale de `:text`.

### 1.2 Salto de línea de una nota nueva

Una nota creada en Android lleva `LF`. Windows ya lee y escribe ambos (`NoteText` une con CRLF solo al juntar título y cuerpo; el contenido guardado tal cual
llega). **Hay que comprobarlo con un vector de sync antes de F5**, no aquí: si Windows reescribiera CRLF sobre una nota LF crearía una versión nueva sin que
nadie la edite. Anotado como riesgo (§6); F4 no depende de ello.

## 2. `app`: arquitectura

- **Una sola `Activity`**, Compose, `ViewModel` + `StateFlow`, sin framework de inyección (un `AppContainer` a mano en `Application`; menos código y nada
  que F-Droid tenga que auditar). `minSdk` 26, `targetSdk` 36, `compileSdk` el de Android Studio. Kotlin y plugin de Android: **una sola versión para todos los
  módulos** en `android/gradle/libs.versions.toml`.
- **Raíz Gradle común** (primera tarea): `android/settings.gradle.kts` con `:core`, `:sync`, `:text`, `:domain`, `:app`; `sync` deja de usar `includeBuild`
  y pasa a `project(":core")`. Los tests de los módulos JVM siguen corriendo con `gradle test` desde su carpeta o desde la raíz; los 87+119+82 actuales no
  pueden perderse (se comprueban antes y después).
- **Gradle en este PC**: Android Studio trae su JDK 21 (`C:\Program Files\Android\Android Studio\jbr`). Usar **el JBR** para `app` y mantener el JDK 21
  Temurin para los módulos puros si se prefiere; en cualquier caso, exportar `JAVA_HOME` en cada comando. Sin wrapper de Gradle comiteado con binarios: se
  decide en la tarea 1 (el wrapper es un `.jar` pequeño y F-Droid lo exige verificado; ver §6).

### 2.1 Almacenamiento (Room + cifrado)

Tablas de la spec madre §4.1, con **todas las columnas ya** (también las de F5-F7, para no migrar enseguida): `note`, `tombstone`, `sync_base`,
`sync_conflict`, `reminder`. En F4 solo se usan `note` y `tombstone`. `exportSchema = true`, carpeta `app/schemas/` comiteada, y **un test de migración
desde la v1** desde el primer día (aunque la primera migración sea trivial; la infraestructura es lo que importa).

Cifrado, en dos capas separadas y con tests propios:
1. **`DataKey`**: 32 bytes aleatorios, generada una vez, **guardada envuelta** (AES-GCM) por una clave del **Android Keystore** (alias fijo, `setUserAuthenticationRequired(false)`,
   `StrongBox` si existe y si no, hardware normal; **sin** `setUnlockedDeviceRequired`, para que sync y widget funcionen con el móvil bloqueado). El envoltorio vive en un
   fichero del almacenamiento privado, no en `SharedPreferences`.
2. **`NoteCipher`** (interfaz en `domain`, implementada con `DataKey` en `app`): AES-256-GCM por campo, nonce de 12 bytes aleatorio, tag de 16, cifra el texto; el id de la
   nota va como **dato asociado** (AAD) para que una fila copiada a otro id no descifre. `textCipher`, `nonce`, `tag` en columnas aparte (como en Windows).
   Es cifrado **local**, independiente del de sync; no comparte formato con él.
- **Qué pasa si el Keystore pierde la clave** (restaurar copia, borrar huellas, algunos fabricantes): el envoltorio no descifra → la app lo detecta, **no borra
  la base**, muestra un aviso claro («No se puede leer la base: la clave del dispositivo cambió») y ofrece «Empezar de cero» con confirmación doble. Se prueba con un
  doble de Keystore que falla. La recuperación real es la sync (F5): el aviso lo dice.
- **Copia de seguridad del sistema excluida** (`dataExtractionRules` y `fullBackupContent`): spec madre §4.3.
- **Búsqueda**: caché en memoria con las notas ya descifradas (spec madre §4.1); se vacía al pasar la app a segundo plano más de 5 minutos.

### 2.2 Pantallas de F4

Las de la spec madre §2.1, §2.2 y §2.5 **sin lo de F5-F7**: lista adaptativa (`NavigableListDetailPaneScaffold`), editor, filtros Activas/Archivadas/Papelera, chip de etiqueta,
búsqueda, color y etiquetas desde el menú de la nota, reordenar por arrastre, deslizar para archivar/papelera, purga automática al abrir la app. Ajustes mínimos: aspecto
(Oscuro/Claro/Sistema), color por defecto de la nota nueva, días de papelera, idioma. El indicador de sync y los botones de F5+ **no se dibujan** (nada de pantallas muertas).

**El editor es la parte difícil** y se diseña aquí porque `BasicTextField` de Compose no da gancho de teclado de bajo nivel como `TextBox` de WPF:
- Estado: `TextFieldValue` (texto + selección). Toda operación de `:text` recibe `(text, caret)` y devuelve `(text, caret)`; se aplica con la selección colapsada en el caret.
- **Enter y Retroceso del teclado virtual**: no llegan como eventos de tecla fiables. Se detectan **comparando el valor anterior con el nuevo** (hecho con `TextFieldState` + `InputTransformation`, no con `onValueChange`: ver tarea 7 y `ANDROID_PROBES.md`): una inserción
  de exactamente `\n` en el caret equivale a Enter (se deshace y se aplica `EnterContinuation` si la línea es de lista y el caret estaba al final de la línea); un borrado de exactamente
  un carácter justo detrás de un prefijo de lista equivale a Retroceso (se aplica `RemoveOnBackspace`). Con teclado físico (tableta) se usan además `onPreviewKeyEvent` para Tab, Mayús+Tab,
  Alt+↑/↓ y Ctrl+Enter. **Esto se valida en el Pixel y en la tableta con teclados reales** (Gboard, y uno de terceros) antes de dar la tarea por buena: es el mayor riesgo técnico de F4.
- **Tocar un `☐`/`☒` lo marca**: se obtiene el desplazamiento del toque con `TextLayoutResult.getOffsetForPosition`; si cae sobre el glifo de una tarea, `ToggleCheckboxAt` y, si el ajuste
  «reordenar al marcar» está activo (apagado por defecto, como en Windows), `SettleToggled` con el cursor mapeado por `mapIndex`.
- **Barra de edición** sobre el teclado (`imePadding`): casilla, viñeta, sangría −/+, subir/bajar línea, y con la lista de tareas: desmarcar todo y quitar hechas (menú «⋯»).
- Guardado automático: pausa de 600 ms sin escribir, al salir de la nota y al pasar la app a segundo plano. Un único «escritor» por nota (cola de una); una escritura tardía no puede
  pisar una más nueva.
- Vista previa de la fila: `NoteTitleHelper.getTitle` / `getPreview`, contador `hechas/total` de `TaskLines.count`.

### 2.3 Colores y aspecto

Los colores de nota: `NoteThemes` + `NoteColorDerivation` de `:text` (ya hechos y verificados). **La paleta de la app (chrome)** necesita `AppPalette`/`AspectPalettes`: se genera sus vectores
desde Core y se porta **antes de la tarea de tema** (F3b, §4 tarea 5); hasta entonces la app usa Material 3 sencillo para no bloquear las demás tareas. No Material You (decisión del usuario).

### 2.4 Hecho (2026-10-17): tareas 1, 3 y 4

- **Tarea 1:** raíz Gradle común (wrapper con Gradle 9.6.0 fijado por hash, AGP 9.4.1, Kotlin 2.4.21, catálogo de versiones), módulo `app` que arranca en el
  Pixel 10a (Android 17). La variante de depuración lleva `applicationIdSuffix ".debug"`: las pruebas instrumentadas desinstalan su app al acabar y así nunca
  tocan una instalación con notas reales.
- **Tarea 3:** Room v1 con `note`, `tombstone`, `sync_base`, `reminder`; **`sync_conflict` se deja para F5** (su forma depende del motor; entrará con la migración
  1→2). Fechas como texto ISO (los instantes de sync llevan hasta 100 ns). `RoomNoteStore` + 8 pruebas en el dispositivo y la prueba de migración.
- **Tarea 4:** `AesGcmNoteCipher` y `DataKeyManager` en `:domain` (JVM, probados con un doble del Keystore); `AndroidKeystoreKeyWrapper` en `app`
  (StrongBox si existe; en el Pixel, clave en hardware seguro y 29 ms para desenvolver) y `AppContainer.open()` → `Ready` o `KeyLost`. Regla: sin clave
  recuperable **nunca** se genera otra ni se borra nada; solo «empezar de cero» (borra datos → fichero → clave del Keystore, en ese orden). El fichero de la
  clave vive en `noBackupFilesDir`. Falta la pantalla del aviso (tarea 6-8).
- **Tarea 6 (2026-10-17):** lista adaptativa (`NavigableListDetailPaneScaffold`), pestañas Notas/Archivadas/Papelera con contador, búsqueda, chips de etiqueta, «+»,
  deslizar para archivar/papelera/restaurar con «Deshacer», borrado definitivo con confirmación, tema de Aldune (`AppPalette`), pantalla de clave perdida y un editor
  de texto plano con guardado automático **en el `ViewModel`** (borradores que sobreviven al panel; una nota nueva no existe hasta el primer carácter). Probado a mano
  en el Pixel con toques reales. **Pendiente de la tarea 6:** reordenar por arrastre (hace falta decidir si se añade una librería; mientras, el orden es el del dominio),
  menú de color y etiquetas por nota, y comprobar la disposición ancha (tableta o emulador). Detalle aprendido: el buscador es el primer campo de texto y Android
  le daba el foco y el teclado al arrancar; se resolvió con un elemento invisible que recibe el foco tras el de la ventana (`NoteListPane`).
- **Tarea 7 (2026-10-17):** editor con casillas, viñetas y sangría. Enter y Retroceso del teclado virtual se deducen por diferencia en `ListKeys` (`:text`, 11 pruebas en la
  JVM); probado con Gboard real en el Pixel, y los atajos físicos con eventos de tecla por adb. Sigue pendiente un teclado de terceros, uno físico real y la tableta. La técnica
  y lo aprendido están en [`docs/ANDROID_PROBES.md`](../../ANDROID_PROBES.md).
  **Corrección posterior (2026-10-18):** con AnySoftKeyboard (teclado de terceros) la primera versión, basada en `onValueChange`/`TextFieldValue`, escribía otra vez la palabra a
  medias tras Enter (la región de composición del teclado no coincidía con el texto reescrito). Pasó a `TextFieldState` con un `InputTransformation`, que corrige dentro de la edición del
  teclado; verificado con Gboard y AnySoftKeyboard en el Pixel. Pendiente solo un teclado físico real.
- **Tarea 8 (2026-10-18):** `android/strings.tsv` (clave + en/es/de/fr/pt-BR) y `android/scripts/gen_strings.py`, que genera los cinco `values*/strings.xml` y falla si falta un
  idioma. Ajustes (`SettingsScreen`, `SettingsStore` en SharedPreferences): aspecto (Sistema/Oscuro/Claro/Pastel/Medianoche), idioma (se aplica en `attachBaseContext` y recrea la
  actividad; sin AppCompat ni selector del sistema), tono de las notas nuevas, días de papelera y «mover abajo las tareas hechas» (**apagado por defecto**, decidido el 18-10, como
  Windows; con Ctrl+Enter el cursor se queda en su sitio, al tocar sigue a su línea). Falta el color por tema de las notas nuevas (los nombres de tema de Core están en español).

## 3. Verificación (puertas)

1. `gradle test` verde en todos los módulos JVM, **incluidos los actuales sin perder ninguno**.
2. `domain`: tests con el reloj y los ids inyectados: abrir y cerrar sin editar no cambia `UpdatedAt`; editar sí, y pone `syncAuthorDeviceId = null`; CRLF se conserva; reordenar y renumerar;
   papelera, purga a 30 días, borrado definitivo deja lápida; etiquetas (límite, duplicados); búsqueda.
3. `app` (instrumentadas, en dispositivo): cifrado round-trip y el caso «Keystore sin clave»; migración v1; la lista no descifra todo cada vez que cambia una nota.
4. **Sondas móviles** (spec madre §7, `docs/ANDROID_PROBES.md`, que se escribe en la tarea 7): pruebas de Compose con base temporal y capturas por `adb exec-out screencap` en el Pixel y la
   tableta: crear, escribir una lista de tareas, marcar, Enter que continúa la lista, Retroceso que quita el prefijo, mover línea, reordenar, archivar, papelera, buscar, rotar, pantalla dividida.
5. Nunca contra datos reales de nadie; la base de la app de desarrollo es la del dispositivo de pruebas.

## 4. Tareas (en orden) y dónde se hacen

| # | Tarea | Dónde | Notas |
|---|---|---|---|
| 1 | Raíz Gradle común, catálogo de versiones, plataforma 36 instalada, módulo `app` vacío que compila e instala (`adb install`) | Local, Sonnet medio | Antes: `gradle test` de core/sync/text. Decidir wrapper (§6). |
| 2 | `android/domain` completo con tests | JVM puro | Se puede empezar ya. |
| 3 | Room (entidades, DAO, migración v1) + `NoteStore` real + tests instrumentados | Local | Necesita dispositivo o emulador. |
| 4 | Keystore + `DataKey` + `NoteCipher` + caso «clave perdida» | Local, **Opus medio** (diseño de seguridad) | Revisión cruzada con agy en solo lectura. |
| 5 | F3b: vectores de `AppPalette`/`AspectPalettes` (en Windows) y su port a `android/text` | Windows | Independiente de las demás. |
| 6 | Lista adaptativa + filtros + búsqueda + reordenar | Local | |
| 7 | Editor con `:text`, barra de edición, toque en casillas; `docs/ANDROID_PROBES.md` y primera sonda | Local | La tarea difícil (§2.2). Teclados reales. |
| 8 | Ajustes mínimos, idioma, aspecto, tabla de cadenas con los cinco idiomas | Local | Script que falla si falta un idioma. |
| 9 | Sondas completas en los dos dispositivos; informe `INFORME-ANDROID-F4.md` | Local | |

Las tareas 2 y 5 no dependen de nada y se pueden hacer **en paralelo** con 1 y 3.

## 5. Qué NO se decide aquí (se pregunta al usuario cuando toque)

- Si «reordenar al marcar» debe estar activo por defecto en Android (Windows: apagado).
- Qué muestran las filas con título largo en tableta (una línea o dos).
- Plataforma 36 o 37 como `targetSdk` (recomendado 36; en la 37 cambian comportamientos que no hace falta absorber ahora).

## 6. Riesgos

1. **Enter/Retroceso con teclados virtuales** (§2.2): puede exigir un `InputConnection` propio si la detección por diferencia falla en algún teclado. Se descubre en la tarea 7, no antes.
2. **Wrapper de Gradle y F-Droid:** F-Droid exige verificar el `gradle-wrapper.jar`; sin wrapper, el build reproducible depende de la versión de Gradle instalada. Decidir en la tarea 1 con la política de F-Droid delante.
3. **`LF` en notas creadas en Android** (§1.2): comprobar con un escenario de sync (F5) que Windows no crea versiones nuevas por el salto de línea.
4. **Plataforma 37 instalada, 36 no:** F4 no se puede construir contra 36 hasta instalarla.
5. **Pérdida de la clave del Keystore** (§2.1): sin sync no hay recuperación; F4 la hace visible pero no la resuelve. Aceptado hasta F5.

# Aldune — Brief de revisión del plan móvil (para otra IA)

> **Qué es esto.** Un encargo de **revisión crítica**, no de implementación, sobre el estudio del cliente
> móvil (`docs/MOBILE_PORT.md`) y el de los sistemas que faltan (`docs/DESKTOP_PORT.md`).
> Escrito el **2026-09-28**, cuando **no hay una sola línea de código** de móvil en el repositorio.
> Pensado para pegarlo tal cual en otro chat o en Claude Code: sección 7 tiene el prompt.

## 0. Qué te pido, y qué no

**Sí:**

1. Buscar **errores de hecho** en los dos documentos: cifras, rutas, nombres de fichero, líneas, APIs,
   afirmaciones sobre plataformas. Cada corrección, con la evidencia (comando o enlace).
2. Atacar los **ocho puntos de la sección 3**, que son los que más pueden invalidar el plan.
3. Decir qué **falta** (una alternativa que no se consideró, un riesgo sin nombrar, un coste sin contar).
4. Responder al checklist de la sección 4 con evidencia, no con opinión.

**No:**

- **No escribas código** ni toques `src/` ni `tests/`. Esto es una revisión de plan.
- **No reabras lo ya decidido** sin una objeción *nueva* y con evidencia: Android primero y probado en una
  tableta Android (`MOBILE_PORT.md` §0.1), sin publicar al principio, y **el dock se queda en Windows**.
  Si crees que una de esas decisiones está mal, dilo, pero con el argumento y el dato que falta.
- **No ejecutes la app de desarrollo contra `%LOCALAPPDATA%\Aldune`**: son las notas reales del usuario
  (regla del proyecto, `AGENTS.md`). Para probar, base de datos temporal.
- **No commitees nada.**

**Cómo quiero la respuesta:** una lista de hallazgos ordenada por gravedad, cada uno con
**(a)** qué está mal o falta, **(b)** la evidencia, **(c)** qué harías distinto, y **(d)** si es una
preferencia o un error. Gravedad: **bloqueante** (invalida el plan), **importante** (cambia una fase o un
coste), **menor** (matiz, redacción, referencia). Para cada punto de la sección 3, termina con
**"qué me haría cambiar de opinión"**: eso es lo que de verdad quiero leer.

## 1. Orden de lectura

1. `AGENTS.md` — reglas del proyecto (idiomas, comentarios en español, test primero, sondas, sin
   `Co-Authored-By`, nunca ejecutar contra los datos reales).
2. `docs/STATUS.md` — última entrada: el estudio y la decisión del 2026-09-28.
3. `docs/MOBILE_PORT.md` — **el plan a revisar**.
4. `docs/DESKTOP_PORT.md` — macOS, Linux, web, Windows ARM64, servidor ARM.
5. `docs/ROADMAP.md` §9 y §1 (investigación de mercado: el concepto ya existe en macOS).
6. Como referencia técnica: `docs/SYNC.md` (modelo de seguridad y compatibilidad),
   `docs/PROTECTED_NOTES.md`, `docs/WPF_PROBES.md` (cultura de verificación) y las specs de
   `docs/superpowers/specs/`.

## 2. Cómo comprobar cada afirmación (comandos y resultado esperado)

Todo esto lo verifiqué el 2026-09-28; si algo no cuadra hoy, es un hallazgo y quiero saberlo.

| Afirmación del plan | Cómo comprobarla | Esperado |
|---|---|---|
| `Aldune.Core` es portable salvo DPAPI | `Select-String -Path src\Aldune.Core\*.cs -Pattern 'ProtectedData\|DllImport\|Registry\|OperatingSystem\.\|Environment\.'` | Solo `DatabaseKeyProvider.cs` usa API de Windows (12 llamadas: `Wrap`, `Unwrap` ×2, `DataProtectionScope` ×2 por sitio); lo demás son comentarios |
| Los tests actuales valen en cualquier cliente | `dotnet build Aldune.slnx -c Debug` y `dotnet test Aldune.slnx --no-build` | 0 errores, **850/850** (4 avisos CA1416 conocidos de DPAPI) |
| 17 llamadas DPAPI en el código de la app | grep de `DatabaseKeyProvider` en `src/Aldune.Core/SyncService.cs` (12), `LocalBackup.cs:153` (1), `src/Aldune/App.xaml.cs` (4) | 17 |
| El formato de sync es el **4** | `SyncCompatibility.CurrentFormat` en `src/Aldune.Core/SyncModels.cs` | 4 |
| Las iteraciones de PBKDF2 son una constante de compatibilidad | `src/Aldune.Core/ProtectedNoteContent.cs:15` | 600.000 fijas en el código (no viajan en el dato) |
| Hay campos que **no** se sincronizan hoy | `SyncNotePayload` (`SyncModels.cs`) frente a las tablas de `NotesDatabase.cs` | No viajan: recordatorios (`NoteReminder`), tareas marcadas (`TaskCompletion`), posiciones de ventana (`NotePlacement`), conflictos, temas propios |
| Se publica solo `win-x64` | `portable.pubxml:23`, `scripts/build-installer.ps1:17` y `:52`, `.github/workflows/release.yml:34` y `:45` | `win-x64` en los cuatro sitios |
| El instalador **sí** acepta Windows 11 ARM64 | `installer/Aldune.iss:21-22` + [identificadores de arquitectura de Inno Setup](https://jrsoftware.org/ishelp/topic_archidentifiers.htm) | `x64compatible` incluye ARM64 con emulación x64 |
| El servidor son ~150 líneas y no tiene nada de Windows | `src/Aldune.SyncServer/Program.cs`, `Dockerfile` | 154 líneas; imagen `dotnet/aspnet:10.0` multi-arquitectura |
| Hay CI, y es solo Windows | `.github/workflows/ci.yml` | `windows-latest`, build + test de `Aldune.slnx` |

## 3. Los ocho puntos que quiero que ataques

### 3.1 MAUI frente a Avalonia y frente a nativo

El plan recomienda **.NET MAUI** con un argumento principal: reutilizar `Aldune.Core` y no volver a
implementar el formato de sync ni el cifrado. Ataques esperados:

- ¿Cuánto se reutiliza **de verdad**, contando que hay que sacar DPAPI de Core, hacer asíncrona toda la
  sincronización y meter serialización con generador de código? Si esos seams son más caros que
  reimplementar el motor en Kotlin, el argumento se cae.
- Da un número: **sesiones para reimplementar el motor de sync y el cifrado en Kotlin** (sobres,
  conflictos, tombstones, perfiles, códigos de invitación, rotación y revocación) **y sus tests**.
  Compáralo con 3 sesiones de seams + 0 de reescritura. Si el número es menor de 5, mi plan está mal.
- MAUI en tableta Android: ¿el editor con casillas `☐`/`☒`, sangría y listas se siente nativo y aguanta
  textos largos? ¿Y el widget de colección?
- ¿Avalonia (que también reutiliza Core, dibuja con Skia y tiene mejor historia de escritorio) no sería
  mejor para un cliente que, en el fondo, es una lista más una nota?

**Qué me haría cambiar de opinión:** un cálculo honesto del ahorro de reutilizar Core, o evidencia (no
opinión) de que MAUI no puede con el editor, el widget o un mazo de 200 notas.

### 3.2 ¿Réplica local con SQLite, o cliente fino contra el servidor?

El plan asume **la misma base SQLite local** en el móvil, reutilizando `NotesRepository`. Ataca:

- ¿Es necesario SQLite en el móvil, o bastaría una caché en fichero? ¿Qué se pierde? (offline real,
  búsqueda rápida, los mismos tests).
- Reutilizar el repositorio arrastra cosas de escritorio: `ScreenOrigin`, la tabla `NotePlacement` y un
  esquema **sin migraciones** (`CREATE TABLE IF NOT EXISTS` y ya, regla del proyecto).
- La resolución de conflictos es la parte más delicada del producto y ya está probada: ¿conviene tener
  **dos** clientes aplicando sobres, o concentrarlo todo en uno?

**Qué me haría cambiar de opinión:** un caso de uso real que el móvil necesite y que la réplica local no
dé, o un riesgo de divergencia de conflictos que no haya considerado.

### 3.3 Los "seams": ¿son suficientes y cómo se hacen?

El plan lista cuatro: `ISecretProtector`, API `async` de sync, `JsonSerializerContext` y rutas. Ataca:

- ¿Falta abstraer el **reloj** (`DateTimeOffset.UtcNow` está disperso), el sistema de ficheros, la red, el
  registro de diagnóstico (`DiagnosticLog` recibe rutas) o las notificaciones?
- "Añadir API async **conservando** la síncrona": ¿eso es un envoltorio con `GetAwaiter().GetResult()`
  —dos caminos que se pueden desincronizar— o un refactor con una sola implementación? Propón la forma
  concreta y di qué tests la fijan.
- `ISecretProtector` como `byte[] → byte[]`: ¿basta, o el Keychain pide metadatos (etiqueta, cuenta,
  accesibilidad, sincronización con iCloud) que el contrato no tiene?

**Qué me haría cambiar de opinión:** un seam que falte y obligue a volver a tocar Core **después** de
empezar el cliente. Eso es exactamente lo que quiero evitar.

### 3.4 La instantánea del widget en claro

El plan propone que la app escriba un JSON con títulos (y opcionalmente el primer renglón) en el
contenedor compartido, para que el widget no descifre nada. Ataca:

- Es una **excepción a "el texto va cifrado en reposo"**, que es la promesa central del producto. ¿Se
  puede evitar? Alternativa: el widget descifra con una clave de un grupo de llavero compartido
  (CryptoKit sabe AES-GCM; el coste es escribir ese descifrado en Swift). En Android el widget vive en el
  mismo proceso y podría leer la base directamente.
- ¿Cuánto cuesta de verdad la alternativa? Si es una sesión, mi diseño está mal y prefiero saberlo ya.

**Qué me haría cambiar de opinión:** que la alternativa sea barata y no añada riesgo nuevo en iOS.

### 3.5 El riesgo de SQLite + AOT en iOS, y su efecto en el orden

Android-primero **aplaza** este riesgo, no lo resuelve. Ataca:

- ¿Sigue siendo real hoy (`e_sqlite3`, enlaces que revientan en AOT-only, recorte) con
  `Microsoft.Data.Sqlite` en .NET 10, o es folklore de la época de Xamarin?
- Si el riesgo es alto, ¿cambia la conclusión de "MAUI para las dos plataformas"? ¿Hay que comprobarlo
  **antes** de empezar el MVP de Android, o se puede descubrir en la fase 3 sin tirar trabajo?

**Qué me haría cambiar de opinión:** evidencia de que en iOS con .NET 10 ya no es un problema (o de que
lo es, y hay que elegir otra biblioteca de SQLite desde el principio).

### 3.6 ¿El formato 4 basta de verdad?

El plan implementa **el formato 4 tal cual** y deja los recordatorios locales al teléfono. Ataca el caso
de uso, no la teoría: **el usuario crea un recordatorio en la tableta y abre el PC.** No viaja. ¿Es
aceptable en una v1, o es una trampa que se cobra en la primera semana de uso? Y si hay que adelantar el
formato 5: ¿cuál es el orden exacto de publicación para no romper compatibilidad (regla de `SYNC.md`: una
versión antigua rechaza lo que no entiende) y cuánto cuesta de verdad?

**Qué me haría cambiar de opinión:** un argumento de frecuencia de uso (¿los recordatorios se ponen más en
el móvil o en el PC?) o un plan de formato 5 más barato que la confusión que evita.

### 3.7 Las estimaciones

23-26 sesiones en total, con **6** para el MVP de Android y **3** para los seams. Ataca:

- Da tu propia estimación, con desglose, y señala dónde está la mayor diferencia.
- ¿Qué se ha olvidado contar? Por ejemplo: empaquetado y firma, el emparejamiento en Windows, el diseño
  de tableta, los cinco idiomas en móvil, la revisión de las tiendas, la curva de aprendizaje de MAUI y de
  su XAML, la sonda móvil.

**Qué me haría cambiar de opinión:** un desglose que muestre que la fase 2 son 12 sesiones y no 6. Eso
cambia si el port se hace o no.

### 3.8 macOS, Linux, web y ARM

En `DESKTOP_PORT.md` la conclusión es: **no** al dock fuera de Windows (el concepto ya existe en macOS y
está ocupado; en Linux haría falta una extensión del escritorio) y **sí** a compartir el Core. Ataca:

- ¿Falta algún camino viable? Se nombraron **XPF** (WPF sin modificar en macOS y Linux, comercial, con los
  P/Invoke del dock por verificar) y un cliente **Avalonia sin dock**. ¿Y Flutter para escritorio, Tauri,
  Electron, un plasmoid de KDE propio…?
- El análisis de **Linux** se apoya en que Mutter (GNOME) no soporta `wlr-layer-shell`. Comprueba el estado
  actual de GNOME (¿extensiones? ¿API de paneles?) antes de dar el tema por cerrado.
- **Windows ARM64**: el plan dice que ya funciona emulado y que un RID nativo es barato. ¿Compensa de
  verdad, o no justifica mantener dos arquitecturas en el instalador y en el CI?

**Qué me haría cambiar de opinión:** un camino a macOS de menos de 6 sesiones, o evidencia de que Windows
ARM64 molesta a usuarios reales (hoy no se conoce ninguno).

## 4. Checklist: contéstalo con evidencia, no con opinión

1. ¿Se puede hacer el MVP de Android (fase 2) **sin tocar `src/Aldune` (WPF)** más allá de los seams?
2. ¿Los 850 tests actuales pasan **tal cual** después de los seams, o hay que reescribir alguno?
3. ¿El `JsonSerializerContext` cambia **algún byte** del sobre que se guarda hoy? Debe existir un test que
   lo fije **antes** de tocar nada; si no existe, es un hallazgo bloqueante.
4. ¿`AesGcm`, PBKDF2 y SQLite funcionan en Android con .NET 10 **sin** dependencias nuevas?
5. ¿Qué versión mínima de Android hace falta en la tableta para que la fase 0 no encalle? El plan no la
   fija; el usuario tiene que mirarla.
6. ¿El widget del mazo se puede hacer en C# puro en Android **con lista desplazable**, o hace falta algo de
   Kotlin/Glance?
7. ¿Se puede instalar un APK firmado a mano en una tableta Android **sin** cuenta de Play y sin avisos
   insalvables? ¿Qué ajustes hay que tocar en la tableta?
8. ¿La sincronización desde fuera de la red local funciona tal como está documentada (HTTPS con Caddy), y
   qué le falta al plan para móvil? (¿reintentos? ¿tiempos de espera? ¿red que cambia?)
9. ¿La app móvil puede escribir con la misma `SettingsService` un `settings.json` **compatible** con el
   del PC, sabiendo que la clave envuelta pertenece a otro sistema?
10. ¿Hay algún sitio donde el plan **dé por hecho** que algo viaja entre dispositivos y en realidad no
    viaja? (recordatorios, tareas hechas, temas propios, posición de ventana).
11. ¿La decisión de no publicar al principio tiene alguna pega real? (firma que caduca, actualizaciones,
    copia de seguridad de la tableta, permisos al reinstalar).
12. ¿Qué pasa si se pierde el `settings.json` del móvil o su llavero, con notas que solo existían allí?
    En Windows hay `DatabaseKeyRecovery` y copias diarias; en el móvil, ¿qué?

## 5. Números y fechas a revalidar antes de empezar

Estaban verificados el **2026-09-28**; algunos envejecen rápido. Si un dato ya no cuadra, dilo:

- **MAUI en .NET 10**: versión y estado de soporte actual; hay que fijar el SDK con `global.json` antes de
  la fase 0.
- **Android target API 36** obligatorio para Play desde el 2026-08-31 (Google). Aunque no se publique al
  principio, fija la versión mínima y el `targetSdk` de la fase 0.
- **Xcode 26 / SDK de iOS 26** obligatorios para subir a la App Store (Apple, agosto 2026). Solo importa
  si se llega a iOS.
- **AES-GCM en iOS**: cifrado en hardware desde .NET 9 (Xamarin/MAUI Blog); verificar en .NET 10.
- **Modelos con los que se revisa este documento** (contexto, precios, cuotas gratuitas): cambian a
  menudo; comprueba la página de precios del proveedor el día de la revisión.
- **Pruebas de precio** de `MOBILE_PORT.md` §6: están hechas con las tarifas del 2026-09-28 y ya son
  orientativas; recalcular si el plan cambia de alcance.

## 6. Ya decidido, no lo reabras (salvo con evidencia nueva)

| Decisión | Razón | Dónde |
|---|---|---|
| **Android primero**, probado en la tableta Android, sin publicar | el usuario tiene tableta Android y un iPhone; la tableta es el dispositivo de referencia (dos paneles, teclado físico) | `MOBILE_PORT.md` §0.1 |
| **El dock se queda en Windows** | fuera de Windows el concepto ya existe y está ocupado, y habría que reescribir la mecánica de borde | `DESKTOP_PORT.md`, `ROADMAP.md` §9 |
| **No reescribir Core en otro lenguaje** | el activo real es el formato de sync, y sobre todo su modelo de seguridad | `MOBILE_PORT.md` §5 |
| **No imitar el dock en táctil** | se pierde lo único que el táctil no hace mejor | `MOBILE_PORT.md` §4 |
| **Formato de sync = 4 en la v1 móvil** | subirlo obliga a publicar antes en Windows | `MOBILE_PORT.md` §7 |
| **No hay PWA ni web ni ChromeOS propio** | la web competiría con la app real de una; ChromeOS se cubre con el APK de Android | `MOBILE_PORT.md` §9, `DESKTOP_PORT.md` |
| **No hacer macOS ni Linux por ahora** | ver las líneas de arriba; el Core compartido es el ahorro real | `DESKTOP_PORT.md` |
| **Las notas reales no salen de este equipo** | regla del proyecto; para probar, base de datos y datos temporales | `AGENTS.md` |

## 7. Prompt listo para pegar en otra sesión (Claude Code, Cline, Gemini…)

```
Eres un revisor crítico de arquitectura. NO escribas código: esto es una revisión de un plan.

Lee, en este orden:
  AGENTS.md
  docs/STATUS.md            (última entrada: el estudio de port a móviles, 2026-09-28)
  docs/MOBILE_PORT.md       (el plan a revisar)
  docs/DESKTOP_PORT.md      (macOS, Linux, web, Windows ARM64, servidor ARM)
  docs/ROADMAP.md           §9 y §1
  docs/SYNC.md y docs/PROTECTED_NOTES.md  (referencia)

Después abre el encargo completo: docs/MOBILE_PORT_REVIEW.md
  — sección 0: qué sí y qué no
  — sección 2: los comandos con los que comprobar cada afirmación y el resultado esperado
  — sección 3: los ocho puntos que quiero que ataques, cada uno con "qué me haría cambiar de opinión"
  — sección 4: el checklist de 12 preguntas
  — sección 6: lo que ya está decidido y no conviene reabrir sin datos nuevos

Verifica con comandos reales (dotnet build / dotnet test / grep), no de memoria.
Salida: hallazgos ordenados por gravedad (bloqueante / importante / menor), cada uno con
evidencia (fichero:línea o comando), qué harías distinto, y si es un error o una preferencia.
No commites nada, no toques src/ ni tests/, y nunca ejecutes la app contra %LOCALAPPDATA%\Aldune.
```

**Consejo de uso:** abre una sesión **por revisión** y no por fase de implementación: el repositorio
es la memoria (`AGENTS.md` → `STATUS.md` → estas guías), así que una sesión nueva no pierde nada y la
consulta cuesta menos. Quien revise no debe ser quien redactó el plan: necesitas ojos frescos.

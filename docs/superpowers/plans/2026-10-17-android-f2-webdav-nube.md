# Prompt de nube: F2 — transporte WebDAV y prueba contra el servidor real

> Para pegar en una sesión de Claude Code en la nube (Sonnet, esfuerzo medio) sobre `NachoPola16/aldune`. Presupuesto: ~6-8 $.
> Requisito: `android/sync` fusionado en `main` (hecho). Se borra cuando se fusione el resultado.

```
Lee AGENTS.md, docs/superpowers/specs/2026-10-17-aldune-android-design.md (§2.6, §3, §5 fila F2), docs/INFORME-ANDROID-SYNC.md y el código de
android/sync (sobre todo HttpSyncTransport.kt y HttpSyncTransportTest.kt, que es el patrón a seguir).
La referencia de comportamiento es src/Aldune.Core/WebDavSyncTransport.cs y src/Aldune.SyncServer/Program.cs: léelos, NO los modifiques.

IDENTIDAD GIT (obligatorio antes del primer commit):
  git config user.name "NachoPola16"
  git config user.email "nachopolac@gmail.com"
Sin Co-Authored-By ni mención a Claude/Anthropic. Antes de terminar: git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD
debe mostrar solo NachoPola16 <nachopolac@gmail.com>. Trabaja en la rama de sesión que te dejen empujar; no abras PR ni toques main.

ENTORNO: JDK 21 y Gradle del sistema (sin wrapper, sin binarios en el repo). cd android/sync && gradle test.
android/core NO se toca (su test reescribe tests/vectors/kotlin-output.json: no lo commitees, restáuralo con git checkout).
No modifiques src/, tests/Aldune.* ni tests/vectors/*.json. Comentarios en español, explicando el porqué.

TAREAS (commit + push + informe tras cada una; si te quedas sin margen, para tras la última completa):
1. WebDavSyncTransport.kt en android/sync (interfaz SyncTransport existente; OkHttp, bloqueante, mismo timeout de 20 s que HttpSyncTransport).
   Copia el comportamiento de WebDavSyncTransport.cs: PROPFIND con Depth 1 para listar, GET/PUT/DELETE (404 tolerado en DELETE), MKCOL de
   la carpeta si falta, autenticación Basic (usuario + contraseña de aplicación), mismas reglas de URL base (barra final) y mismos mensajes de
   error. Un objeto ilegible o de formato desconocido se SALTA al leer (como Core en WebDAV), a diferencia de HTTP. Parseo del XML multistatus
   con javax.xml (DocumentBuilderFactory con DTD y entidades externas desactivadas), tolerando prefijos de espacio de nombres distintos
   (d:, D:, sin prefijo) y hrefs con codificación porcentual.
2. Tests contra MockWebServer, con el patrón de HttpSyncTransportTest: lista vacía, varios objetos, respuestas tipo Nextcloud y tipo
   ownCloud/otro con prefijos distintos, 401, 404, 207 con errores, MKCOL cuando la carpeta no existe, objeto con formato 5 saltado,
   Basic correcto, y dos dispositivos sincronizándose a través del mock.
3. Prueba de integración contra el Aldune.SyncServer REAL, solo si puedes instalar el SDK de .NET 10 en la sesión; si no se puede, no
   gastes presupuesto: déjalo escrito como test que se salta si falta la propiedad de sistema `aldune.server.url` y anótalo en el informe.
   Arranca `dotnet run --project src/Aldune.SyncServer` en localhost con un token de prueba (mira docs/SYNC.md y Program.cs), y con
   HttpSyncTransport + planSync + execute y dos «dispositivos» comprueba: subida, bajada, borrado firmado, que un PUT de una versión más
   antigua NO pisa la nueva, y 401 con un token malo. Nunca datos reales ni un servidor que no arranques tú.

INFORME: docs/INFORME-ANDROID-F2.md con el esquema de INFORME-ANDROID-SYNC.md (entorno, casilla por tarea, diferencias con Core, sin hacer).
Actualízalo y commitéalo tras cada tarea. Al final: gradle test completo en android/sync (todo en verde, incluidos los 93 anteriores),
comprobación de identidad, push y resumen de 10 líneas.
```

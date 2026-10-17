# Vectores de compatibilidad del formato de sync 4

`sync-format4.json` lo genera `Aldune.Core` (la fuente de verdad) y lo verifica `tests/Aldune.Core.Tests/SyncVectorsTests.cs`
en cada ejecución. Un cliente que **no** use `Aldune.Core` (Kotlin en Android, Swift en iOS) es compatible si pasa estos
vectores **y** escribe cosas que Core lee. Si cambia el formato, el test de Core se pone en rojo antes que ningún cliente.

Regenerar (solo si se cambia el formato a propósito; piensa antes en las versiones ya instaladas, ver `docs/SYNC.md`):

```
ALDUNE_REGEN_VECTORS=1 dotnet test tests/Aldune.Core.Tests --filter "FullyQualifiedName~SyncVectorsTests"
```

## Qué dice el formato (lo que no se adivina leyendo el JSON)

**Clave de sincronización.** 32 bytes. Se escribe en base64url sin relleno (`+`→`-`, `/`→`_`, sin `=`); al leer se acepta con
o sin relleno y debe dar exactamente 32 bytes. Los vectores usan la clave `00 01 02 … 1f`.

**Sobre (`envelopeJson`).** JSON sin sangría, claves en camelCase y lectura insensible a mayúsculas:
`format` (4), `noteId` (GUID), `tombstone` (bool), `updatedAt` (ISO 8601 con desfase, p. ej. `2026-10-17T10:30:15.5+00:00`:
**.NET recorta los ceros finales de la fracción**; al leer vale cualquier ISO 8601 con desfase), `deviceId`,
`cipherText`, `nonce`, `tag` (los tres en base64 estándar con relleno). Un cliente rechaza `format` mayor que 4 (mensaje claro,
sin aplicar nada) y menor que 1.

**Cifrado del contenido de una nota.** AES-256-GCM, nonce de 12 bytes aleatorio por cifrado, etiqueta de 16 bytes, sin datos
asociados (AAD). **Lo que se cifra es el texto base64 (estándar, con relleno) del JSON de la nota, codificado en UTF-8** — no el
JSON directamente. Para descifrar: AES-GCM → texto UTF-8 → decodificar base64 → JSON. `payloadJson` de cada vector es ese
JSON ya decodificado.

**JSON de la nota (`payloadJson`).** Claves en camelCase: `id`, `text`, `color`, `createdAt`, `updatedAt`, `state`
(**número**: 0 activa, 1 archivada, 2 papelera), `screenOrigin`, `tags` (lista, puede faltar), `isProtected`,
`protectedContent` (objeto o `null`), `dockPosition` (número o `null`). .NET escribe los caracteres no ASCII como `\uXXXX`
(también los pares sustitutos de emoji): al leer, cualquier JSON válido sirve. **Tras descifrar hay que comprobar** que `id` y
`updatedAt` del contenido coinciden con `noteId` y `updatedAt` de la cabecera (si no, la nota se rechaza) y que una nota
protegida trae `protectedContent`.

**Borrado (tombstone).** Mismo sobre con `tombstone=true`. El contenido cifrado (misma técnica: base64 del JSON) es
`{"id": <noteId>, "deletedAt": <updatedAt>}`. Un borrado es **auténtico** solo si descifra con la clave y su `id` y su
`deletedAt` coinciden con la cabecera (`noteId`, `updatedAt`). Uno que no lo sea **nunca borra**: ver `docs/SYNC.md` →
«Modelo de seguridad» y `SyncService.HandleUnsignedTombstones`. Vector `borrado con fecha falsificada`.

**Nota protegida (`protectedNotes`).** Va dentro del JSON de la nota como `protectedContent`:
`{version: 1, salt, cipherText, nonce, tag}` (todo base64 estándar). La clave se deriva de la contraseña con
**PBKDF2-HMAC-SHA256, 600.000 iteraciones**, sal de 16 bytes, 32 bytes de salida; después AES-256-GCM (nonce 12, tag 16) del
texto UTF-8 **directamente** (aquí no hay capa base64). El número de iteraciones **no viaja en el dato**: es una constante de
compatibilidad.

**Código de perfil (`shareCodes`).** Prefijo `aldune-profile-v2:` seguido de base64url sin relleno de un JSON UTF-8 **con
claves en PascalCase** (`Version` (2), `Key`, `Scope`, `NoteIds`, `Tag`, `ProfileName`, `Transport`, `ServerUrl`).
`Scope`: 0 todas, 1 elegidas, 2 etiqueta. `Transport`: 0 carpeta, 1 servidor, 2 WebDAV (o `null`). Nunca lleva el token.
Los códigos antiguos (`fanote-profile-v1:` / `-v2:`) se siguen aceptando al importar.

**Orden de versiones (`versionOrder`).** Se compara primero `updatedAt` (el instante, sin importar el desfase) y, en empate,
`deviceId` por orden ordinal (unidades UTF-16 una a una, como `String.CompareOrdinal`; no cultural, y no por punto de código). El signo esperado de `Compare(a, b)` va en `expectedSign`.

## Detalles que salieron al portar a Kotlin (2026-10-17)

- **`expected` y `envelopeJson`/`payloadJson` escriben las fechas distinto:** `expected` lleva siempre 7 decimales
  (`...15.5000000+00:00`) y lo cifrado las recorta (`...15.5+00:00`). Compárense **como instantes**, no como texto.
- **El código de perfil se lee distinguiendo mayúsculas** (`Version`, `Key`…; uno en camelCase se rechaza), a diferencia del sobre, que
  se lee sin distinguirlas.
- **Si el sobre no trae `format`, vale 4** (es el valor por defecto de la propiedad en Core).
- **Contraseña de nota protegida:** mínimo 4 caracteres y no en blanco; se codifica en UTF-8 antes de PBKDF2.
- **URL del código de perfil:** Core la normaliza con `Uri.ToString()` (esquema y host en minúsculas, ruta vacía → `/`, puerto por defecto fuera).
- **Orden por `deviceId`:** ningún vector distingue unidades UTF-16 de puntos de código; el cliente Kotlin lo cubre con un test propio
  (emoji frente a U+FF5E). Tampoco hay vectores de los prefijos `fanote-profile-v1/v2:` ni del contenido descifrado de un borrado
  (`{"id": noteId, "deletedAt": updatedAt}`; confirmado reproduciendo byte a byte el cifrado).
- **Dos clientes, un contrato:** `kotlin-output.json` lo escribe `android/core` y lo lee Core (`SyncVectorsTests`). Cambia en cada ejecución del
  test de Kotlin (nonces y sales aleatorios): pendiente de escribirlo solo con una variable de entorno, como hace Core con el suyo.

## Vectores de escenario del motor (`sync-scenarios.json`)

Los genera y verifica `tests/Aldune.Core.Tests/SyncScenarioVectorsTests.cs` con el `SyncService` real (carpeta temporal como almacén).
Regenerar solo a propósito: `ALDUNE_REGEN_VECTORS=1 dotnet test tests/Aldune.Core.Tests --filter "FullyQualifiedName~RegenerateScenarioVectors"`.
Hay que hacerlo en Windows (el motor usa DPAPI). Spec: `docs/superpowers/specs/2026-10-17-aldune-android-design.md` §6.

Cada escenario (`scenarios[]`): `name` (E01…), `covers` (dónde está en `SyncService.cs`), `settings` (`deviceId` local, `scope` 0/1/2, `tag`,
`noteIds`), `local` (`notes` con `authorDeviceId` —null = versión de este equipo—, `bases`, `tombstones`), `remote` (`envelopeJson` tal cual
estaba en el almacén, ya cifrado, con `kind`, `key` y `plain`) y `expected`:

- `error` (null o texto), `uploaded`, `downloaded`, `conflictsResolved`, `tombstonesApplied`: los contadores de `SyncResult`.
- `remoteAfter`: cada objeto que queda en el almacén (ordenado por `noteId`): cabecera, `rewritten` (¿lo escribió esta pasada?), y la nota
  **descifrada** (`note`; null si no descifra con la clave) o `authentic` si es un borrado. Un objeto de formato desconocido sale como
  `unreadable` y debe quedar intacto. Un objeto que desaparece del almacén es que la pasada lo borró.
- `localAfter` (notas con su autor), `localTombstonesAfter`, `basesAfter`, `conflicts` (ganador y perdedora: fecha, dispositivo, `tombstone`, texto).

Reglas para quien las consuma: las claves son la de los vectores (`syncKeyHex`) y «other» (`otherKeyHex`); las fechas se comparan como
**instantes**; `"<now>"` es una fecha puesta por el reloj (comprobar solo que es reciente); no se comparan los bytes de un sobre ni el
`id` ni la fecha del conflicto (dependen del reloj). El mapa de escenarios es el de la spec §6.7.

Hallazgos que fijan estos vectores (comportamiento de Core hoy, no necesariamente deseable): un borrado remoto **sin firma** no queda en el
almacén visible para el motor, así que la nota local se manda a la papelera **y se sube de nuevo** pisándolo (E12a); con alcance por etiqueta
el borrado local se sube aunque la nota ya no tuviera la etiqueta (E26); edición frente a borrado deja un conflicto aunque gane el borrado (E27).

Pendiente de añadir: E14 (no se puede montar con carpeta: un solo fichero por id), E21 (nota vinculada sin sync, necesita montar `NoteFileLink`),
el reparto entre varias versiones del mismo id (HTTP/WebDAV) y la rotación de clave.

## Lo que estos vectores NO cubren (y hay que cubrir antes de dar el motor por compatible)

Los de formato (`sync-format4.json`) solo fijan los datos. El **algoritmo** lo fijan los de escenario de arriba, pero no cubren los
transportes reales ni la rotación de clave: eso se prueba aparte, contra un servidor real.

## Vectores de texto, color y orden (`text-lines.json`, `text-color.json`, `text-order.json`)

Los genera y verifica `tests/Aldune.Core.Tests/TextVectorsTests.cs` (F3 de la spec de Android) con las funciones reales de Core, sin tocarlas.
Regenerar solo a propósito: `ALDUNE_REGEN_VECTORS=1 dotnet test tests/Aldune.Core.Tests --filter "FullyQualifiedName~RegenerateTextVectors"`.
La salida es determinista (regenerar sin cambiar Core no cambia los ficheros).

Cada caso es `{"op", "args", "expected"}`, uno por línea. `op` es `Clase.Método` de Core; `args` son sus parámetros por nombre; `expected` es lo que
devolvió Core, ya pasado por JSON. Convenciones de la traducción a JSON:

- **Posiciones y cursores**: unidades UTF-16 (`string.Length`), las mismas que `String` de Kotlin. Hay cursores fuera de rango (`-1`, `len+5`) para fijar el recorte.
  Los cursores que parten un par sustituto (emoji) no se prueban.
- **`(string Text, int Caret)`** → `{"text", "caret"}`; si la función devuelve `null` (`EnterContinuation`, `RemoveOnBackspace`, `Move`, `ToggleCheckboxAt`,
  `CheckboxOnLine`, `RestOutlineFor`, `OklchColor.FromHex`, `Luminance`, `Between`), `expected` es `null`.
- `Count` → `{"done", "total"}`; `NoteText.Split` → `{"title", "body"}`; `IndentLines`/`OutdentLines` → `{"text", "selectionStart", "selectionLength"}`.
- **`TextEdit`** (`UncheckAll`, `RemoveChecked`, `SettleToggled`) → `{"text", "changed", "map"}`, con `map[i] = MapIndex(i)` para cada `i` de 0 a la longitud del texto original.
- **Enums como número**: `LineMovement.Move.direction` 0 arriba / 1 abajo; `NoteColorAssigner` `tone` 0 claro / 1 oscuro / 2 ambos, `rule` 0 rotar evitando vecinas / 1 rotar /
  2 más distinto / 3 fijo (en el enum de Core el orden es ese: `RotateAvoidNeighbors`, `Rotate`, `MostDistinct`, `Fixed`).
- **Saltos de línea**: los textos llevan LF, CRLF y mezclas; `EnterContinuation` recibe `newLine` (los dos). Core no normaliza nada: el port tampoco.
- **`NoteTitleHelper`** usa el texto de relleno por defecto de Core, `"New note"` (en la app lo fija el idioma). En Kotlin es un parámetro.
- **Números reales** (OKLCH, distancias, luminancia, `Between`): se escriben con el formato más corto que recupera el mismo `double`. Compáralos con una tolerancia
  pequeña (1e-9 relativa); los `#RRGGBB` se comparan **exactos**. Si algún hex difiere en una unidad de un canal por la diferencia entre `Math.Cbrt`/`Math.Pow` de .NET y
  de la JVM, **no ensanches la tolerancia en silencio**: es un hallazgo para apuntar en el informe.
- `NoteColorAssigner.Assign`: `themeId` (de serie) o `theme` en línea (`{id, name, dark, light}`); `dockColors` son los colores del mazo en orden.
  `NoteThemes.Resolve`/`Sanitize` reciben `custom` (lista de temas propios) cuando hace falta.

Rarezas de Core que estos vectores fijan tal cual (no son errores del port): `EnterContinuation` solo continúa o termina una lista con el cursor al **final** de la línea;
al terminar una lista vacía (tarea o viñeta) la línea queda vacía: se pierde también la sangría (`TrimEnd`); `TaskLines.PrefixLength` vale 1 si no hay espacio detrás del glifo;
`SettleToggled` no mueve nada si alguna tarea de la lista tiene subtareas; `Outdent` cuenta una tabulación como un nivel; `LineMovement` intercambia contenidos, no saltos;
`Between` devuelve `null` cuando las vecinas ya no admiten un número entre ellas.

**Fuera de estos vectores** (cubierto aparte): `NoteSearch` (Android pliega acentos y Core no, así que tests escritos a mano en Kotlin), `ColorFit`, `ColorMix`,
`AppPalette` y `AspectPalettes` (paleta de la app: pendiente de decidir si F3b o F4).

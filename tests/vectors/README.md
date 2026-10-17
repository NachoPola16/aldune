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

## Lo que estos vectores NO cubren (y hay que cubrir antes de dar el motor por compatible)

El **algoritmo** de `SyncService.Synchronize` (qué se sube, qué se baja, cuándo hay conflicto, las «bases» por nota, el alcance
por etiqueta/notas elegidas, los borrados, la rotación de clave) no está en estos vectores: son solo el formato de los datos.
El comportamiento de referencia es `src/Aldune.Core/SyncService.cs` y sus tests (`SyncConflictTests`, etc.). Antes de
publicar un cliente nuevo hay que añadir **vectores de escenario** (estado local + estado del almacén → acciones esperadas) generados
desde ese motor, y probar contra un servidor real.

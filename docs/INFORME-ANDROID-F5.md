# Informe — F5 (la sync dentro de la app Android)

Cierra las tareas 3, 4 y 5 de [`2026-10-18-aldune-android-f5-design.md`](superpowers/specs/2026-10-18-aldune-android-f5-design.md) §7; las tareas 1 y 2 las hizo la
sesión de nube ([`INFORME-NUBE-F5.md`](INFORME-NUBE-F5.md)). Probado en la tableta (Lenovo TB350FU, Android 14) por USB, con un servidor de sync de pruebas en este PC
y un «PC» simulado ([`ANDROID_PROBES.md`](ANDROID_PROBES.md), «Sonda de la sync»). Después se repitió en el Pixel y contra el servidor desplegado en el CT 103 (ver «Sondas en el Pixel y en el CT 103»).

## Estado

| # | Tarea | Estado |
|---|---|---|
| 1 | `domain`: notas protegidas | hecha (nube) |
| 2 | `syncclient` | hecha (nube, 165 pruebas) |
| 3 | Room v2, `RoomLocalSyncStore`, batería del contrato en el dispositivo | hecha |
| 4 | Secretos, perfil, Ajustes → Sincronización, primera conexión | hecha |
| 5 | Icono de estado, tirar para sincronizar, candado y conflicto en la fila, disparadores, WorkManager | hecha |
| 6 | Sondas | hecha en la tableta, en el Pixel y contra el servidor del CT 103 |

## Verificación

- `./gradlew test`: **605 pruebas en la JVM, 0 fallos** (core 82, sync 119, text 100, domain 123, syncclient 181).
- `./gradlew :app:connectedDebugAndroidTest` en la tableta: **43 pruebas, 0 fallos** (las 16 de F4, las **26 de `LocalSyncStoreContract` con Room** y la migración 1→2 con datos).
- `lintDebug` sin errores. `python android/scripts/gen_strings.py --check`: 124 cadenas en 5 idiomas.

## Lo que se hizo y por qué es así

- **La batería del contrato pasó a JUnit 4.** `AndroidJUnit4` no ejecuta JUnit 5 y el riesgo 1 de la spec (§8) se confirmó. Los tests de la batería llevan nombres en camelCase:
  el DEX de `minSdk` 26 no admite espacios en un nombre de método (con comillas invertidas el empaquetado falla). En la JVM los ejecuta el motor vintage de JUnit 5. No hizo falta el
  módulo `:syncclient-contract`.
- **Room v2**: `sync_conflict` (el JSON con las dos versiones va cifrado con el cifrador de las notas y el id de nota con prefijo `conflict:` como dato asociado, para que una fila de
  conflicto no descifre como texto de nota) y `RoomNoteStore` ya guarda `isProtected` y el JSON del contenido protegido. `RoomLocalSyncStore` comparte base y transacciones con las notas.
- **Una sola sync por proceso.** `SyncController` vive en `AppContainer` (que ahora es único por proceso, en `AldunaApp`): la interfaz y el trabajador de WorkManager comparten la misma, así
  que nunca hay dos pasadas a la vez. Conectar, desconectar y las pasadas se serializan con un cerrojo; desconectar no puede dejar bases de un perfil olvidado.
- **Secretos**: `SecretVault` (JVM, 14 pruebas) guarda clave y token/contraseña en `noBackupFilesDir`, envueltos por una clave del Keystore **distinta** de la de las notas (empezar de cero con
  las notas no los toca). Si no se pueden abrir, la pantalla lo dice y ofrece desconectar; nunca se borran solos.
- **Primera conexión**: se guardan secretos y perfil **antes** de descartar notas (descartar no se deshace). El formulario no usa `rememberSaveable`: el código lleva la clave.
- **Cuándo sincroniza**: abrir, primer plano (la actividad pasa por `onStart`; antes de que la base esté abierta se ignora), cada guardado, tirar de la lista, el botón y WorkManager (15 min con red).
- **Una nota abierta que la sync cambia**: si el usuario no ha escrito nada nuevo, el editor se recarga; si no se hiciera, su siguiente tecla guardaría el texto viejo encima del recién
  llegado y esa edición se perdería **sin conflicto**. Si la nota la borra la sync, el editor se cierra.
- `usesCleartextTraffic="true"`: el servidor de casa suele ir por `http` en la LAN (Windows lo admite). La pantalla avisa cuando la dirección no es `https`.

## Sondas en la tableta

| Sonda | Resultado |
|---|---|
| Código de perfil truncado (por `adb`) | «El código de perfil está dañado…» correcto |
| Código válido → dirección precargada, aviso de HTTP, token oculto | ok |
| Primera conexión con una nota local → diálogo; «Subirlas» | ok; la nota sale del servidor y **la descifra la sonda externa** con la clave del perfil |
| Nota del «PC» → «Sincronizar ahora» | ok, aparece en la lista |
| Edición del «PC» con esa nota abierta en el editor + tirar de la lista | ok: el editor se recarga con el texto nuevo |
| Conflicto (el «PC» y la tableta editan la misma nota) | ok: gana la más reciente (tableta), se sube, el «PC» queda como conflicto y la fila lleva el icono ⚠ |
| Borrado **firmado** del «PC» | ok: la nota desaparece y no resucita |
| Borrado **sin firma** (firmado con otra clave) | ok: la nota va a la papelera y se republica como papelera; **no se borra** |
| Nota **protegida** del «PC» | ok: candado, título «Nota protegida», sin vista previa; al abrirla, aviso de solo lectura; en el almacén sigue intacta |
| Servidor caído → «Sincronizar ahora» | «Sin conexión · última sync hace 2 min», detalle técnico en el diálogo, sin avisos emergentes; el estado sobrevive a reiniciar la app |
| Servidor otra vez en marcha → tirar de la lista | ok |
| Trabajo periódico de WorkManager | registrado (`RUNNABLE`) al conectar y **cancelado** al desconectar |
| Desconectar | ok: se borra `syncsecrets.bin`, se cancela el trabajo, las notas se quedan |

## Sondas en el Pixel y en el CT 103

**Pixel 10a (Android 17)**, 1080x2424, con el modo «No molestar» activado (una notificación de WhatsApp tapó la pantalla en la primera tentativa y se paró hasta activarlo).
Primero contra el servidor local por `adb reverse`, después contra el **servidor desplegado en el CT 103** por WiFi (`http://192.168.1.73:8098`).

El servidor del CT 103 se probó con un **contenedor desechable** (misma imagen que el servidor real, puerto 8098, sin volumen, `--rm`, token aleatorio), no con el servidor
real ni con el perfil real del usuario. Se borró al terminar; el servidor real (`aldune-sync`, puerto 8097) siguió arriba y `/health` contestó `ok` antes y después.

| Sonda | Pixel + servidor local | Pixel + CT 103 |
|---|---|---|
| Conectar con el móvil **vacío** (sin diálogo) y recibir las notas del almacén, con la protegida con candado y la papelera | ok | n/a |
| Conectar con notas locales → diálogo («Notas en este dispositivo: 3»); el texto nuevo ya no tiene el plural | n/a | ok |
| «Descartarlas»: el móvil queda vacío y **no se sube nada** (sin lápidas) | n/a | ok |
| Nota creada en el Pixel → sube sola a los pocos segundos y la **descifra la sonda externa** | n/a | ok |
| Edición del «PC» + tirar de la lista | ok | n/a |
| Conflicto (el «PC» y el Pixel editan la misma nota): gana la más reciente (Pixel), sube, icono ⚠ legible | ok | n/a |
| Borrado **firmado** del «PC» → la nota desaparece del Pixel | n/a | ok |
| Aviso de HTTP legible (antes casi invisible) | ok | ok |
| Desconectar y volver a conectar con otro perfil | ok | ok |

Hallazgos nuevos:

1. **Bitwarden ofrece guardar el token** al conectar (el campo es de contraseña y el gestor lo toma por una). No es un fallo, pero conviene decidir si se marca el campo para que
   el autorrelleno lo ignore (`importantForAutofill`) o se deja, porque un gestor de contraseñas es un buen sitio para un token.
2. En un teléfono el diálogo de primera conexión apila «Subirlas» arriba y «Cancelar / Descartarlas» abajo. Se entiende, pero «Descartarlas» queda pegado a «Cancelar».
   Es una acción que no se deshace: revisar el orden y quizá pedir confirmación.
3. El contador del diálogo cuenta también las notas de la papelera («Notas en este dispositivo: 3» con 2 activas y 1 en la papelera). Es lo que dice la spec (§4.8: cualquier estado).

## Hallazgos de la sonda (corregidos)

1. **«unexpected end of stream» se clasificaba como error desconocido.** Un proxy, un túnel o un servidor que corta a mitad de la respuesta cierran el socket y OkHttp lanza un
   `EOFException` envuelto. Ahora es «Sin conexión» (prueba nueva con `SocketPolicy.DISCONNECT_AFTER_REQUEST`).
2. **Tirar para sincronizar no funcionaba con la lista vacía**: el mensaje de «sin notas» no era desplazable y el gesto no enganchaba. Ahora lo es.
3. «Subir estas 1 notas»: el texto ya no depende del plural («Notas en este dispositivo: N…»).
4. El rojo de error del tema oscuro era `DangerStrong` (un relleno) y se veía casi negro sobre el fondo: ahora `error` es `ErrorText`.
5. El campo del código de perfil lo estropeaban las mayúsculas automáticas y la corrección del teclado: ahora es ASCII, sin capitalización ni autocorrección.
6. Una nota abierta que la sync borra dejaba el editor con su texto viejo, y lo que se escribiera se perdía sin avisar: el editor se cierra.

## Sin resolver o por decidir

- El servidor real del usuario no se ha tocado: se probó un contenedor desechable con la misma imagen. Falta, si se quiere, una sonda con el perfil real, que solo
  debería hacer el usuario (sincroniza sus notas de verdad).
- **Edición simultánea con el editor**: si el usuario **está escribiendo** en una nota y la sync trae una versión nueva de esa misma nota, gana lo que escribe el usuario (se guarda como una
  edición más reciente) y la versión que llegó se pierde sin dejar conflicto. Evitarlo del todo exige un editor con fusión; es un caso raro (misma nota, a la vez, en dos sitios).
- Una nota protegida que llega **no se puede abrir** (F7); sí se puede archivar, mandar a la papelera, colorear y mover.
- Los conflictos se guardan y se señalan, pero **no se resuelven** (F7). Hoy gana siempre la versión más reciente.
- El texto de «dañado» del código de perfil no distingue una clave rota de una versión nueva: los dos salen como «dañado o de una versión más nueva».
- Falta probar: rotar durante una pasada, WorkManager en la tableta con la app cerrada y la pantalla apagada (el sistema decide cuándo), y el modo PC de Lenovo con el teclado físico.
- **Las traducciones del alemán, francés y portugués las escribió Claude sin revisión de una persona**, igual que en F4.
- Una nota con saltos de línea mezclados (`\n` y `\r\n`) pierde la mezcla al pasar por `Note` y generaría una versión nueva al volver a subirse (ya dicho por la sesión de nube).

# Android: varios perfiles de sincronización

Fecha: 26-10-2026. Estado: pendiente de revisión. Primer bloque de la ampliación de la sync (los siguientes: emparejamiento por QR y
pegado/compartir de códigos; transparencia de la sync en segundo plano; pasada de huecos en errores y conflictos).

## Objetivo

Que un móvil pueda estar unido a **más de un perfil de sync a la vez**. Caso que lo motiva: el usuario sincroniza todas sus notas con su
servidor (perfil «todas») y además se une al perfil de la familia, que solo comparte una nota (la lista de la compra), alojado por otra
persona. Windows ya lo permite; Android hoy guarda un único perfil (`SyncController._profile`, `SyncProfileStore.load()` devuelve uno).

## Decisiones tomadas con el usuario

1. **Cada perfil sincroniza según su alcance, como en Windows.** Una nota que cae en dos alcances (la lista familiar también está en
   «todas») se sube a los dos almacenes. No hay una marca «nota de perfil ajeno».
2. **Solo unirse.** Android importa tantos códigos como se quiera. Crear y compartir perfiles desde Android (selector de notas, generar
   clave y código) queda para un bloque posterior; esos perfiles se siguen creando en Windows.

## Fuera de alcance

- Formato de sync: sigue siendo el 4. No se tocan `tests/vectors/` ni `Aldune.Core`.
- QR, pegar/compartir el código, aviso de batería, indicador de «nota compartida» dentro de la nota.
- Cambios en el servidor de sync.

## Diseño

### 1. Datos

- Cada perfil tiene un `profileId` (UUID, generado al conectar). `SyncProfile` pasa a llevarlo. Transporte, URL, alcance y nombre se
  guardan por perfil.
- **Secretos:** un fichero por perfil (`SecretVault`, con el `profileId` en el nombre), con el mismo envoltorio del Keystore y la misma
  escritura atómica. El formato del fichero no cambia.
- **Estado** (último éxito, intento, fallo): por perfil.
- **`sync_base` y `sync_conflict`:** pasan a clave `(profileId, noteId)`. Migración de Room con test.
- **Notas, lápidas y recordatorios** siguen siendo globales: son del dispositivo. Una lápida creada al borrar una nota la sube cada
  perfil cuyo alcance incluía la nota (lo decide el planner, igual que hoy).
- **`deviceId`** es único por instalación y sigue sobreviviendo a desconectar.

### 2. Migración de lo existente

El perfil único actual se convierte en el primero: recibe un `profileId` nuevo y se mueven a él sus preferencias, su fichero de
secretos, sus bases y sus conflictos. Sin pasos manuales. Una instalación sin perfil sigue sin perfil. Si el fichero de secretos no se
abre (`VaultState.Unreadable`), el perfil migrado queda con ese mismo estado y se vuelve a resolver pegando el código, como hoy.

### 3. Pasadas

- `SyncController` mantiene la lista de perfiles y recorre los que hay **de uno en uno**, dentro del mismo `passLock`. El fallo de un
  perfil queda en su estado y no detiene a los demás.
- `LocalSyncStore` se instancia por perfil (la vista de Room recibe el `profileId` y lo aplica a bases y conflictos). `runSync` y el
  planner no cambian: siguen trabajando con un solo almacén.
- Una sola `SyncTriggerPolicy` para toda la app. Cada disparo (abrir, primer plano, cambio local, manual, periódico) hace una pasada que
  recorre todos los perfiles. El trabajador de segundo plano no cambia de programación.
- `SyncStatus` y `secretsUnreadable` pasan a ser por perfil.

### 4. Primera conexión de un perfil adicional

Hoy, al conectar con notas ya en el dispositivo, se pregunta «subir o descartar», y descartar **borra todas las notas locales** sin
lápidas. Con un segundo perfil de alcance «elegidas» eso borraría las notas personales.

- El primer perfil se comporta como hoy.
- En un **perfil adicional** no hay opción de descartar ni pregunta: solo se sube lo que el alcance incluye y se baja lo que haya en el
  almacén. `firstConnectDecision` recibe si es el primer perfil.
- Una nota local con el mismo id que otra del almacén se resuelve por el planner existente (conflicto o gana la más reciente), como en
  cualquier pasada.

### 5. Desconectar

Se desconecta un perfil sin tocar los demás: borra sus secretos, bases y conflictos y deja las notas y las lápidas. Al desconectar el
último, el estado vuelve a ser el actual sin perfil (`policy.setProfileConfigured(false)`, se cancela el trabajo periódico).

### 6. Interfaz

- Ajustes → Sincronización pasa a una **lista de perfiles**: nombre, tipo, alcance (`scopeText`) y último estado, con «Sincronizar
  ahora» global y «Desconectar» por perfil.
- «Añadir perfil» abre el flujo actual (pegar código → elegir transporte → credencial → Conectar).
- Un perfil con credenciales ilegibles se marca en su fila.
- Las cadenas nuevas van en `android/strings.tsv` (cinco idiomas) y se regeneran con `python android/scripts/gen_strings.py`; `--check`
  debe pasar.

### 7. Conflictos

La clave de un conflicto guardado es `(profileId, noteId)`. Si una nota tiene conflicto en dos perfiles, la interfaz actual de
conflictos (por nota) enseña el más reciente y, al resolverlo, se limpian los de esa nota en los dos perfiles; la siguiente pasada
vuelve a sincronizar el resultado en ambos. Es la parte con más incertidumbre: se confirma con un test antes de implementarla.

## Pruebas

Lógica nueva con test primero, en la JVM (`android/syncclient`, `./gradlew test`):

- Dos perfiles sobre el mismo almacén en memoria con alcances solapados (todas + una nota): las bases no se mezclan, cada pasada sube
  y baja lo suyo, y un fallo en uno no afecta al otro.
- Borrado de una nota compartida: la lápida llega a los dos almacenes.
- Primera conexión del perfil adicional: no ofrece descartar y no borra notas.
- Desconectar un perfil deja intactos los demás y las notas.
- Migración de preferencias y secretos del perfil único (en `app`, instrumentada o Robolectric según lo que ya haya) y migración de
  Room de `sync_base`/`sync_conflict`.

Interfaz: compilación debug en el Pixel con datos propios y un servidor/WebDAV de prueba (sin notas reales; sin capturas de la pantalla
de inicio ni de notificaciones). Se comprueba: añadir un segundo perfil, que las notas personales sobreviven, desconectar uno y
mantener el otro, y la migración desde una instalación con un perfil.

## Documentación al terminar

`docs/SYNC.md` (Android admite varios perfiles; paso a paso para la nota familiar), `docs/STATUS.md`, `docs/MOBILE_PORT.md` si algo de lo
descartado cambia, `DECISIONES-2026-10-26.md` (QR) y la memoria según el protocolo.

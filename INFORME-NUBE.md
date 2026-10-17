# Informe de la sesión en la nube — android/core

Rama: `nube/android-core` (desde `main` 74e89e0). Módulo: `android/core` (Kotlin/JVM puro, Gradle KTS, JUnit 5).

## Entorno
- JDK 21, Gradle 8.14.3 del sistema. Red a Maven Central y al portal de plugins: **funciona**.
- No hay `gradle-wrapper.jar` en el repo (sin binarios). Se usa `gradle` instalado: `cd android/core && gradle test`.
- Compilación con `jvmTarget` 11 y `--release 11`.
- Dependencias: kotlin-stdlib, kotlinx-serialization-json 1.8.0, JUnit Jupiter 5.11.4 (solo test).

## Tareas
- [x] 0. Andamiaje Gradle.
- [x] 1. Clave de sync (`SyncKeyFormat`): 6 tests verdes. Sin diferencias con el README.
  - Nota: Core acepta también el alfabeto estándar mezclado (normaliza `-`/`_` y decodifica); Kotlin hace lo mismo.
- [x] 2. Sobre de sync, lectura (`SyncEnvelopeCodec.deserialize/decryptNote`, `ContentCipher`): 17 tests verdes, los 4 sobres de los vectores descifran y cuadran id/updatedAt con la cabecera.
  - Validación copiada de `SyncEnvelopeCodec.Validate` de Core: formato 1..4 (mensajes idénticos), id nulo, deviceId en blanco, `updatedAt` por defecto, nota sin cipherText/nonce/tag, id/updatedAt del contenido distinto de la cabecera, nota protegida sin `protectedContent`.
  - Las fechas se comparan como instante (`toInstant()`), no con `equals` (que mira el desfase).
  - Diferencias de comportamiento respecto a Core (no cubiertas por el README, las he replicado a propósito): si falta `format` en el sobre vale 4 (inicializador de la propiedad en C#); las claves se leen sin distinguir mayúsculas.
  - Errores: formato → `SyncFormatException`; clave equivocada/dato manipulado → `AEADBadTagException` (GeneralSecurityException); base64 o tamaños de nonce/tag incorrectos → `IllegalArgumentException`.
- [x] 3. Escritura de sobres (`createNote`, `serialize`, `noteToJson`, `ContentCipher.encrypt`): 18 tests verdes.
  - Nonce aleatorio (`SecureRandom`) por cifrado; el parámetro `nonce` de `encrypt` solo lo usan los tests.
  - Prueba fuerte: con el nonce de cada vector, Kotlin reproduce **byte a byte** `cipherText` y `tag` del sobre generado por C#. Confirma "se cifra el texto base64 del JSON", sin AAD.
  - El JSON de la nota que escribe Kotlin es igual (mismas claves, mismo orden) que `payloadJson` de los vectores.
  - Fechas: formato `2026-10-17T10:30:15.5+00:00` (desfase con `+00:00`, nunca `Z`, fracción recortada y máx. 7 dígitos). Java 11 no tiene nada que cambie esto en Android.
  - Los `double` se escriben con `Double.toString` de Java: un `2.0` sale como `2.0` (C# escribe `2`). Es JSON válido y Core lo lee igual.
- [x] 4. Borrados (`createTombstone`, `isAuthenticTombstone`): 7 tests verdes. "borrado firmado" → auténtico; "borrado con fecha falsificada" → no auténtico.
  - Con el nonce del vector, Kotlin reproduce byte a byte el cifrado del borrado firmado, lo que confirma que el contenido es exactamente `{"id":<noteId>,"deletedAt":<updatedAt>}` (texto base64 del JSON).
  - Cualquier fallo al verificar (sin contenido, base64 roto, etiqueta, JSON raro, `tombstone=false`) devuelve `false`, nunca lanza: igual que Core.
- [x] 5. Nota protegida (`ProtectedNotes`): 8 tests verdes; descifra el vector con `contraseña-de-prueba` (con ñ) → PBKDF2-HMAC-SHA256 600.000 it., sal 16, AES-GCM directo sobre UTF-8.
  - Contraseña mínima de 4 caracteres (UTF-16) y no en blanco, como `ValidatePassword` de Core.
  - Android: `PBKDF2WithHmacSHA256` existe desde API 26 (minSdk) y codifica la contraseña en UTF-8, igual que Core. **No verificado en un dispositivo real**, solo en JDK 21.
  - Rendimiento: 600.000 iteraciones en móvil pueden tardar ~0,3–1 s; llamar siempre fuera del hilo de UI (estimación, sin medir).
- [x] 6. Código de perfil (`ProfileCodes`): 15 tests verdes. Decodifica los 3 códigos de los vectores y **codificar los datos esperados reproduce exactamente la cadena de C#** (mismo orden de claves, `null` explícitos, base64url sin relleno). Prefijos `fanote-profile-v1:`/`-v2:` aceptados (el prefijo y `Version` del documento tienen que cuadrar, como en Core).
  - Detalle que el README no dice: el documento se lee **distinguiendo mayúsculas** (Core usa `JsonSerializer.Deserialize` con opciones por defecto, a diferencia del sobre, que usa perfil Web). Un código con `version`/`key` en camelCase se rechaza. Lo he replicado y hay test.
  - Normalización de la URL (`Uri.ToString()` de .NET): replicada solo para casos normales (esquema/host en minúsculas, ruta vacía → `/`, puerto por defecto fuera). Casos raros (IDN, escapes, userinfo) pueden diferir de .NET. Los vectores son URL ya normales.

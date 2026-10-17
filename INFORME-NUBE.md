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

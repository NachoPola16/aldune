# Informe de la sesión en la nube — android/core

Rama: `nube/android-core` (desde `main` 74e89e0). Módulo: `android/core` (Kotlin/JVM puro, Gradle KTS, JUnit 5).

## Entorno
- JDK 21, Gradle 8.14.3 del sistema. Red a Maven Central y al portal de plugins: **funciona**.
- No hay `gradle-wrapper.jar` en el repo (sin binarios). Se usa `gradle` instalado: `cd android/core && gradle test`.
- Compilación con `jvmTarget` 11 y `--release 11`.
- Dependencias: kotlin-stdlib, kotlinx-serialization-json 1.8.0, JUnit Jupiter 5.11.4 (solo test).

## Tareas
- [x] 0. Andamiaje Gradle.

# Informe de la sesión en la nube — android/sync

Rama: `claude/upbeat-archimedes-br02lb` (el prompt pedía `nube/android-sync`; la sesión está fijada a esta rama y no se empuja a otra).
Módulo: `android/sync` (Kotlin/JVM puro, Gradle KTS, JUnit 5, `includeBuild("../core")`).

## Entorno
- JDK 21, Gradle 8.14.3 del sistema; sin wrapper ni binarios en el repo: `cd android/sync && gradle test`.
- `jvmTarget` 11 y `--release 11`, igual que `android/core`.
- Maven Central devolvió 429 (límite de peticiones del egreso compartido) en las primeras resoluciones; reintentando con pausas se descargó todo
  y Gradle lo guarda en caché.

## Tareas
- [x] 0. Andamiaje Gradle + test que carga `sync-scenarios.json` y cuenta los escenarios (34). Verde.

## Diferencias encontradas (SyncService.cs / README / vectores / prompt)
_(se rellena al avanzar)_

## Sin hacer
_(se rellena al avanzar)_

# Informe de la sesión en la nube — F3 (módulo `android/text`)

Rama: `claude/sweet-meitner-coa4zr`. Módulo: `android/text` (Kotlin/JVM puro, paquete `app.aldune.text`, sin dependencias de Android).

## Entorno
- JDK 21 y Gradle del sistema, sin wrapper: `cd android/text && gradle test`.
- La rama de sesión salió desfasada de `main` (sin los vectores `text-*.json`); se avanzó con `git merge --ff-only origin/main` antes de empezar.
- Maven Central devuelve 429 de vez en cuando; se reintenta con pausa y queda en caché.
- Los tests leen `tests/vectors/text-lines.json`, `text-color.json` y `text-order.json` (un test dinámico por operación; al fallar imprime op, args,
  esperado y obtenido). `Operations.kt` (test) es el espejo del `Execute` de `TextVectorsTests.cs`.

## Tareas
- [x] 1. Lector de vectores, `LineText` (interno), `TaskLines`, `BulletLines`, `ListPrefix`, `ListIndent`, `LineMovement` (24 operaciones de `text-lines.json` en verde).
- [x] 2. `TextEdit` (con `mapIndex`), `TaskLists`, `NoteTitleHelper` (relleno como parámetro `placeholder`), `NoteText`: las 13 operaciones restantes de `text-lines.json` en verde.
- [ ] 3. Color.
- [ ] 4. Orden.
- [ ] 5. Búsqueda.
- [ ] 6. Pruebas propias.

## Diferencias con Core
1. `Trim`/`TrimEnd`/`IsWhiteSpace` de .NET se copian en `DotNetText.kt` (el conjunto de espacios de Kotlin difiere: cuenta U+001C..U+001F y no U+0085).

## Sin hacer
(se completa al final)

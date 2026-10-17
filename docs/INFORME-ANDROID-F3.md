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
- [x] 3. Color: `OklchColor`, `NoteColorContrast`, `NoteColorDerivation`, `NoteThemes` (`builtIn`, legacy, `resolve`, `sanitize`), `NoteTheme`, `NoteDisplayColor`, `NoteColorAssigner`. Las 18 operaciones de `text-color.json` en verde; **todos los `#RRGGBB` coinciden exactos** (ver hallazgo 1).
- [ ] 4. Orden.
- [ ] 5. Búsqueda.
- [ ] 6. Pruebas propias.

## Diferencias con Core
1. `Trim`/`TrimEnd`/`IsWhiteSpace` de .NET se copian en `DotNetText.kt` (el conjunto de espacios de Kotlin difiere: cuenta U+001C..U+001F y no U+0085).

## Hallazgos
1. **`OklchColor.FromHex` en grises puros** (`#010101`, `#7F7F7F`, `#FEFEFE`; 3 de 177 casos): el croma real es ~1e-8 (ruido de la resta de los canales a y b)
   y `Math.cbrt`/`Math.pow` de la JVM difieren de .NET en 1 ulp, que la resta amplifica: croma con diferencia relativa ~3e-11 (absoluta <1e-18) y matiz
   con ~1e-8 relativa (el matiz de un vector de longitud ~0 no significa nada). La tolerancia global sigue siendo 1e-9 relativa; solo para ese caso
   (`FromHex` con croma esperado < 1e-6) el test compara la claridad estricta, el croma con tolerancia absoluta de 1e-12 y no compara el matiz
   (`TextVectorsTest.matches`). Ningún `#RRGGBB` depende de ello: las 534 `ToHex`, las derivaciones y el reparto de color coinciden exactos, incluidos los
   grises del tema Grafito.

## Sin hacer
(se completa al final)

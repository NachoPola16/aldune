# Informe de la nube — F6, parte JVM pura

> 2026-10-18. Spec: `docs/superpowers/specs/2026-10-18-aldune-android-f6-design.md` §3. Solo se tocó `android/domain` (código y tests) y este informe.

## Qué se hizo

Un commit por paso:

1. **`DockOrder`** (§3.1): `object DockOrder { fun sort(notes: Collection<Note>): List<Note> }` con la regla de siempre (posición, sin posición = 1e18; luego
   `createdAt`; luego `id`). `NoteService.dockOrdered` ahora delega en él y la constante `UNPOSITIONED` se movió con la regla. Los tests de `NoteService*` no
   se tocaron y siguen verdes. 6 tests nuevos (`DockOrderTest`).
2. **`WidgetContent`** (§3.2): `WidgetStrip` (sellado, `Open` y `Locked`), `WidgetContent`, `WidgetOptions`, `WidgetContentBuilder`. Un test por regla (1-9), el
   de propiedad de la regla 3 (marcador en `text`, etiquetas y `protectedContentJson`, con y sin `showPreview`, ningún `toString()` lo contiene) y el del par
   sustituto. 17 tests nuevos (`WidgetContentTest`).
3. **`SharedNote` / `SharedNoteText.compose`** (§3.3): reglas 1-8, casos de borde de la 9 y la comprobación de que todo `Text` lo acepta `NoteService.create`.
   22 tests nuevos (`SharedNoteTextTest`).

## Tests por módulo (antes → después)

| Módulo | Antes | Después |
|---|---|---|
| core | 82 | 82 |
| sync | 119 | 119 |
| text | 100 | 100 |
| domain | 123 | 168 (+45) |
| syncclient | 181 | 181 |

`./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde (con `--rerun-tasks`). `tests/vectors/kotlin-output.json` descartado antes de cada commit.
La primera ejecución de la línea base falló una vez por un 429 de Maven Central (red del entorno); reintentada, sin más.

## Decisiones donde la spec no decía nada

- **Corte con «…»**: el resultado mide como mucho 120 (título) o 200 (vista previa) unidades UTF-16 **contando** los puntos suspensivos. Si el corte cae a
  media pareja sustituta, se quita el emoji entero (el resultado puede medir una menos).
- **Constantes con nombre**: `WidgetOptions.DEFAULT_LIMIT` (50), `WidgetContentBuilder.MAX_TITLE_LENGTH` (120) y `MAX_PREVIEW_LENGTH` (200),
  `SharedNoteText.MAX_LENGTH` (1.000.000).
- **Tope de `SharedNoteText`**: se mide sobre el texto final, después de convertir las casillas de Markdown (que nunca alargan el texto) y contando el asunto y
  el salto que se añaden. El blanco recortado no cuenta.
- **Regla 5**: «empieza por» con `startsWith(ignoreCase = true)` de Kotlin sobre la primera línea ya recortada con la definición .NET de blanco.
- **Regla 2**: el blanco es el de .NET (`U+0085` sí, `U+001C..U+001F` no). `DotNetText.kt` de `domain` ya tenía una copia privada de `isDotNetWhiteSpace`; se
  hizo `internal` y se añadió `trimEndDotNet` allí, en vez de tocar `:text`.
- **Protegidas**: `WidgetContentBuilder` decide por `isProtected` antes de mirar `text`, tags o contenido protegido.
- **Orden de los tests**: los tests se escribieron antes que la implementación, pero no se ejecutó una pasada en rojo separada (el código no compilaba sin los tipos).

## Qué quedó sin hacer

Nada de §3. No se tocó nada de `:app` ni de otros módulos, y no se necesitó nada `internal` de `:text` (se duplicó dentro de `domain`, como ya hacía `trimDotNet`).
No hay SDK de Android en este entorno, así que `:app` no se compiló; la parte Android (§4) queda para la sesión local.

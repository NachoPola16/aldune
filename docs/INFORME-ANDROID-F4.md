# Informe — F4 (app Android: notas locales, sin sync)

Cierra la fase F4 de [`2026-10-17-aldune-android-f4-design.md`](superpowers/specs/2026-10-17-aldune-android-f4-design.md). Todo se ha probado en el Pixel 10a
(Android 17) por USB con la app de depuración `io.github.nachopola16.aldune.debug`. La técnica de pruebas está en [`ANDROID_PROBES.md`](ANDROID_PROBES.md).
**La tableta (Lenovo Tab P11, Android 14) no se ha probado todavía.**

## Estado

| # | Tarea | Estado |
|---|---|---|
| 1 | Raíz Gradle común, módulo `app` | hecha |
| 2 | `android/domain` | hecha (nube, 113 pruebas) |
| 3 | Room v1 + `NoteStore` | hecha |
| 4 | Keystore, clave de datos, clave perdida | hecha |
| 5 | `AppPalette` en `android/text` | hecha (F3b) |
| 6 | Lista adaptativa, filtros, búsqueda, reordenar, color y etiquetas por nota | hecha |
| 7 | Editor con listas, barra, atajos, `ANDROID_PROBES.md` | hecha |
| 8 | Ajustes, cinco idiomas | hecha (falta el color por tema de las notas nuevas) |
| 9 | Sondas completas e informe | hecha en el Pixel; **falta la tableta** |

## Verificación

- `./gradlew test`: **414 pruebas en la JVM, 0 fallos** (core 82, sync 119, text 100, domain 113).
- `./gradlew :app:connectedDebugAndroidTest`: **16 pruebas en el dispositivo, 0 fallos** (Keystore, migración de Room, `RoomNoteStore`).
- `lintDebug` sin errores. `assembleRelease` (R8) compila (1,6 MB); **no se ha instalado ni ejecutado**: usa el id real de la app.
- `python android/scripts/gen_strings.py --check`: 67 cadenas en 5 idiomas, tabla y XML al día.

## Sondas en el Pixel (tabla de la spec §3.4)

| Sonda | Resultado |
|---|---|
| Crear una nota (el «+»; no existe hasta el primer carácter) | ok |
| Escribir una lista de tareas, tocar la casilla | ok (ver hallazgo 2) |
| Enter que continúa la lista, Enter en tarea vacía, Retroceso que quita el prefijo | ok con Gboard y con AnySoftKeyboard (ver hallazgo 3) |
| Tab, Mayús+Tab, Alt+↑, Ctrl+Enter (eventos de tecla por adb) | ok |
| Barra de edición y menú «⋯» (desmarcar todo, quitar hechas) | ok |
| Mover línea | ok (barra y Alt+↑) |
| Reordenar notas arrastrando; el orden sobrevive a reiniciar | ok |
| Color y etiquetas por nota (el chip aparece en la lista) | ok |
| Archivar, papelera, «Deshacer» | ok (ver hallazgo 4) |
| Restaurar y borrado definitivo con confirmación | ok |
| Buscar | ok |
| Ajustes: aspecto (Claro, Sistema), idioma (Deutsch), tono, días de papelera, «mover abajo las tareas hechas» | ok, se aplican en vivo |
| Rotar (con el texto a medias escrito) | ok: dos paneles en apaisado, el texto se guarda |
| Pantalla dividida | **simulada** con una ventana de media altura (`wm size`): usable, el editor deja ver ~1,5 líneas con el teclado abierto. No se ha probado el modo dividido real |
| Clave perdida | probada en la tarea 4 (instrumentada y a mano), no repetida con la interfaz final |

## Hallazgos (todos corregidos salvo los marcados)

1. **El teclado se abría al arrancar** y tapaba el «+». Compose daba el foco inicial al buscador y ocultar el teclado después era una carrera. El buscador
   ya no puede tomar el foco hasta que se toca (`focusProperties`); 4 arranques seguidos sin teclado.
2. **Un cambio hecho en el mismo evento del toque se perdía**: el campo reemite el texto anterior con el cursor nuevo. Marcar una casilla se pospone un fotograma.
3. **Enter con un teclado de terceros (AnySoftKeyboard) escribía otra vez la palabra a medias.** La primera versión corregía Enter/Retroceso en
   `onValueChange` (`TextFieldValue`), y la región de composición del teclado quedaba desajustada. Con `TextFieldState` + `InputTransformation` la
   corrección va dentro de la propia edición. **Gboard solo no lo habría descubierto**: probar siempre con un teclado de terceros.
4. **Una nota archivada o enviada a la papelera aparecía en su nueva pestaña ya «descartada», fuera de la pantalla** (solo se veía el fondo «Restore»):
   `SwipeToDismissBox` guarda su estado por id (`rememberSaveable`) y lo restauraba. Se repone al entrar en la composición.
5. Los avisos («Nota archivada… Deshacer») salían con el morado por defecto de Material: ahora usan la paleta de Aldune.
6. `lintDebug` fallaba por `setIsStrongBoxBacked` (API 28 con `minSdk` 26): ahora hay una guarda.

## Sin resolver o por decidir

- **Un deslizamiento en la pestaña Papelera abrió la nota en vez de pedir confirmación** una vez (con un gesto de 250 ms). Repetido a 500 ms funcionó. Parece
  una cuestión del gesto inyectado, no se ha reproducido; conviene probarlo con un dedo real.
- **Una nota de la papelera se puede abrir y editar.** Falta decidir si debe ser de solo lectura hasta restaurarla (qué hace Windows: mirar antes de cambiarlo).
- Los contadores de las pestañas (Notas 2, Archivadas 2…) ignoran la búsqueda; es coherente, pero conviene confirmarlo.
- El alemán, el francés y el portugués los escribió Claude sin revisión de una persona; solo se ha visto el alemán en pantalla.
- Color por tema de las notas nuevas: los nombres de los temas de Core están en español.

## Pendiente (en este orden)

1. **Tableta (Lenovo Tab P11, Android 14):** disposición ancha real, teclado físico real, y repetir las sondas de editor.
2. Probar a mano en el Pixel: teclado físico, pantalla dividida real, y el deslizamiento de la papelera con el dedo.
3. Ejecutar `assembleRelease` firmado en un móvil de pruebas sin instalación real de Aldune (y revisar con R8 que Room y Compose funcionan).
4. `/code-review ultra` de la rama (créditos de la nube hasta el 5-11-2026); solo lo puede lanzar el usuario.
5. Siguiente fase: F5, el motor de sync dentro de la app.

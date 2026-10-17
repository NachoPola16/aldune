# Informe — F4 (app Android: notas locales, sin sync)

Cierra la fase F4 de [`2026-10-17-aldune-android-f4-design.md`](superpowers/specs/2026-10-17-aldune-android-f4-design.md). Todo se ha probado en el Pixel 10a
(Android 17) por USB con la app de depuración `io.github.nachopola16.aldune.debug`. La técnica de pruebas está en [`ANDROID_PROBES.md`](ANDROID_PROBES.md).
La tableta (Lenovo TB350FU «Tab P11», Android 14, 1200×2000) se probó el 18-10 (ver más abajo).

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
| 9 | Sondas completas e informe | hecha (Pixel y tableta) |

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

## Sondas en la tableta (Lenovo TB350FU, Android 14, Gboard de tableta)

| Sonda | Resultado |
|---|---|
| Disposición en apaisado y en vertical | ok: lista a la izquierda y editor a la derecha en las dos orientaciones (en el hueco del editor, la barra queda sobre el teclado, que ocupa todo el ancho) |
| Crear nota, tarea, Enter que continúa, Enter en vacío, Retroceso que quita el prefijo | ok |
| Tab (la tecla Tab del teclado de tableta llega como tecla real) | ok: sangra la línea |
| Arrastrar para reordenar | ok |
| Ajustes en pantalla ancha | ok tras limitarlos a una columna centrada (antes se estiraban a todo el ancho) |
| Rotar con una nota recién creada | **fallaba** (hallazgo 7); ok tras el arreglo |

**Teclado físico real** (Bluetooth 3.0 Keyboard, con la tableta en modo PC de Lenovo): el usuario comprobó a mano Tab, Alt+↓, Ctrl+Enter, Mayús+Tab y Enter en una lista de tareas, y funcionó todo.

En modo PC la app va maximizada con barra de tareas y mantiene los dos paneles (ventana de 1333 dp de ancho). Una vez, justo tras el cambio de modo, se vio un solo panel de lista a todo el ancho; tras relanzar la app no volvió a pasar y no se reprodujo. Redimensionar la ventana (`am task resize`) no funciona con la app maximizada, así que el cambio de tamaño en vivo no se ha probado.

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
7. **Al rotar con una nota recién creada con «+», el editor quedaba en blanco.** El panel se recrea con la clave de «nota nueva» (vacía), que ya no apunta a la nota;
   si se escribía, se creaba una segunda nota. Ahora el borrador de la nota nueva sobrevive a la recreación (`startNewNote` lo reinicia al pulsar «+»).
8. Los ajustes se estiraban a todo el ancho en la tableta: ahora, una columna centrada de 640 dp como máximo.

## Sin resolver o por decidir

- Un deslizamiento en la pestaña Papelera abrió la nota en vez de pedir confirmación una vez en el Pixel (gesto inyectado de 250 ms). En la tableta, 6 gestos de 100 ms a
  1 s dieron siempre el diálogo de borrado: no reproducido, tratado como artefacto del gesto inyectado.
- **Una nota de la papelera se puede abrir y editar.** Falta decidir si debe ser de solo lectura hasta restaurarla (qué hace Windows: mirar antes de cambiarlo).
- En pantalla ancha, la fila de la nota recién creada con «+» no se marca como seleccionada en la lista (el panel sigue con la clave vacía). Es cosmético.
- Los contadores de las pestañas (Notas 2, Archivadas 2…) ignoran la búsqueda; es coherente, pero conviene confirmarlo.
- El alemán, el francés y el portugués los escribió Claude sin revisión de una persona; solo se ha visto el alemán en pantalla.
- Color por tema de las notas nuevas: los nombres de los temas de Core están en español.

## Pendiente (en este orden)

1. Probar a mano la pantalla dividida real (y el cambio de tamaño de ventana en modo PC).
2. Ejecutar `assembleRelease` firmado en un móvil de pruebas sin instalación real de Aldune (y revisar con R8 que Room y Compose funcionan).
3. Delegar en una sesión de Claude Code en la web lo que compile en Linux (los créditos caducan el 5-11-2026). `/code-review ultra` es aparte: cuota propia, no gasta esos créditos.
4. Siguiente fase: F5, el motor de sync dentro de la app.

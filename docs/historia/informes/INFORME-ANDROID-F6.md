# Informe — F6 (captura rápida y widget)

Cierra la parte Android de [`2026-10-18-aldune-android-f6-design.md`](../../superpowers/specs/2026-10-18-aldune-android-f6-design.md) §4; la parte JVM la hizo la sesión de nube
([`INFORME-NUBE-F6.md`](INFORME-NUBE-F6.md): `DockOrder`, `WidgetContentBuilder`, `SharedNoteText`, 45 pruebas nuevas en `domain`). Probado en el **Pixel 10a** (Android 17)
por USB, con «No molestar» activado. **La tableta no estaba conectada**: ver «Sin probar».

## Estado

| Puerta / pieza | Estado |
|---|---|
| Acceso directo del icono (`shortcuts.xml`, estático) | hecho; registrado en el sistema y probado por intent (no con pulsación larga) |
| Compartir hacia Aldune (`ACTION_SEND` de texto) | hecho y probado |
| Mosaico de Ajustes rápidos | hecho y probado (con la app cerrada) |
| Notificación fija «Nota nueva» (apagada por defecto) | hecha y probada (permiso, denegar, tocar); falta el reinicio |
| Ajustes → Captura rápida (notificación, vista previa del widget, «Añadir el widget», «Añadir el mosaico») | hecho |
| Widget del mazo (Glance 1.2.0) | hecho y probado en el Pixel |

## Verificación

- JVM (`:core :sync :text :domain :syncclient`): **650 pruebas, 0 fallos** (82 + 119 + 100 + 168 + 181). La suite se ejecutó tras fusionar la rama de la nube.
- `lintDebug` sin errores. `python android/scripts/gen_strings.py --check`: 148 cadenas en 5 idiomas.
- No se ejecutó `connectedDebugAndroidTest`: desinstala la app de depuración al acabar y se habría llevado el widget y las notas de prueba. No se ha tocado Room.

### Sondas en el Pixel

| # | Sonda | Resultado |
|---|---|---|
| 1 | Acción `NEW_NOTE` en frío (4 veces) y con la app abierta | editor de nota nueva con el teclado (ver hallazgo 1); salir sin escribir no deja nota |
| 2 | Compartir enlace con asunto | nota «asunto / URL» en el editor |
| 3 | Compartir un texto que empieza por su asunto; `- [ ] a`; solo espacios | sin repetir asunto; `☐ a`; aviso «There is no text to save» y ninguna nota |
| 4 | Rotar dos veces tras compartir | una sola nota |
| 5 | Notificación: activar sin permiso → «No permitir» | interruptor apagado, explicación y botón a los ajustes del sistema |
| 6 | Notificación: conceder y activar; tocarla con la app en segundo plano | sale en «Silent», con el texto; abre el editor con el teclado |
| 7 | Mosaico: «Añadir el mosaico» (diálogo del sistema); tocarlo con la app cerrada | editor de nota nueva con el teclado |
| 8 | Widget: «Añadir el widget» (diálogo del sistema con la vista previa) | se coloca; cuadrícula de 2 columnas con 4×2 celdas; tiras con el color de cada nota |
| 9 | Tocar una tira / el «+» | abre esa nota (sin teclado) / una nota nueva |
| 10 | Intent `OPEN_NOTE` con un id que no existe | lista y aviso «That note is no longer in the deck» |
| 11 | Crear una nota (compartiendo) con el widget puesto, con el proceso vivo y en frío | el widget la pinta en 3-6 s |
| 12 | **Una protegida** (fila marcada en la base de pruebas), con y sin vista previa | tira «🔒 Protected note» en su sitio y color; su texto no aparece en ninguna parte |
| 13 | Tocar la protegida | lista con su fila señalada 3 s |
| 14 | «Mostrar más texto en el widget» con una nota de dos líneas | título y vista previa, sin recortes (ver hallazgo 5) |
| 15 | 53 notas | 50 tiras, desplazables, y al final «More notes: 3» |
| 16 | Estrechar el widget | una columna |
| 17 | Tema claro del sistema | el widget cambia a la paleta clara |

## Hallazgos y decisiones

1. **Una nota nueva abre el teclado siempre** (`NoteEditorPane`): antes dependía de una carrera con el foco (en frío no, con la app abierta sí), porque `stateHidden` de la ventana
   se come un `show()` temprano. Ahora el editor de una nota nueva pide el foco y, 250 ms después, el teclado (4 de 4 arranques en frío y en caliente). **También afecta al «+» de la lista.**
   Una nota existente no lo abre. Es una decisión mía que cambia F4: revisar.
2. **`RemoteViews` solo admite ciertas clases**: un `<View>` en `widget_preview.xml` hacía fallar la inflada en el lanzador («Couldn't add widget») y, de paso, el widget ni salía en el
   selector. Va con `FrameLayout`.
3. **El selector de widgets del lanzador no listaba el widget** (con la vista previa rota) aunque el sistema lo tenía registrado. El botón «Añadir el widget» (`requestPinAppWidget`) no
   depende de él y es útil por sí mismo.
4. **Glance reutiliza la sesión de un widget y no vuelve a llamar a `provideGlance`**: lo cargado ahí se quedaba viejo. El widget recarga dentro de la composición, atado a un contador
   (`WidgetUpdater.version`). Además, una petición que llega **mientras se pinta** se descartaba: ahora hay una marca `dirty` y el bucle repinta otra vez.
5. **El `GridView` de un widget dimensiona todas las filas con la primera**: la vista previa quedaba recortada. Las tiras tienen altura fija y uniforme (40 dp; 66 dp con la vista previa).
6. AGP 9 exige `buildFeatures.resValues = true` para el `resValue` del paquete del acceso directo (el XML no recibe las variables del manifiesto).
7. `startActivityAndCollapse(Intent)` está obsoleta desde la 14, pero antes es la única: lint se silencia con una nota.
8. Compartir en la tarea del que comparte no se ha podido comprobar con una app real (ver abajo).

## Sin probar

- **La tableta (TB350FU)**: el widget estirado a 3 columnas, modo PC, y toda la checklist (no estaba conectada).
- **Reiniciar el móvil**: que la notificación vuelva (`BootReceiver`) y que el widget siga. Hace falta que el usuario desbloquee tras arrancar.
- **Compartir desde Chrome de verdad**: que Atrás vuelva a Chrome y no deje dos copias de la app. (Con `adb`, un intent idéntico al de la raíz de la tarea no se entrega: ver
  `ANDROID_PROBES.md`.)
- **Pulsación larga en el icono** (el acceso directo solo se probó por intent), el mosaico con el móvil bloqueado, los cinco idiomas dentro del widget, los aspectos Pastel y Medianoche.
- Una prueba instrumentada del widget con `glance-appwidget-testing`: la garantía de las protegidas está en el tipo (`WidgetStrip.Locked` no tiene texto, probado en la JVM) y en la sonda 12.
- Las traducciones de alemán, francés y portugués las escribió Claude sin revisión de una persona.

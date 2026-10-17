# Sondas de la app Android

Los tests de `android/*` (JVM) no cubren Compose ni el teclado. La interfaz se verifica en el dispositivo de pruebas (Pixel 10a por USB)
con la **app de depuración** (`io.github.nachopola16.aldune.debug`: tiene su propia base de datos, nunca toca una instalación con notas
reales) y toques reales por `adb`. Es la contraparte de [`WPF_PROBES.md`](WPF_PROBES.md).

## Preparar

```bash
export JAVA_HOME="/c/Program Files/Eclipse Adoptium/jdk-21.0.12.101-hotspot" ANDROID_HOME="$LOCALAPPDATA/Android/Sdk"
cd android && ./gradlew :app:assembleDebug
A="$ANDROID_HOME/platform-tools/adb.exe"        # adb no está en el PATH
$A install -r app/build/outputs/apk/debug/app-debug.apk
$A shell am force-stop io.github.nachopola16.aldune.debug   # arrancar de cero
$A shell am start -n io.github.nachopola16.aldune.debug/app.aldune.MainActivity
```

## Mirar y tocar

- Captura: `$A exec-out screencap -p > captura.png` y leerla. Pantalla 1080x2424; **si se ve reducida (891x2000), multiplicar por 1,21**
  las coordenadas antes de tocar.
- Toque: `$A shell input tap X Y`, con `sleep 0.7` entre toques (el teclado y la recomposición tardan).
- **Teclado virtual (Gboard): tocar sus teclas por coordenadas**, no `input text`. `input text` inyecta eventos de tecla física y no ejercita
  `onValueChange` como lo hace el teclado real, que es justo lo que se quiere probar (Enter y Retroceso por diferencia de texto).
- Teclado físico: `input keyevent KEYCODE_TAB`, y con modificadores `input keycombination SHIFT_LEFT TAB`, `ALT_LEFT DPAD_UP`,
  `CTRL_LEFT ENTER`.
- Arrastrar tras mantener pulsado (reordenar notas): `$A shell input draganddrop X1 Y1 X2 Y2 1500`. Un `swipe` no vale: empieza a moverse antes de que
  se cumpla la pulsación larga.
- En Git Bash, `adb shell ... /sdcard/...` convierte la ruta: exportar `MSYS_NO_PATHCONV=1`. `uiautomator dump` + `cat` da el árbol de vistas (qué campo tiene el foco).
- `dumpsys input_method | grep mInputShown` dice si el teclado está abierto sin hacer captura.
- El estado persiste entre pruebas (borradores y notas): los toques a coordenadas fijas fallan si la lista ya tiene notas. Mirar una
  captura antes de fiarse de un guion; `force-stop` no borra datos.

## Posiciones de referencia (Pixel 10a, tema oscuro, teclado Gboard abierto)

Original 1080x2424: «+» de la lista (962, 2242); barra de edición a y≈1395 (☐ x=63, ⋯ x=818); Enter de Gboard (992, 2172); Retroceso (992, 2005);
primera línea del editor y≈332. Cambian con el teclado, el idioma y el tamaño de letra: son una pista, no un contrato.

## Sonda del editor (tarea 7)

Resultado el 2026-10-17, Pixel 10a, Android 17, Gboard en español:

| Gesto | Esperado | Resultado |
|---|---|---|
| ☐ de la barra y escribir | `☐ a` | ok |
| Enter de Gboard al final de una tarea | otra casilla debajo | ok |
| Enter de Gboard en una tarea vacía | termina la lista | ok |
| Retroceso de Gboard tras `☐ ` | quita el prefijo entero | ok |
| Tocar la casilla | ☐ ↔ ☒ | ok (ver «Aprendido») |
| ⋯ → Quitar hechas | borra las líneas hechas | ok |
| Tab / Mayús+Tab | sangra / quita sangría | ok |
| Alt+↑ | sube la línea | ok |
| Ctrl+Enter | marca la tarea de la línea | ok |

Teclado de terceros: AnySoftKeyboard 1.13 (F-Droid; `adb install`, `ime enable`/`ime set`; al acabar `ime set` con Gboard y `adb uninstall`) también pasa Enter,
Enter en vacío y Retroceso. No probado aún: un teclado físico real, la tableta, rotar y pantalla dividida.

## Aprendido

- **Un cambio hecho en el mismo evento del toque se pierde.** El campo, al recibir ese toque, emite el texto anterior con el cursor nuevo
  y pisa lo que se haya aplicado antes. Marcar la casilla se pospone un fotograma (`withFrameNanos`). Si un `LaunchedEffect` depende de un
  estado, no se puede poner ese estado a `null` al empezar: la nueva clave cancela el efecto; se limpia al final.
- Abrir el menú ⋯ le quita el foco al campo y oculta el teclado. Es aceptable; el cursor se conserva.
- Un registro temporal (`Log.d` + `adb logcat -d -s TAG`) es la forma más rápida de saber si un gesto llegó; quitarlo antes de commitear.
- **El teclado al arrancar es una carrera.** Compose enfoca el primer elemento enfocable (el buscador) y pide el teclado; ocultarlo después con
  `keyboard.hide()` a veces se pierde. La solución estable es que el buscador no pueda tomar el foco hasta que se toca (`focusProperties`).
  Comprobarlo arrancando 4 veces seguidas (`force-stop` + `am start`) y mirando `mInputShown`.
- **Un teclado de terceros puede mantener una región de composición.** Corregir Enter/Retroceso en `onValueChange` (con `TextFieldValue`) dejaba al teclado
  re-escribiendo la palabra a medias en la línea nueva. Con `TextFieldState` y un `InputTransformation` la corrección va dentro de la propia edición del
  teclado y no hay desajuste. Probar siempre con Gboard **y** un teclado de terceros; Gboard solo no lo habría descubierto.

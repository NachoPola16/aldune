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

## Sonda de la sync (F5)

La app habla con un servidor de sync **de pruebas** (nunca el real) y un «PC» simulado hace de la otra mitad. Todo con datos temporales.

1. **Servidor local con datos temporales**: `ALDUNE_SYNC_TOKEN=probe-token-123 ALDUNE_DATA_DIR=<carpeta temporal> dotnet run --project src/Aldune.SyncServer --urls http://127.0.0.1:5077`.
   La carpeta de datos se pasa con **`ALDUNE_DATA_DIR`** (sin ella el servidor usa `/data`, que en Windows es `C:\data`). Al dispositivo le llega con
   `adb reverse tcp:5077 tcp:5077` (la URL del perfil es `http://127.0.0.1:5077/`); al acabar, `adb reverse --remove tcp:5077`.
2. **El «PC»**: `ProbePcSimulator` (en `android/syncclient/src/test`, se salta si no hay `ALDUNE_PROBE`). Se invoca con
   `./gradlew :syncclient:test --tests app.aldune.syncclient.ProbePcSimulator --rerun-tasks` y las variables `ALDUNE_PROBE` (`code`, `list`, `put`, `protected`,
   `delete`, `unsigned`), `ALDUNE_PROBE_URL`, `ALDUNE_PROBE_TOKEN`, `ALDUNE_PROBE_KEYFILE` (la clave de la sonda; se crea sola), `ALDUNE_PROBE_OUT` (donde escribe el
   resultado) y `ALDUNE_PROBE_ID` / `_TEXT` / `_STATE`. `code` imprime el código de perfil; `list` enseña el almacén **descifrado con la clave del perfil**, que es
   lo que comprueba que lo subido por la app es legible desde fuera; `unsigned` escribe un borrado con la firma de otra clave. Cada llamada tarda unos 20 s (Gradle).
3. **Escribir el código de perfil por adb**: `adb shell input text` pierde caracteres con cadenas largas; se manda en trozos de 30 con `sleep 0.4`. Un código mal
   escrito sale como «dañado», lo cual también prueba ese mensaje.
4. **No usar Atrás en la tableta en modo PC**: cierra la app y lo escrito sin guardar se pierde. Y para escribir en el editor hay que tocarlo antes (el teclado
   solo se abre con el foco).
5. **Un conflicto se provoca** así: el «PC» escribe la nota, y **antes** de que la tableta sincronice (la política espera 5 s tras dejar de editar) se escribe en la
   tableta. La pasada ve los dos lados cambiados desde la base: gana la versión más reciente y la otra queda guardada como conflicto (icono en la fila).
6. **Servidor caído con `adb reverse`**: el túnel acepta la conexión y la corta, así que no sale «conexión rechazada» sino «unexpected end of stream».
   Es un corte de conexión como cualquier otro y se clasifica «Sin conexión».
7. Tirar para sincronizar: `adb shell input swipe 250 450 250 1000 500` sobre la lista. Con la lista vacía exige que el mensaje de «sin notas» sea desplazable.
8. Comprobar lo que queda en el dispositivo: `adb shell run-as io.github.nachopola16.aldune.debug ls no_backup` (el fichero `syncsecrets.bin` existe conectado y no
   existe desconectado) y `adb shell dumpsys jobscheduler | grep aldune` (el trabajo periódico de WorkManager).

## Sonda de la captura y el widget (F6)

Resultados y hallazgos: en el historial de git (`INFORME-ANDROID-F6.md`, retirado del árbol). Técnica:

1. **Mandar intents a mano.** Acción propia: `am start -a app.aldune.action.NEW_NOTE -n io.github.nachopola16.aldune.debug/app.aldune.MainActivity`. Compartir:
   `am start -f 0x10008000 -a android.intent.action.SEND -t text/plain -n .../app.aldune.MainActivity --es android.intent.extra.TEXT '...' --es android.intent.extra.SUBJECT '...'`.
   **El `-f 0x10008000` (NEW_TASK + CLEAR_TASK) es obligatorio al repetir:** tras una reinstalación o una prueba anterior, el sistema ve un intent «igual» al de la raíz de la
   tarea (los extras no cuentan para eso) y solo trae la tarea al frente: «Activity not started, its current task has been brought to the front». La nota no se crea y parece un fallo de la app.
2. **Poner el widget sin el selector del lanzador**: Ajustes → Captura rápida → «Añadir el widget». Para el mosaico, «Añadir el mosaico» (cae en la segunda página del panel: deslizar).
   Redimensionar: pulsación larga sobre el widget (`input swipe x y x y 900`) y `input draganddrop` sobre el tirador del borde. El widget del selector del sistema vive en la
   pantalla de inicio **real** del móvil: quitarlo al acabar.
3. **Fijar una nota como protegida para ver el widget** (las protegidas solo llegan por sync): parar la app, copiar `databases/aldune.db*` con `adb exec-out run-as <pkg> cat ...`
   (la salida binaria sí llega bien así), abrir la copia con `sqlite3` de Python (`PRAGMA wal_checkpoint(TRUNCATE)`; `UPDATE note SET isProtected = 1 WHERE rowid = ...`) y
   **devolverla con `adb push` a `/data/local/tmp` + `run-as cp`**, y borrar antes `-wal` y `-shm`. Pasar el binario por una tubería de Git Bash (`cat f | adb shell run-as ...`) lo estropea.
   Solo con la base de la app de depuración.
4. Mirar la notificación: `cmd statusbar expand-notifications`; el panel de mosaicos: `cmd statusbar expand-settings`; tema: `cmd uimode night no|yes`.
5. **Qué mirar al probar un widget**: la hora a la que se pinta con el proceso vivo y en frío (Glance reutiliza la sesión de composición); con 53 notas, el tope y la fila final; con la
   vista previa activa, que ninguna tira quede recortada; y, con una protegida, que su título no salga en ningún sitio (`uiautomator dump` no ve dentro del widget: mirar la captura).
6. **El teclado de una nota nueva**: arrancar 4 veces seguidas con `force-stop` + `am start` y `dumpsys input_method | grep mInputShown`, como en la sonda del editor.
7. `logcat -d | grep AppWidgetHostView` dice por qué un widget no se infla (una clase no admitida en `RemoteViews`, p. ej. un `<View>`).

## Sonda de los recordatorios (F7)

Resultados: en el historial de git (`INFORME-ANDROID-F7.md`, retirado del árbol). Técnica:

1. **Git Bash estropea las rutas de `adb`**: `export MSYS_NO_PATHCONV=1` o `adb shell cat /sdcard/u.xml` falla. Para leer la pantalla, `uiautomator dump /sdcard/u.xml` y buscar
   `bounds` con `grep`; una función que calcule el centro del rectángulo (`ctr`) evita tocar coordenadas a mano. La tableta y el Pixel tienen pantallas distintas: nunca reusar coordenadas.
2. **Poner un recordatorio a unos minutos**: menú «⋯» → «Elegir fecha y hora» → Aceptar (hoy) → el selector arranca en la próxima hora en punto; tocar la hora y los minutos
   (`content-desc="N minutes"`, solo múltiplos de 5) → Aceptar. Con «Hora exacta» activada la alarma tiene `window=0` en `dumpsys alarm | grep -A3 "REMINDER_FIRE$"`.
3. **Matar sin cancelar alarmas**: `input keyevent KEYCODE_HOME` y `am kill <pkg>` (**nunca `am force-stop`**). Esperar el aviso con un bucle sobre
   `dumpsys notification --noredact | grep -c "pkg=<pkg>.*id="` y anotar `adb shell date`. **Limpiar antes los avisos anteriores** (el bucle sale al primero que ve).
4. **Reposo profundo**: `dumpsys battery unplug` → `dumpsys deviceidle force-idle`; al acabar `dumpsys deviceidle unforce` y `dumpsys battery reset`.
5. **Botones del aviso**: `cmd statusbar expand-notifications`; si hay varios, se agrupan y hay que expandir cada uno (en el Pixel, `input swipe` hacia abajo sobre el aviso). La
   tableta: pulsar el chevron del aviso.
6. **Cambiar la zona**: `settings put global auto_time_zone 0` + `service call alarm 3 s16 Europe/Lisbon`; restaurar con `Europe/Madrid` y `auto_time_zone 1`.
7. **Permiso de alarmas exactas**: el interruptor «Hora exacta» abre `Settings$AlarmsAndRemindersAppActivity`; tocar la fila y volver; comprobar con
   `cmd appops get <pkg> SCHEDULE_EXACT_ALARM`.
8. **Deslizar para archivar no funciona con `adb input swipe`** (ni `touchscreen swipe`): probarlo a mano.
9. En el móvil del usuario hay notas reales: crear una nota propia de prueba y no tocar las existentes; borrarla al acabar.

## Sonda de los aspectos retro

25-10-2026, Pixel 10a con la versión de depuración y notas de prueba. Técnica: elegir el aspecto en Ajustes → Aspecto y mirar el bloque «Colores del aspecto»; para recorrer muchos aspectos con colores extremos sin tocar la pantalla, escribir `shared_prefs/settings.xml` de la app de depuración (`appearance` 0-10 y `aspectColors` como JSON con `&quot;`) con `adb push` + `run-as <pkg> cp`, `am force-stop` (solo la de depuración) y relanzar.

Resultado: los 11 aspectos salen con su nombre; Bash cambia lista, editor y diálogos a la paleta de terminal, con texto monoespaciado, y propone el tema Sereno (Usarlo lo aplica); las muestras, «Cambiar», la paleta Ubuntu y «Restablecer colores» (vuelve a Gruvbox) funcionan; Telecom claro con tinta blanca y Fósforo con fósforo blanco se siguen leyendo (el texto se ajusta solo); XP usa la fuente de siempre. Sin textos ilegibles ni desbordes. La barra de estado se arregló tras la revisión (los iconos siguen la claridad del aspecto) y se comprobó con el sistema en oscuro y XP claro.

**Widget (rc5-rc7, Pixel real, 25-10-2026):** con Bash se veía como el Oscuro de siempre, porque el widget solo pinta fondo, texto y tiras (`Ground`/`Text`/`Surface`, casi iguales en Oscuro y Bash). Se añadió el acento (título «Aldune» y «+», con `ColorFit` contra el fondo) y la fuente monoespaciada en Bash, Telecomunicaciones y Fósforo. Truco: el widget pertenece a la app real, no a la de depuración; para verlo hay que instalar una release firmada, y `adb shell dumpsys appwidget | grep aldune` dice si está puesto. Con fuente monoespaciada los nombres largos se cortan antes con «…».

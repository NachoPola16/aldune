# Registro de diagnóstico del dock

Desde la 1.0.1 Aldune apunta en un registro local lo que le pasa al dock y a las pantallas. Sirve para dos
fallos que no se pueden reproducir a voluntad:

1. **La tira de reposo del dock a veces deja de verse** y vuelve en cuanto se pasa el ratón por donde
   debería estar (visto en un portátil, dock en el borde derecho, sin nada a pantalla completa).
2. **El dock no vuelve a la pantalla principal** al apagarla y encenderla (con el botón del monitor u otra
   forma).

## Dónde está

`%LOCALAPPDATA%\Aldune\logs\dock.log`. Al pasar de 256 KB pasa a `dock.log.old` (solo se guarda uno), así
que nunca ocupa más de medio mega. No contiene nada de las notas: solo estados de ventanas y pantallas, y
de otras ventanas únicamente su clase y el nombre del proceso (nunca su título).

## Qué hacer cuando vuelva a pasar

1. **No pases el ratón por el dock todavía** si puedes: al entrar en la tira se vuelve a subir y a pintar,
   y lo que interesa es el estado de antes. Si ya lo has pasado, no pasa nada: queda apuntado igual.
2. Apunta la hora aproximada.
3. Manda `dock.log` (y `dock.log.old` si existe).

Para el fallo de pantallas: apaga la pantalla principal, espera unos segundos, enciéndela, espera 10 s y
manda el registro, con la forma en que la apagaste (botón, cable, Win+P, reposo de pantalla).

## Qué se apunta

| Categoría | Cuándo | Ejemplo |
|---|---|---|
| `app` | Al arrancar | `arranque, versión 1.0.1` |
| `pantallas` | Cada `DisplaySettingsChanged` y cada vez que se reconstruyen los docks | pantallas vistas con su `StableId`, la elegida y dónde quedan los docks |
| `sistema` | Suspender/reanudar, bloquear/desbloquear, conexión remota | `energía: Resume`, `sesión: SessionUnlock` |
| `dock <borde> <pantalla>` | Cada cambio de estado de ese dock (se comprueba cada 500 ms, solo se escribe si cambia) | ver abajo |

Estados del dock:

- `en reposo, siempre encima: sí, sobre la tira: el dock` — lo normal.
- `siempre encima: NO` — el dock ha perdido `WS_EX_TOPMOST`. Nada en Aldune se lo quita; si aparece, algo
  externo lo hizo.
- `sobre la tira: '<clase>' de <proceso>` — otra ventana está encima de la tira (la que Windows daría al
  hacer clic en su centro).
- `…, pero en pantalla no se ve (color #RRGGBB)` — el dock está encima y en su sitio, pero lo que se ve en
  ese punto no es ni el fondo de la tira ni ninguno de sus guiones. Se comprueba cada 5 s sobre la tira
  real. Justo después de un cambio de pantallas es normal un `#000000` suelto (el monitor aún en negro).
  **En la 1.0.1 esta comprobación estaba mal** (medía 10 px por encima de la tira, en la zona ampliada
  para el ratón): sus líneas de "no se ve" no significan nada.
- `…, ventana en X,Y en vez de X,Y` — la ventana del dock no está donde el dock la calculó.
- `Windows ha movido la ventana a X,Y: vuelve a X,Y` — (1.0.2) se ha detectado y corregido.
- `la pantalla del dock ha cambiado sin aviso: se reconstruyen los docks` — (1.0.2) la ventana se movió
  porque su pantalla cambió; se reconstruyen como tras un cambio de pantallas.
- `se oculta por pantalla completa de '<clase>' de <proceso>` / `vuelve: …` — la ocultación a propósito
  ante juegos y vídeos (Ajustes → ocultar con pantalla completa).
- `el ratón entra en la tira cuando no se veía (…)` — el momento en que "reaparece al pasar el ratón", con
  el estado que tenía justo antes.

## Lo que ya se ha encontrado

- **Windows mueve la ventana del dock al cambiar la disposición de las pantallas** (visto el 2026-09-25:
  al apagar la principal con el botón el dock quedó debajo de Firefox y el abanico se abría en medio de
  la pantalla; reproducido con `DisplaySwitch.exe /internal` y `/extend`, que desplaza la ventana a
  -1440,419 en vez de 0,540). Desde la 1.0.2 el dock comprueba cada 500 ms, en reposo, que su ventana
  está donde la calculó (`DockPlacement`) y la devuelve o reconstruye los docks.
- Puede ser también la causa de la tira que desaparece en el portátil (acoplar, reanudar, cambiar de
  resolución mueven ventanas igual). Si vuelve a pasar con la 1.0.2 y el registro no dice "Windows ha
  movido la ventana", es otra cosa.

- **Pantallas apagadas con su botón que Windows sigue viendo** (2026-09-26): la 1.0.3 pregunta al
  monitor por DDC/CI si está encendido y, si no, lleva el dock a otra pantalla. En el registro:
  `<pantalla> dice estar apagada (DDC/CI)` y, al volver, `encendida`. Si un monitor no contesta, no sale
  nada: para esos está la opción "En la pantalla donde esté el ratón" (`el ratón está en …`).

## Cómo leerlo (y qué arreglo toca en cada caso)

- **`siempre encima: NO`** antes del fallo → otra aplicación le quita el topmost al dock. Arreglo: reafirmar
  `EnsureTopmost` al detectarlo (el sondeo ya corre cada 500 ms), y averiguar qué proceso lo hace por lo
  que haya en `sistema`/`sobre la tira` alrededor de esa hora.
- **`sobre la tira: '<clase>'`** con el dock aún `siempre encima` → otra ventana topmost se pone encima (un
  overlay, una utilidad del fabricante del portátil, el panel de gestos del touchpad…). Arreglo: según la
  clase — subir el dock cuando esa ventana aparezca, o ignorarla si es transparente.
- **`en pantalla no se ve`** con todo lo demás normal → es la hipótesis de STATUS.md: la ventana "layered"
  (`AllowsTransparency=True`) deja de recomponerse. Arreglo: forzar un repintado (tocar `Opacity`, como
  hace la animación al pasar el ratón) cuando se detecte, o enganchado al evento de `sistema` que lo
  precede (por ejemplo `energía: Resume`).
- **`se oculta por pantalla completa`** sin estar a pantalla completa → falso positivo de
  `NativeMethods.IsFullscreenAppCovering`: la clase y el proceso dicen qué ventana hay que excluir. (Con
  este caso el ratón no debería devolver el dock, así que es el menos probable.)

## Código

- `Aldune.Core.DiagnosticLog`: escritura con fecha, rotación y sin excepciones (con tests).
- `Aldune.Interop.DockDiagnostics`: descripción de ventanas y pantallas, lectura de píxel.
- `EdgeDockWindow.RecordDiagnostics`: el estado de la tira, en el mismo temporizador que la detección de
  pantalla completa.
- `App.xaml.cs`: arranque, eventos de energía/sesión/pantallas y elección de pantalla del dock.

Cuando los dos fallos estén resueltos se puede quitar la parte del dock (o dejarla: cuesta una llamada a
`WindowFromPoint` cada 500 ms y una lectura de píxel cada 5 s, solo con el dock en reposo).

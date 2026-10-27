# Registro de diagnóstico del dock (retirado)

**Estado: retirado el 2026-10-27.** Los dos fallos para los que se escribió están resueltos y no se han vuelto a ver:

1. **La tira de reposo del dock a veces dejaba de verse** y volvía en cuanto se pasaba el ratón por donde debía estar.
2. **El dock no volvía a la pantalla principal** al apagarla y encenderla.

## Qué los arregló (sigue en el código)

- **Windows mueve la ventana del dock al cambiar la disposición de las pantallas** (visto el 2026-09-25 y reproducido con `DisplaySwitch.exe /internal` y
  `/extend`). Desde la 1.0.2 `EdgeDockWindow.KeepWindowInPlace` comprueba cada 500 ms, en reposo, que su ventana está donde la calculó (`DockPlacement`) y
  la devuelve a su sitio o reconstruye los docks si su pantalla cambió.
- **Pantallas apagadas con su botón que Windows sigue viendo**: desde la 1.0.3 `DockScreenWatcher` pregunta al monitor por DDC/CI si está encendido y, si no,
  lleva el dock a otra pantalla. Con un monitor que no contesta, queda la opción «En la pantalla donde esté el ratón».
- **Una ventana normal sobre la tira** (Firefox, Code…): `KeepStripOnTop` vuelve a subir el dock sin esperar al ratón. Las de la capa superior (menú Inicio,
  recortes) se respetan.
- **La tira sin pintar** (ventana «layered» que deja de recomponerse): `RepaintStripIfUnpainted` lee cada 5 s el color que se ve en pantalla y, con dos
  lecturas seguidas sin el color esperado, repinta y vuelve a subir el dock.

## Qué se quitó

El registro `logs\dock.log` (estados del dock cada vez que cambiaban, pantallas vistas, energía y sesión, y el cierre por pantalla completa). Quedó
`logs\errores.log`, solo con errores inesperados de las tareas en segundo plano (`AppLog`, antes en el mismo registro). Un `dock.log` o `dock.log.old` que
quede en `%LOCALAPPDATA%\Aldune\logs` se puede borrar a mano.

Si algún fallo del dock volviera: el registro completo y su guía de lectura están en el historial de git (`git log -S RecordDiagnostics` y la versión de este
documento anterior al 2026-10-27).

# Informe — F7 (recordatorios en Android: interfaz y sondas)

Cierra la parte de recordatorios de [`2026-10-18-aldune-android-f7-design.md`](superpowers/specs/2026-10-18-aldune-android-f7-design.md) §4-§5. El núcleo (alarma por nota,
aviso, reconciliar) está en `e5466c2`; la interfaz, en `e243e29`. La parte JVM la hizo la nube ([`INFORME-NUBE-F7.md`](INFORME-NUBE-F7.md): `ReminderPresets`, `ReminderLabel`,
`ReminderRules`). Probado el 24-10-2026 en la **tableta Lenovo TB350FU** y en el **Pixel 10a**, con «No molestar» en el Pixel.

## Estado

| Pieza | Estado |
|---|---|
| Hoja «Recordatorio» en el menú «⋯» de la nota (atajos +1 h / esta noche 20:00 / mañana 9:00, fecha y hora, quitar, texto «solo avisa en este dispositivo») | hecha y probada |
| Permiso de notificaciones (se pide al poner el primer recordatorio) | hecho y probado |
| Campana y etiqueta en la fila de la lista («Hoy 20:00», «Mañana 9:00», fecha, «Vencido» en rojo) | hecha y probada |
| Ajustes → Recordatorios → «Hora exacta» (pantalla del sistema si falta el permiso; se activa al volver) | hecho y probado |
| Aviso con Hecho · +1 h · Mañana 9:00; tocarlo abre la nota | hecho y probado |
| Textos en los cinco idiomas | 11 + 3 cadenas nuevas; `gen_strings.py --check`: 199 cadenas |

Archivos nuevos o tocados: `ui/ReminderSection.kt` (hoja, selector de fecha y hora, `reminderText`), `NoteOptionsDialog`, `NoteListPane`, `SettingsScreen`,
`NotesViewModel.reconcileReminders`.

## Verificación

- `assembleDebug` y `lintDebug` sin errores; `gen_strings.py --check` limpio. No se tocaron módulos JVM ni Room, y no se volvió a ejecutar la suite JVM en esta pasada.
- La etiqueta y la hora usan el idioma de la app (`LocalConfiguration`, ya envuelto por `Languages.wrap`) y el ajuste de 24 h del dispositivo.

### Sondas (alarmas matadas con `am kill`; **nunca** `am force-stop`, que las cancela)

| # | Sonda | Resultado |
|---|---|---|
| 1 | Recordatorio inexacto con la app matada | avisó al final de su ventana (00:29:04 para las 00:25; ventana de 4 min) |
| 2 | Recordatorio exacto (tras activar «Hora exacta») | 00:35:02 para las 00:35:00; en el Pixel, 08:55:02 para las 08:55:00, con la pantalla apagada |
| 3 | Botón **+1 h** | reprograma a +1 h y cierra el aviso (tableta y Pixel) |
| 4 | Botón **Mañana 9:00** | reprograma al día siguiente a las 09:00 |
| 5 | Botón **Hecho** | cierra el aviso y no deja alarma pendiente |
| 6 | Tocar el aviso | abre la nota y quita el aviso |
| 7 | Reposo profundo (`dumpsys battery unplug` + `deviceidle force-idle`), exacta | tableta 00:50:00 y Pixel 09:00:04, a su hora |
| 8 | Reposo profundo, inexacta | 00:58:09 para las 00:55 (final de la ventana de ~3 min) |
| 9 | Cambio de zona (`service call alarm 3 s16 Europe/Lisbon`) | la alarma conserva el instante; la etiqueta se adapta («Hoy 00:10») |
| 10 | Reinicio con una alarma pendiente | alarma rearmada al arrancar y avisó dentro de su ventana (01:11:42 para la 01:10) |
| 11 | Apagar «Hora exacta» con alarmas puestas | pasan a ventana inexacta sin tocar nada más |

## Hallazgos y decisiones

1. **El retraso medido** es el esperado por el sistema: inexacta = hasta el final de su ventana (≈ 75 % del tiempo que falta, hasta 4-8 min en las pruebas); exacta = 0-4 s,
   también en reposo profundo. Por eso «Hora exacta» existe como ajuste y está apagado por defecto.
2. El selector de hora arranca en la **próxima hora en punto**, y el botón Aceptar queda desactivado si el instante elegido ya pasó.
3. Mientras hay varios avisos de la misma app, Android los agrupa y los botones solo se ven al expandir cada uno; no es un fallo de la app.
4. En una nota nueva sin texto el menú «⋯» no se abre (no tiene id todavía), así que no se puede poner recordatorio ahí: coincide con lo decidido.

## Sin probar

- **Archivar con recordatorio** (debe borrar la alarma y no avisar): el gesto de deslizar de la lista no se dispara con `adb input swipe`; se probará a mano.
- **Vencido con el dispositivo apagado** (una notificación por nota al arrancar).
- **Nota protegida** (el aviso no muestra su texto).
- Pendientes de F7 de otras partes: editor protegido con teclado de terceros, conflicto con versión protegida real, rotar con la sesión abierta, protegida creada en
  Android y abierta en Windows; y la decisión menor de evitar la segunda derivación al proteger.

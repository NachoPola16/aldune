# Decisiones del 26-10-2026

Tomadas con el usuario tras la rc8 de Android (publicada el 25-10) y la 1.7.11 de Windows. Nada de esto está
implementado todavía; este documento es el orden de trabajo. Si algo se descarta aquí, no hay que volver a
investigarlo salvo que cambie la razón.

## Ajustes de Windows que Android no tiene

| Ajuste | Decisión | Razón |
|---|---|---|
| 5. Ocultar tareas hechas (`AutoHideCompletedTasks`) | **Hacer**, como *ocultar*, no borrar: las tareas siguen en la nota y un interruptor «mostrar hechas» las enseña | Así deja de ser destructivo y no hace falta un diseño largo |
| 7. Modo simplificado (`SimplifiedMode`) | **Descartado por ahora** | Nadie lo ha pedido en el móvil; habría que revisar primero qué hace en Windows |
| 8. Sonidos por evento | **Descartado** | En Android el sonido lo gestiona el canal de notificación del sistema |
| 9. Vista previa en la lista | **Descartado** | El móvil ya la enseña siempre |
| 11. Temas propios (`CustomThemes`) | **Aplazado** | Bastan los temas de serie y el color libre; se retoma si alguien lo pide |

## Logo

Se queda el de la rc8 (más grande, sin borde fino). Si en el móvil sigue viéndose pixelado, se hace el cambio general
(opción B/C desde `src/Aldune/Assets/aldune-logo.svg`) antes de las capturas.

## Windows 1.7.12 (release pequeña)

- Copiar la ruta y abrir la nota original también desde el dock, en las notas abiertas desde un directorio
  (hoy solo está en el menú «⋯» de la nota).
- Importar un `.md` como nota normal (hoy solo se abre, se vincula o se arrastra).
- Publicar es una acción hacia fuera: se confirma con el usuario antes. Después, actualizar el servidor de sync
  (ver `RELEASING.md`).

## Markdown

- Importar `.md` como nota normal: **hacer** (va en la 1.7.12).
- Subrayado y tipografía de lo seleccionado: **descartado** (exige texto enriquecido; ver `ROADMAP.md:424-439`).
- Pegar fotos: sigue descartado.

## Emparejamiento por QR

**No se hace por ahora.** El código `aldune-profile-v2:` lleva clave, scope, noteIds y URL; con muchas notas un solo QR
sale demasiado denso y habría que dividirlo en dos. El emparejamiento actual (pegar el código y teclear el token
aparte) funciona. Se retoma solo si la validación con 3-5 personas demuestra que es un obstáculo. En cualquier caso,
**el token nunca va dentro del QR**.

## Negocio

- Sigue **GPL-3.0**.
- No se vende todavía: primero validar con 3-5 personas reales.
- **Se retoman IzzyOnDroid y F-Droid** (compatibles con GPL, visibilidad gratis para esa validación). Sustituye la
  pausa del 24-10.
- La sync alojada de pago queda anotada (`ROADMAP.md`, ideas de negocio) sin actuar hasta tener esas personas.

## Sin cambios

- Capturas de la ficha: después de cerrar lo del logo.
- Las 3 sondas menores de F7: se dan por hechas.
- Dock que a veces no se oculta: sigue como estaba (repintado a los 10 s y `KeepStripOnTop`).
- MSIX / Microsoft Store: sigue aplazado.

## Orden de trabajo

1. Ocultar tareas hechas en Android (decisión 5), con test primero en `android/domain`.
2. Windows 1.7.12: dock + importar `.md`, con tests en Core.
3. Confirmar logo en el móvil → capturas → IzzyOnDroid/F-Droid.

## Hecho el 26-10 en Android (commits locales, sin push)

- Ocultar tareas hechas (ajuste + botón «Ver N tareas hechas» en el editor; solo visual, el texto no cambia).
- Margen inferior de la lista para que el «+» no tape la última nota.
- Deshacer y rehacer en la barra del editor (el historial se limpia al cargar la nota, para que el primer «deshacer» no la vacíe).
- Contador «hechas/total» en la cabecera del editor, con descripción para lectores de pantalla.
- Vibración corta al marcar o desmarcar una casilla.

## Probar con el móvil conectado (nada de esto está visto en pantalla)

1. Ocultar tareas hechas: que desaparezcan pasado el plazo, tocar casillas con líneas ocultas encima, escribir junto a una línea oculta, el botón de mostrarlas.
2. Que el «+» no tape la última nota y que el margen no se vea raro con pocas notas.
3. Deshacer/rehacer con el teclado de Samsung/Gboard y con uno de terceros; que tras abrir una nota «deshacer» esté desactivado.
4. Contador y vibración: que no molesten; TalkBack leyendo el contador.
5. Con la misma pasada, mirar accesibilidad (tamaño de letra grande, TalkBack, zonas de toque de 48 dp) y qué se siente copiado del PC.

## Ideas para mirar (sin decidir)

- Móvil: sombra/elevación en la nota arrastrada; buscador que se esconde al bajar; acciones en lote (seleccionar varias).
- Del móvil al PC: contador «hechas/total» en la nota; «ver tareas hechas» como ocultar sin borrar en Windows (hoy borra la línea).

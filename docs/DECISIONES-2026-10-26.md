# Decisiones del 26-10-2026

Tomadas con el usuario tras la rc8 de Android (publicada el 25-10) y la 1.7.11 de Windows. Nada de esto está
implementado todavía; este documento es el orden de trabajo. Si algo se descarta aquí, no hay que volver a
investigarlo salvo que cambie la razón.

## Ajustes de Windows que Android no tiene

| Ajuste | Decisión | Razón |
|---|---|---|
| 5. Ocultar tareas hechas (`AutoHideCompletedTasks`) | **Hacer**, como *ocultar*, no borrar: las tareas siguen en la nota y un interruptor «mostrar hechas» las enseña | Así deja de ser destructivo y no hace falta un diseño largo |
| 7. Modo simplificado (`SimplifiedMode`) | **Hecho el 27-10** (ver `DECISIONES-2026-10-27.md`) | Se pidió después de revisar qué hace en Windows: es un interruptor de la ventana de Ajustes, no otra app |
| 8. Sonidos por evento | **Descartado** | En Android el sonido lo gestiona el canal de notificación del sistema |
| 9. Vista previa en la lista | **Descartado** | El móvil ya la enseña siempre |
| 11. Temas propios (`CustomThemes`) | **Aplazado** | Bastan los temas de serie y el color libre; se retoma si alguien lo pide |

## Logo

**Cerrado el 26-10:** el símbolo definitivo es **la «A» de tres notas** (candidato G), un único dibujo del que salen todos los
formatos (`scripts/branding/generar-iconos.py`). Sustituye a las tres pestañas con barra, que en una máscara circular parecían
un rectángulo metido en un círculo. Se guarda el candidato C (notas apiladas) como alternativa. Detalle, colores, reglas de
uso y lo descartado: [`BRANDING.md`](BRANDING.md).

## Widgets y burbujas (Android, 26-10)

- **Más widgets en el selector:** además del mazo 4×2, ahora hay el mazo **2×2** (compacto), el mazo **4×4** (grande) y un botón
  **1×1** de «Nota nueva» (el símbolo; un toque abre el editor). Los tres del mazo reutilizan `DockWidget`, que ya se adapta al
  tamaño y se repinta junto (Glance guarda el conjunto de receptores de cada clase). Todos se pueden redimensionar salvo el 1×1.
  **Descartado el 4×1:** entre cabecera y relleno no cabe una tira útil.
- **Burbuja:** **no hay nada que construir.** Android 17 permite convertir cualquier app en burbuja con una pulsación larga en su
  icono (blog oficial de Android 17); en el Pixel funciona con la rc9 tal cual: ventana multiventana, la actividad es
  redimensionable por defecto (`targetSdk` 36) y `adjustResize` ya está. Una burbuja propia desde la notificación de captura
  (`BubbleMetadata`) se descarta: el sistema ya cubre el caso y nos obligaría a mantener otra actividad incrustada.
  Sin probar todavía: la app en una ventana de burbuja muy pequeña (tableta o plegable).

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

**Revisado el 27-10:** se hace; ver `superpowers/specs/2026-10-27-qr-emparejamiento-design.md` (cabe en un solo QR y se descarta dividirlo). Texto original:

**No se hace por ahora.** El código `aldune-profile-v2:` lleva clave, scope, noteIds y URL; con muchas notas un solo QR
sale demasiado denso y habría que dividirlo en dos. El emparejamiento actual (pegar el código y teclear el token
aparte) funciona. Se retoma solo si la validación con 3-5 personas demuestra que es un obstáculo. En cualquier caso,
**el token nunca va dentro del QR**.

## Negocio

- Sigue **GPL-3.0**.
- No se vende todavía: primero validar con 3-5 personas reales.
- **Se retoman IzzyOnDroid y F-Droid** (compatibles con GPL, visibilidad gratis para esa validación). Sustituye la
  pausa del 24-10.

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

## Auditoría de seguridad de Windows y servidor (26-10) y 1.7.13

Pasada de solo lectura con el agente `seguridad`: nada crítico ni alto. Corregido en la 1.7.13:

- **WebDAV (media):** un `href` del listado que apuntase a otro host recibía el usuario y la contraseña. Ahora solo se usa su ruta contra el servidor configurado y lo que caiga en otro host se descarta (`WebDavSyncTransport.ResolveHref`, con tests). Android ya lo hacía bien.
- **Contraseña de nota protegida (media-baja):** mínimo de 4 a 8 caracteres para las nuevas; desbloquear las existentes sigue sin mínimo.
- **Servidor de sync (baja-media):** rechaza con 400 lo fechado más de un día en el futuro (antes bloqueaba la nota para siempre).
- **Archivos vinculados:** lectura con tope (MaxBytes + 1) en vez de cargar el archivo entero, y «Abrir en su editor» solo con extensiones de texto.
- **Workflows:** acciones fijadas por SHA de commit (con el tag en comentario) y `no-new-privileges` en los tres compose del servidor.

Aplazado a propósito (anotado en PENDIENTES):

- Contenedor del servidor como usuario `app` en vez de root: hace falta migrar los permisos del volumen `/data` ya creado; no se toca sin probarlo en el CT 103.
- Puerto publicado en `0.0.0.0` por defecto: el despliegue real depende de ello (NPM en otro equipo); ya está documentado y la app avisa. No se cambia el valor por defecto.
- Sin límite de intentos fallidos de token, `GET /objects` lee todos los objetos en cada llamada, sin tope de tamaño en las respuestas del servidor al cliente, sin cuota de objetos, `release.yml` con `contents: write` en todo el job, sin lockfile de NuGet.

## Resultado de la prueba en el Pixel 10a (26-10, app debug, ya desinstalada)

Comprobado en pantalla: contador «hechas/total» en la cabecera (3/5 → 4/5 al marcar), ocultar tareas hechas (con 2 de 3 caducadas se esconden y aparece «Show 2 done tasks»; el botón las enseña y vuelve a ocultarlas), tocar una casilla con líneas ocultas encima marca la tarea correcta, deshacer y rehacer (desactivados al abrir la nota, deshacer devuelve la casilla, rehacer se activa), margen bajo la lista (con 13 notas la última queda por encima del «+»), letra del sistema al 160 % (sin cortes; la barra del editor sigue desplazable).

Sin comprobar: la vibración al marcar (no se ve en capturas), TalkBack, teclados de terceros, y el plazo real de 1 h/1 día/1 semana (se probó adelantando los tiempos guardados).

Pendiente de mirar: el botón «Ver N tareas hechas» queda a la izquierda y poco destacado; la barra del editor se corta por la derecha («⋯» a medias) aunque se desplaza, quizá un degradado o menos botones visibles.

# Fanote — Hoja de ruta y decisiones pendientes

Documento hermano de `STATUS.md`. Aquel cuenta **lo que está hecho y por qué**; este guarda **lo que
no se ha hecho, lo que se descartó y con qué razón**, para que no haya que volver a investigarlo ni
volver a discutirlo desde cero.

Sesión de origen: 2026-09-06.

---

## 1. Investigación de mercado (2026-09-06)

Se buscó si el concepto de Fanote ya existe y si hay hueco. Resumen de lo encontrado, con fuentes.

### El hallazgo principal

**El concepto de Fanote no existe en Windows.** Las dos apps que inspiran el diseño son ambas solo
macOS, y no hay equivalente nativo en Windows:

| App | Plataforma | Modelo | Precio |
|---|---|---|---|
| [Hold My Notes](https://holdmynotes.app/) | **solo macOS** | Pestañas ancladas al borde que se despliegan en abanico. Cifrado local, sin cuenta, sync opcional por iCloud | 6,99 $ oferta (14,99 $ normal), pago único, 3 Macs |
| [noty](https://github.com/aimen08/noty) | **solo macOS** | El mismo concepto, open source. AES-GCM, solo local, sin cuenta | Gratis (381 ★) |
| [SideNotes](https://www.apptorium.com/sidenotes) | solo macOS/iOS | Panel lateral, no abanico | 19,99 $ pago único |

Los jugadores de **Windows** (Notezilla, Simple Sticky Notes, 7 Sticky Notes, Stickies de Zhorn,
Microsoft Sticky Notes) son **todos** del modelo clásico "notas sueltas por el escritorio". Ninguno
usa el modelo de mazo anclado al canto.

**Conclusión**: si Fanote sale al público, no entra en un mercado saturado — ocupa un hueco vacío.

### Qué critican de la competencia

- **Notezilla** (29,95 $ pago único o 19,95 $/año): lo más alabado son los **recordatorios** y la
  sincronización. Lo más criticado: *"colocar las notas exactamente donde quieres es molesto"* — que
  es justo lo que Fanote ya resuelve con la posición recordada por pantalla — y el precio.
- **Microsoft Sticky Notes**: sin etiquetas, sin listas separadas, sin ordenar arrastrando, retrasos
  de sync con OneDrive, cuatro años sin actualizaciones importantes.
- Existe una categoría entera de apps de notas **solo-local + cifradas + sin cuenta** con demanda
  real (Secure Local Notes, LocalNotes, y adyacentes como Obsidian/Joplin/Logseq). Fanote ya cumple
  eso de fábrica y conviene decirlo explícitamente en cualquier página de presentación.

### Precio, si algún día se vende

El mercado se ha movido hacia el **pago único** para utilidades pequeñas (del 6,4 % al 10,3 % de
cuota entre 2023 y 2025), y encaja con la promesa de "sin cuenta, sin nube": pedir un login para
cobrar una suscripción la contradice. Rango de referencia: **7-15 $ pago único**.

Fuentes: [Hold My Notes](https://holdmynotes.app/) ·
[noty](https://github.com/aimen08/noty) ·
[BetterStickies — comparativa 2026](https://betterstickies.com/blog/best-sticky-notes-apps-2026) ·
[Notezilla en Capterra](https://www.capterra.com/p/147902/Notezilla/reviews/) ·
[Quejas de Microsoft Sticky Notes](https://techcommunity.microsoft.com/discussions/windowsinsiderprogram/sticky-notes-app-missing-features-adding-tags-creating-separate-notes-list-sorti/774946) ·
[Pago único vs suscripción](https://www.strayspark.studio/blog/one-time-purchase-vs-subscriptions-indie-studios)

---

## 2. Pendiente y decidido: se hará

### ~~Volver sobre el título de la nota~~ — RESUELTO (opción E)

Se maquetaron y renderizaron las cuatro alternativas al lado (script en el scratchpad de la sesión) y
al hacerlo apareció el argumento que faltaba: **el texto de la nota se guarda cifrado en un solo
bloque**. Un título guardado aparte tendría que cifrarse por su cuenta (blob, nonce y tag propios) o
ir en claro, filtrando justo lo más descriptivo de cada nota. Y meterlo dentro del mismo bloque
cifrado es, literalmente, "la primera línea".

De ahí salió una quinta opción que no estaba sobre la mesa y es la que se implementó: **la cabecera
muestra y edita la primera línea, y el cuerpo empieza en la segunda** (`Fanote.Core.NoteText` parte y
recompone). Sin duplicado, sin cabecera vacía, con título editable, y sin cambiar absolutamente nada
de cómo se guarda ni se cifra.

Lo que sigue anotado por si se retoma: **no** se implementó unir con Retroceso al principio del
cuerpo (fusionar la línea con el título). Hacerlo bien exige lógica de fusión de líneas; hacerlo a
medias se siente roto. Se puede subir con la flecha Arriba, que es lo que hace cualquier control.

### Nota histórica: el descarte anterior del título (por si vuelve el debate)

El usuario dijo explícitamente que **la solución actual no le convence** (se quitó el texto de la
cabecera; el título es la primera línea del cuerpo). Queda pendiente darle una vuelta **con
alternativas maquetadas y renderizadas**, no solo argumentadas — la técnica de rasterizar en
aislamiento con `RenderTargetBitmap` ya sirvió para decidir los glifos de las casillas y aquí aplica
igual.

Alternativas a poner sobre la mesa, con su render al lado:
1. Como está ahora (cabecera sin texto).
2. Cabecera con el título, como antes (aceptando la duplicación).
3. Título editable de verdad, separado del cuerpo — ahora es barato, porque **no hay datos que
   preservar**; el argumento de la migración que se dio al principio no era válido y el usuario lo
   señaló con razón. Ver el descarte más abajo para las razones que sí valen y la que sí lo
   justificaría.
4. La cabecera muestra la primera línea **solo cuando el cuerpo está scrolleado** y esa línea ya no
   se ve. Resuelve la duplicación y el único caso que se pierde hoy, a cambio de una regla de
   comportamiento no obvia.

### ~~Reordenar notas arrastrando en el mazo~~ — HECHO

Ver `STATUS.md`. Se mantiene aquí la decisión de diseño por si hay que retomarla: tabla `NoteOrder`
aparte con `Position REAL`, para mover una nota escribiendo una sola fila en vez de renumerar.

### Reordenar notas arrastrando en el mazo (diseño original, ya implementado)

Hoy el orden es `CreatedAt` y no se puede cambiar. En una metáfora de mazo de fichas, no poder
decidir cuál está arriba es un hueco real; noty lo tiene.

**Decisión de diseño ya tomada**: la posición **no** se guarda añadiendo una columna a la tabla
`Note` — esa tabla tiene datos reales del usuario y esta app no tiene sistema de migraciones. Se hace
con una tabla aparte, `NoteOrder (NoteId TEXT PRIMARY KEY, Position REAL NOT NULL)`, igual que se
hizo con `NotePlacement`. `Position` en `REAL` y no `INTEGER` **a propósito**: permite insertar entre
dos notas (media aritmética de sus posiciones) sin renumerar toda la lista en cada arrastre.

### ~~Dock a la izquierda no se ve bien~~ — RESUELTO (ver `STATUS.md`)

Causa real, encontrada con capturas del usuario (2026-09-09): `RestStrip`, las pestañas del abanico
y el pie de botones estaban alineados "a la derecha" **en el XAML mismo**, sin condicionar por
borde — valor correcto solo para `EdgePosition.Right`, que fue el único con el que se diseñó esto
originalmente. La ventana del dock (`EdgeGeometry.WindowRect`) ya se posicionaba bien pegada al
borde físico de la pantalla con `DockEdge = Left`, pero ese contenido interno seguía alineado contra
el lado equivocado (el interior del dock, no el exterior), dejando el hueco que se veía en las
capturas. Arreglado espejando esas tres alineaciones por código cuando el borde es Izquierda — ver
`STATUS.md` para el detalle técnico. `EdgeDockWindow.PositionNoteWindow` (dónde se abre la nota en
sí) nunca tuvo el bug: ya distinguía los bordes correctamente.

**Segunda ronda, misma causa de fondo**: la propia forma de cada pestaña (`CornerRadius`/
`BorderThickness`/degradado de brillo en `NoteTabButtonStyle`) también estaba pensada solo para el
borde derecho — redondeada por la izquierda, a ras por la derecha. Espejada igual, verificado con un
render aislado antes de tocar el XAML real.

**Tercera ronda, la causa raíz de verdad**: seguía habiendo hueco (tira, abanico y botones del pie
por igual) porque el `Grid` que envuelve *todo* el contenido del dock tiene su propio
`Margin="18,18,0,18"` asimétrico en el XAML — sin margen a la derecha, correcto solo para
`EdgePosition.Right`. Ningún ajuste dentro de ese `Grid` podía compensar que el propio contenedor ya
estuviera encogido por el lado equivocado. Espejado también. Detalle completo en `STATUS.md`.

### Bordes Arriba y Abajo para el dock — sigue pendiente, arquitectónico

El usuario pidió explícitamente diseñar también los bordes **Arriba** y **Abajo** — la spec original
de las pestañas en abanico los excluyó a propósito ("exigiría deslizar en vertical, que queda fuera
de esta ronda", ver `EdgeDockWindow.PopulateEdges`, que hoy solo ofrece Izquierda/Derecha en
Ajustes). **Hallazgo al investigar el bug de arriba**: `Fanote.Core.EdgeGeometry` (la geometría pura,
con tests) **ya contempla los cuatro bordes** — `WindowRect`/`RestingVisibleRect` tienen casos
`Top`/`Bottom` completos, no solo Izquierda/Derecha. Lo que falta es todo lo demás: exponer la
opción en Ajustes, la tira de reposo dibujada horizontal en vez de vertical (el `RestStrip`/`RestList`
del XAML asumen columna), rotar o quitar el giro de las etiquetas de pestaña, y
`EdgeDockWindow.PositionNoteWindow` (hoy solo distingue Izquierda de "lo demás") para las cuatro
direcciones. Menos trabajo del que parecía en un principio gracias a la geometría ya hecha, pero
sigue siendo su propia sesión de diseño — empezar por ahí la próxima vez.

### Plantillas de disposición de notas en el escritorio — pendiente, sin diseñar

Idea del usuario (2026-09-09): en vez de arrastrar cada nota a mano para dejarlas ordenadas,
ofrecer plantillas de disposición automática (rejilla, cascada uniforme, columnas...) que las
coloquen todas de golpe. Conecta directamente con "Abrir todas las notas" (ver `STATUS.md`, misma
sesión): hoy esa función solo cascada con un desplazamiento diagonal fijo
(`EdgeDockWindow.NoteWindowCascadeStep`/`NoteWindowMaxCascadeSteps`), que con varias notas abiertas a
la vez deja una pila desordenada en vez de algo "ordenado".

**La colocación libre actual (arrastrar cada nota donde se quiera) tiene que seguir disponible** —
las plantillas son una opción más, no un reemplazo. Encaja con `AppSettings.RememberNotePositions`
tal como existe hoy: seguirá siendo el modo por defecto, y una plantilla es algo que el usuario pide
explícitamente cuando quiere ordenar de golpe, no algo que se imponga.

Preguntas sin responder para cuando le toque su sesión de diseño:

- ¿Qué plantillas ofrecer de entrada? Rejilla y cascada uniforme parecen las dos obvias; ¿alguna más?
- ¿Se aplica solo al usar "Abrir todas", o también como acción aparte sobre notas que ya están
  abiertas (reordenarlas sin cerrarlas)?
- Interacción con `AppSettings.RememberNotePositions`: aplicar una plantilla sobrescribe la posición
  guardada de cada nota. ¿Se pierde ese recuerdo sin más, se pregunta antes, o se puede deshacer?
- En multimonitor, ¿la plantilla se aplica por pantalla (cada dock coloca solo las suyas) o hay que
  pensar en el conjunto de todos los monitores a la vez?

### Color de nota libre, además de la paleta — pendiente, sin diseñar

Idea del usuario (2026-09-11): además de los 6 colores de `NoteColorPalette`, poder elegir un color
libre para una nota concreta. **La paleta actual se queda como está por defecto** — mismo patrón que
"plantillas de disposición" y "sincronización" más arriba: la opción estructurada de fábrica sigue
siendo el camino normal, y la libertad es algo que se pide explícitamente, no un reemplazo. Motivo
para no tocar el valor por defecto: la paleta está pensada a propósito (seis colores derivados en
OKLCH con la misma claridad exacta, para que "el color sea identidad, no jerarquía" y ninguna nota
pese visualmente más que otra) — un color elegido libremente puede romper ese equilibrio a propósito,
que es justo lo que se busca al elegirlo.

**Ya existe la mitad de la infraestructura**: `NoteColorPalette.RimFor`/`LabelFor` ya calculan un
borde y un color de etiqueta razonables para cualquier hexadecimal fuera de la paleta (oscureciendo
proporcionalmente) — hoy ese camino solo se usa para notas heredadas con colores antiguos, pero
serviría igual para un color elegido a mano. Falta el propio selector de color (un `ColorDialog` de
Windows, o algo propio a juego con el resto de la app) y decidir dónde vive: ¿un color más en la fila
de pastillas del menú "⋯" que abre un selector, o una entrada aparte? Su propia sesión de diseño
cuando le toque — no es grande, pero antes se descartó explícitamente un selector de color en la
creación de la nota ("complejidad innecesaria para el beneficio", ver más abajo), así que conviene no
repetir ese argumento sin pensarlo primero.

### ~~Internacionalizar a inglés~~ — HECHO (ver `STATUS.md`)

No fue solo traducir: el usuario usa Fanote en español a diario, así que se añadió un selector
Español/Inglés en Ajustes (`AppSettings.Language`) en vez de sustituir sin más. Textos en
`Fanote.Resources.Strings` (diccionario a mano, no `.resx` — motivo en `STATUS.md`). El cambio de
idioma pide reiniciar la app para verse en todas las ventanas.

**Hueco que queda a propósito**: `HotkeyBinding.DisplayName` (Core) no traduce "Espacio"/"Supr"/"sin
asignar" — ver el porqué en `STATUS.md`. Si se retoma, hay que convertir esa propiedad en un método
parametrizado y tocar `HotkeyBindingTests`, que fija esos tres textos literalmente.

### ~~Exportar a Markdown / texto plano~~ — HECHO (ver `STATUS.md`)

Lo tienen las dos rivales de macOS. Además de utilidad es una **función de confianza** ("no te
secuestro tus datos"), que pesa más si algún día se cobra. Al exportar, las casillas `☐`/`☒` se
traducen a la sintaxis de tareas de Markdown (`- [ ]` / `- [x]`), tal como decía este punto — se
implementó solo Markdown, no texto plano aparte (ver el brainstorming en `STATUS.md` para el porqué).
Una nota a la vez desde `NoteWindow`, o en bloque desde `NotesManagerWindow`.

### Recordatorios con notificación de Windows

**El mayor hueco funcional frente a la competencia de Windows** y lo más alabado de Notezilla. Fanote
no tiene nada. Convierte "notas bonitas" en "notas que te avisan". Necesita sesión de diseño propia:
programación, persistencia, integración con las notificaciones del sistema, posponer.

### Sincronización entre dispositivos — decidido: se hará, las dos vías

Antes estaba aparcado como "descartado por ahora, no tocar hasta que alguien lo pida de verdad" —
el usuario lo pidió (2026-09-09), así que pasa aquí. Todavía sin spec ni plan:
es arquitectónico y le toca su propia sesión de diseño completa cuando se aborde. Lo que ya se decidió
en el brainstorming de esta sesión, para no volver a discutirlo desde cero:

- **Las dos vías, no una sola** — decisión explícita del usuario tras ver el trade-off:
  1. **Carpeta elegida por el usuario** (OneDrive, Google Drive, Dropbox, Syncthing — cualquiera que
     ya sincronice una carpeta normal del disco). Fanote nunca habla con ninguna nube: guarda un
     fichero cifrado por nota dentro de esa carpeta y reconstruye. Mantiene "sin cuenta, sin
     servidor", funciona con cualquier proveedor (incluido ninguno, con Syncthing), y es la vía que
     de forma natural sirve también a un futuro cliente en otro sistema operativo (Android/iOS,
     mencionados por el usuario como posibles pero sin decidir) — ese cliente futuro sincronizaría
     leyendo la misma carpeta a través de su propia app de Drive/OneDrive, sin que Fanote tenga que
     hablar con la API de nadie.
  2. **Fanote habla directamente con la API de Google Drive** (OAuth, sin cliente de escritorio de
     por medio). Pedida explícitamente a pesar del coste, que quede anotado para cuando se diseñe:
     hace falta el flujo de login de Google, guardar y renovar un token (delicado, misma familia de
     problema que la clave de cifrado — ver `DatabaseKeyProvider` en `STATUS.md`), registrar la app
     en Google Cloud y, si Fanote se publica algún día, pasar su proceso de verificación de apps
     OAuth (si no, sale un aviso de "app no verificada"). Y contradice, para quien la use, el propio
     diferenciador que la investigación de mercado de más arriba señaló ("sin cuenta, nunca") — por
     eso tiene que ser una vía **opcional**, nunca la única, con la vía 1 siempre disponible por
     defecto.
- **Aviso técnico que sigue vigente para la vía 1** (ya estaba anotado antes de que esto se decidiera):
  *no* sincronizar el fichero SQLite directamente — las carpetas de sync corrompen bases de datos
  abiertas por dos máquinas a la vez. Un fichero cifrado por nota, reconstruible, es la única forma
  segura.
- **Sin decidir todavía, para la sesión de diseño**: qué pasa si la misma nota se edita en dos
  dispositivos antes de sincronizar (probablemente "gana la más reciente" por `UpdatedAt`, dado que
  esto es una herramienta personal de una persona, no colaborativa — pero no se ha confirmado con el
  usuario); formato exacto del fichero por nota; cómo detectar cambios remotos sin un cliente que
  avise (¿vigilar la carpeta con `FileSystemWatcher`, sondear al arrancar, las dos?); qué pasa si dos
  dispositivos crean una nota nueva "al mismo tiempo" (con `Guid` como Id, no debería colisionar,
  pero conviene confirmarlo explícitamente en la spec).

---

## 3. Descartado por ahora, con su razón

Que no se vuelva a proponer sin leer esto primero.

### Notas por escritorio virtual de Windows — descartado

Idea atractiva (sería algo propio de Windows que ninguna app de Mac puede copiar), pero la API de
escritorios virtuales de Windows (`IVirtualDesktopManager` y sus interfaces COM internas) **no está
documentada y cambia entre builds de Windows**: el código se rompería solo en cada actualización
grande del sistema. No merece la pena mantenerlo.

### Selector de tipografía — descartado; si acaso, de tamaño

Decidido con un render comparativo de 8 fuentes de Windows. El cuerpo se queda en la fuente de
sistema y el título pasa a Ink Free (ver `STATUS.md`). Un **selector de familia** se descarta por dos
motivos, el segundo descubierto al renderizar:

1. Según la búsqueda de mercado, en apps de notas de Windows lo que la gente pide es **tamaño de
   letra** (legibilidad, accesibilidad), no elegir familia. Notezilla lo vende por eso.
2. Las fuentes manuscritas **no tienen los glifos `☐`/`☒`**. Windows los sustituye por otra fuente y
   quedan desalineados con el texto — exactamente el problema que se arregló eligiendo `☒` por sus
   métricas. Dejar elegir familia libremente lo reintroduce, y encima de forma intermitente según la
   fuente que elija cada uno.

Si algún día se añade algo, que sea **tamaño de texto** (pequeño / normal / grande) aplicado a todas
las notas: es lo que se pide, no rompe la alineación y es barato.

### Captura rápida sin robar el foco — aplazado

Una barra mínima que aparece con el atajo, escribes y desaparece. Valor medio, esfuerzo medio. Por
detrás de recordatorios y exportar.

### Persistir el "siempre encima" por nota — aplazado

El interruptor ya existe (menú "⋯" de la nota) pero es por sesión: al reabrir la nota vuelve a estar
fijada. Persistirlo exigiría una columna nueva en la tabla `Note`, que es la que tiene datos reales
del usuario y no hay migraciones. Se puede resolver con una tabla aparte si se confirma que alguien
lo usa así de verdad.

### Vista previa al pasar el ratón por una pestaña — descartado por ahora

Lo tiene noty, y el usuario lo propuso como forma de que el título importara menos. Dos razones para
no hacerlo:

1. En un **abanico** hay que cruzar por encima de varias pestañas para llegar a la que quieres, así
   que un panel flotante al hacer hover estaría apareciendo y desapareciendo todo el rato durante un
   gesto normal. Por eso noty lo ofrece como **opción**, no por defecto.
2. Su motivo principal desapareció al quitar el título duplicado de la cabecera: la pestaña ya
   enseña título, una línea de vista previa y el progreso de tareas.

Si se retoma, que sea como ajuste opcional y con un retardo de ~500 ms antes de aparecer.

### Arrastrar para reordenar líneas dentro de una nota — descartado a favor de un atajo

Pedido explícito del usuario (2026-09-09): mover de sitio las tareas con casilla arrastrándolas
dentro del cuerpo de la nota, como el mazo ya permite con notas enteras. Se descartó **el arrastre en
sí**, no la posibilidad de reordenar: el cuerpo es un `TextBox` plano a propósito (spec v1, sin texto
enriquecido), así que arrastrar líneas ahí dentro exige simular el gesto a mano — distinguirlo del
clic de marcar la casilla, dibujar una línea fantasma que siga el ratón, reconstruir el texto al
soltar — bastante más complejo que nada hecho hasta ahora y en tensión directa con esa decisión de
diseño. Implementado en su lugar (ver `STATUS.md`): Alt+Arriba/Alt+Abajo intercambia la línea del
cursor con la vecina, mismo resultado sin arrastrar nada. Si el arrastre de verdad se pide otra vez
con más insistencia, el coste de arriba sigue siendo el mismo — no ha cambiado nada que lo abarate.

### Otros descartes menores

- **Etiquetas y carpetas**: es la queja nº1 de Microsoft Sticky Notes, pero choca con el minimalismo
  de Fanote (el mazo son notas activas, punto). Si crece la lista de notas, la respuesta correcta
  probablemente sea mejorar el gestor y la búsqueda, no añadir jerarquía.
- **Adjuntar imágenes / capturas** (lo tienen MS Sticky Notes y BetterStickies): rompería el modelo
  de texto plano, que es lo que hace posible el cifrado simple, la búsqueda y la exportación. No sin
  una razón muy buena.
- **Título duplicado en la nota** — **resuelto**: se quitó el texto de la cabecera. El título es la
  primera línea del cuerpo (justo debajo) y lo que enseña la pestaña del dock; la cabecera sigue
  siendo reconocible como "la pestaña que viajó con la nota" por su color y su troquelado.

- **Campo de título editable, separado del cuerpo — descartado por diseño, no por coste.**
  Conviene dejar claro el porqué, porque la primera vez se argumentó mal: se dijo que obligaría a una
  columna nueva en la tabla `Note` y que no hay migraciones. El usuario señaló, con razón, que ahora
  mismo no hay datos que preservar — así que ese argumento no vale. Las razones que sí valen:
  1. **No resuelve el problema que parecía resolver.** La queja de fondo era una pila de pestañas
     que ponían todas "NUEVA NOTA". Eso pasa porque son notas **vacías**, y una nota vacía tendría
     también el título vacío: con campo de título se vería exactamente la misma pila.
  2. **Cuesta un paso en cada nota.** El gesto de esta app es "abrir y escribir". Un campo de título
     mete una decisión ("¿cómo llamo a esto?") delante de anotar tres palabras, que es el 90 % de los
     casos.
  3. Mantener una sola pieza de texto es lo que hace que cifrado, búsqueda y exportación no tengan
     casos especiales.

  Si algún día se retoma, el argumento a favor sería poder nombrar una nota **independientemente** de
  su contenido (una nota larga cuya primera línea no la describe). Ese sí es un motivo válido, y no
  tiene nada que ver con el que se dio al principio.

---

## 4. Si se decide publicar

Por orden:

1. Terminar la internacionalización (ver arriba). Sin inglés no hay mercado.
2. **Instalador firmado**. Un `.exe` sin firmar dispara SmartScreen y se lleva por delante buena
   parte de las descargas. La vía barata es una cuenta de desarrollador de **Microsoft Store**
   (~19 $ una vez): resuelve firma, actualizaciones y cobro de golpe. Un certificado propio son
   200-400 $/año.
3. Página de presentación. Lo que hay que decir, por orden de fuerza: es lo único así **en Windows**;
   las notas están **cifradas y no salen del equipo**; **no hay cuenta ni nube**; hay **versión
   portable** que no se instala (ningún competidor la ofrece).
4. Precio: pago único en el rango 7-15 $, o gratis con desbloqueo de pago único.

---

## 5. Revisión de apariencia — estado

Hecho en esta sesión: cuerpo de la nota liberado de botones permanentes (eran ~90 px de 320, casi un
tercio), acciones movidas al panel del "⋯", panel oscuro bajo los botones del dock vacío.

Pendiente, por orden de importancia:

1. ~~`SettingsWindow` puede crecer más que la pantalla.~~ **Hecho** (ver `STATUS.md`, sesión
   2026-09-07): primero se le puso `MaxHeight` + `ScrollViewer` como red de seguridad, pero el
   usuario reportó con captura real que la ventana se le seguía saliendo por abajo — el problema de
   fondo no era la falta de scroll, sino demasiada altura. Se rediseñó a dos columnas (760px de
   ancho), verificado con maqueta antes de implementar: la altura baja a la mitad. El
   `MaxHeight`/`ScrollViewer` se mantienen como red de seguridad para pantallas muy pequeñas.
2. **Dos lenguajes de control mezclados en Ajustes**: tarjetas con radio (monitor, borde) conviven
   con casillas sueltas (arranque, atajo, pantalla completa, posiciones). Unificar: o todo tarjetas,
   o las casillas a interruptores alineados a la derecha, estilo Configuración de Windows 11.
3. **Escala tipográfica**: hay 11/12/13/14/15/17 px repartidos a ojo por las cuatro ventanas. Fijar
   tres tamaños y aplicarlos.
4. **`NotesManagerWindow`** es la ventana más cargada (452 líneas de XAML) y la que más se aleja del
   minimalismo del resto. Revisión pendiente.
5. **Tema claro para el chrome**: hoy el dock y los paneles son siempre oscuros. Sobre un escritorio
   claro pesan. Baja prioridad.
6. ~~El abanico puede dejar de ser compacto con muchas notas.~~ **Hecho** — ver `STATUS.md`. La
   ventana tiene tope (`EdgeGeometry.FanBudget`), lo que sobra se scrollea, y la tira de reposo solo
   dibuja los guiones que caben.

## 6. Logo

El concepto actual (tres pestañas de color pegadas al canto derecho, cortadas por el borde) es
correcto y se mantiene: dibuja literalmente el producto y usa la paleta real.

El problema es de tamaño pequeño: a 16 px — bandeja, barra de tareas, Alt+Tab, que es donde más se ve
— tres barras finas con huecos se empastan y acaban leyéndose como un icono genérico de lista. La
solución es la estándar en diseño de iconos: **una variante propia para 16 y 32 px**, con menos
barras y más gruesas, en vez de reescalar la de 256.

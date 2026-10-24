# Aldune — Aspectos retro, opciones de forma y color único (design spec)

Decidido con el usuario el 2026-10-03, sobre maquetas en un lienzo privado
(<https://claude.ai/artifact/N2AdurN792fDCFb3WeiCSX>, siete mesas de trabajo, cada una con sus *Tweaks*).
Las maquetas son la referencia visual; esta spec fija qué se construye y cómo encaja en el código.

## Qué se pidió

- Aspectos "más retro": XP, Windows 95, terminal de Linux, telecomunicaciones, monitor de fósforo.
- Que los colores de esos aspectos se puedan **personalizar** (el color de `nacho@aldune`, el de la barra
  de XP, etc.).
- Esquinas **rectas** en notas, dock y ventanas, y una **señal de sincronización**, elegibles en
  **cualquier** aspecto.
- Poder pintar **todas las notas del mismo color** (monocromo) sin cambiarlas una a una, con cualquier
  tema.
- Un tema de notas oscuro mejor que los actuales, y revisar los temas que hay.
- Exportar e importar la configuración visual y los ajustes (la sincronización de ajustes no existe).
- El dock rediseñado a partir del real (tira de reposo + tarjetas de 208×52 + pie), no inventado.

## Fuera de alcance

- Markdown, notas vinculadas a archivos y fotos (su propia spec, después).
- MSIX (spec de 2026-09-28, después de esta).
- Cambiar la estructura del dock o de la nota: los aspectos cambian cómo se pintan las mismas piezas.

## Decisiones

### 1. Aspecto = paleta + piel

Hoy un aspecto es solo una paleta de ~55 colores (`AppPalette`). Un aspecto retro necesita además
**forma**. Se separan las dos cosas:

- **Paleta** (colores del chrome, en Core): como hoy. Los aspectos retro la **derivan** de Dark o Light
  con el mismo mecanismo que Pastel y Medianoche (`AppPalette.Tint`: se conserva la claridad OKLCH de
  cada clave y se cambia el matiz), más los colores que elige el usuario. Así heredan el contraste ya
  comprobado. Claves nuevas: `TitleBar`, `TitleBarEnd` (degradado), `OnTitleBar`, `BevelLight`,
  `BevelDark` (relieve de Windows 95). En Dark y Light valen lo mismo que hoy se ve (barra = fondo, sin
  relieve), así que los aspectos actuales no cambian.
- **Piel** (`AppSkin`, en Core, datos puros): tipografía del chrome y de las notas, estilo de borde
  (`Flat` | `Bevel`), barra de título (`Plain` | `Gradient`), estilo de tarjeta del dock
  (`Filled` | `Stripe` | `Tinted`), adornos de texto (ninguno | prefijo de canal `CH1` | sufijo de
  carpeta `hoy/` | mayúsculas) y línea de prompt en la nota (sí/no). La capa WPF la convierte en
  recursos (`AldunePrimaryFont`, `AlduneNoteFont`, estilos con `DataTrigger` sobre el estilo de borde)
  igual que `ThemeManager` hace con la paleta.

`AppearanceMode` gana valores nuevos (5 en adelante, escritos como número; un `settings.json`
antiguo sigue cargando y una versión anterior que lea un valor desconocido cae en Oscuro, que es el
`_` de `AppPalette.For`).

### 2. Los aspectos nuevos

| Aspecto | Base | Piel | Colores que elige el usuario |
|---|---|---|---|
| **XP claro** (Luna) | Light | Tahoma; barra en degradado vertical; botones con brillo; tarjetas `Filled`; esquinas redondeadas por defecto | Barra (azul Luna, oliva, plata, teja…), acento (botón de nueva nota, casillas) |
| **XP + 95 oscuro** | Dark | Tahoma; relieve de Windows 95 (`Bevel`); barra en degradado horizontal de Windows 98; esquinas rectas | Barra (azul marino, verde azulado, morado, granate…), acento |
| **Telecomunicaciones claro** (laboratorio) | Light (papel) | Cascadia Mono; líneas de 1 px; tarjetas `Tinted` por canal con su forma de onda; prefijo `CH1…`; rectas | Tinta, acento |
| **Telecomunicaciones oscuro** (osciloscopio) | Dark (panel) | Igual, con retícula de fondo en la nota | Panel, traza (CH1) |
| **Bash** | Dark | Cascadia Mono; notas con fondo de terminal **sin teñir** y el color solo en una franja (`Stripe`); sufijo `hoy/`; línea de prompt `usuario@aldune:~/notas$`; rectas | Paleta (Gruvbox por defecto, Ubuntu, Tango), y sobre ella usuario, ruta y acento |
| **Fósforo** (monitor antiguo) | Dark | Cascadia Mono; mayúsculas; sin brillo ni barrido; rectas | Fósforo (verde, ámbar, azul, blanco), detalles |

Detalles que salieron de la revisión con el usuario y hay que respetar:

- XP + 95 oscuro: la nota es grafito neutro, **sin** el marrón/naranja de Royale Noir.
- Bash: nada de colores fuertes por defecto (Gruvbox apagado); las notas **siempre oscuras**, solo la
  franja lleva color; título de la tarjeta en el color del texto, no en el de la nota.
- Fósforo y telecomunicaciones son aspectos distintos: los canales (`CH1`, `RX`, formas de onda) solo
  en telecomunicaciones. Colores de canal de osciloscopio: CH1 amarillo, CH2 cian, CH3 magenta, CH4
  azul (oscurecidos en la variante clara); el canal y su forma de onda (senoidal, cuadrada, triangular,
  diente de sierra) dependen del **puesto** en el dock, cíclicos de 4 en 4.
- Los adornos son de presentación: el texto de las notas no cambia (`hoy/` o `CH1 HOY` solo se pintan).

**Colores personalizables**: cada aspecto declara sus huecos (`AspectColorSlot`: id, nombre visible,
valor por defecto, rol en la derivación). Se guardan en `settings.json` como
`AspectColors: { "<aspecto>": { "<hueco>": "#RRGGBB" } }` (falta = por defecto). Ajustes → Aspecto
enseña los huecos del aspecto elegido, cada uno con su muestra, su valor y "Cambiar" (el selector de
color que ya existe, `CustomColorWindow`), más "Restablecer colores". Para que un color elegido no
rompa la legibilidad, la derivación **ajusta la claridad** del color del usuario cuando hace falta (por
ejemplo, una barra demasiado clara para texto blanco se oscurece hasta 4.5:1): se conserva el matiz,
que es lo que el usuario está eligiendo. Tests con colores extremos (blanco, negro, amarillo puro).

### 3. Opciones para cualquier aspecto

En Ajustes → Aspecto, debajo de los colores, valen con todos los aspectos (también los de hoy):

- **Esquinas rectas en notas y dock** (`SquareCorners`, `bool?`; `null` = lo que diga el aspecto: rectas
  en bash, telecomunicaciones, fósforo y XP + 95; redondeadas en el resto). Requiere sacar los radios
  escritos a mano (~83 `CornerRadius` en 12 XAML) a recursos `AlduneRadiusSmall/Medium/Large/Pill` y
  `AlduneTabRadius` (el de la pestaña pegada al borde, que se espeja con el dock a la izquierda: hoy lo
  hace `ApplyLeftEdgeTabShape` por código). Con esquinas rectas, `NativeMethods.ApplyRoundedCorners` pide
  a DWM esquinas rectas (`DWMWCP_DONOTROUND`) en vez de las pequeñas.
- **Señal de sincronización** (`ShowSyncSignal`, por defecto según el aspecto): al pie de cada nota,
  `▂▄▆█ sync` en monoespaciada. Estado real, calculado en Core (`NoteSyncSignal`): sincronizada (su fecha
  coincide con la versión base guardada), pendiente de subir, en conflicto, o sin sincronizar (excluida
  por la sync selectiva). Sin sync configurada no se muestra.
- **Mismo color en todas las notas** (`UniformNoteColor`: `null` = desactivado, o `#RRGGBB`): solo
  cambia cómo se **pintan** (nota, pestaña del dock, cápsula de la tira); cada nota conserva su color
  guardado, así que desactivarla lo devuelve todo y la sincronización no ve cambios. El color se elige
  entre los del tema de notas activo o libre; la tinta, el borde y la etiqueta se derivan de él como
  siempre (`NoteColorDerivation`). En el estilo `Stripe`, el color único es el de todas las franjas.
  Un único sitio decide el color visible (`NoteDisplayColor.Resolve(color, settings)` en Core) y lo
  usan los convertidores del dock y `NoteWindow.ApplyColor`.

### 4. Temas de notas

Revisión medida (distancia OKLab mínima entre dos colores de un tema; los tests exigen ≥ 0.03):
Clásico 0.074, Pastel 0.044, Otoño 0.033, Océano 0.030, **Sereno 0.020 (oscuros) y 0.008 (claros)**,
**Grafito 0.004–0.006**. Decidido:

- **Sereno se renueva** (mismo id `serene` y mismo nombre). Colores con la misma claridad, croma
  justo para separarse y matices repartidos por igual:
  - Oscuros (L 0.31, C 0.052, cada 45°): `#472525 #422A11 #36310E #1F371F #033936 #113447 #2B2D4A
    #3E273F` (distancia mínima 0.033, contraste de la tinta ≥ 10:1).
  - Claros (L 0.925, C 0.042, cada 60°): `#FFDDD4 #EFE7C7 #D3EFD8 #C7EFF4 #D9E7FF #F5DDF6` (0.037,
    ≥ 13:1).
  Sustituye a la "Penumbra" de las maquetas: dos temas apagados casi iguales sobraban. Los tests
  existentes de distancia y contraste se extienden a todos los temas de serie (hoy no cubren Sereno ni
  Grafito, por eso pasaban).
- **Grafito se quita**: su papel (monocromo) lo cubre mejor "mismo color en todas las notas", que deja
  elegir el gris. Migración: quien lo tenga activo pasa a `UniformNoteColor` con su gris (`#2E2E2E` o
  `#E8E8E8` según `NewNoteTone`) y a Clásico como tema; sus notas no cambian (guardan su color). Un
  `settings.json` que nombre `graphite` ya cae en Clásico (`NoteThemes.Resolve`).
- **Recoloreo de Sereno**: una vez, al arrancar la versión nueva, cada nota cuyo color sea uno de los
  antiguos de Sereno pasa al nuevo del mismo puesto y tono (oscuro → oscuro, claro → claro). Es un cambio
  de color normal (`NotesRepository.SetColor`) y se sincroniza. Se marca en `settings.json`
  (`SereneRecolored`) para no repetirlo. Riesgo: dos equipos que actualizan a la vez recolorean la misma
  nota con el mismo resultado; la sincronización tiene que tratarlo como el mismo cambio y no como
  conflicto. Test de integración de sync antes de dar la migración por buena; si hoy lo marcara como
  conflicto, se arregla ahí (contenido y color idénticos = sin conflicto).
- Clásico, Pastel, Otoño y Océano no cambian. Los aspectos Pastel y Medianoche tampoco.
- **XP** (claro): los colores post-it de las maquetas (`#FFFFE1 #E1F0FF #E8F5D8 #FCE4D6 #EDE3F7 #FFF0C2
  #DDEFEF #F0EFEA`), mismos tests.
- Cada aspecto **propone** un tema al elegirlo, con una pregunta
  "¿Usar también el tema de notas X?": no se cambia el tema del usuario sin preguntar. Aspecto y tema
  siguen siendo independientes. (XP claro → XP; los oscuros → Sereno.)
- Con `Stripe` (bash), la franja necesita verse sobre el fondo oscuro aunque el color de la nota sea
  apagado: se deriva del color de la nota subiendo su claridad (L≈0.72) y con un croma mínimo, en Core
  (`NoteColorDerivation.StripeColor`), con test de contraste ≥ 3:1 contra el fondo.

### 5. El dock en cada aspecto

Misma estructura y medidas que hoy (`EdgeDockWindow.xaml`: tira de reposo de 20 px con cápsulas de
12×26; tarjetas de 208×52 pegadas al borde con título y vista previa; pie en píldora con Ver todas,
Gestionar, Sincronizar y Nueva nota de 40 px). Lo que cambia por piel:

- Tarjetas: `Filled` (hoy), `Stripe` (fondo del chrome + franja de 3 px a la izquierda, o a la derecha
  con el dock en ese lado), `Tinted` (tinte suave + franja + forma de onda de su canal). En `Tinted` el
  color es el del **canal** (por puesto en el dock), no el de la nota: es lo que da sentido a `CH1…`.
  Con "mismo color en todas las notas" activado, el color único sustituye a los de canal y solo las
  formas de onda y el número de canal siguen distinguiendo las tarjetas.
- Bordes `Bevel`: relieve claro arriba-izquierda y oscuro abajo-derecha en tarjetas, cápsulas, pie y
  botones.
- Pie: con esquinas rectas deja de ser píldora; los botones redondos pasan a cuadrados.
- Telecomunicaciones: la forma de onda es un `Path` fijo por canal (cuatro geometrías en recursos).

### 6. Exportar e importar la configuración

Un archivo, `*.aldune-config.json`, desde Ajustes → Acerca de ("Exportar configuración…",
"Importar configuración…"):

- Al exportar se marcan las secciones: **Aspecto** (aspecto, colores personalizados de cada aspecto,
  esquinas, señal de sync, mismo color, tema de notas activo, temas propios, tono y asignación de color
  de las notas nuevas) y **Ajustes** (idioma, modo de interfaz, atajos, dock: borde, vista, mantener
  abierto, vista previa, pantalla completa, trackpad; tareas: mover hechas, ocultar hechas y su plazo;
  papelera; posiciones recordadas sí/no; consulta de versiones).
- **Nunca** se exportan: perfiles y contraseñas de sincronización, claves, la pantalla elegida
  (`MonitorInfo.StableId` es de cada equipo), posiciones de ventanas, arranque con Windows (es del
  registro de cada equipo).
- Formato: `{"format": "aldune-config", "version": 1, "app": "1.5.0", "appearance": {…}, "settings": {…}}`.
  Una sección que falta no se toca al importar. Campos desconocidos se ignoran; colores inválidos se
  descartan (`NoteThemes.Sanitize` para los temas propios); enums fuera de rango caen en su valor por
  defecto. Un archivo de una versión futura (`version` > 1) se lee igualmente con lo que se entienda.
- Al importar: vista previa con la lista de lo que cambiaría (valor actual → valor nuevo), botones
  "Aplicar" y "Cancelar". Se aplica en vivo (aspecto y atajos sin reiniciar). Antes de aplicar se
  guarda una copia del `settings.json` actual junto a él (como ya hace la copia diaria), para poder
  volver atrás.
- Lógica en Core (`ConfigExport.Build(settings, sections)` y `ConfigImport.Plan(json, current)` →
  lista de cambios + ajustes resultantes), con tests: ida y vuelta, secciones parciales, archivo
  corrupto, campos raros, que nunca salgan credenciales ni la pantalla.

## Orden de trabajo

Tres bloques que se pueden publicar por separado, cada uno con sus tests y su sonda:

1. **Opciones transversales y temas**: radios a recursos, esquinas rectas, señal de sync, mismo color,
   Sereno renovado (con su recoloreo), Grafito fuera (con su migración) y XP. No cambia ningún aspecto
   existente salvo por las opciones nuevas.
2. **Piel**: `AppSkin`, tipografía, relieve, barra en degradado, estilos de tarjeta, adornos, prompt; las
   claves nuevas de paleta con sus valores neutros en Dark/Light.
3. **Los seis aspectos** con sus huecos de color y la página de Ajustes → Aspecto ampliada.
4. **Exportar e importar** la configuración (independiente de los demás; puede ir en cualquier punto).

Todo antes de la 1.5.0, junto con las listas y el atajo ya hechos.

## Pruebas

- Core (TDD): derivación de cada paleta retro con los colores por defecto y con colores extremos
  (contraste de todos los textos sobre sus fondos, como los tests de paleta actuales);
  `NoteSyncSignal` en sus cuatro estados más "sin sync"; `NoteDisplayColor.Resolve` (desactivado, color
  único, color inválido en `settings.json`); `StripeColor` (contraste ≥ 3:1 con cada fondo oscuro);
  distancia y contraste en **todos** los temas de serie (Sereno renovado y XP incluidos); recoloreo de
  Sereno (cada color antiguo al nuevo de su puesto, una sola vez, colores ajenos intactos); migración de
  Grafito; sync con el mismo recoloreo en dos equipos sin conflicto; exportar/importar; carga de un
  `settings.json` antiguo sin los campos nuevos.
- Sondas con datos temporales: capturas de nota, dock (reposo y desplegado, a la derecha y a la
  izquierda) y Ajustes en cada aspecto, con esquinas rectas y redondeadas; cambio de aspecto en vivo;
  mismo color activado y desactivado. Se comparan con las maquetas.
- Textos nuevos en los cinco idiomas con `Strings.T` (nombres de aspectos, huecos de color, opciones).

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
- Un tema de notas oscuro mejor que los actuales.
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

- **Penumbra** (nuevo, oscuro): ocho colores con la misma claridad OKLCH (~0.30) y poco croma (~0.04),
  matices repartidos por igual: `#3E2A2C #3D2E22 #37331F #283624 #1F3532 #23303F #2C2D42 #382A3B`
  (valores de partida; los ajustan los tests de distancia OKLab ≥ 0.03 y de contraste de tinta, como a
  los demás temas). Sirve con cualquier aspecto.
- **XP** (claro): los colores post-it de las maquetas (`#FFFFE1 #E1F0FF #E8F5D8 #FCE4D6 #EDE3F7 #FFF0C2
  #DDEFEF #F0EFEA`), mismos tests.
- Cada aspecto **propone** un tema al elegirlo (XP claro → XP; los oscuros → Penumbra), con una pregunta
  "¿Usar también el tema de notas X?": no se cambia el tema del usuario sin preguntar. Aspecto y tema
  siguen siendo independientes.
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

## Orden de trabajo

Tres bloques que se pueden publicar por separado, cada uno con sus tests y su sonda:

1. **Opciones transversales y temas**: radios a recursos, esquinas rectas, señal de sync, mismo color,
   Penumbra y XP. No cambia ningún aspecto existente salvo por las opciones nuevas.
2. **Piel**: `AppSkin`, tipografía, relieve, barra en degradado, estilos de tarjeta, adornos, prompt; las
   claves nuevas de paleta con sus valores neutros en Dark/Light.
3. **Los seis aspectos** con sus huecos de color y la página de Ajustes → Aspecto ampliada.

Todo antes de la 1.5.0, junto con las listas y el atajo ya hechos.

## Pruebas

- Core (TDD): derivación de cada paleta retro con los colores por defecto y con colores extremos
  (contraste de todos los textos sobre sus fondos, como los tests de paleta actuales);
  `NoteSyncSignal` en sus cuatro estados más "sin sync"; `NoteDisplayColor.Resolve` (desactivado, color
  único, color inválido en `settings.json`); `StripeColor` (contraste ≥ 3:1 con cada fondo oscuro);
  Penumbra y XP con las reglas de distancia y contraste de los temas; carga de un `settings.json`
  antiguo sin los campos nuevos.
- Sondas con datos temporales: capturas de nota, dock (reposo y desplegado, a la derecha y a la
  izquierda) y Ajustes en cada aspecto, con esquinas rectas y redondeadas; cambio de aspecto en vivo;
  mismo color activado y desactivado. Se comparan con las maquetas.
- Textos nuevos en los cinco idiomas con `Strings.T` (nombres de aspectos, huecos de color, opciones).

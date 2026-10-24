# Aldune Android: aspectos retro (colores y fuente)

Fecha: 2026-10-25. Estado: **spec, pendiente de revisión**. Decisión del usuario del mismo día; revierte la nota de `AppPalette.kt` y de
`tests/vectors/README.md` («los aspectos retro son de Windows y en Android no se ofrecen»).

## Qué se pide

Poder elegir en Android los aspectos retro que ya existen en Windows (**XP claro, XP oscuro, Telecomunicaciones claro, Telecomunicaciones
oscuro, Bash y Fósforo**) con sus colores personalizables, y que se vean con los mismos colores que en el PC. Alcance acordado: **colores y
fuente**. Nada que el PC no tenga.

Referencia en Windows: `docs/historia/specs/2026-10-03-aspectos-retro-design.md`, `src/Aldune.Core/AspectCatalog.cs`,
`AspectPalettes.cs`, `ColorFit.cs`, `ColorMix.cs`.

## Fuera de alcance

La «piel» de Windows: relieve de Windows 95, barras de título con degradado, tarjetas con franja o tinte por canal, prefijos y sufijos
(`CH1`, `hoy/`), mayúsculas de Fósforo, línea de prompt en la nota, retícula del osciloscopio. Tampoco las opciones «Esquinas rectas», «Señal de
sincronización» y «Mismo color en todas las notas». Cada una es una decisión de diseño propia en Compose y no hace falta para que el aspecto
se reconozca. Tahoma (XP) y Cascadia Mono no existen en Android.

## Diseño

### 1. Núcleo en Kotlin (módulo `text`, sin Android)

Port literal de Core, con los mismos nombres:

- `ColorFit` (`ratio`, `background`, `foreground`, `lighter`, `darker`) y `ColorMix.toward`.
- `AspectCatalog`: los 6 aspectos con id estable (`xp-light`, `xp-dark`, `telecom-light`, `telecom-dark`, `bash`, `phosphor`), sus huecos
  (`AspectColorSlot`: id, valor por defecto, muestras), las paletas de Bash (Gruvbox por defecto, Ubuntu, Tango) y el tema de notas sugerido
  (`SuggestedThemeId`). `resolve(mode, elegidos)`: lo elegido que sea `#RRGGBB` válido, y lo de fábrica para el resto.
- `AspectPalettes.derive(mode, colores)`, con las mismas funciones privadas que en Core (`xpLight`, `xpDark`, `telecomLight`, `telecomDark`,
  `bash`, `phosphor`, `moveBackgrounds`, `fitText`, `setAccent`).
- `AppPalette.forMode(mode, systemUsesLight, aspectColors = null)` acepta los modos 5 a 10 y devuelve la paleta derivada. `isLight` incluye
  los retro claros (5 y 7), que ya contempla. Un modo desconocido sigue cayendo en Oscuro.

El `OklchColor` de Android ya existe; `AppPalette.tint` también. La paleta derivada es la de Core y se fija con vectores.

### 2. Vectores de compatibilidad

Nuevos casos en `tests/vectors/text-color.json`, generados por `TextVectorsTests` de Core con las funciones reales:

- `ColorFit.Ratio`, `Background`, `Foreground`, `Lighter`, `Darker`, y `ColorMix.Toward`, con entradas ordinarias y extremas.
- `AppPalette.For` para los modos 5 a 10 con los colores de fábrica y con colores extremos (blanco, negro, amarillo puro) en cada hueco, y el
  `windowsUsesLight` de siempre. Hace falta que el caso lleve `aspectColors` (id de hueco → hex).
- `AspectCatalog.Resolve` con entradas válidas, inválidas y ausentes.

Los verifican los dos lados (`TextVectorsTests.cs` y `TextVectorsTest.kt`). Añadir casos no cambia el formato de sync, y regenerar sin cambiar
Core no cambia los ficheros (la salida es determinista). `tests/vectors/README.md` se actualiza: ya no dice que los retro no tienen vectores.

Además, los tests de contraste de Core sobre cada aspecto con colores extremos se portan a Kotlin tal cual (`AspectCatalogTests`,
`AppPaletteTests`), para que la propiedad «el texto se lee» no dependa solo de que los vectores coincidan.

### 3. Ajustes

- `SettingsStore`: `appearance` acepta 0 a 10 (hoy recorta a 0-4). Valor desconocido → `SYSTEM`, como hoy.
- Clave nueva `aspectColors`: JSON con la misma forma que `AspectColors` de `settings.json` del PC
  (`{"bash": {"accent": "#D79921"}}`), en `SharedPreferences`. Falta = de fábrica. Se guardan los de todos los aspectos, no solo el activo
  (volver a uno ya personalizado lo encuentra como se dejó). Un JSON ilegible se ignora y vale lo de fábrica.
- `AppSettings.aspectColors: Map<String, Map<String, String>>`, vacío por defecto. Una instalación existente carga sin migración.

### 4. Interfaz

- **Ajustes → Aspecto**: la lista actual (Sistema, Oscuro, Claro, Pastel, Medianoche) suma los 6, con los nombres del PC traducidos a los
  cinco idiomas (`Strings.cs`: `AppearanceXpLight` y similares; se reutiliza su texto).
- **Colores del aspecto**: con un aspecto retro elegido aparece, debajo de la lista, un bloque con un renglón por hueco (nombre, muestra,
  valor `#RRGGBB` y «Cambiar», que abre el `CustomColorDialog` ya hecho), las paletas de Bash como atajos de un toque, y «Restablecer colores».
  Las muestras de un hueco (`Suggestions`) salen como atajos del propio renglón.
- **Tema de notas sugerido**: al elegir un aspecto con `SuggestedThemeId` distinto del tema activo, un diálogo ofrece pasar a ese tema
  («Aplicar» / «Mantener»), como `AspectSuggestTheme` en Windows. No cambia las notas existentes.
- **Tema de Compose** (`AldunaTheme`): recibe la paleta ya derivada (ya lo hace con Pastel y Medianoche); las claves nuevas de los retro
  (`TitleBar`, `OnTitleBar`, `BevelLight`…) se ignoran donde no hay equivalente. La barra de estado y el contraste de la barra del sistema se
  eligen con `isLight`.
- **Fuente**: Bash, Telecomunicaciones (claro y oscuro) y Fósforo usan `FontFamily.Monospace` en la interfaz y en el editor de notas, como
  Cascadia Mono en el PC. XP y los aspectos de siempre mantienen la fuente actual.
- **Widget y mosaico** de Ajustes rápidos: usan la paleta del aspecto como hoy con Pastel y Medianoche, sin cambios propios.

### 5. Compatibilidad

- `settings.json`/preferencias antiguas cargan sin migración. Un `appearance` guardado por una versión nueva y leído por una antigua (valor
  5-10) cae en Sistema, no en un fallo.
- Los aspectos son locales a cada dispositivo (como el aspecto actual): no viajan por la sync.
- No se toca el formato de sync (4), los prefijos de códigos de perfil ni los vectores de sync.

## Pruebas

1. **Kotlin puro, con test primero**: `ColorFit`, `ColorMix`, `AspectCatalog`, `AspectPalettes`, `AppPalette.forMode` con modos 5 a 10.
2. **Vectores** (sección 2) en los dos lados.
3. **Contraste**: cada aspecto con colores extremos, portado de Core.
4. **`SettingsStore`**: carga con `appearance` fuera de rango y con `aspectColors` ilegible (prueba instrumentada solo si ya hay infraestructura;
   si no, la lógica de lectura va en una función pura y se prueba en JVM).
5. **Sonda en el Pixel** (versión de depuración, notas inventadas): cada aspecto, un color cambiado, «Restablecer», el diálogo del tema
   sugerido y que no hay texto ilegible en la lista, el editor y los diálogos. Capturas del Pixel y, si hay forma barata, comparación con la
   paleta del PC.

## Orden de trabajo y tamaño

Estimado en sesiones como las de este proyecto (≈ 3-4):

1. `ColorFit`, `ColorMix`, `AspectCatalog` y `AspectPalettes` con sus tests y los vectores de Core regenerados.
2. `AppPalette.forMode` extendido y verificación de vectores en Kotlin.
3. Ajustes (`SettingsStore`, `AppSettings`) y la pantalla de aspecto con los huecos de color.
4. Fuente monoespaciada, diálogo del tema sugerido, textos en cinco idiomas y sonda en el Pixel.

Cada paso deja el proyecto compilando y con tests en verde, y se puede commitear solo. No se publica ninguna versión sin confirmación.

## Riesgos y decisiones abiertas

- **Fidelidad de color**: un redondeo distinto en `OklchColor` entre C# y Kotlin movería un canal ±1. El port de paletas ya existe
  (`Pastel`, `Medianoche`) y pasa sus vectores, así que se espera lo mismo; si un vector difiere en ±1 se investiga la causa, no se afloja el test.
- **Claridad del fondo en Bash/Telecom/Fósforo**: `moveBackgrounds` limita la claridad a [0.14, 0.30] por contraste; se respeta tal cual.
- **Tamaño de la lista de Ajustes**: con 11 aspectos y los huecos, la pantalla crece. Si resulta incómoda en el móvil, el aspecto se elige en un
  selector y los huecos en una hoja aparte; se decide en la sonda, no antes.
- **XP sin Tahoma**: el aspecto se reconoce por la paleta y la barra no existe en Android, así que puede parecer «solo un beige». Se acepta
  (alcance acordado) y se revisa tras la sonda.
- **Idiomas**: los nombres de los aspectos y de los huecos ya están en los cinco idiomas en `Strings.cs`; se copian a `strings.tsv`.

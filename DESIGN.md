# Design

Fuente de verdad en código: `src/Aldune.Core/AppPalette.cs` y `AspectPalettes.cs` (chrome por claves semánticas), `NoteThemes.cs` (colores de nota). Este documento resume el sistema; si discrepan, manda el código.

## Visual theme

Escena: alguien con otra app abierta, con luz de oficina o de noche en casa, que mira Aldune unos segundos. Por eso el defecto es un oscuro cálido (marrón tostado, no negro ni gris azulado) y un claro de papel cálido (no blanco puro). Estrategia de color: **Restrained** en la app base (neutros cálidos y un acento arena), **Committed** en cada aspecto retro, donde la piel entera es el aspecto.

## Colors

Chrome por claves semánticas (`Ground`, `Surface`, `Raised`, `Text`, `MutedText`, `Accent`, `Danger*`, `Border`, `TitleBar`…), nunca hex en XAML.

| Rol | Oscuro | Claro |
|---|---|---|
| Fondo (`Ground`) | `#2A261F` | `#F6F1E8` |
| Texto | `#EDE7DC` | `#2A261F` |
| Acento | `#A18465` | arena/tostado equivalente |
| Peligro (texto) | `#E8A0A0` | derivado |

Aspectos: Oscuro, Claro, Pastel (lavanda con acento rosa), Medianoche (azul noche), XP claro/oscuro (Luna, barra azul en degradado, Tahoma), Telecom claro/oscuro (papel de laboratorio / osciloscopio, canales CH1-CH4), Bash (Gruvbox), Fósforo (monocromo), Libreta de viaje y Cuaderno de campo (claro y oscuro). El «tema del sistema» sigue a Windows.

Colores de nota: por temas (Clásico, Sereno, Grafito, Gris, Pastel, Otoño, Océano, XP) con tono claro, oscuro o alterno; borde y etiqueta se derivan del color de la cara (`NoteColorDerivation`). El logo usa amarillo `#EBD38B`, cian `#83E7F2` y rosa `#F9BEF2` sobre `#2A261F`.

## Typography

Segoe UI (Segoe Fluent Icons para glifos) en la app base; Tahoma en XP; monoespaciada en Telecom, Bash y Fósforo; fuentes manuscritas o de papel en las libretas. El cuerpo de la nota define su propia fuente y altura de línea (`AlduneNoteBodyFont`, `AlduneNoteLineHeight`) y todo lo que se alinea con el texto (marcador vacío, renglones) las usa. Tamaño del texto de 100 a 150 %.

## Elevation and shape

Plano por defecto, con borde fino y esquinas redondeadas; «Esquinas rectas» las anula. Los aspectos retro usan bisel (`BevelLight`/`BevelDark`) en vez de sombra difusa. Sin glassmorfismo. Un icono dentro de un botón con bisel o sombra de un lado puede parecer descentrado aunque esté centrado: se mide la geometría, no la impresión.

## Components

Dock de pestañas en un borde (se despliega en abanico), notas como ventanas con cabecera y etiquetas, gestor, Ajustes por pestañas, avisos (`ToastWindow`), diálogos (`AppDialog`), selector de color. En Android: lista, editor, Ajustes por pestañas, hoja de opciones, widgets. Foco visible en todos.

## Motion

Corto y funcional (abanico del dock, apertura de nota). Curvas de salida suaves, sin rebote. Se respeta «reducir movimiento».

## Do / Don't

- Sí: claves de `AppPalette`, textos con `Strings.T`, centrado medido, contraste fijado por tests.
- No: hex sueltos, sombras sin pensar en el centrado óptico, retro que baje el contraste, funciones solo de una plataforma sin motivo.

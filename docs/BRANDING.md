# Identidad visual de Aldune

Decidido el 26-10-2026 (ver también `DECISIONES-2026-10-26.md`). El símbolo es **la «A» de tres notas**: dos patas
inclinadas, una amarilla y otra cian, y un travesaño rosa. Es **un único dibujo**, definido como geometría en
`scripts/branding/generar-iconos.py`; todos los formatos salen de él. No se retoca ningún icono a mano.

## El símbolo

- Tres formas, tres colores de nota, y huecos reales entre ellas (no un trazo del color del fondo): por eso sirve igual en
  color que en monocromo, y en un `VectorDrawable` de Android.
- Cabe en la **zona segura de 66 dp** del icono adaptativo (radio máximo del dibujo: 30,2 de 33), así que se ve igual en
  máscara circular, squircle y cuadrada, y no se pega a los bordes.
- No depende del contenedor: la «A» es una silueta compacta y centrada.

| Elemento | Valor |
|---|---|
| Fondo | `#2A261F` (marrón oscuro del logo de siempre) |
| Pata izquierda (nota amarilla) | `#EBD38B` |
| Pata derecha (nota cian) | `#83E7F2` |
| Travesaño (nota rosa) | `#F9BEF2` |
| Capas | la cian va sobre la amarilla; el travesaño sobre las dos; hueco de 1,2 (en 108) entre ellas |

El maestro es `docs/branding/aldune-symbol.svg` (sin fondo, lienzo de 108).

## Reglas de uso

- Siempre el símbolo **solo**, sin texto. El nombre «Aldune» va aparte, como texto.
- Fondo oscuro `#2A261F` en los iconos. Sobre fondo claro (README, web) se usa la ficha con su fondo oscuro, nunca el
  símbolo suelto: el amarillo y el rosa pierden contraste sobre blanco.
- Monocromo: una sola tinta con los mismos huecos (Android lo hace el sistema con `ic_launcher_monochrome`).
- Por debajo de 24 px se usa la **versión gruesa** (`aldune-logo-small.svg`: patas de 15 en vez de 13 y huecos de 2 en
  vez de 1,2). Es la misma «A», solo con más masa para que no se pierda.
- No inclinar, no rotar, no cambiar los colores de las notas ni añadir sombras o brillos.
- Colores de nota de la app y de su interfaz siguen las reglas de `AGENTS.md` (temas y `AppPalette`); este documento solo
  fija el **logo**.

## Dónde está cada formato

| Formato | Fichero | Cómo se obtiene |
|---|---|---|
| Símbolo maestro | `docs/branding/aldune-symbol.svg` | generador |
| Ficha de Windows | `src/Aldune/Assets/aldune-logo.svg`, `aldune-logo-small.svg` | generador |
| Icono de Windows y bandeja | `src/Aldune/Assets/aldune.ico` (16-256 px, PNG) | generador; la bandeja lee este mismo `.ico` |
| Android: icono adaptativo | `ic_launcher_foreground.xml` (108 dp, símbolo dentro de 66 dp), fondo `ic_launcher_colors.xml`, `ic_launcher_monochrome.xml` | generador |
| Android: notificaciones | `ic_stat_aldune.xml` (24 dp, una tinta, versión gruesa); lo usa `CaptureNotification` | generador |
| Android: acceso directo | `ic_shortcut_new_note.xml` | generador |
| Android: widget | `ic_aldune_symbol.xml` en `widget_preview.xml` (vista previa del selector) | generador |
| Ficha de tiendas | `android/fastlane/metadata/android/en-US/images/icon.png` (512 px, cuadrado completo) | generador |
| README | `docs/branding/aldune-logo-128.png` (y `-512.png`) | generador |
| Favicon | `docs/branding/favicon.ico` y `favicon.svg` | generador |
| Imagen al compartir el repo | `docs/branding/social-preview-1280x640.png` | símbolo + nombre y lema en Segoe UI (el texto va aparte del símbolo); se sube a mano en GitHub → Settings → Social preview, no se regenera con el script |

Para regenerar todo: `pip install shapely pillow` y `python scripts/branding/generar-iconos.py`. El script comprueba antes
que el símbolo cabe en la zona segura y falla si no.

## Qué se descartó y por qué

Se dibujaron ocho candidatos (capturas en `docs/branding/capturas/`, código de la exploración en
`docs/branding/alternativas/`) y se probaron con el mismo código a 16, 24, 48, 108 y 512 px, en máscara circular, squircle y
cuadrada, sobre fondo claro y oscuro y en monocromo. Después se hizo una segunda prueba de **los tres finalistas** a 16-32 px
reales, en grises y con simulación de daltonismo (deuteranopia y protanopia), y entre iconos vecinos.

| Candidato | Resultado |
|---|---|
| **G · «A» de tres notas** | **Elegido.** Nítido a 16 px, el más reconocible entre vecinos, aguanta grises y daltonismo, y es la inicial de Aldune hecha con los tres colores de nota |
| **C · Notas apiladas** | Finalista, **guardado** como alternativa (`alternativas/aldune-symbol-C-notas-apiladas.svg`). Cuenta mejor «nota adhesiva», pero a 16 px el rosa y el cian se funden en un bloque |
| A · Tres pestañas (el concepto anterior) | A 16 px la barra y las pestañas se vuelven un borrón; el concepto del dock «pegado al borde» exigía la barra, que en un círculo no funciona |
| F · Pestañas gruesas | Arregla A, pero en monocromo parece un icono de menú o lista, no de notas |
| E · Abanico de notas | A 16 px es una mancha amarilla |
| B · Nota con índices, D · Nota con cinta, H · Pila vista de frente | Las lengüetas o la cinta se pierden a tamaño pequeño; D y H parecen portapapeles o caja |

**Lo que se pierde con la G**: la idea del dock pegado al borde de la pantalla, que ya casi no se leía a 48 px. Si algún día se
quiere recuperar, C o A son el punto de partida. El riesgo conocido de G es que una letra en un icono es menos distintiva
que una forma propia; se compensa con los tres colores y con los huecos.

## Verificación

- Windows: `aldune.exe` compilado lleva el icono nuevo (`capturas/08-pc-exe.png`); la bandeja usa el mismo `.ico`.
- Pixel 10a: cajón de aplicaciones (`06-pixel-cajon.png`) y vista previa del widget (`07-pixel-widget.png`). La notificación
  de captura usa `ic_stat_aldune` (comprobado con `dumpsys`); Pixel oculta en la barra de estado el icono de las
  notificaciones silenciosas, así que la silueta se revisó aparte (`05-icono-de-estado.png`).
- Los SVG se contrastaron con Inkscape: diferencia máxima de 40/255 respecto al rasterizado propio (solo antialiasing).

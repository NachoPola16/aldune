# Temas de notas — diseño

Fecha: 2026-09-23. Forma parte de la Fase 2 del plan de cierre de la 1.0
(`docs/superpowers/plans/2026-09-23-aldune-1.0-cierre.md`). Maqueta de referencia: paletas A y B y
acabado "filo superior", validados por el usuario el mismo día.

## Objetivo

Que el usuario decida el aspecto de sus notas sin tener que elegir el color de cada una:

- Elegir un **tema**: un conjunto de colores oscuros y claros pensados para ir juntos.
- Elegir si las notas nuevas nacen **claras, oscuras o de los dos tipos**.
- Elegir **cómo se asigna el color** a cada nota nueva.
- **Crear sus propios temas**.
- **Aplicar un tema a las notas que ya existen**.

Condiciones: el color personalizado de cada nota se mantiene; el tema actual sigue siendo el de
fábrica, así que actualizar la app no cambia nada hasta que el usuario lo decida.

## Fuera de alcance (1.0)

- **Tematizar la interfaz** (dock, menús, ajustes, gestor). Hay 361 colores escritos a mano en los
  XAML y habría que pasarlos a tokens primero. Queda para la 1.x.
- **Sincronizar temas y ajustes de color.** Los colores de las notas ya viajan con cada nota, así que
  las notas se ven igual en todos los dispositivos. Lo que no viaja es la configuración, y además
  tiene sentido que sea por dispositivo. Sincronizarla abriría conflictos de edición a cambio de
  poco. Queda para la 1.x si se echa en falta.

## Modelo

### Tema

```text
NoteTheme
  Id           string   estable; "classic", "serene", "graphite" en los de serie; GUID en los propios
  Name         string
  DarkColors   string[] #RRGGBB, en el orden de rotación
  LightColors  string[] #RRGGBB, en el orden de rotación
  IsBuiltIn    bool     los de serie no se editan ni se borran; sí se duplican
```

Vive en `Aldune.Core`, sin dependencias de WPF. Los temas de serie son constantes en código; los
propios se guardan en `AppSettings.CustomThemes`.

Temas de serie, con los valores ya calculados en OKLCH (misma L dentro de cada grupo):

| Tema | Oscuros | Claros |
|---|---|---|
| **Clásico** (`classic`) | — | Los 6 actuales de `NoteColorPalette.Colors` (L 0.87) |
| **Sereno** (`serene`) | Grafito `#2E3034`, Pizarra `#26323E`, Tinta `#262F47`, Petróleo `#1D3538`, Musgo `#283426`, Tabaco `#3C2D21`, Burdeos `#462527`, Ciruela `#392A3C` (L 0.31) | Hueso `#EBE6D9`, Piedra `#EBE5E0`, Niebla `#DEE8F0`, Salvia `#DEEADE`, Lino `#F0E4D7`, Polvo `#F2E2E1` (L 0.925) |
| **Grafito** (`graphite`) | Carbón `#2F2D2C`, Grafito `#2B2E33`, Humo `#332C29`, Acero `#262F36`, Ónice `#2E2E2E` (L 0.30) | Papel `#EBE7E0`, Tiza `#E4E8ED`, Arena `#EFE6DD`, Ceniza `#E8E8E8` (L 0.93) |

Un tema puede tener una sola de las dos listas (Clásico solo tiene claros). Si el tono pedido no
existe en el tema, se usa la otra lista: nunca se deja una nota sin color.

### Ajustes nuevos (`AppSettings`)

| Campo | Tipo | Por defecto | Nota |
|---|---|---|---|
| `ActiveThemeId` | string? | null = `classic` | Si apunta a un tema borrado, se cae a `classic`. |
| `NewNoteTone` | enum `Light`/`Dark`/`Both` | `Light` | Con `Both` se alterna oscuro/claro. |
| `ColorAssignment` | enum `Rotate`/`MostDistinct`/`RotateAvoidNeighbors`/`Fixed` | `RotateAvoidNeighbors` | |
| `FixedNoteColor` | string? | null = primer color del tono pedido | Solo con `Fixed`. |
| `CustomThemes` | List\<NoteTheme\> | vacío | |

Todos tienen un valor por defecto que funciona cuando falta el campo: un `settings.json` anterior
se lee sin migración (mismo criterio que `GlobalHotkeyEnabled`).

## Asignación de color (`NoteColorAssigner`, Core, pura y probada)

Entrada: el tema, el tono pedido, la regla y los colores de las notas activas **en el orden del
dock**. Salida: el `#RRGGBB` de la nota nueva.

1. **Candidatos**: la lista del tono pedido. Con `Both`, oscuro si la última nota del dock es clara
   y claro si es oscura (oscuro si no hay notas).
2. **Regla**:
   - `Rotate`: el candidato siguiente al último color de ese tono usado en el dock. Si no hay
     ninguno, el primero. Sustituye al `existing % Colors.Length` actual, que repetía colores al
     borrar notas.
   - `MostDistinct`: el candidato cuya distancia mínima en OKLab a los colores del dock es mayor.
     En empate, el primero en orden de la lista.
   - `RotateAvoidNeighbors`: como `Rotate`, saltando los candidatos iguales a los colores de las
     **dos notas junto a las que aparecerá la nueva** en el dock. Si todos los candidatos están
     excluidos (tema con uno o dos colores), cae a `Rotate`.
   - `Fixed`: `FixedNoteColor`, o el primer candidato si no hay.

La comparación de colores es sin distinguir mayúsculas/minúsculas en el hex.

Sustituye a las dos copias de la asignación actual (`AppCoordinator.CreateAndOpenNote` y
`EdgeDockWindow.CreateNote`), que pasan a llamar a un único punto.

## Color derivado: borde y etiqueta (`NoteColorDerivation`, Core)

Hoy el borde (`Rims`) y la etiqueta (`Labels`) están escritos a mano para los 6 colores de fábrica,
y el resto se oscurece con un factor RGB aproximado. Con temas, cualquier color tiene que salir
bien, así que se calculan en OKLCH a partir de la cara:

- **Cara clara** (L ≥ 0.6): borde a L−0.16 y etiqueta a L−0.44, mismo matiz y croma. Es la regla
  con la que se diseñaron los valores actuales. Los 6 de fábrica **conservan sus hex exactos**
  mediante una tabla de excepciones (se ajustaron a mano y la fórmula no los reproduce al píxel; un
  test lo fija). Los calculados se ajustan al gamut sRGB reduciendo el croma.
- **Cara oscura** (L < 0.6): borde a L+0.07 (el "filo") y etiqueta a L 0.86, con croma
  `min(C·1.6, 0.08)`.
- Si la etiqueta calculada no llega a 4,5:1 contra la cara, se usa `ForegroundFor(cara)`, como
  hoy.

`NoteColorPalette.RimFor/LabelFor` delegan en esta clase; `Darken` desaparece.

## Acabado de la pestaña

- **Cara clara**: sin cambios (reflejo `TabSheen` y borde de 1 px con `RimFor`).
- **Cara oscura**: sin reflejo. En lugar del borde completo, una línea de 1 px arriba con el color
  del filo (`RimFor`), y la misma sombra. Se decide por pestaña según la luminosidad de su cara,
  en el mismo sitio donde hoy se elige `TabSheen`/`TabSheenLeftEdge`.
- La ventana de la nota abierta usa ya `RimFor` y `ForegroundFor`; con la derivación nueva, las
  oscuras salen con cabecera y tinta coherentes sin más cambios.

## Interfaz

### Ajustes → sección "Temas" (nueva, antes de "Sincronización")

- **Tema**: desplegable con los temas de serie y los propios. Debajo, una tira con sus colores.
- **Botones**: "Duplicar", "Nuevo", "Editar" y "Eliminar". Editar y eliminar solo están
  disponibles para los temas propios.
- **Las notas nuevas nacen**: Claras / Oscuras / De los dos tipos.
- **Color de las notas nuevas**: Rotar por el tema / El más distinto / Rotar sin repetir el de al
  lado (recomendado) / Siempre el mismo color, con selector de ese color.
- **"Aplicar a las notas existentes…"** abre una confirmación: *"Se cambiará el color de tus N
  notas activas, incluidos los colores que elegiste a mano. Se sincronizará a tus otros
  dispositivos."* Recorre las notas activas en el orden del dock y asigna a cada una con la regla
  y el tono actuales, como si se crearan en ese orden. Las archivadas y las de la papelera no se
  tocan.

### Editor de tema (ventana pequeña)

Nombre y dos filas, "Oscuros" y "Claros". Se pulsa un color para seleccionarlo, y los botones "←",
"→" y "Quitar" actúan sobre el seleccionado. Se añaden con "+", que abre el `CustomColorWindow`
existente (solo deja elegir colores legibles). Con botones y no arrastrando, que es más fiable y
funciona con teclado. Con una sola lista vacía el tema es válido; con las dos vacías, no se puede
guardar.

### Acceso rápido

La paleta rápida del menú "⋯" de la nota y la del menú de la pestaña del dock muestran los colores
del tema activo: primero los oscuros y luego los claros, en dos filas si hay de los dos tipos. El
"Color personalizado…" sigue igual.

### Textos

Todos por `Strings.T(en, es)`, como el resto de la app.

## Datos y sincronización

- No cambia el esquema de la base de datos ni el formato de sync: la nota sigue guardando un
  `#RRGGBB`.
- "Aplicar a las notas existentes" es una edición de color normal de cada nota, así que se
  sincroniza igual que si se cambiaran a mano.
- Los temas y los ajustes nuevos viven en `settings.json`, local a cada dispositivo.

## Errores y casos límite

- Tema activo borrado, o `settings.json` con un id desconocido: se usa `classic`.
- Tema propio con colores no válidos (hex mal formado, editado a mano en el JSON): esos colores se
  ignoran al cargar; si no queda ninguno, el tema se descarta.
- `FixedNoteColor` no válido o que no es del tema activo (se eligió en otro tema): primer candidato.
- Dock sin notas: `Rotate` y `RotateAvoidNeighbors` dan el primer candidato; `MostDistinct`,
  también.

## Pruebas

- `NoteColorAssignerTests`: cada regla, `Both` alternando, caída a la otra lista, tema de un solo
  color, dock vacío, hex en mayúsculas o minúsculas, y que borrar notas ya no repite vecinos.
- `NoteColorDerivationTests`: los 6 de fábrica dan exactamente sus `Rims`/`Labels` actuales; todo
  color de los temas de serie da etiqueta ≥ 4,5:1 contra su cara; los colores calculados están en
  gamut.
- `NoteThemeTests`: temas de serie válidos; la carga ignora colores no válidos; se cae a `classic`.
- `SettingsServiceTests`: un `settings.json` sin los campos nuevos carga con los valores por
  defecto.
- Verificación visual con render real (`RenderTargetBitmap`) de pestañas claras y oscuras, y
  comprobación manual del usuario en el dock.

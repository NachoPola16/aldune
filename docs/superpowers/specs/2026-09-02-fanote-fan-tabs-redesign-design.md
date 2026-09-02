# Fanote — Rediseño de pestañas en abanico (design spec)

Rediseña cómo se muestra el panel desplegado del dock: de una lista de
botones en un `ItemsControl`/`ScrollViewer` a pestañas individuales
escalonadas con etiqueta de texto vertical, inspirado en una captura de
referencia de una app tipo Hold My Notes que compartió el usuario. Ver
`docs/STATUS.md`, sección "Idea pendiente de brainstorming... pestañas en
abanico estilo Hold My Notes", para el contexto de cómo surgió esto.

## Contexto y motivación

El dock actual, en su estado desplegado, muestra las notas como una lista
vertical de botones rectangulares dentro de un `ScrollViewer` — funcional,
pero visualmente plano. La captura de referencia que motivó esto muestra
un patrón más vivo: en reposo, una franja fina con un guión de color por
nota (esto **ya lo tenemos**, sin cambios); al pasar el ratón, las notas
se despliegan como un abanico de pestañas que aparecen escalonadas en el
tiempo, cada una mostrando su color y una etiqueta de texto rotada
verticalmente; al abrir una, la ventana de la nota parece crecer desde la
posición exacta de su propia pestaña, no aparecer en un sitio genérico.

Es un cambio real de interacción y de cómo `EdgeDockWindow` representa y
anima cada nota — no encaja en un ajuste rápido, de ahí el spec completo.

## Alcance

**Dentro:**

- El panel desplegado (`PanelContent`/`TabsList` en `EdgeDockWindow.xaml`)
  se rediseña: cada nota es una pestaña individual con su color de fondo
  y una etiqueta de texto vertical (el título de la nota, vía
  `NoteTitleConverter`, igual que hoy). No lleva etiqueta de estado
  ("Archivada"/"Papelera") porque el abanico deja de mostrar
  archivadas/papelera — ver el punto de simplificación de controles, más
  abajo.
- Disposición: columna alineada (todas las pestañas sobresalen la misma
  distancia del borde, apiladas verticalmente) — no cascada en
  profundidad. Decidido explícitamente así por ser lo más fiel a la
  referencia y lo más simple de implementar.
- Animación de entrada escalonada: cada pestaña aparece con un
  `BeginTime` creciente (~45ms por índice) respecto a la anterior,
  combinando opacidad (0→1) y una traslación perpendicular al borde
  (desde "escondida bajo el pill" hasta su posición final) — un
  deslizamiento, no solo un fundido.
- Al hacer clic en una pestaña, la `NoteWindow` correspondiente anima su
  aparición **desde la posición en pantalla de esa pestaña** (capturada
  con `UIElement.PointToScreen`) hasta el tamaño/posición final ya
  calculado por el `PositionNoteWindow`/cascada existente — esa lógica de
  cascada no cambia, solo el punto de partida de la animación de
  aparición.
- Simplificación de controles: se quita el botón "Archivadas"/"Activas"
  del dock. El abanico desplegado muestra **solo notas activas**. El
  icono de engranaje ("Gestionar notas") sigue siendo la única puerta a
  archivadas/papelera — sin cambios en `NotesManagerWindow`. El botón
  "+ Nueva nota" y el de "Gestionar" pasan de botones de texto en una fila
  a dos botones circulares pequeños al final de la columna de pestañas.

**Fuera de alcance (no cambia):**

- El pill en reposo (`PillSwatches`, los guiones de color) — la
  referencia ya lo describe como está implementado hoy.
- `EdgeGeometry.PillRect`/`ExpandedRect` y el tamaño dinámico según nº de
  notas (de la sesión anterior) — el panel sigue usando `ExpandedRect`
  para su rectángulo exterior; lo que cambia es solo cómo se rellena por
  dentro.
- `NotesManagerWindow`, `AppCoordinator`, `NotesRepository`,
  `NoteWindow` (salvo la nueva animación de apertura) — sin cambios.
- Multi-monitor/DPI (Fase 3a) — ortogonal a esto, ya cerrado.

## Componentes y cambios

### `EdgeDockWindow.xaml` — plantilla de pestaña

El `DataTemplate` de `TabsList` (hoy un `Button` con `NoteTabButtonStyle`)
pasa a ser un `Border` con:
- `Background="{Binding Color}"` (igual que hoy).
- Un `TextBlock` con el título (`NoteTitleConverter`) dentro de un
  `Grid`/`ContentControl` con `LayoutTransform` de `RotateTransform`
  `Angle="-90"`, para que el texto corra de abajo hacia arriba a lo largo
  de la pestaña (convención habitual de "pestaña de carpeta" vertical).
- Un `MouseLeftButtonUp` (o `Button` sin chrome, reutilizando el patrón
  de `NoteTabButtonStyle` pero con la plantilla nueva) para mantener
  `OnTabClick` funcionando igual — la lógica de qué nota abrir no cambia,
  solo la plantilla visual.
- `Tag="{Binding}"` se mantiene, igual que hoy, para que `OnTabClick` siga
  extrayendo la `Note` igual.

Como el abanico ya no muestra archivadas/papelera, la `TextBlock` de
etiqueta de estado (`NoteStateLabelConverter`) que hoy vive en el mismo
`DataTemplate` se **quita** de aquí, y con ella la declaración de
`<local:NoteStateLabelConverter x:Key="NoteStateLabelConverter" />` en
`EdgeDockWindow.xaml.Resources` (queda sin uso en este fichero). El
convertidor en sí no se borra — sigue existiendo y usándose en
`NotesManagerWindow.xaml`, que no cambia.

### `EdgeDockWindow.xaml` — controles al final de la columna

`NewNoteButton`/`ToggleArchiveButton`/`ManageArchiveButton` (hoy una fila
de botones de texto/icono) se sustituyen por dos botones circulares
pequeños (mismo tamaño entre sí, ~28-32px de diámetro) apilados al final
de la columna de pestañas, dentro del mismo `ScrollViewer`/contenedor:
"+" (crear nota, mismo `OnNewNoteClick`) y el icono de engranaje (mismo
`OnManageArchiveClick`, ahora sin necesitar `_coordinator.OpenOrActivateNotesManager()`
condicionado a nada). `_viewingArchive`, `OnToggleArchiveClick`, y las
referencias a `NewNoteButton.Visibility`/`ToggleArchiveButton.Content` se
eliminan de `EdgeDockWindow.xaml.cs` — el abanico ya no tiene dos vistas.

### `EdgeDockWindow.xaml.cs` — animación de entrada escalonada

Cuando `TabsList.ItemsSource` cambia (dentro de `SetNotes`, que ya se
llama en cada `Refresh()`), tras la actualización del `ItemsSource`, hay
que esperar a que el `ItemsControl` genere los contenedores
(`ItemContainerGenerator.StatusChanged` o `Dispatcher.BeginInvoke` con
prioridad `Loaded`, patrón estándar de WPF para "hazlo después de que el
layout de los items exista") y, para cada contenedor generado en orden,
lanzar una animación de opacidad + `TranslateTransform` con
`BeginTime = TimeSpan.FromMilliseconds(45 * índice)`. Antes de lanzar
animaciones nuevas, limpiar (`BeginAnimation(dp, null)`) las que pudiera
haber de una generación anterior de items — mismo patrón defensivo que ya
existe para `PanelContent`/`PillSwatches` en `ApplyGeometry`, para que un
hover de entrada/salida rápido (que dispara `SetNotes`/`Refresh` varias
veces seguidas) no dispare animaciones huérfanas ni acumule handlers.

**Importante (lección de la Fase 3a)**: estas animaciones deben llevar
siempre `From` explícito (opacidad 0, traslación en el valor "escondida")
— nunca depender de que WPF mire el valor "actual" de la propiedad como
origen implícito, por el mismo motivo que causó el
`AnimationException: ... default origin value of 'NaN'` en
`EdgeDockWindow.ApplyGeometry`.

### `EdgeDockWindow.xaml.cs` — animación de apertura desde la pestaña

`OnTabClick` (o el nuevo handler equivalente en el `Border`) captura la
posición en pantalla del elemento pulsado antes de pedir al coordinador
que abra la nota:

```csharp
var tabElement = (FrameworkElement)sender;
var screenPosition = tabElement.PointToScreen(new Point(0, 0));
var tabRect = new Rect(screenPosition.X, screenPosition.Y, tabElement.ActualWidth, tabElement.ActualHeight);
_coordinator.OpenOrActivateNote(note, this, tabRect);
```

`AppCoordinator.OpenOrActivateNote` gana un parámetro `Rect originRect`
(en píxeles de pantalla/DIPs de este monitor — mismo espacio de
coordenadas que `Window.Left/Top`, ya que `PointToScreen` sobre un
elemento de una ventana devuelve coordenadas ya en el mismo sistema que
usa `Window.Left/Top` de esa ventana), que pasa a `NoteWindow` para que
anime su aparición desde ahí. Si `originRect` es nulo (no aplica, p. ej.
si en el futuro se abre una nota desde otro sitio que no sea un clic en
una pestaña), `NoteWindow` usa su comportamiento actual (aparecer
directamente en la posición final, sin animación de crecimiento).

`NoteWindow` necesita, tras `PositionNoteWindow` haberle fijado su
posición/tamaño final (`Left`/`Top`/`Width`/`Height` ya calculados por la
cascada existente), animar **desde** `originRect` hasta esos valores
finales, con `From`/`To` explícitos (mismo patrón que
`EdgeDockWindow.ApplyGeometry`) — probablemente en un método
`AnimateFromOrigin(Rect origin)` llamado justo antes de `Show()`.

### `AppCoordinator.cs`

`OpenOrActivateNote(Note note, EdgeDockWindow requestingDock)` gana un
tercer parámetro `Rect? originRect = null` (opcional, con valor por
defecto para no romper otras llamadas si las hubiera) y lo reenvía al
construir la `NoteWindow`.

## Manejo de errores

- Si el `ItemsControl` todavía no ha generado contenedores cuando se
  intenta lanzar la animación escalonada (p. ej. colapsar y volver a
  expandir muy rápido, antes de que termine la generación anterior), el
  código debe comprobar `ItemContainerGenerator.Status` y no asumir que
  el contenedor ya existe — si no existe, se salta esa pestaña sin
  animación de entrada en vez de lanzar una excepción de referencia nula.
- Ningún caso nuevo de fallo de arranque ni de repositorio — esto es
  puramente de presentación.

## Testing

Sin lógica nueva de `Fanote.Core` que testear — la posición de cada
pestaña es layout estándar de WPF, y la posición en pantalla para animar
la apertura se lee con `PointToScreen` en el momento del clic, sin
cálculo propio que aislar en una función pura. Se verifica a mano,
consistente con el resto de la mecánica de ventana de esta app.
Checklist manual sugerido:

1. Al pasar el ratón, las pestañas aparecen escalonadas en el tiempo
   (no todas de golpe), cada una deslizándose desde el borde con su
   propio color y la etiqueta de texto vertical legible.
2. Clic en una pestaña → la ventana de la nota parece crecer desde la
   posición de esa pestaña, no aparecer en un sitio genérico.
3. Con varias notas ya abiertas, la cascada de posicionamiento sigue
   evitando que se solapen exactamente (comportamiento ya existente, no
   debe romperse).
4. Con muchas notas (más de las que caben en `ExpandedMaxLength`), sigue
   habiendo scroll dentro del panel.
5. Colapsar y volver a expandir rápido varias veces seguidas no crashea
   ni dispara animaciones acumuladas/erráticas.
6. El botón "+" y el de engranaje funcionan igual que antes (crear nota,
   abrir "Gestionar notas").
7. El botón "Archivadas" ya no existe en el dock; entrar en "Gestionar
   notas" y filtrar por Archivadas/Papelera sigue funcionando como antes.

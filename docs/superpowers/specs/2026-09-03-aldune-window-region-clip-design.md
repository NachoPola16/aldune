# Aldune — Recorte de forma del panel desplegado con SetWindowRgn (design spec)

Quita el fondo oscuro rectangular que hoy queda visible en los "escalones"
entre pestañas de distinto ancho (una vez implementado el diseño de
pestañas escalonadas validado en maqueta — ver más abajo), recortando la
forma real del `HWND` del panel desplegado con `SetWindowRgn` para que el
escritorio se vea a través de esos huecos, sin usar `AllowsTransparency`
(descartado en la spec v1 por romper ClearType). Ver `docs/STATUS.md`,
sección "Diseño visual de las pestañas — validado en maqueta, PENDIENTE de
implementar en código", para el contexto de cómo surgió esto.

## Contexto y motivación

El rediseño de pestañas en abanico (`2026-09-02-aldune-fan-tabs-redesign-design.md`)
cambió la interacción pero dejó fuera de alcance explícitamente el ancho de
cada pestaña — hoy todas ocupan el ancho completo del panel
(`EdgeGeometry.ExpandedThickness=220`). Una maqueta visual externa, iterada en 4 rondas,
validó un ancho creciente por índice (`Width ≈ 32 + índice×14`) con
solape vertical, como un fajo de fichas escalonado — ese diseño **no es
parte de esta spec**: es trabajo bounded, previo y separado, que se
implementa directamente en `EdgeDockWindow.xaml`/`.xaml.cs` sin spec
formal (mismo patrón que otros retoques visuales de esta app), y esta
spec **asume que ya existe en el código** cuando se implemente lo de
aquí.

Con ese ancho variable en su sitio, el fondo del dock (`Background="#3A3A3A"`
en `EdgeDockWindow.xaml`) sigue siendo un rectángulo — ceñido al ancho de
la pestaña más ancha (eso ya lo resuelve el propio rediseño de ancho), pero
para las pestañas más estrechas queda un "escalón" de fondo oscuro visible
a su izquierda, entre el borde de esa pestaña y el borde del contenedor.
Esta spec quita ese escalón recortando la forma real de la ventana con
Win32, no con transparencia WPF.

## Alcance

**Dentro:**

- Recorte de forma (`SetWindowRgn`) del panel **desplegado** de
  `EdgeDockWindow` únicamente — union de un rectángulo-redondeado por
  pestaña (redondeado solo en el lado libre, cuadrado en el lado pegado
  al borde de pantalla) más un rectángulo plano para la fila de botones
  "+"/engranaje.
- Nueva función pura `Aldune.Core.TabRegionShape.BuildRegion(...)`
  (testeable con `dotnet test`, sin Win32) que calcula qué piezas
  (rectángulo + radio de esquina) forman la silueta, a partir de los
  rects de pestaña y del footer.
- Nuevo interop en `Aldune.Interop.NativeMethods`
  (`CreateRoundRectRgn`/`CreateRectRgn`/`CombineRgn`/`SetWindowRgn`/
  `DeleteObject`) y un punto de aplicación en `EdgeDockWindow` que
  construye el `HRGN` a partir de las piezas y lo aplica/quita en los
  momentos correctos de la animación existente (ver más abajo — sin
  hook nuevo de por-frame).
- Conversión DIP→píxeles físicos con `VisualTreeHelper.GetDpi(this)`
  (mismo patrón ya usado en `PollHoverState`/`OnTabClick`), por ventana
  — cada `EdgeDockWindow` tiene su propio DPI, coherente con Fase 3a.

**Fuera de alcance (no cambia):**

- El pill en reposo (`PillSwatches`) — sigue usando
  `NativeMethods.ApplyRoundedCornersAndShadow` (DWM), sin `SetWindowRgn`.
  No tiene pestañas separadas ni huecos reales que recortar, solo
  pastillas de color dibujadas sobre una franja sólida.
- La fila de botones "+"/engranaje (footer) se queda como un rectángulo
  plano de fondo — no se recorta a la silueta de los dos círculos.
  Decisión explícita: simplifica la región (no hace falta unir también
  dos elipses) y esa fila no tiene el patrón escalonado que motiva todo
  esto.
- El ancho creciente por índice de cada pestaña — trabajo previo,
  bounded, fuera de esta spec (ver "Contexto y motivación").
- `EdgeGeometry.PillRect`/`ExpandedRect`, el tamaño dinámico según nº de
  notas, y el resto de la geometría exterior del panel — sin cambios,
  esta spec solo afecta a qué parte de esa caja exterior es visible.
- Multi-edge: `App.xaml.cs` crea todos los `EdgeDockWindow` con
  `EdgePosition.Right` (hardcoded, sin UI para elegir otro borde
  todavía) — el cálculo de la silueta en esta spec asume pestañas
  colgando del lado izquierdo de un panel pegado al borde derecho de
  pantalla (redondeadas a la izquierda, cuadradas a la derecha). Si en
  el futuro se soporta otro `EdgePosition` de verdad, `BuildRegion`
  necesitará un parámetro de orientación — no se generaliza aquí sin un
  caso de uso real que lo pida (mismo criterio ya aplicado a
  `ScreenOrigin`/`"primary"` en Fase 3a).

## Componentes y cambios

### `Aldune.Core.TabRegionShape` (nuevo)

Función pura, sin Win32, testeable:

```csharp
namespace Aldune.Core;

public readonly record struct RegionPiece(Rect Bounds, double CornerRadius);

public static class TabRegionShape
{
    // cornerRadius: mismo radio para todas las piezas (9px en la maqueta) — no hace falta
    // variar por pestaña. Los rects ya vienen en el sistema de coordenadas que use el
    // llamador (esta función es agnóstica de DIP vs físicos, solo hace geometría).
    public static IReadOnlyList<RegionPiece> BuildRegion(
        IReadOnlyList<Rect> tabRects, Rect footerRect, double cornerRadius)
    {
        var pieces = new List<RegionPiece>(tabRects.Count + 1);
        foreach (var tab in tabRects)
        {
            pieces.Add(new RegionPiece(tab, cornerRadius));
        }
        pieces.Add(new RegionPiece(footerRect, 0)); // footer: rectángulo plano, ver Alcance
        return pieces;
    }
}
```

No hace de-dup ni combina rects — cada pieza se pasa tal cual al interop,
que ya sabe unirlas (`CombineRgn(RGN_OR)`) sin que a esta función pura le
importe cómo se combinan a nivel Win32. Deliberadamente simple: la única
lógica real es "una pieza redondeada por pestaña más una plana para el
footer", así que no hay mucho que testear más allá de recuento de piezas
y que el footer sale con radio 0 — pero se hace TDD igualmente por
consistencia con el resto de `Aldune.Core` y porque es el único sitio
donde se puede verificar esto sin lanzar la app real.

### `Aldune.Interop.NativeMethods` — nuevas declaraciones

```csharp
[DllImport("gdi32.dll")]
private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int cornerWidth, int cornerHeight);

[DllImport("gdi32.dll")]
private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

[DllImport("gdi32.dll")]
private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int combineMode);

[DllImport("gdi32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool DeleteObject(IntPtr hObject);

[DllImport("user32.dll")]
private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

private const int RGN_OR = 2;
```

Dos métodos internos nuevos, siguiendo el mismo estilo que
`ApplyRoundedCornersAndShadow`:

```csharp
/// <summary>
/// Recorta la forma visible de la ventana a la unión de las piezas dadas (coordenadas en
/// píxeles físicos, relativas a la esquina superior izquierda de la ventana). Solo el
/// lado izquierdo de cada pieza se redondea (CreateRoundRectRgn redondea las 4 esquinas por
/// igual — para tener solo 2 redondeadas, se une el RoundRect completo con un Rect plano que
/// cubre la mitad derecha de la pieza, que "cuadra" esas dos esquinas encima). Toma posesión
/// del último HRGN que le pasa a SetWindowRgn (Windows lo libera él solo); cualquier HRGN
/// intermedio que no llegue a eso se libera aquí mismo con DeleteObject.
/// </summary>
internal static void SetTabFanRegion(IntPtr hWnd, IReadOnlyList<Aldune.Core.RegionPiece> pieces)
{
    IntPtr accumulated = CreateRectRgn(0, 0, 0, 0);
    foreach (var (bounds, cornerRadius) in pieces)
    {
        int left = (int)bounds.X, top = (int)bounds.Y;
        int right = (int)(bounds.X + bounds.Width), bottom = (int)(bounds.Y + bounds.Height);

        IntPtr piece;
        if (cornerRadius > 0)
        {
            IntPtr rounded = CreateRoundRectRgn(left, top, right, bottom, (int)(cornerRadius * 2), (int)(cornerRadius * 2));
            IntPtr rightHalfSquared = CreateRectRgn(left + (right - left) / 2, top, right, bottom);
            piece = CreateRectRgn(0, 0, 0, 0);
            CombineRgn(piece, rounded, rightHalfSquared, RGN_OR);
            DeleteObject(rounded);
            DeleteObject(rightHalfSquared);
        }
        else
        {
            piece = CreateRectRgn(left, top, right, bottom);
        }

        CombineRgn(accumulated, accumulated, piece, RGN_OR);
        DeleteObject(piece);
    }

    SetWindowRgn(hWnd, accumulated, true); // el sistema pasa a poseer `accumulated` desde aquí
}

/// <summary>
/// Quita cualquier recorte de forma aplicado por SetTabFanRegion, devolviendo la ventana a su
/// rectángulo completo normal (necesario porque el pill en reposo no usa regiones).
/// </summary>
internal static void ClearWindowRegion(IntPtr hWnd) => SetWindowRgn(hWnd, IntPtr.Zero, true);
```

### `EdgeDockWindow.xaml.cs` — cuándo se aplica

**No hay hook nuevo de por-frame.** Se reutilizan los dos puntos que
`ApplyGeometry` ya tiene para alternar el contenido visible/invisible:

- **Al expandir**: la aparición del contenido (`fadeIn`, `BeginTime=120ms`,
  `Duration=80ms`, dentro de los 200ms totales de `ApplyGeometry`) es lo
  que hoy hace visible el panel. La región recortada tiene que estar
  puesta **antes de que empiece esa rampa de opacidad**, no cuando
  termina — si se aplicara al `Completed` de la animación (t=200ms, el
  mismo instante en que el contenido ya lleva un rato totalmente
  visible), se vería exactamente el "pop" que la opción per-frame evitaba
  y que ya se descartó al elegir este enfoque: contenido ya opaco dentro
  de un rectángulo, que de repente se recorta a la forma final. Puesta en
  t=120ms (el `BeginTime` del fade), la región ya tiene la forma correcta
  *antes* de que haya nada visible que mostrar mal recortado — un
  `DispatcherTimer` de un solo disparo con `Interval=120ms`, arrancado en
  el mismo momento en que se lanza `fadeIn`, hace ese trabajo (mismo
  patrón que `_collapseTimer`/`_hoverPollTimer`, ya `DispatcherTimer` en
  este fichero).
- **Al colapsar**: se quita la región inmediatamente y de forma síncrona
  (sin timer) en el mismo punto donde se decide `expanding == false` —
  no hace falta timing fino aquí, el pill nunca usó regiones así que
  cuanto antes se quite, mejor (evita que una región con forma de
  abanico, calculada para el tamaño expandido, se quede aplicada
  mientras la ventana se encoge hacia el tamaño de pill).
- **Con el panel ya expandido y cambia el nº de notas**: no hace falta un
  tercer punto de enganche. `SetNotes` ya llama a `ApplyGeometry()` en
  cada cambio, y `ApplyGeometry` no distingue "primera vez que se
  expande" de "ya estaba expandido, solo cambió el tamaño" — pasa
  siempre por la misma rama `expanding`, así que el mismo timer de
  120ms vuelve a dispararse y recalcula con los rects nuevos. (Esto
  simplifica el diseño respecto a lo comentado en la conversación: se
  pensó en un tercer punto de enganche aparte, pero mirando el código
  real, `ApplyGeometry` ya cubre ese caso con el mismo mecanismo de los
  otros dos.)
- **Accesibilidad (`!SystemParameters.ClientAreaAnimation`)**: esa rama
  ya hace un `return` temprano y fija `Opacity` directamente sin
  animación — ahí la región se aplica/quita de forma síncrona junto con
  esa asignación directa, sin pasar por el timer (no hay nada que
  temporizar si no hay animación).
- **Guarda de `hwnd`**: `ApplyGeometry()` se llama una vez desde el
  propio constructor, antes de que `SourceInitialized` haya disparado
  (por tanto antes de que exista un `hwnd` real) — el código nuevo
  necesita un campo `_hwnd` (capturado dentro del lambda de
  `SourceInitialized`, junto a las llamadas ya existentes a
  `NativeMethods.MakeNonActivating`/`ApplyRoundedCornersAndShadow`) y
  comprobar `_hwnd != IntPtr.Zero` antes de llamar a
  `SetTabFanRegion`/`ClearWindowRegion` — igual que esas dos llamadas ya
  existentes solo se hacen dentro de ese mismo lambda, nunca desde el
  constructor directamente.
- **Timer huérfano**: como con las animaciones de `Left`/`Top`/etc., un
  `SetNotes`/hover-in-hover-out rápido puede disparar `ApplyGeometry`
  varias veces seguidas antes de que el timer de 120ms de una llamada
  anterior llegue a disparar. Al principio de `ApplyGeometry`, junto a
  los `BeginAnimation(..., null)` ya existentes, se para
  (`_regionApplyTimer?.Stop()`) cualquier timer pendiente de una llamada
  previa, para que no aplique una región calculada con rects ya
  obsoletos por encima de una decisión más reciente (p. ej. una región
  de "expandido" aplicándose *después* de que ya se decidió colapsar).

Cómo se obtienen los rects para pasar a `TabRegionShape.BuildRegion`
(dentro del `Tick` del timer de 120ms, momento en que el layout de las
pestañas — ancho por índice, ya colocadas por `PlayTabEntrance` — está
resuelto):

```csharp
var dpi = VisualTreeHelper.GetDpi(this);
var tabRects = new List<Aldune.Core.Rect>();
for (int i = 0; i < TabsList.Items.Count; i++)
{
    if (TabsList.ItemContainerGenerator.ContainerFromIndex(i) is Button button)
    {
        var origin = button.TranslatePoint(new Point(0, 0), this); // DIP, relativo a esta ventana
        tabRects.Add(new Aldune.Core.Rect(
            origin.X * dpi.DpiScaleX, origin.Y * dpi.DpiScaleY,
            button.ActualWidth * dpi.DpiScaleX, button.ActualHeight * dpi.DpiScaleY));
    }
}
var footerOrigin = FooterPanel.TranslatePoint(new Point(0, 0), this);
var footerRect = new Aldune.Core.Rect(
    footerOrigin.X * dpi.DpiScaleX, footerOrigin.Y * dpi.DpiScaleY,
    FooterPanel.ActualWidth * dpi.DpiScaleX, FooterPanel.ActualHeight * dpi.DpiScaleY);

var pieces = TabRegionShape.BuildRegion(tabRects, footerRect, cornerRadius: 9 * dpi.DpiScaleX);
NativeMethods.SetTabFanRegion(_hwnd, pieces);
```

(`FooterPanel` es el `x:Name` que hay que darle al `StackPanel` de los
botones "+"/engranaje en `EdgeDockWindow.xaml`, que hoy no tiene nombre.)

`TranslatePoint` a la propia ventana (no `PointToScreen`, que es lo que
usa `OnTabClick` para el punto de partida de la animación de apertura de
nota) porque `SetWindowRgn` quiere coordenadas relativas al origen de la
ventana, no de la pantalla — son necesidades distintas aunque ambas
partan del mismo elemento.

## Manejo de errores

- Sin casos nuevos de fallo de arranque ni de repositorio — esto es
  puramente de presentación, igual que el rediseño de pestañas.
- Si `ContainerFromIndex` devuelve `null` para algún índice (mismo caso
  ya contemplado en `ApplyGeometry` para la animación de entrada — el
  `ItemsControl` no ha terminado de generar contenedores), esa pestaña
  se salta al construir `tabRects` en vez de lanzar una excepción; la
  región sale con una pieza menos para ese frame, se corrige sola en el
  próximo `ApplyGeometry`.
- Verificación manual pendiente (no se puede resolver solo razonando
  sobre el código): si la sombra nativa de Windows 11
  (`DwmExtendFrameIntoClientArea`, ya aplicada una vez en
  `SourceInitialized` y sin motivo para tocarse aquí) sigue el contorno
  real del `HRGN` recortado o solo la caja rectangular completa. Si se
  ve mal (sombra "flotando" fuera del contorno visible), la salida de
  emergencia es no aplicar `ApplyRoundedCornersAndShadow` al panel
  expandido — la caja de emergencia ya existe como patrón (esta app ya
  oculta/revela contenido en otros tramos de la animación).

## Testing

- `TabRegionShape.BuildRegionTests` (TDD, `Aldune.Core.Tests` o el
  proyecto de test que corresponda): 0 pestañas (solo footer), 1
  pestaña, N pestañas, que cada pestaña sale con el `cornerRadius` dado
  y el footer con 0.
- Todo lo demás (el `HRGN` real, `SetWindowRgn`, su efecto visual) no es
  testeable por `dotnet test`, igual que el resto del código Win32/DWM
  de esta app. Checklist manual sugerido:
  1. Con varias notas de distinto ancho de pestaña, al expandir se ve el
     escritorio en los escalones entre pestañas (no fondo oscuro).
  2. Un clic con el ratón exactamente en uno de esos huecos pasa al
     escritorio/ventana de detrás, no al dock (hit-testing sigue la
     forma recortada).
  3. El sondeo de hover (`PollHoverState`, cada 50ms) sigue detectando
     correctamente cuándo el ratón está "dentro" del panel aunque parte
     de su rectángulo exterior ya no sea clicable — usa el rectángulo
     completo (`Left`/`Top`/`Width`/`Height`), no la forma recortada, así
     que no debería cambiar de comportamiento; confirmar que colapsar al
     salir por un hueco sigue funcionando igual que salir por una
     pestaña.
  4. Colapsar y volver a expandir rápido varias veces seguidas no deja
     la región a medias ni con forma de una llamada anterior.
  5. Crear/archivar una nota con el panel ya expandido recalcula la
     forma sin glitches.
  6. Con los dos monitores del usuario (DPI distintos) la forma recortada
     encaja con las pestañas reales en ambos, no solo en el principal.
  7. Sombra nativa: comprobar a simple vista si sigue el contorno
     recortado o no (ver "Manejo de errores").

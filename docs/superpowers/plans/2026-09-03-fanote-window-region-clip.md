# Recorte de forma del panel desplegado con SetWindowRgn — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quitar el fondo oscuro que hoy queda visible en los huecos entre
pestañas de distinto ancho (y en el hueco a la izquierda de la pestaña más
ancha) recortando la forma real del `HWND` del panel desplegado con
`SetWindowRgn`, para que se vea el escritorio ahí en vez de un rectángulo
oscuro — sin `AllowsTransparency` (rompe ClearType, descartado en la spec
v1).

**Architecture:** Una función pura en `Fanote.Core` calcula qué piezas
(rectángulo + radio de esquina) forman la silueta del abanico a partir de
los rects de pestaña/footer ya conocidos por WPF; un interop nuevo en
`Fanote.Interop.NativeMethods` construye el `HRGN` real (Win32
`CreateRoundRectRgn`/`CombineRgn`/`SetWindowRgn`) a partir de esas piezas;
`EdgeDockWindow` llama a ese interop en los dos puntos que su animación de
expandir/colapsar ya tiene para alternar contenido visible/invisible — no
se añade ningún hook nuevo de por-frame.

**Tech Stack:** .NET 10 / WPF, P/Invoke a `gdi32.dll`/`user32.dll`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-03-fanote-window-region-clip-design.md`

## Global Constraints

- Nunca `AllowsTransparency="True"` en ninguna ventana — rompe ClearType
  (spec v1). Todo el recorte de forma va por `SetWindowRgn`, no por WPF.
- `SetWindowRgn` recibe coordenadas en **píxeles físicos**, relativas a la
  esquina superior izquierda de la ventana — conversión DIP→físicos vía
  `VisualTreeHelper.GetDpi(this)`, igual que ya hace `PollHoverState`.
- Solo el panel **desplegado** usa `SetWindowRgn`. El pill en reposo sigue
  con `NativeMethods.ApplyRoundedCornersAndShadow` (DWM), sin cambios.
- El footer ("+"/engranaje) no se recorta a la silueta de los botones —
  se queda como pieza rectangular plana (`CornerRadius=0`).
- Esta spec asume `EdgePosition.Right` (lo único que `App.xaml.cs` crea
  hoy) — pestañas ancladas a la derecha, redondeadas a la izquierda.
- Recorte aplicado en **un solo recálculo por transición**, sincronizado
  con el `BeginTime=120ms` del fundido de entrada que `ApplyGeometry` ya
  tiene — nunca un hook de `CompositionTarget.Rendering` por frame.

---

## Task 1: `TabRegionShape` — cálculo puro de la silueta (Fanote.Core, TDD)

**Files:**
- Create: `src/Fanote.Core/TabRegionShape.cs`
- Test: `tests/Fanote.Core.Tests/TabRegionShapeTests.cs`

**Interfaces:**
- Produces: `Fanote.Core.RegionPiece` (record struct: `Rect Bounds`,
  `double CornerRadius`) y `Fanote.Core.TabRegionShape.BuildRegion(
  IReadOnlyList<Rect> tabRects, Rect footerRect, double cornerRadius)
  -> IReadOnlyList<RegionPiece>` — los usa `NativeMethods.SetTabFanRegion`
  en la Task 2 y `EdgeDockWindow` en la Task 3.

- [ ] **Step 1: Escribir los tests (fallarán porque `TabRegionShape` no existe aún)**

```csharp
using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class TabRegionShapeTests
{
    private static readonly Rect FooterRect = new(0, 300, 116, 80);

    [Fact]
    public void BuildRegion_NoTabs_ReturnsOnlyTheFooterPiece()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), FooterRect, cornerRadius: 9);

        var piece = Assert.Single(pieces);
        Assert.Equal(FooterRect, piece.Bounds);
        Assert.Equal(0, piece.CornerRadius);
    }

    [Fact]
    public void BuildRegion_OneTab_ReturnsTabPieceThenFooterPiece()
    {
        var tab = new Rect(0, 0, 32, 80);

        var pieces = TabRegionShape.BuildRegion(new[] { tab }, FooterRect, cornerRadius: 9);

        Assert.Equal(2, pieces.Count);
        Assert.Equal(tab, pieces[0].Bounds);
        Assert.Equal(9, pieces[0].CornerRadius);
        Assert.Equal(FooterRect, pieces[1].Bounds);
        Assert.Equal(0, pieces[1].CornerRadius);
    }

    [Fact]
    public void BuildRegion_MultipleTabs_EachTabGetsTheGivenCornerRadius()
    {
        var tabs = new[]
        {
            new Rect(0, 0, 32, 80),
            new Rect(0, 56, 46, 80),
            new Rect(0, 112, 60, 80),
        };

        var pieces = TabRegionShape.BuildRegion(tabs, FooterRect, cornerRadius: 12);

        Assert.Equal(4, pieces.Count); // 3 pestañas + footer
        for (int i = 0; i < tabs.Length; i++)
        {
            Assert.Equal(tabs[i], pieces[i].Bounds);
            Assert.Equal(12, pieces[i].CornerRadius);
        }
        Assert.Equal(0, pieces[^1].CornerRadius);
    }

    [Fact]
    public void BuildRegion_FooterPiece_AlwaysHasZeroCornerRadius()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), FooterRect, cornerRadius: 999);

        Assert.Equal(0, Assert.Single(pieces).CornerRadius);
    }
}
```

- [ ] **Step 2: Confirmar que fallan por falta del tipo**

Run: `dotnet test --filter TabRegionShapeTests`
Expected: FAIL — error de compilación, `TabRegionShape`/`RegionPiece` no existen en `Fanote.Core`.

- [ ] **Step 3: Implementar `TabRegionShape`**

```csharp
namespace Fanote.Core;

public readonly record struct RegionPiece(Rect Bounds, double CornerRadius);

// Deliberadamente sin combinar ni deduplicar rects — cada pieza se pasa tal cual al interop
// Win32 (NativeMethods.SetTabFanRegion), que ya sabe unirlas con CombineRgn sin que a esta
// función pura le importe cómo se combinan a nivel de sistema operativo.
public static class TabRegionShape
{
    public static IReadOnlyList<RegionPiece> BuildRegion(
        IReadOnlyList<Rect> tabRects, Rect footerRect, double cornerRadius)
    {
        var pieces = new List<RegionPiece>(tabRects.Count + 1);
        foreach (var tab in tabRects)
        {
            pieces.Add(new RegionPiece(tab, cornerRadius));
        }
        pieces.Add(new RegionPiece(footerRect, 0)); // footer: rectángulo plano, ver spec "Alcance"
        return pieces;
    }
}
```

- [ ] **Step 4: Confirmar que pasan**

Run: `dotnet test --filter TabRegionShapeTests`
Expected: PASS — 4/4.

- [ ] **Step 5: Correr toda la suite y comprobar que no se rompió nada**

Run: `dotnet test`
Expected: PASS — 84/84 (los 80 ya existentes + estos 4 nuevos).

- [ ] **Step 6: Commit**

```bash
git add src/Fanote.Core/TabRegionShape.cs tests/Fanote.Core.Tests/TabRegionShapeTests.cs
git commit -m "Add TabRegionShape: pure computation of the fan-shaped region's pieces"
```

---

## Task 2: Interop Win32 — `SetTabFanRegion`/`ClearWindowRegion`

**Files:**
- Modify: `src/Fanote/Interop/NativeMethods.cs`

**Interfaces:**
- Consumes: `Fanote.Core.RegionPiece` (Task 1).
- Produces: `NativeMethods.SetTabFanRegion(IntPtr hWnd, IReadOnlyList<RegionPiece> pieces)` y
  `NativeMethods.ClearWindowRegion(IntPtr hWnd)` — los usa `EdgeDockWindow` en la Task 3.

No hay test automático posible aquí (igual que el resto del interop Win32/DWM
de este fichero) — se verifica con un build limpio; el comportamiento real
se comprueba al cablearlo en la Task 3 y en el checklist manual de la
Task 4.

- [ ] **Step 1: Añadir las declaraciones P/Invoke y los dos métodos, al final de la clase (antes de la última llave de cierre)**

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

/// <summary>
/// Recorta la forma visible de <paramref name="hWnd"/> a la unión de las piezas dadas
/// (coordenadas en píxeles físicos, relativas a la esquina superior izquierda de la ventana).
/// CreateRoundRectRgn redondea las 4 esquinas por igual, así que una pieza con radio > 0 se
/// construye como la unión de un RoundRect completo con un Rect plano que cubre su mitad
/// derecha — eso "cuadra" las dos esquinas de la derecha encima, dejando solo las de la
/// izquierda redondeadas (el lado libre de cada pestaña, lejos del borde físico de pantalla;
/// ver EdgeDockWindow y la spec de este recorte). El HRGN final que llega a SetWindowRgn pasa a
/// ser propiedad del sistema (se libera solo, al reemplazarlo o cerrar la ventana) — cualquier
/// HRGN intermedio que no llegue ahí se libera aquí mismo con DeleteObject.
/// </summary>
internal static void SetTabFanRegion(IntPtr hWnd, IReadOnlyList<Fanote.Core.RegionPiece> pieces)
{
    IntPtr accumulated = CreateRectRgn(0, 0, 0, 0);
    foreach (var (bounds, cornerRadius) in pieces)
    {
        int left = (int)bounds.X;
        int top = (int)bounds.Y;
        int right = (int)(bounds.X + bounds.Width);
        int bottom = (int)(bounds.Y + bounds.Height);

        IntPtr piece;
        if (cornerRadius > 0)
        {
            int diameter = (int)(cornerRadius * 2);
            IntPtr rounded = CreateRoundRectRgn(left, top, right, bottom, diameter, diameter);
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

    SetWindowRgn(hWnd, accumulated, true);
}

/// <summary>
/// Quita cualquier recorte de forma aplicado por SetTabFanRegion, devolviendo la ventana a su
/// rectángulo completo normal — necesario porque el pill en reposo no usa regiones.
/// </summary>
internal static void ClearWindowRegion(IntPtr hWnd) => SetWindowRgn(hWnd, IntPtr.Zero, true);
```

- [ ] **Step 2: Añadir el using que falta**

Al principio de `NativeMethods.cs`, junto a los `using` ya existentes, añadir:

```csharp
using Fanote.Core;
```

- [ ] **Step 3: Build limpio**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errores (los 4 warnings `CA1416` preexistentes de `DatabaseKeyProvider` no cuentan).

- [ ] **Step 4: Commit**

```bash
git add src/Fanote/Interop/NativeMethods.cs
git commit -m "Add SetTabFanRegion/ClearWindowRegion Win32 interop"
```

---

## Task 3: Cablear el recorte en `EdgeDockWindow`

**Files:**
- Modify: `src/Fanote/Windowing/EdgeDockWindow.xaml.cs`

**Interfaces:**
- Consumes: `TabRegionShape.BuildRegion` (Task 1),
  `NativeMethods.SetTabFanRegion`/`ClearWindowRegion` (Task 2).
- Depende de `FooterPanel` (el `x:Name` del `StackPanel` de los botones
  "+"/engranaje) — ya añadido al `EdgeDockWindow.xaml` en el commit
  `a793dbe` (paso bounded previo, ancho de pestaña por índice).

**Nota sobre por qué no hace falta un tercer punto de enganche**: `SetNotes`
ya llama a `ApplyGeometry()` en cada cambio de notas, y `ApplyGeometry` no
distingue "primera vez que se expande" de "ya estaba expandido, solo
cambió el nº de notas" — pasa siempre por la misma rama `expanding`. Cablear
el recorte ahí cubre automáticamente crear/archivar una nota con el panel
ya abierto, sin código aparte.

- [ ] **Step 1: Añadir los campos nuevos y capturar `_hwnd`**

En la sección de campos, junto a `_currentRect`:

```csharp
private IntPtr _hwnd;
private readonly DispatcherTimer _regionApplyTimer;
```

En el constructor, junto a la construcción de `_collapseTimer` (antes de
`ApplyGeometry();` al final del constructor):

```csharp
// Recorta la forma del panel desplegado exactamente cuando su contenido empieza a hacerse
// visible (el fadeIn de ApplyGeometry tiene BeginTime=120ms) — no antes, ni al terminar la
// animación entera. Si se aplicara al terminar (Completed, t=200ms), el contenido ya llevaría
// un rato totalmente visible dentro de un rectángulo sin recortar, y el recorte final se vería
// como un "pop" — aplicado en el instante en que Opacity empieza a subir desde 0, en cambio,
// no hay nada visible todavía que se vea mal recortado. Si el BeginTime del fadeIn cambia
// alguna vez, este Interval tiene que moverse con él.
_regionApplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
_regionApplyTimer.Tick += (_, _) =>
{
    _regionApplyTimer.Stop();
    ApplyTabFanRegion();
};
```

Y modificar el handler de `SourceInitialized` para guardar el `hwnd` en el campo en vez de una variable local:

```csharp
SourceInitialized += (_, _) =>
{
    _hwnd = new WindowInteropHelper(this).Handle;
    NativeMethods.MakeNonActivating(_hwnd);
    NativeMethods.ApplyRoundedCornersAndShadow(_hwnd);
};
```

- [ ] **Step 2: Añadir los métodos privados que calculan y aplican/quitan la región**

Justo debajo de `ApplyGeometry`:

```csharp
private void ApplyTabFanRegion()
{
    if (_hwnd == IntPtr.Zero) return;

    var dpi = VisualTreeHelper.GetDpi(this);
    var tabRects = new List<Fanote.Core.Rect>();
    for (int i = 0; i < TabsList.Items.Count; i++)
    {
        if (TabsList.ItemContainerGenerator.ContainerFromIndex(i) is Button button)
        {
            var origin = button.TranslatePoint(new Point(0, 0), this);
            tabRects.Add(new Fanote.Core.Rect(
                origin.X * dpi.DpiScaleX, origin.Y * dpi.DpiScaleY,
                button.ActualWidth * dpi.DpiScaleX, button.ActualHeight * dpi.DpiScaleY));
        }
    }

    var footerOrigin = FooterPanel.TranslatePoint(new Point(0, 0), this);
    var footerRect = new Fanote.Core.Rect(
        footerOrigin.X * dpi.DpiScaleX, footerOrigin.Y * dpi.DpiScaleY,
        FooterPanel.ActualWidth * dpi.DpiScaleX, FooterPanel.ActualHeight * dpi.DpiScaleY);

    var pieces = TabRegionShape.BuildRegion(tabRects, footerRect, cornerRadius: 9 * dpi.DpiScaleX);
    NativeMethods.SetTabFanRegion(_hwnd, pieces);
}

private void ClearTabFanRegion()
{
    if (_hwnd == IntPtr.Zero) return;
    NativeMethods.ClearWindowRegion(_hwnd);
}
```

- [ ] **Step 3: Llamar a estos métodos desde `ApplyGeometry`**

Al principio de `ApplyGeometry`, junto a los `BeginAnimation(..., null)` ya
existentes (misma defensa: que un hover rápido no deje un cálculo de
región obsoleto pendiente de disparar por encima de una decisión más
reciente):

```csharp
_regionApplyTimer.Stop();
```

En la rama `!SystemParameters.ClientAreaAnimation`, justo antes de
`_currentRect = rect; return;`:

```csharp
if (expanding) ApplyTabFanRegion(); else ClearTabFanRegion();
```

Al final del método (después de las líneas que arrancan `fadeIn`/`fadeOut`
en `PanelContent`/`PillSwatches`):

```csharp
if (expanding)
{
    _regionApplyTimer.Start();
}
else
{
    ClearTabFanRegion();
}
```

- [ ] **Step 4: Build y arranque manual como humo (sin excepciones)**

Run: `dotnet build`
Expected: `Build succeeded`.

Run: `dotnet run --project src/Fanote` (lanzar y cerrar a los pocos
segundos, o `Ctrl+C` desde la terminal)
Expected: arranca sin excepciones, igual que antes de este cambio — la
verificación visual completa es la Task 4.

- [ ] **Step 5: Correr toda la suite**

Run: `dotnet test`
Expected: PASS — 84/84 (sin tests nuevos en esta tarea, solo confirmar que no se rompió nada de `Fanote.Core`).

- [ ] **Step 6: Commit**

```bash
git add src/Fanote/Windowing/EdgeDockWindow.xaml.cs
git commit -m "Clip the expanded dock panel's shape to its tabs via SetWindowRgn"
```

---

## Task 4: Verificación manual (checklist humano)

No automatizable — mismo patrón que el resto de la mecánica de ventana de
esta app. `dotnet run --project src/Fanote` y comprobar a mano:

- [ ] Con varias notas de distinto ancho de pestaña, al expandir se ve el
  escritorio en los escalones entre pestañas y en el hueco a la izquierda
  de la más ancha — no fondo oscuro.
- [ ] Un clic exactamente en uno de esos huecos pasa al escritorio/ventana
  de detrás, no al dock (el hit-testing sigue la forma recortada).
- [ ] El sondeo de hover (`PollHoverState`, cada 50ms) sigue detectando
  bien cuándo el ratón está "dentro" del panel aunque parte de su
  rectángulo exterior ya no sea clicable — colapsar al salir por un hueco
  funciona igual que salir por encima de una pestaña.
- [ ] Colapsar y volver a expandir rápido varias veces seguidas no deja la
  región a medias ni con forma de una llamada anterior.
- [ ] Crear o archivar una nota con el panel ya expandido recalcula la
  forma sin glitches visibles.
- [ ] Con los dos monitores del usuario (DPI distintos) la forma recortada
  encaja con las pestañas reales en ambos, no solo en el principal.
- [ ] Sombra nativa de Windows 11 (`DwmExtendFrameIntoClientArea`, ya
  aplicada en `SourceInitialized`): comprobar a simple vista si sigue el
  contorno recortado o se queda como una caja rectangular — no es
  bloqueante (ver spec, "Manejo de errores"), pero hay que verlo antes de
  fusionar. Si se ve mal, la salida de emergencia documentada en la spec
  es no aplicar la sombra al panel expandido.

Si algo de esto falla, arreglarlo antes de fusionar esta rama a `master`
(no forma parte de las tareas anteriores porque son bugs a diagnosticar,
no pasos previstos).

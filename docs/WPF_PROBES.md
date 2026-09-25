# Sondas de WPF

Una **sonda** es un programa de consola desechable que abre ventanas reales de Aldune con datos
temporales, las maneja (clics y teclas físicos, reflexión sobre miembros privados) y guarda capturas.
Sirve para comprobar lo que los tests de `Aldune.Core` no ven: la interfaz. Va **fuera del
repositorio** (una carpeta temporal) y se tira al terminar; si una comprobación merece quedarse, se pasa
a `tests/Aldune.Ui.SmokeTests`.

## Proyecto

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>CA1416</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <!-- Ruta absoluta al repositorio -->
    <EmbeddedResource Include="C:\ruta\aldune\src\Aldune\App.xaml" LogicalName="Aldune.App.xaml" />
    <ProjectReference Include="C:\ruta\aldune\src\Aldune\Aldune.csproj" />
  </ItemGroup>
</Project>
```

## Esqueleto

```csharp
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Windowing;
using Microsoft.Data.Sqlite;
using Rect = System.Windows.Rect; // Aldune.Core también tiene Rect

internal static class Program
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static int Main()
    {
        // Base de datos TEMPORAL: nunca %LOCALAPPDATA%\Aldune, que son las notas reales.
        var dir = Path.Combine(Path.GetTempPath(), "AldunePrueba", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LoadAppResources(app);
        using var cipher = new ContentCipher(RandomNumberGenerator.GetBytes(32));
        var repo = new NotesRepository(new NotesDatabase(Path.Combine(dir, "p.db")), cipher);
        var settings = new AppSettings();
        var coord = new AppCoordinator(repo, settings);
        int failures = 0;

        var note = repo.Create("Título\n☐ tarea", "#EBD38B", "primary");
        var w = new NoteWindow(note, repo, coord, settings) { Left = 300, Top = 200 };
        w.Show(); w.Activate();
        Pump(500);

        // ... comprobaciones: if (!ok) failures++; Console.WriteLine(...)

        w.Close(); coord.Dispose(); app.Shutdown();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(failures == 0 ? "RESULT: all OK" : $"RESULT: {failures} FAIL");
        return failures;
    }

    // Deja correr el dispatcher: animaciones, temporizadores y layout.
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    // Los estilos de producción sin instanciar Aldune.App (su arranque abriría los datos reales).
    // x:Shared solo vale compilado y XamlReader lo rechaza: se quita.
    static void LoadAppResources(Application app)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("Aldune.App.xaml")!;
        var root = System.Xml.Linq.XDocument.Load(stream).Root!;
        var res = root.Element(root.Name.Namespace + "Application.Resources")!;
        foreach (var a in res.Descendants().SelectMany(e => e.Attributes())
                     .Where(a => a.Name.LocalName == "Shared").ToList()) a.Remove();
        var dict = new System.Xml.Linq.XElement(root.Name.Namespace + "ResourceDictionary",
            root.Attributes().Where(a => a.IsNamespaceDeclaration), res.Elements());
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dict.ToString());
    }

    // Captura de un elemento a PNG. El fondo de una ventana lo pone la Window, no su Content:
    // pasarlo en "background" o la captura sale con huecos blancos.
    static void Save(FrameworkElement element, string path, string? background)
    {
        element.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(element);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var r = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            if (background is not null) dc.DrawRectangle((Brush)new BrushConverter().ConvertFromString(background)!, null, r);
            dc.DrawRectangle(new VisualBrush(element), null, r);
        }
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
```

## Clics y teclas físicos

```csharp
[System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
[System.Runtime.InteropServices.DllImport("user32.dll")] static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
[System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

// Clic izquierdo en un punto de pantalla (PointToScreen ya devuelve píxeles físicos).
SetCursorPos((int)p.X, (int)p.Y); Pump(120);
mouse_event(0x02, 0, 0, 0, UIntPtr.Zero); Pump(60); mouse_event(0x04, 0, 0, 0, UIntPtr.Zero); Pump(250);
// Tecla: keybd_event(vk, 0, 0, …) y keybd_event(vk, 0, 2, …) para soltarla.
```

## Lo que se ha aprendido (evita repetir errores)

- **Las teclas van a la ventana que esté en primer plano.** Antes de enviar teclas, activar la ventana
  de la prueba y comprobar que tiene el foco. El dock es `WS_EX_NOACTIVATE`: lo que se teclea con él
  "delante" acaba en la aplicación del usuario (le pasó a esta guía: se escribió en el editor).
- **Eventos enrutados ≠ clic real.** `RaiseEvent(ClickEvent)` no pasa por la captura del ratón de un
  `Popup` con `StaysOpen=False`; el bug del "⋯" solo se reproducía con `mouse_event`.
- **Miembros privados por reflexión** (`GetField("_fanState", Priv)`). Si un nombre es ambiguo con uno
  de WPF (`Render`), pasar la firma: `GetMethod("Render", Priv, Type.EmptyTypes)`.
- **El monitor ficticio en (100000, 100000)** saca las ventanas de la vista del usuario; para `Popup`
  hace falta un monitor real (WPF los encaja en una pantalla existente).
- **`GetRectFromCharacterIndex` devuelve el rectángulo del cursor (ancho 0)**: el ancho de un carácter
  es la diferencia con el siguiente.
- **Ver la sonda fallar sin el arreglo** (quitándolo un momento) antes de dar el arreglo por bueno.

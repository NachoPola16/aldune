# WPF probes

A **probe** is a throwaway console program that opens real Aldune windows with temporary data, drives them (physical clicks and keys, reflection on private
members) and saves screenshots. It is used to check what the `Aldune.Core` tests do not see: the interface. It lives **outside the
repository** (a temporary folder) and is thrown away when done; if a check deserves to stay, it moves
to `tests/Aldune.Ui.SmokeTests`.

## Project

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
    <!-- Absolute path to the repository -->
    <EmbeddedResource Include="C:\path\aldune\src\Aldune\App.xaml" LogicalName="Aldune.App.xaml" />
    <ProjectReference Include="C:\path\aldune\src\Aldune\Aldune.csproj" />
  </ItemGroup>
</Project>
```

## Skeleton

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
using Rect = System.Windows.Rect; // Aldune.Core also has a Rect

internal static class Program
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    static int Main()
    {
        // TEMPORARY database: never %LOCALAPPDATA%\Aldune, which holds the real notes.
        var dir = Path.Combine(Path.GetTempPath(), "AldunePrueba", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LoadAppResources(app);
        using var cipher = new ContentCipher(RandomNumberGenerator.GetBytes(32));
        var repo = new NotesRepository(new NotesDatabase(Path.Combine(dir, "p.db")), cipher);
        var settings = new AppSettings();
        var coord = new AppCoordinator(repo, settings);
        int failures = 0;

        var note = repo.Create("Title\n☐ task", "#EBD38B", "primary");
        var w = new NoteWindow(note, repo, coord, settings) { Left = 300, Top = 200 };
        w.Show(); w.Activate();
        Pump(500);

        // ... checks: if (!ok) failures++; Console.WriteLine(...)

        w.Close(); coord.Dispose(); app.Shutdown();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(failures == 0 ? "RESULT: all OK" : $"RESULT: {failures} FAIL");
        return failures;
    }

    // Lets the dispatcher run: animations, timers and layout.
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    // The production styles without instantiating Aldune.App (its startup would open the real data).
    // x:Shared only works compiled and XamlReader rejects it: it is removed.
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

    // Capture of an element to PNG. A window's background is set by the Window, not by its Content:
    // pass it in "background" or the capture comes out with white gaps.
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

## Physical clicks and keys

```csharp
[System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
[System.Runtime.InteropServices.DllImport("user32.dll")] static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
[System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

// Left click at a screen point (PointToScreen already returns physical pixels).
SetCursorPos((int)p.X, (int)p.Y); Pump(120);
mouse_event(0x02, 0, 0, 0, UIntPtr.Zero); Pump(60); mouse_event(0x04, 0, 0, 0, UIntPtr.Zero); Pump(250);
// Key: keybd_event(vk, 0, 0, …) and keybd_event(vk, 0, 2, …) to release it.
```

## What has been learned (avoid repeating mistakes)

- **Keys go to the window that is in the foreground.** Before sending keys, activate the test window
  and check that it has focus. The dock is `WS_EX_NOACTIVATE`: what is typed with it
  “in front” ends up in the user's application (it happened to this guide: it was typed into the editor).
- **Routed events ≠ a real click.** `RaiseEvent(ClickEvent)` does not go through the mouse capture of a
  `Popup` with `StaysOpen=False`; the “⋯” bug only reproduced with `mouse_event`.
- **Private members by reflection** (`GetField("_fanState", Priv)`). If a name is ambiguous with one
  from WPF (`Render`), pass the signature: `GetMethod("Render", Priv, Type.EmptyTypes)`.
- **The fictitious monitor at (100000, 100000)** takes windows out of the user's sight; for `Popup`
  a real monitor is needed (WPF fits them to an existing screen).
- **`GetRectFromCharacterIndex` returns the cursor rectangle (width 0)**: the width of a character
  is the difference with the next one.
- **See the probe fail without the fix** (removing it for a moment) before accepting the fix as good.
- **Do not touch the mouse or keyboard while a probe or the smoke test runs**: they drive the real cursor and keys, and any intervention fails a
  test that is not at fault (a Core test failed once because of this).

## Learned on 2026-10-05 (flicker, sharpness and sizes)

- **Measuring a flicker with screenshots**: a separate thread runs `Graphics.CopyFromScreen` in a loop over the window (at 240 Hz it gives
  ~240 fps with a small region) while the interface thread presses keys with `keybd_event`. Per frame the following are measured: the
  fraction of pixels of the face color, pure white or black pixels, the row of the first ink stroke (the text
  shifts) and the mean luminance of the header and the edges. With that a flicker in the note was ruled out on this PC.
  `AllowUnsafeBlocks` is needed if pixels are walked with pointers. **Count the lines before and after**: sometimes the keys
  do not reach the window (focus) and the test looks good without having tested anything.
- **`VisualTreeHelper.SetRootDpi` does not simulate a different scale**: the measurements come out identical. For sharpness at 125% or 150% it has
  to be seen on a real monitor; what can be checked is that the window is born with `TextFormattingMode=Display` and
  `UseLayoutRounding`.
- **An implicit `Window` style in `App.xaml` only matches the exact type**, not subclasses (Settings, dialogs...). The
  properties that must reach all windows go in the default style of `App.OnStartup` (see the comment there). That includes the default
  `FontSize`: without it, text that does not set its own size does not follow “Text size”.
- **Testing position and size restoration**: `AppCoordinator.TryRestorePlacement` is private but is called by reflection with
  a real note and a temporary database; `repo.SavePlacement` beforehand and looking at `Left/Top/Width/Height` is enough.
- **`GetRectFromCharacterIndex` with `LineStackingStrategy=BlockLineHeight`** returns the glyph height, not the 21 px
  line height; the click area of the checkboxes stays fitted to its glyph and it was checked with a screenshot of the highlight.
- **A white strip when resizing** is measured with a black background behind (another black `Window`) and the note alternating between two
  heights every 12 ms while a thread captures the screen: each frame with white pixels is the strip. With
  `_hasManualSize = true` by reflection, so that auto-fit does not return the note to its height. Save the bad frame in a PNG and look at it: that is where
  it was seen that the new area was white and not the face color. The color of the unpainted area
  is set by `DWMWA_CAPTION_COLOR`; `DwmGetWindowAttribute` cannot read it (E_INVALIDARG), so it is only tested with screenshots.

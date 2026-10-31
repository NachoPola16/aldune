using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Windowing;
using Microsoft.Data.Sqlite;

namespace Aldune.Ui.SmokeTests;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Aldune.Ui.SmokeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppCoordinator? coordinator = null;
        EdgeDockWindow? dock = null;
        var result = 0;
        try
        {
            LoadAppResources(app);
            // La paleta del chrome no está en App.xaml: la pone ThemeManager, como hace la app al arrancar.
            ThemeManager.Apply(app, light: false);
            using var cipher = new ContentCipher(RandomNumberGenerator.GetBytes(32));
            var repository = new NotesRepository(new NotesDatabase(Path.Combine(directory, "smoke.db")), cipher);
            var settings = new AppSettings { KeepDockOpen = false, DockView = DockViewKind.Active };
            coordinator = new AppCoordinator(repository, settings);
            var tagged = repository.Create("Smoke tagged note", "#F7E6A3", "SMOKE");
            repository.SetTags(tagged.Id, ["smoke-tag"]);
            repository.Create("Smoke untagged note", "#F7E6A3", "SMOKE");
            var monitor = new MonitorInfo("SMOKE", new WorkingArea(100000, 100000, 1920, 1080), 1, false);
            dock = new EdgeDockWindow(EdgePosition.Right, monitor, repository, coordinator, settings)
            {
                ShowActivated = false,
                ShowInTaskbar = false
            };
            coordinator.RegisterDock(dock);
            coordinator.RefreshAll();
            dock.Show();
            // Fullscreen detection depends on the user's foreground application, not this regression.
            Field<DispatcherTimer>(dock, "_fullscreenPollTimer").Stop();
            Pump(TimeSpan.FromMilliseconds(150));
            Check(Field<int>(dock, "_noteCount") == 2, "Initial repository refresh contains two notes");
            RunDockRound24(dock, settings, coordinator, repository);
            RunTagSelection(dock, settings);
            // The two scenarios below take the real mouse, grab the real screen and activate note windows (they steal keyboard focus):
            // ALDUNE_SMOKE_QUIET=1 skips them so the suite can run while someone is working at the PC.
            if (Environment.GetEnvironmentVariable("ALDUNE_SMOKE_QUIET") == "1")
                Console.WriteLine("SKIP: note grow flash and note menu toggle (physical mouse, screen and focus) in quiet mode");
            else
            {
                RunNoteGrowFlash(repository, coordinator, settings);
                RunNoteMenuToggle(repository, coordinator, settings);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            result = 1;
        }
        finally
        {
            try
            {
                coordinator?.CloseAllDocks();
                coordinator?.Dispose();
                if (dock is not null)
                    foreach (var field in typeof(EdgeDockWindow).GetFields(PrivateInstance))
                        if (field.GetValue(dock) is DispatcherTimer timer)
                        {
                            timer.Stop();
                            Check(!timer.IsEnabled, $"Cleanup stopped {field.Name}");
                        }
                app.Shutdown();
                SqliteConnection.ClearAllPools();
                Directory.Delete(directory, recursive: true);
                Check(!Directory.Exists(directory), "Temporary database directory removed");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Cleanup failed: {exception}");
                result = 1;
            }
        }
        return result;
    }

    private static void RunTagSelection(EdgeDockWindow dock, AppSettings settings)
    {
        var popup = (Popup)dock.FindName("DockViewPopup");
        // WPF clamps popups to real monitors. Retain HWND/events without flashing a visible menu.
        popup.Child.Opacity = 0;
        var opened = 0;
        var closed = 0;
        popup.Opened += (_, _) => opened++;
        popup.Closed += (_, _) => closed++;
        Call(dock, "OpenDockViewPopup");
        Pump(TimeSpan.FromMilliseconds(100));
        Check(popup.IsOpen && opened == 1, "Real DockViewPopup opened and raised Opened");
        var openingDeadline = Field<DateTime>(dock, "_interactionGraceUntil");
        var choices = (StackPanel)dock.FindName("DockTagItems");
        var choice = choices.Children.OfType<Button>().Single(button => Equals(button.Tag, "smoke-tag"));
        var beforeClick = DateTime.UtcNow;
        choice.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, choice));
        var afterClick = DateTime.UtcNow;
        var deadline = Field<DateTime>(dock, "_interactionGraceUntil");
        Check(settings.DockView == DockViewKind.Tag && settings.DockTagFilter == "smoke-tag",
            "Generated tag button invokes real handler and changes coordinator view");
        Check(Field<int>(dock, "_noteCount") == 1, "Filtering refresh changes geometry from two notes to one");
        Check(deadline >= beforeClick.AddSeconds(4) && deadline <= afterClick.AddSeconds(4),
            "Selection arms exactly four seconds of grace");
        Check(deadline > openingDeadline, "Selection renews the opening grace deadline");
        Pump(TimeSpan.FromMilliseconds(200));
        Check(!popup.IsOpen && closed == 1, "Popup closes normally and raises real Closed event");
        Check(Field<DateTime>(dock, "_interactionGraceUntil") == deadline,
            "Closed preserves selection grace deadline (regression cause)");
        AssertHeld(dock, "after Closed");
        var samples = 0;
        while (DateTime.UtcNow < deadline.AddMilliseconds(-300))
        {
            AssertHeld(dock, "during grace", quiet: true);
            samples++;
            Pump(TimeSpan.FromMilliseconds(40));
        }
        Check(samples >= 20, $"Observed {samples} dispatcher samples during real grace");
        AssertHeld(dock, "near expiry");
        // Al caducar, la cortesía cuenta como uso: el cierre espera el largo (EngagedCloseDelay).
        Pump(deadline - DateTime.UtcNow + DockHoverTuning.EngagedCloseDelay - TimeSpan.FromMilliseconds(150));
        Check(Field<FanStateMachine>(dock, "_fanState").IsExpanded,
            "After grace, the dock waits the engaged close delay instead of collapsing at once");
        Pump(TimeSpan.FromMilliseconds(500));
        Check(!Field<FanStateMachine>(dock, "_fanState").IsExpanded,
            "Real hover polling collapses after expiry with cursor outside");
        Console.WriteLine("PASS: dock tag selection/Closed/grace/expiry regression scenario");
    }

    /// <summary>
    /// Round 2.4.0 on the dock: accent ring on the cogwheel for a filtered view, the view label's fixed placement, the wheel session that keeps
    /// the window size and then releases it, and the note's closing ghost. No mouse involved; the state is restored for the next scenario.
    /// </summary>
    private static void RunDockRound24(EdgeDockWindow dock, AppSettings settings, AppCoordinator coordinator, NotesRepository repository)
    {
        // A filtered view keeps its label up while the fan is open (the cue that the dock is filtered); «Active» does not.
        settings.KeepDockOpen = true; // keeps the fan open while the hover poll runs with the cursor elsewhere
        Pump(TimeSpan.FromMilliseconds(400));
        coordinator.SetDockView(DockViewKind.Tag, "smoke-tag"); // a view with notes: the label is not shown over an empty strip
        Pump(TimeSpan.FromMilliseconds(1700));
        Check(typeof(EdgeDockWindow).GetField("_viewLabelPopup", PrivateInstance)!.GetValue(dock) is Popup { IsOpen: true }, "The view label stays up (past its 1.1 s) while the dock shows a filtered view with the fan open");
        coordinator.SetDockView(DockViewKind.Active, null);
        Pump(TimeSpan.FromMilliseconds(1700));
        Check(typeof(EdgeDockWindow).GetField("_viewLabelPopup", PrivateInstance)!.GetValue(dock) is not Popup { IsOpen: true }, "The view label goes away on the Active view");
        // The mode decides when the label is fixed: «always» also in Active, «only when I change» never. A click on the fixed label opens the view menu.
        Popup? Label() => typeof(EdgeDockWindow).GetField("_viewLabelPopup", PrivateInstance)!.GetValue(dock) as Popup;
        settings.DockViewLabel = DockViewLabelMode.Always;
        coordinator.RefreshAll();
        Pump(TimeSpan.FromMilliseconds(1700));
        Check(Label() is { IsOpen: true }, "In the «always» mode the view label is fixed in Active too");
        var labelBorder = (Border)Label()!.Child;
        Check(labelBorder.IsHitTestVisible, "The fixed view label is clickable");
        labelBorder.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        Pump(TimeSpan.FromMilliseconds(200));
        Check(((Popup)dock.FindName("DockViewPopup")).IsOpen, "Clicking the fixed view label opens the view menu");
        ((Popup)dock.FindName("DockViewPopup")).IsOpen = false;
        settings.DockViewLabel = DockViewLabelMode.WhenChanging;
        coordinator.SetDockView(DockViewKind.Tag, "smoke-tag");
        Pump(TimeSpan.FromMilliseconds(1700));
        Check(Label() is not { IsOpen: true }, "In the «only when I change» mode the label is not fixed even on a filtered view");
        settings.DockViewLabel = DockViewLabelMode.WhenFiltered;
        coordinator.SetDockView(DockViewKind.Active, null);
        settings.KeepDockOpen = false;

        // View label: custom placement, centred on the notes column at the free end (above for the right edge, below for the top edge).
        typeof(EdgeDockWindow).GetMethod("ShowViewLabel", PrivateInstance)!.Invoke(dock, ["smoke"]);
        var popup = Field<Popup>(dock, "_viewLabelPopup");
        Check(popup.Placement == PlacementMode.Custom && ReferenceEquals(popup.PlacementTarget, dock.FindName("TabsScroll")),
            "View label uses a custom placement on the notes column");
        var places = popup.CustomPopupPlacementCallback!(new Size(100, 30), new Size(200, 300), default);
        Check(places.Length == 1 && Math.Abs(places[0].Point.X - 50) < 0.01 && places[0].Point.Y < 0,
            "View label is centred over the column and above it on the right edge");
        popup.IsOpen = false;

        // Wheel session: while the wheel keeps turning a wheel hook (limited to the area the dock covered) feeds the view change, and nothing
        // is painted on the dock window (an almost transparent background rendered as black bands on some systems).
        typeof(EdgeDockWindow).GetMethod("CycleDockView", PrivateInstance)!.Invoke(dock, [1, false]);
        Check(Field<IntPtr>(dock, "_wheelHook") != IntPtr.Zero, "A wheel session installs the wheel hook");
        Check(dock.Background is not System.Windows.Media.SolidColorBrush { Color.A: > 0 }, "The dock window paints nothing extra during a wheel session");
        Pump(TimeSpan.FromMilliseconds(1900));
        Check(Field<IntPtr>(dock, "_wheelHook") == IntPtr.Zero, "The wheel session ends and removes the wheel hook");
        coordinator.SetDockView(DockViewKind.Active, null);
        Pump(TimeSpan.FromMilliseconds(150));

        // Closing ghost: the note can be photographed (background + content), which is what lets the whole window fade.
        var note = repository.Create("Smoke ghost note", "#F7E6A3", "SMOKE");
        var window = new NoteWindow(note, repository, coordinator, settings) { Left = 300, Top = 200, ShowActivated = false };
        window.Show();
        Pump(TimeSpan.FromMilliseconds(400));
        var capture = typeof(NoteWindow).Assembly.GetType("Aldune.Windowing.ClosingGhost")!
            .GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [window]);
        Check(capture is System.Windows.Media.Imaging.BitmapSource { PixelWidth: > 100, PixelHeight: > 100 }, "The note can be photographed for the closing ghost");
        window.Close();
        Pump(TimeSpan.FromMilliseconds(500));
        Check(!window.IsLoaded, "The closing animation ends and the note window is gone");
        // «What's new»: every catalog entry composes into a non-empty text with its heading, in the current language.
        var whatsNew = typeof(NoteWindow).Assembly.GetType("Aldune.Resources.WhatsNew")!;
        var versions = (IEnumerable<Version>)whatsNew.GetProperty("Versions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var compose = whatsNew.GetMethod("Compose", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var version in versions)
        {
            var text = (string)compose.Invoke(null, [new[] { version }])!;
            Check(text.Contains(version.ToString(3)) && text.Contains("•"), $"What's new has text for {version.ToString(3)}");
        }

        Console.WriteLine("PASS: round 2.4.0 dock scenario");
    }

    /// <summary>
    /// El "⋯" de la nota con clics físicos (SendInput): abrir, volver a pulsar "⋯" para cerrar,
    /// reabrir y cerrar pulsando fuera. Este fallo volvió tres veces porque se probaba con eventos
    /// enrutados, que no pasan por la captura del popup; solo un clic real lo reproduce (ver
    /// PopupToggle). Mueve el ratón: no tocarlo mientras corre.
    /// </summary>
    private static void RunNoteMenuToggle(NotesRepository repository, AppCoordinator coordinator, AppSettings settings)
    {
        var note = repository.Create("Smoke menu note", "#F7E6A3", "SMOKE");
        var window = new NoteWindow(note, repository, coordinator, settings) { Left = 300, Top = 200 };
        window.Show();
        window.Activate();
        Pump(TimeSpan.FromMilliseconds(600));
        try
        {
            var button = (Button)window.FindName("MenuButton");
            var popup = (Popup)window.FindName("ActionsPopup");
            var center = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));

            ClickAt(center);
            Check(popup.IsOpen, "Physical click on the note's menu button opens the menu");
            ClickAt(center);
            Check(!popup.IsOpen, "Clicking the menu button again closes the menu (does not reopen)");
            ClickAt(center);
            Check(popup.IsOpen, "A third click reopens the menu");

            var bottomRight = window.PointToScreen(new Point(window.ActualWidth - 30, window.ActualHeight - 20));
            ClickAt(bottomRight);
            Check(!popup.IsOpen, "Clicking outside the menu closes it");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Al crecer la nota (Enter, arrastrar el borde) la zona nueva no puede salir blanca. DWM la compone
    /// con el color del marco hasta que WPF pinta, y por defecto es blanco con el tema claro: era el
    /// destello en portátiles. Se mide como en la sonda de 2026-10-05: una hebra captura la pantalla
    /// mientras la nota crece y encoge sobre un fondo negro, y se cuentan los fotogramas con blanco
    /// (sin el arreglo, ~25 % de ellos). Mueve nada con el ratón, pero necesita la pantalla visible.
    /// </summary>
    private static void RunNoteGrowFlash(NotesRepository repository, AppCoordinator coordinator, AppSettings settings)
    {
        var back = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.Black,
            Left = 200, Top = 100, Width = 700, Height = 800, ShowInTaskbar = false };
        back.Show();
        var note = repository.Create("Smoke grow note", "#F7E6A3", "SMOKE");
        var window = new NoteWindow(note, repository, coordinator, settings) { Left = 300, Top = 200 };
        window.Show();
        Pump(TimeSpan.FromMilliseconds(800));
        try
        {
            // Sin autoajuste: que el alto lo mande esta prueba y no el contenido (vacío, volvería a 320).
            typeof(NoteWindow).GetField("_hasManualSize", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            double scale = System.Windows.PresentationSource.FromVisual(window).CompositionTarget.TransformToDevice.M11;
            int x0 = (int)((window.Left + 14) * scale), w = (int)((window.Width - 28) * scale);
            int y0 = (int)((window.Top + 330) * scale), h = (int)(170 * scale);
            int frames = 0, whiteFrames = 0;
            bool stop = false;
            var capture = new System.Threading.Thread(() =>
            {
                using var bitmap = new System.Drawing.Bitmap(w, h);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                while (!stop)
                {
                    graphics.CopyFromScreen(x0, y0, 0, 0, bitmap.Size);
                    int white = 0;
                    for (int y = 4; y < h; y += 12)
                        for (int x = 4; x < w; x += 12)
                        {
                            var c = bitmap.GetPixel(x, y);
                            if (c.R > 235 && c.G > 235 && c.B > 235) white++;
                        }
                    frames++;
                    if (white > 3) whiteFrames++;
                }
            });
            capture.Start();
            for (int i = 0; i < 120; i++)
            {
                window.Height = i % 2 == 0 ? 520 : 320;
                Pump(TimeSpan.FromMilliseconds(12));
            }
            stop = true;
            capture.Join();
            Check(frames > 50, $"Grow probe captured enough frames ({frames})");
            Check(whiteFrames == 0, $"No white strip while the note grows ({whiteFrames} of {frames} frames had white)");
        }
        finally
        {
            window.Close();
            back.Close();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    private static void ClickAt(Point screen)
    {
        const uint leftDown = 0x02, leftUp = 0x04;
        SetCursorPos((int)screen.X, (int)screen.Y);
        Pump(TimeSpan.FromMilliseconds(120));
        mouse_event(leftDown, 0, 0, 0, UIntPtr.Zero);
        Pump(TimeSpan.FromMilliseconds(60));
        mouse_event(leftUp, 0, 0, 0, UIntPtr.Zero);
        Pump(TimeSpan.FromMilliseconds(700));
    }

    private static void AssertHeld(EdgeDockWindow dock, string phase, bool quiet = false)
    {
        Check(Field<FanStateMachine>(dock, "_fanState").IsExpanded, $"Dock expanded {phase}", quiet);
        Check(Field<DockHoverPolicy>(dock, "_hover").IsPointerInside, $"Hover policy held {phase}", quiet);
    }

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, PrivateInstance)?.GetValue(target)
            ?? throw new MissingFieldException(target.GetType().Name, name));

    private static void Call(object target, string name) =>
        (target.GetType().GetMethod(name, PrivateInstance)
            ?? throw new MissingMethodException(target.GetType().Name, name)).Invoke(target, null);

    private static void Check(bool condition, string message, bool quiet = false)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
        if (!quiet) Console.WriteLine($"PASS: {message}");
    }

    private static void Pump(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = duration };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
    }

    // Embed the production App.xaml and parse its resources, never instantiate production App.
    private static void LoadAppResources(Application app)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("Aldune.App.xaml")
            ?? throw new InvalidOperationException("Missing embedded production App.xaml");
        var document = System.Xml.Linq.XDocument.Load(stream);
        var root = document.Root!;
        var resources = root.Element(root.Name.Namespace + "Application.Resources")!;
        // x:Shared solo vale en diccionarios compilados y XamlReader.Parse lo rechaza. Aquí se quita:
        // solo lo usa el glifo del autoscroll (AutoScrollOriginGlyph), que esta prueba no pinta.
        foreach (var shared in resources.Descendants().SelectMany(element => element.Attributes())
                     .Where(attribute => attribute.Name.LocalName == "Shared").ToList())
        {
            shared.Remove();
        }
        var dictionary = new System.Xml.Linq.XElement(root.Name.Namespace + "ResourceDictionary",
            root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration), resources.Elements());
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
    }
}

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
            RunTagSelection(dock, settings);
            RunNoteMenuToggle(repository, coordinator, settings);
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
        Pump(deadline - DateTime.UtcNow + TimeSpan.FromMilliseconds(500));
        Check(!Field<FanStateMachine>(dock, "_fanState").IsExpanded,
            "Real hover polling and collapse timer collapse after expiry with cursor outside");
        Check(!Field<DispatcherTimer>(dock, "_collapseTimer").IsEnabled, "Collapse timer stops after firing");
        Console.WriteLine("PASS: dock tag selection/Closed/grace/expiry regression scenario");
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
        Check(!Field<DispatcherTimer>(dock, "_collapseTimer").IsEnabled, $"Collapse timer stopped {phase}", quiet);
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

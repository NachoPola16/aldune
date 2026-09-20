using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Aldune;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// El icono de Aldune en la bandeja del sistema.
///
/// Antes de esto la app no tenía forma de cerrarse ni de configurarse: se lanzaba a mano y se
/// cerraba matando el proceso. Un dock sin ventana propia necesita algún sitio donde vivir, y la
/// bandeja es el sitio convenido en Windows para exactamente eso.
///
/// Usa <c>System.Windows.Forms.NotifyIcon</c> (de ahí <c>UseWindowsForms</c> en el csproj). WPF no
/// trae nada equivalente, y el envoltorio de WinForms es notablemente menos código que hablar con
/// <c>Shell_NotifyIcon</c> por P/Invoke.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AppCoordinator _coordinator;

    /// <summary>El NotifyIcon que ya crea esta clase, reutilizado por ReminderScheduler para avisar
    /// de recordatorios — un segundo icono de bandeja sería confuso.</summary>
    internal NotifyIcon Icon => _icon;

    internal TrayIcon(AppCoordinator coordinator, Action? checkForUpdates = null)
    {
        _coordinator = coordinator;

        var menu = new ContextMenuStrip
        {
            // El renderer por defecto de WinForms es gris claro y con bordes de otra epoca. Con
            // uno propio el menu usa los mismos tonos que el dock y el gestor.
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            // ShowImageMargin TIENE que quedarse en true: en WinForms la marca de verificación se
            // dibuja precisamente en ese margen, así que apagarlo (que era lo que había, por
            // estética) dejaba "Abrir al iniciar sesión" funcionando a ciegas — se activaba de
            // verdad, pero sin nada que lo dijera.
            ShowImageMargin = true,
            BackColor = Ground,
            ForeColor = Ink
        };
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayNewNote, null, (_, _) => _coordinator.CreateAndOpenNote()));
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayManageNotes, null, (_, _) => _coordinator.OpenNotesManager()));
        menu.Items.Add(new ToolStripSeparator());

        // Ocultar el dock. Es la salida para las pantallas completas que Windows no reporta como
        // tales (vídeo a pantalla completa en el navegador), donde el dock se queda encima. Va con
        // marca de verificación invertida — la fila se llama "Ocultar el dock" y sale marcada
        // mientras está oculto, que es el estado que acaba de provocar el clic.
        var hideDockItem = new ToolStripMenuItem(Strings.TrayToggleDock) { CheckOnClick = false };
        hideDockItem.Click += (_, _) => _coordinator.ToggleDocksVisible();
        menu.Items.Add(hideDockItem);
        // La marca se refresca al abrir el menú en vez de por evento: es gratis, y así no hay forma
        // de que se quede desincronizada (el atajo global también oculta el dock, sin pasar por aquí).
        menu.Opening += (_, _) => hideDockItem.Checked = !_coordinator.DocksVisible;
        menu.Items.Add(new ToolStripSeparator());
        // "Ajustes…" en vez del interruptor de arranque suelto: ya son dos ajustes (arranque y
        // atajo global) y van a ser mas, y un menu contextual no es sitio para configurar nada.
        menu.Items.Add(new ToolStripMenuItem(Strings.TraySettings, null, (_, _) => _coordinator.OpenSettings()));
        if (checkForUpdates is not null)
            menu.Items.Add(new ToolStripMenuItem(Strings.TrayCheckForUpdates, null, (_, _) => checkForUpdates()));
        menu.Items.Add(new ToolStripSeparator());
        // Shutdown de WPF, no Application.Exit de WinForms: el bucle de mensajes lo lleva WPF, y
        // ademas "Application" seria ambiguo entre los dos espacios de nombres.
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayExit, null,
            (_, _) => System.Windows.Application.Current.Shutdown()));

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = AppInfo.DisplayVersion,
            Visible = true,
            ContextMenuStrip = menu
        };

        // Doble clic al icono abre el gestor, que es la única ventana "de verdad" de la app.
        _icon.DoubleClick += (_, _) => _coordinator.OpenNotesManager();
    }

    private static readonly Color Ground = ColorTranslator.FromHtml("#2A261F");
    private static readonly Color Ink = ColorTranslator.FromHtml("#EDE7DC");

    /// <summary>Paleta del menu, a juego con el resto de la app.</summary>
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Ground;
        // Hover de las filas: sin degradado (los gradientes del renderer por defecto son los que
        // hacen que un item "encendido" parezca de otro tema) y con el mismo tono que usa el dock
        // para sus menús (#4E4840). El borde del item activo se mantiene en el mismo tono para que
        // el resalte se lea como relleno y no como una caja con borde.
        public override Color MenuItemSelected => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuItemSelectedGradientBegin => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuItemSelectedGradientEnd => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuBorder => ColorTranslator.FromHtml("#3C3730");
        public override Color ImageMarginGradientBegin => Ground;
        public override Color ImageMarginGradientMiddle => Ground;
        public override Color ImageMarginGradientEnd => Ground;
        public override Color SeparatorDark => ColorTranslator.FromHtml("#3C3730");
        public override Color SeparatorLight => ColorTranslator.FromHtml("#3C3730");
        // El hover de una fila marcada (Checked) pinta encima con estos dos: sin ellos, la marca de
        // verificación sobre "Ocultar el dock" se leía sobre el gris del tema de sistema.
        public override Color CheckBackground => ColorTranslator.FromHtml("#4E4840");
        public override Color CheckSelectedBackground => ColorTranslator.FromHtml("#4E4840");
        public override Color ButtonSelectedHighlight => ColorTranslator.FromHtml("#4E4840");
        public override Color ButtonSelectedGradientBegin => ColorTranslator.FromHtml("#4E4840");
        public override Color ButtonSelectedGradientEnd => ColorTranslator.FromHtml("#4E4840");
        public override Color ButtonSelectedBorder => Color.Transparent;
    }

    private static System.Drawing.Icon LoadIcon()
    {
        // El icono viaja incrustado en el ejecutable (ApplicationIcon en el csproj), así que se
        // saca de ahí en vez de depender de que exista un fichero suelto junto al .exe.
        var path = Environment.ProcessPath;
        if (path is not null)
        {
            var extracted = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (extracted is not null) return extracted;
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        // Sin esto el icono se queda "fantasma" en la bandeja hasta que pasas el ratón por encima:
        // el shell no sabe que el proceso murió hasta que intenta hablar con él.
        _icon.Visible = false;
        _icon.Dispose();
    }
}

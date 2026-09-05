using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Fanote.Core;

namespace Fanote.Windowing;

/// <summary>
/// El icono de Fanote en la bandeja del sistema.
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
    private readonly ToolStripMenuItem _startupItem;
    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;

    internal TrayIcon(NotesRepository repository, AppCoordinator coordinator)
    {
        _repository = repository;
        _coordinator = coordinator;

        _startupItem = new ToolStripMenuItem("Abrir al iniciar sesión")
        {
            CheckOnClick = false, // se marca desde el estado real del registro, no del clic
            Checked = StartupRegistration.IsEnabled()
        };
        _startupItem.Click += (_, _) =>
        {
            // Se refleja lo que de verdad quedó guardado, no lo que se pidió: si el registro está
            // restringido por directiva, la marca no debe mentir.
            _startupItem.Checked = StartupRegistration.SetEnabled(!_startupItem.Checked);
        };

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
        menu.Items.Add(new ToolStripMenuItem("Nueva nota", null, (_, _) => CreateNote()));
        menu.Items.Add(new ToolStripMenuItem("Gestionar notas…", null, (_, _) => _coordinator.OpenNotesManager()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        // Shutdown de WPF, no Application.Exit de WinForms: el bucle de mensajes lo lleva WPF, y
        // ademas "Application" seria ambiguo entre los dos espacios de nombres.
        menu.Items.Add(new ToolStripMenuItem("Salir", null,
            (_, _) => System.Windows.Application.Current.Shutdown()));

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Fanote",
            Visible = true,
            ContextMenuStrip = menu
        };

        // Se relee el registro cada vez que se abre el menú, no solo al crearlo: el arranque
        // automático también se puede quitar desde Administrador de tareas > Inicio, y sin esto el
        // menú seguiría enseñándolo marcado.
        menu.Opening += (_, _) => _startupItem.Checked = StartupRegistration.IsEnabled();

        // Doble clic al icono abre el gestor, que es la única ventana "de verdad" de la app.
        _icon.DoubleClick += (_, _) => _coordinator.OpenNotesManager();
    }

    private static readonly Color Ground = ColorTranslator.FromHtml("#2A261F");
    private static readonly Color Ink = ColorTranslator.FromHtml("#EDE7DC");

    /// <summary>Paleta del menu, a juego con el resto de la app.</summary>
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Ground;
        public override Color MenuItemSelected => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuItemSelectedGradientBegin => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuItemSelectedGradientEnd => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuItemBorder => ColorTranslator.FromHtml("#4E4840");
        public override Color MenuBorder => ColorTranslator.FromHtml("#3C3730");
        public override Color ImageMarginGradientBegin => Ground;
        public override Color ImageMarginGradientMiddle => Ground;
        public override Color ImageMarginGradientEnd => Ground;
        public override Color SeparatorDark => ColorTranslator.FromHtml("#3C3730");
        public override Color SeparatorLight => ColorTranslator.FromHtml("#3C3730");
    }

    private static Icon LoadIcon()
    {
        // El icono viaja incrustado en el ejecutable (ApplicationIcon en el csproj), así que se
        // saca de ahí en vez de depender de que exista un fichero suelto junto al .exe.
        var path = Environment.ProcessPath;
        if (path is not null)
        {
            var extracted = Icon.ExtractAssociatedIcon(path);
            if (extracted is not null) return extracted;
        }
        return SystemIcons.Application;
    }

    private void CreateNote()
    {
        var existing = _repository.GetByState(NoteState.Active).Count;
        var color = NoteColorPalette.Colors[existing % NoteColorPalette.Colors.Length];
        _repository.Create(string.Empty, color, screenOrigin: "primary");
        _coordinator.RefreshAll();
    }

    public void Dispose()
    {
        // Sin esto el icono se queda "fantasma" en la bandeja hasta que pasas el ratón por encima:
        // el shell no sabe que el proceso murió hasta que intenta hablar con él.
        _icon.Visible = false;
        _icon.Dispose();
    }
}

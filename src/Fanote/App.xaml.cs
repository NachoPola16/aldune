using System.Windows;
using Fanote.Core;
using Fanote.Windowing;

namespace Fanote;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var area = SystemParameters.WorkArea;
        var workingArea = new WorkingArea(area.Left, area.Top, area.Width, area.Height);

        var dock = new EdgeDockWindow(EdgePosition.Right, workingArea);
        dock.SetNotes(new[]
        {
            new NoteModel { Id = Guid.NewGuid(), Text = "Primera nota de prueba", Color = "#F5E3B3" },
            new NoteModel { Id = Guid.NewGuid(), Text = "Segunda nota", Color = "#C9E4DE" },
            new NoteModel { Id = Guid.NewGuid(), Text = "Tercera nota con más texto para probar el ajuste", Color = "#F2C6DE" },
        });
        dock.Show();
    }
}

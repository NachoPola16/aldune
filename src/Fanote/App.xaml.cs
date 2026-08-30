using System.Configuration;
using System.Data;
using System.Windows;

namespace Fanote;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var area = SystemParameters.WorkArea;
        var workingArea = new Fanote.Core.WorkingArea(area.Left, area.Top, area.Width, area.Height);
        new Fanote.Windowing.EdgeDockWindow(Fanote.Core.EdgePosition.Right, workingArea).Show();
    }
}


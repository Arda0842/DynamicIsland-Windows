using System.Configuration;
using System.Data;
using System.Windows;

namespace DynamicIsland;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var current = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("DynamicIsland"))
            {
                if (p.Id != current.Id)
                {
                    try { p.Kill(); } catch { }
                }
            }
        }
        catch { }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                System.IO.File.WriteAllText("crash.log", args.ExceptionObject.ToString());
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                System.IO.File.WriteAllText("crash.log", args.Exception.ToString());
            }
            catch { }
            args.Handled = true;
        };
    }
}


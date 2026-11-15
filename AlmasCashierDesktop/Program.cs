using System;
using System.IO;
using System.Windows;

namespace AlmasCashierDesktop;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        if (args.Length > 0 && args[0] == "--smoke-test")
        {
            try { WpfDashboard.SmokeTest(args.Length > 1 ? args[1] : null); return 0; }
            catch (Exception e) { File.WriteAllText("wpf-smoke-error.txt", e.ToString()); return 1; }
        }
        return app.Run(new WpfDashboard());
    }
}

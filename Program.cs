using System;
using System.Windows.Forms;

namespace AlmasCashierDesktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CashierWindow());
    }
}

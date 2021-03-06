using Microsoft.Extensions.DependencyInjection;
using Remotely.ScreenCast.Core;
using Remotely.ScreenCast.Core.Services;
using Remotely.Shared.Utilities;
using Remotely.Shared.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Remotely.Desktop.Win
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Write(e.Exception);
            MessageBox.Show("There was an unhandled exception.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (Environment.GetCommandLineArgs().Contains("-elevate"))
            {
                var commandLine = Win32Interop.GetCommandLine().Replace(" -elevate", "");

                Logger.Write($"Elevating process {commandLine}.");
                var result = Win32Interop.OpenInteractiveProcess(
                    commandLine,
                    -1,
                    false,
                    "default",
                    true,
                    out var procInfo);
                Logger.Write($"Elevate result: {result}. Process ID: {procInfo.dwProcessId}.");
                Environment.Exit(0);
            }
        }
    }
}

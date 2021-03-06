using Remotely.Shared.Models;
using Remotely.ScreenCast.Core;
using Remotely.ScreenCast.Core.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Remotely.Shared.Win32;
using System.Threading;
using Remotely.ScreenCast.Win.Services;
using Remotely.ScreenCast.Core.Interfaces;
using Remotely.ScreenCast.Core.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.ScreenCast.Core.Models;
using Remotely.Shared.Utilities;

namespace Remotely.ScreenCast.Win
{
    public class Program
	{
        private static Conductor Conductor { get; set; }
        private static CasterSocket CasterSocket { get; set; }
        private static ICursorIconWatcher CursorIconWatcher { get; set; }
        private static IServiceProvider Services => ServiceContainer.Instance;
        public static async void CursorIconWatcher_OnChange(object sender, CursorInfo cursor)
        {
            if (Conductor?.Viewers?.Count > 0)
            {
                foreach (var viewer in Conductor.Viewers.Values)
                {
                    await viewer.SendCursorChange(cursor);
                }
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                BuildServices();

                Conductor = Services.GetRequiredService<Conductor>();
                CasterSocket = Services.GetRequiredService<CasterSocket>();
                Conductor.ProcessArgs(Environment.GetCommandLineArgs().SkipWhile(x => !x.StartsWith("-")).ToArray());

                if (Conductor.Mode == Core.Enums.AppMode.Chat)
                {
                    Services.GetRequiredService<ChatHostService>().StartChat(Conductor.RequesterID, Conductor.OrganizationName).Wait();
                }
                else
                {
                    Task.Run(StartScreenCasting);
                }

                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Write(ex);
                throw;
            }
        }

        private static void BuildServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConsole().AddDebug().AddEventLog();
            });

            serviceCollection.AddSingleton<ICursorIconWatcher, CursorIconWatcherWin>();
            serviceCollection.AddSingleton<IScreenCaster, ScreenCaster>();
            serviceCollection.AddSingleton<IKeyboardMouseInput, KeyboardMouseInputWin>();
            serviceCollection.AddSingleton<IClipboardService, ClipboardServiceWin>();
            serviceCollection.AddSingleton<IAudioCapturer, AudioCapturerWin>();
            serviceCollection.AddSingleton<CasterSocket>();
            serviceCollection.AddSingleton<IdleTimer>();
            serviceCollection.AddSingleton<Conductor>();
            serviceCollection.AddSingleton<ChatHostService>();
            serviceCollection.AddTransient<IScreenCapturer, ScreenCapturerWin>();
            serviceCollection.AddTransient<Viewer>();
            serviceCollection.AddScoped<IWebRtcSessionFactory, WebRtcSessionFactory>();
            serviceCollection.AddScoped<IFileTransferService, FileTransferService>();

            ServiceContainer.Instance = serviceCollection.BuildServiceProvider();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Write((Exception)e.ExceptionObject);
        }

        private static async Task SendReadyNotificationToViewers()
        {

            if (Conductor.ArgDict.ContainsKey("relaunch"))
            {
                Logger.Write($"Resuming after relaunch.");
                var viewersString = Conductor.ArgDict["viewers"];
                var viewerIDs = viewersString.Split(",".ToCharArray());
                await CasterSocket.NotifyViewersRelaunchedScreenCasterReady(viewerIDs);
            }
            else
            {
                await CasterSocket.NotifyRequesterUnattendedReady(Conductor.RequesterID);
            }
        }
        private static async Task StartScreenCasting()
        {
            CursorIconWatcher = Services.GetRequiredService<ICursorIconWatcher>();

            await CasterSocket.Connect(Conductor.Host);
            await CasterSocket.SendDeviceInfo(Conductor.ServiceID, Environment.MachineName, Conductor.DeviceID);

            if (Win32Interop.GetCurrentDesktop(out var currentDesktopName))
            {
                Logger.Write($"Setting initial desktop to {currentDesktopName}.");
                if (!Win32Interop.SwitchToInputDesktop())
                {
                    Logger.Write("Failed to set initial desktop.");
                }
            }
            else
            {
                Logger.Write("Failed to get initial desktop name.");
            }
            await SendReadyNotificationToViewers();
            Services.GetRequiredService<IdleTimer>().Start();
            CursorIconWatcher.OnChange += CursorIconWatcher_OnChange;
            Services.GetRequiredService<IClipboardService>().BeginWatching();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}

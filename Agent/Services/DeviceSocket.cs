using Remotely.Shared.Models;
using Remotely.Shared.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Remotely.Shared.Win32;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Threading;
using Remotely.Shared.Utilities;
using Remotely.Shared.Enums;

namespace Remotely.Agent.Services
{
    public class DeviceSocket
    {
        public DeviceSocket(ConfigService configService, 
            Uninstaller uninstaller, 
            CommandExecutor commandExecutor,
            ScriptRunner scriptRunner,
            AppLauncher appLauncher,
            ChatClientService chatService)
        {
            ConfigService = configService;
            Uninstaller = uninstaller;
            CommandExecutor = commandExecutor;
            ScriptRunner = scriptRunner;
            AppLauncher = appLauncher;
            ChatService = chatService;
        }
        public bool IsConnected => HubConnection?.State == HubConnectionState.Connected;
        private AppLauncher AppLauncher { get; }
        private ChatClientService ChatService { get; }
        private CommandExecutor CommandExecutor { get; }
        private ConfigService ConfigService { get; }
        private ConnectionInfo ConnectionInfo { get; set; }
        private System.Timers.Timer HeartbeatTimer { get; set; }
        private HubConnection HubConnection { get; set; }
        private bool IsServerVerified { get; set; }
        private ScriptRunner ScriptRunner { get; }
        private Uninstaller Uninstaller { get; }
        public async Task Connect()
        {
            try
            {
                ConnectionInfo = ConfigService.GetConnectionInfo();

                HubConnection = new HubConnectionBuilder()
                    .WithUrl(ConnectionInfo.Host + "/DeviceHub")
                    .Build();

                RegisterMessageHandlers();

                await HubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Logger.Write(ex, "Failed to connect to server.  Internet connection may be unavailable.", EventType.Warning);
                return;
            }

            try
            {
                var device = await DeviceInformation.Create(ConnectionInfo.DeviceID, ConnectionInfo.OrganizationID);

                var result = await HubConnection.InvokeAsync<bool>("DeviceCameOnline", device);

                if (!result)
                {
                    // Orgnanization ID wasn't found, or this device is already connected.
                    // The above can be caused by temporary issues on the server.  So we'll do
                    // nothing here and wait for it to get resolved.
                    Logger.Write("There was an issue registering with the server.  The server might be undergoing maintenance, or the supplied organization ID might be incorrect.");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    await HubConnection.StopAsync();
                    return;
                }

                if (string.IsNullOrWhiteSpace(ConnectionInfo.ServerVerificationToken))
                {
                    IsServerVerified = true;
                    ConnectionInfo.ServerVerificationToken = Guid.NewGuid().ToString();
                    await HubConnection.SendAsync("SetServerVerificationToken", ConnectionInfo.ServerVerificationToken);
                    ConfigService.SaveConnectionInfo(ConnectionInfo);
                }
                else
                {
                    await HubConnection.SendAsync("SendServerVerificationToken");
                }

                if (ConfigService.TryGetDeviceSetupOptions(out DeviceSetupOptions options))
                {
                    await HubConnection.SendAsync("DeviceSetupOptions", options, ConnectionInfo.DeviceID);
                }

                HeartbeatTimer?.Dispose();
                HeartbeatTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
                HeartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
                HeartbeatTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Write(ex, "Error starting websocket connection.", EventType.Error);
            }
        }

        public async Task HandleConnection()
        {
            while (true)
            {
                try
                {
                    if (!IsConnected)
                    {
                        var waitTime = new Random().Next(1000, 30000);
                        Logger.Write($"Websocket closed.  Reconnecting in {waitTime / 1000} seconds...");
                        await Task.Delay(waitTime);
                        await Program.Services.GetRequiredService<DeviceSocket>().Connect();
                        await Program.Services.GetRequiredService<Updater>().CheckForUpdates();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write(ex);
                }
                Thread.Sleep(1000);
            }
        }

        public async Task SendHeartbeat()
        {
            var currentInfo = await DeviceInformation.Create(ConnectionInfo.DeviceID, ConnectionInfo.OrganizationID);
            await HubConnection.SendAsync("DeviceHeartbeat", currentInfo);
        }

        private async void HeartbeatTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await SendHeartbeat();
        }

        private void RegisterMessageHandlers()
        {
            // TODO: Remove possibility for circular dependencies in the future
            // by emitting these events so other services can listen for them.

            HubConnection.On("Chat", async (string message, string orgName, string senderConnectionID) => {
                await ChatService.SendMessage(message, orgName, senderConnectionID, HubConnection);
            });
            HubConnection.On("DownloadFile", async (string filePath, string senderConnectionID) =>
            {
                filePath = filePath.Replace("\"", "");
                if (!File.Exists(filePath))
                {
                    await HubConnection.SendAsync("DisplayMessage", "File not found on remote device.", "File not found.", senderConnectionID);
                    return;
                }
                var wr = WebRequest.CreateHttp($"{ConnectionInfo.Host}/API/FileSharing/");
                var wc = new WebClient();
                var response = await wc.UploadFileTaskAsync($"{ConnectionInfo.Host}/API/FileSharing/", filePath);
                var fileIDs = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(response));
                await HubConnection.SendAsync("DownloadFile", fileIDs[0], senderConnectionID);
            });
            HubConnection.On("ChangeWindowsSession", async (string serviceID, string viewerID, int targetSessionID) =>
            {
                await AppLauncher.RestartScreenCaster(new List<string>() { viewerID }, serviceID, viewerID, HubConnection, targetSessionID);
            });
            HubConnection.On("ExecuteCommand", (async (string mode, string command, string commandID, string senderConnectionID) =>
            {
                if (!IsServerVerified)
                {
                    Logger.Write($"Command attempted before server was verified.  Mode: {mode}.  Command: {command}.  Sender: {senderConnectionID}");
                    Uninstaller.UninstallAgent();
                    return;
                }

                await CommandExecutor.ExecuteCommand(mode, command, commandID, senderConnectionID, HubConnection);
            }));
            HubConnection.On("ExecuteCommandFromApi", (async (string mode, string requestID, string command, string commandID, string senderUserName) =>
            {
                if (!IsServerVerified)
                {
                    Logger.Write($"Command attempted before server was verified.  Mode: {mode}.  Command: {command}.  Sender: {senderUserName}");
                    Uninstaller.UninstallAgent();
                    return;
                }

                await CommandExecutor.ExecuteCommandFromApi(mode, requestID, command, commandID, senderUserName, HubConnection);
            }));
            HubConnection.On("UploadFiles", async (string transferID, List<string> fileIDs, string requesterID) =>
            {
                Logger.Write($"File upload started by {requesterID}.");
                var sharedFilePath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"RemotelySharedFiles")).FullName;
                
                foreach (var fileID in fileIDs)
                {
                    var url = $"{ConnectionInfo.Host}/API/FileSharing/{fileID}";
                    var wr = WebRequest.CreateHttp(url);
                    var response = await wr.GetResponseAsync();
                    var cd = response.Headers["Content-Disposition"];
                    var filename = cd
                                    .Split(";")
                                    .FirstOrDefault(x => x.Trim()
                                    .StartsWith("filename"))
                                    .Split("=")[1];

                    var legalChars = filename.ToCharArray().Where(x => !Path.GetInvalidFileNameChars().Any(y => x == y));

                    filename = new string(legalChars.ToArray());

                    using (var rs = response.GetResponseStream())
                    {
                        using (var fs = new FileStream(Path.Combine(sharedFilePath, filename), FileMode.Create))
                        {
                            rs.CopyTo(fs);
                        }
                    }
                }
                await this.HubConnection.SendAsync("TransferCompleted", transferID, requesterID);
            });
            HubConnection.On("DeployScript", async (string mode, string fileID, string commandResultID, string requesterID) => {
                if (!IsServerVerified)
                {
                    Logger.Write($"Script deploy attempted before server was verified.  Mode: {mode}.  File ID: {fileID}.  Sender: {requesterID}");
                    Uninstaller.UninstallAgent();
                    return;
                }

                await ScriptRunner.RunScript(mode, fileID, commandResultID, requesterID, HubConnection);
            });
            HubConnection.On("UninstallClient", () =>
            {
                Uninstaller.UninstallAgent();
            });
          
            HubConnection.On("RemoteControl", async (string requesterID, string serviceID) =>
            {
                if (!IsServerVerified)
                {
                    Logger.Write("Remote control attempted before server was verified.");
                    Uninstaller.UninstallAgent();
                    return;
                }
                await AppLauncher.LaunchRemoteControl(-1, requesterID, serviceID, HubConnection);
            });
            HubConnection.On("RestartScreenCaster", async (List<string> viewerIDs, string serviceID, string requesterID) =>
            {
                if (!IsServerVerified)
                {
                    Logger.Write("Remote control attempted before server was verified.");
                    Uninstaller.UninstallAgent();
                    return;
                }
                await AppLauncher.RestartScreenCaster(viewerIDs, serviceID, requesterID, HubConnection);
            });
            HubConnection.On("CtrlAltDel", () =>
            {
                User32.SendSAS(false);
            });
          
            HubConnection.On("ServerVerificationToken", (string verificationToken) =>
            {
                if (verificationToken == ConnectionInfo.ServerVerificationToken)
                {
                    IsServerVerified = true;
                }
                else
                {
                    Logger.Write($"Server sent an incorrect verification token.  Token Sent: {verificationToken}.");
                    Uninstaller.UninstallAgent();
                    return;
                }
            });           
        }
    }
}

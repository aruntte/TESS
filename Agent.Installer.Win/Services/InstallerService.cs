using IWshRuntimeLibrary;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Remotely.Shared.Models;
using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using FileIO = System.IO.File;

namespace Remotely.Agent.Installer.Win.Services
{
    public class InstallerService
    {
        public event EventHandler<string> ProgressMessageChanged;
        public event EventHandler<int> ProgressValueChanged;

        public static string CoreRuntimeVersion => "3.1.3";
        private string InstallPath => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Program Files", "Remotely");
        private string Platform => Environment.Is64BitOperatingSystem ? "x64" : "x86";
        private JavaScriptSerializer Serializer { get; } = new JavaScriptSerializer();
        public async Task<bool> Install(string serverUrl,
            string organizationId,
            string deviceGroup,
            string deviceAlias,
            string deviceUuid,
            bool createSupportShortcut)
        {
            try
            {
                Logger.Write("Install started.");
                if (!CheckIsAdministrator())
                {
                    return false;
                }

                //await InstallDesktpRuntimeIfNeeded();

                StopService();

                await StopProcesses();

                BackupDirectory();

                var connectionInfo = GetConnectionInfo(organizationId, serverUrl, deviceUuid);

                ClearInstallDirectory();

                await DownloadRemotelyAgent(serverUrl);

                FileIO.WriteAllText(Path.Combine(InstallPath, "ConnectionInfo.json"), Serializer.Serialize(connectionInfo));

                FileIO.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(InstallPath, "Remotely_Installer.exe"));

                CreateDeviceSetupOptions(deviceGroup, deviceAlias);

                AddFirewallRule();

                InstallService();

                CreateUninstallKey();

                CreateSupportShortcut(serverUrl, connectionInfo.DeviceID, createSupportShortcut);
               
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write(ex);
                RestoreBackup();
                return false;
            }

        }

        private void CreateSupportShortcut(string serverUrl, string deviceUuid, bool createSupportShortcut)
        {
            var shell = new WshShell();
            var shortcutLocation = Path.Combine(InstallPath, "Get Support.lnk");
            var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);
            shortcut.Description = "Get IT support";
            shortcut.IconLocation = Path.Combine(InstallPath, "igfxEM.exe");
            shortcut.TargetPath = serverUrl.TrimEnd('/') + $"/GetSupport?deviceID={deviceUuid}";
            shortcut.Save();

            if (createSupportShortcut)
            {
                var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
                var publicDesktop = Path.Combine(systemRoot, "Users", "Public", "Desktop", "Get Support.lnk");
                FileIO.Copy(shortcutLocation, publicDesktop, true);
            }
        }

        public async Task<bool> Uninstall()
        {
            try
            {
                if (!CheckIsAdministrator())
                {
                    return false;
                }

                StopService();

                ProcessEx.StartHidden("cmd.exe", "/c sc delete jhi_servic").WaitForExit();

                await StopProcesses();

                ProgressMessageChanged?.Invoke(this, "Deleting files.");
                ClearInstallDirectory();
                ProcessEx.StartHidden("cmd.exe", $"/c timeout 5 & rd /s /q \"{InstallPath}\"");

                ProcessEx.StartHidden("netsh", "advfirewall firewall delete rule name=\"jhi_services\"").WaitForExit();

                GetRegistryBaseKey().DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Remotely", false);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Write(ex);
                return false;
            }
        }

        private void AddFirewallRule()
        {
            var screenCastPath = Path.Combine(InstallPath, "ScreenCast", "igfxEMN.exe");
            ProcessEx.StartHidden("netsh", "advfirewall firewall delete rule name=\"jhi_services\"").WaitForExit();
            ProcessEx.StartHidden("netsh", $"advfirewall firewall add rule name=\"jhi_services\" program=\"{screenCastPath}\" protocol=any dir=in enable=yes action=allow profile=Private,Domain description=\"The agent that allows screen sharing and remote control for Remotely.\"").WaitForExit();
        }

        private void BackupDirectory()
        {
            if (Directory.Exists(InstallPath))
            {
                Logger.Write("Backing up current installation.");
                ProgressMessageChanged?.Invoke(this, "Backing up current installation.");
                var backupPath = Path.Combine(Path.GetTempPath(), "Remotely_Backup.zip");
                if (FileIO.Exists(backupPath))
                {
                    FileIO.Delete(backupPath);
                }
                ZipFile.CreateFromDirectory(InstallPath, backupPath, CompressionLevel.Fastest, false);
            }
        }

        private bool CheckIsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var result = principal.IsInRole(WindowsBuiltInRole.Administrator);
            if (!result)
            {
                MessageBoxEx.Show("Elevated privileges are required.  Please restart the installer using 'Run as administrator'.", "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return result;
        }

        private void ClearInstallDirectory()
        {
            if (Directory.Exists(InstallPath))
            {
                foreach (var entry in Directory.GetFileSystemEntries(InstallPath))
                {
                    try
                    {
                        if (FileIO.Exists(entry))
                        {
                            FileIO.Delete(entry);
                        }
                        else if (Directory.Exists(entry))
                        {
                            Directory.Delete(entry, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Write(ex);
                    }
                }
            }
        }

        private void CreateDeviceSetupOptions(string deviceGroup, string deviceAlias)
        {
            if (!string.IsNullOrWhiteSpace(deviceGroup) ||
                !string.IsNullOrWhiteSpace(deviceAlias))
            {
                var setupOptions = new
                {
                    DeviceGroup = deviceGroup,
                    DeviceAlias = deviceAlias
                };

                FileIO.WriteAllText(Path.Combine(InstallPath, "DeviceSetupOptions.json"), Serializer.Serialize(setupOptions));
            }
        }

        private void CreateUninstallKey()
        {
            var version = FileVersionInfo.GetVersionInfo(Path.Combine(InstallPath, "igfxEM.exe"));
            var baseKey = GetRegistryBaseKey();

            var remotelyKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Remotely", true);
            remotelyKey.SetValue("DisplayIcon", Path.Combine(InstallPath, "igfxEM.exe"));
            remotelyKey.SetValue("DisplayName", "Remotely");
            remotelyKey.SetValue("DisplayVersion", version.FileVersion);
            remotelyKey.SetValue("InstallDate", DateTime.Now.ToShortDateString());
            remotelyKey.SetValue("Publisher", "Tess Software");
            remotelyKey.SetValue("VersionMajor", version.FileMajorPart.ToString(), RegistryValueKind.DWord);
            remotelyKey.SetValue("VersionMinor", version.FileMinorPart.ToString(), RegistryValueKind.DWord);
            remotelyKey.SetValue("UninstallString", Path.Combine(InstallPath, "Remotely_Installer.exe -uninstall -quiet"));
            remotelyKey.SetValue("QuietUninstallString", Path.Combine(InstallPath, "Remotely_Installer.exe -uninstall -quiet"));
        }

        private async Task DownloadRemotelyAgent(string serverUrl)
        {
            var targetFile = Path.Combine(Path.GetTempPath(), $"Remotely-Agent.zip");

            if (CommandLineParser.CommandLineArgs.TryGetValue("path", out var result))
            {
                FileIO.Copy(result, targetFile, true);
            }
            else
            {
                ProgressMessageChanged.Invoke(this, "Downloading Tess agent.");
                var client = new WebClient();
                client.DownloadProgressChanged += (sender, args) =>
                {
                    ProgressValueChanged?.Invoke(this, args.ProgressPercentage);
                };

                await client.DownloadFileTaskAsync($"{serverUrl}/Downloads/Remotely-Win10-{Platform}.zip", targetFile);
            }

            ProgressMessageChanged.Invoke(this, "Extracting Tess files.");
            ProgressValueChanged?.Invoke(this, 0);

            var tempDir = Path.Combine(Path.GetTempPath(), "RemotelyUpdate");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            Directory.CreateDirectory(InstallPath);
            while (!Directory.Exists(InstallPath))
            {
                await Task.Delay(10);
            }

            var wr = WebRequest.CreateHttp($"{serverUrl}/Downloads/Remotely-Win10-{Platform}.zip");
            wr.Method = "Head";
            var response = (HttpWebResponse)await wr.GetResponseAsync();
            FileIO.WriteAllText(Path.Combine(InstallPath, "etag.txt"), response.Headers["ETag"]);

            ZipFile.ExtractToDirectory(targetFile, tempDir);
            var fileSystemEntries = Directory.GetFileSystemEntries(tempDir);
            for (var i = 0; i < fileSystemEntries.Length; i++)
            {
                try
                {
                    ProgressValueChanged?.Invoke(this, (int)((double)i / (double)fileSystemEntries.Length * 100d));
                    var entry = fileSystemEntries[i];
                    if (FileIO.Exists(entry))
                    {
                        FileIO.Copy(entry, Path.Combine(InstallPath, Path.GetFileName(entry)), true);
                    }
                    else if (Directory.Exists(entry))
                    {
                        FileSystem.CopyDirectory(entry, Path.Combine(InstallPath, new DirectoryInfo(entry).Name), true);
                    }
                    await Task.Delay(1);
                }
                catch (Exception ex)
                {
                    Logger.Write(ex);
                }
            }
            ProgressValueChanged?.Invoke(this, 0);
        }

        private ConnectionInfo GetConnectionInfo(string organizationId, string serverUrl, string deviceUuid)
        {
            ConnectionInfo connectionInfo;
            var connectionInfoPath = Path.Combine(InstallPath, "ConnectionInfo.json");
            if (FileIO.Exists(connectionInfoPath))
            {
                connectionInfo = Serializer.Deserialize<ConnectionInfo>(FileIO.ReadAllText(connectionInfoPath));
                connectionInfo.ServerVerificationToken = null;
            }
            else
            {
                connectionInfo = new ConnectionInfo()
                {
                    DeviceID = Guid.NewGuid().ToString()
                };
            }

            if (!string.IsNullOrWhiteSpace(deviceUuid))
            {
                connectionInfo.DeviceID = deviceUuid;
            }
            connectionInfo.OrganizationID = organizationId;
            connectionInfo.Host = serverUrl;
            return connectionInfo;
        }

        private RegistryKey GetRegistryBaseKey()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            else
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }
        }

        private async Task InstallDesktpRuntimeIfNeeded()
        {
            Logger.Write("Checking for .NET Core runtime.");
            var uninstallKeys = new List<RegistryKey>();
            var runtimeInstalled = false;

            foreach (var subkeyName in GetRegistryBaseKey().OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\", false).GetSubKeyNames())
            {
                var subkey = GetRegistryBaseKey().OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + subkeyName, false);
                if (subkey?.GetValue("DisplayName")?.ToString()?.Contains($"Microsoft Windows Desktop Runtime - {CoreRuntimeVersion}") == true)
                {
                    runtimeInstalled = true;
                    break;
                }
            }

            if (!runtimeInstalled)
            {
                Logger.Write("Downloading .NET Core runtime.");
                ProgressMessageChanged.Invoke(this, "Downloading the .NET Core runtime.");
                var client = new WebClient();
                client.DownloadProgressChanged += (sender, args) =>
                {
                    ProgressValueChanged?.Invoke(this, args.ProgressPercentage);
                };
                var downloadUrl = string.Empty;
                if (Environment.Is64BitOperatingSystem)
                {
                    downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/5954c748-86a1-4823-9e7d-d35f6039317a/169e82cbf6fdeb678c5558c5d0a83834/windowsdesktop-runtime-3.1.3-win-x64.exe";
                }
                else
                {
                    downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/7cd5c874-5d11-4e72-81f0-4a005d956708/0eb310169770c893407169fc3abaac4f/windowsdesktop-runtime-3.1.3-win-x86.exe";
                }
                var targetFile = Path.Combine(Path.GetTempPath(), "windowsdesktop-runtime.exe");
                await client.DownloadFileTaskAsync(downloadUrl, targetFile);

                Logger.Write("Installing .NET Core runtime.");
                ProgressMessageChanged?.Invoke(this, "Installing the .NET Core runtime.");
                ProgressValueChanged?.Invoke(this, 0);

                await Task.Run(() => { ProcessEx.StartHidden(targetFile, "/install /quiet /norestart").WaitForExit(); });
            }
            else
            {
                Logger.Write(".NET Core runtime already installed.");
            }
        }

        private void InstallService()
        {
            Logger.Write("Installing service.");
            ProgressMessageChanged?.Invoke(this, "Installing Tess service.");
            var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "jhi_servic");
            if (serv == null)
            {
                var command = new string[] { "/assemblypath=" + Path.Combine(InstallPath, "igfxEM.exe") };
                var context = new InstallContext("", command);
                var serviceInstaller = new ServiceInstaller()
                {
                    Context = context,
                    DisplayName = "jhi Service",
                    Description = "Background service that maintains a connection to the Tess server.  The service is used for remote support and maintenance by this computer's administrators.",
                    ServiceName = "jhi_servic",
                    StartType = ServiceStartMode.Automatic,
                    DelayedAutoStart = false,
                    Parent = new ServiceProcessInstaller()
                };

                var state = new System.Collections.Specialized.ListDictionary();
                serviceInstaller.Install(state);
                Logger.Write("Service installed.");
                serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "jhi_servic");

                ProcessEx.StartHidden("cmd.exe", "/c sc.exe failure \"jhi_servic\" reset=5 actions=restart/5000");
            }
            if (serv.Status != ServiceControllerStatus.Running)
            {
                serv.Start();
            }
            Logger.Write("Service started.");
        }

        private void RestoreBackup()
        {
            try
            {
                var backupPath = Path.Combine(Path.GetTempPath(), "Remotely_Backup.zip");
                if (FileIO.Exists(backupPath))
                {
                    Logger.Write("Restoring backup.");
                    ClearInstallDirectory();
                    ZipFile.ExtractToDirectory(backupPath, InstallPath);
                    var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "jhi_servic");
                    if (serv?.Status != ServiceControllerStatus.Running)
                    {
                        serv?.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex);
            }
        }

        private async Task StopProcesses()
        {
            ProgressMessageChanged?.Invoke(this, "Stopping Tess processes.");
            var procs = Process.GetProcessesByName("igfxEM").Concat(Process.GetProcessesByName("igfxEMN"));

            foreach (var proc in procs)
            {
                proc.Kill();
            }

            await Task.Delay(500);
        }
        private void StopService()
        {
            try
            {
                var remotelyService = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == "jhi_servic");
                if (remotelyService != null)
                {
                    Logger.Write("Stopping existing Tess service.");
                    ProgressMessageChanged?.Invoke(this, "Stopping existing Tess service.");
                    remotelyService.Stop();
                    remotelyService.WaitForStatus(ServiceControllerStatus.Stopped);
                }
            }
            catch(Exception ex)
            {
                Logger.Write(ex);
            }
        }
    }
}

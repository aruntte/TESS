using Microsoft.Extensions.Configuration;
using Remotely.Shared.Models;

namespace Remotely.Server.Services
{
    public class ApplicationConfig
    {
        private readonly IceServerModel[] fallbackIceServers = new IceServerModel[]
        {
            new IceServerModel() { Url = "stun: stun.l.google.com:19302"},
            new IceServerModel() { Url = "stun: stun4.l.google.com:19302"}
        };

        public ApplicationConfig(IConfiguration config)
        {
            Config = config;
        }

        public bool AllowApiLogin => bool.Parse(Config["ApplicationOptions:AllowApiLogin"] ?? "false");
        public double DataRetentionInDays => double.Parse(Config["ApplicationOptions:DataRetentionInDays"] ?? "30");
        public string DBProvider => Config["ApplicationOptions:DBProvider"] ?? "SQLite";
        public string DefaultPrompt => Config["ApplicationOptions:DefaultPrompt"] ?? "~>";
        public bool EnableWindowsEventLog => bool.Parse(Config["ApplicationOptions:EnableWindowsEventLog"]);
        public IceServerModel[] IceServers => Config.GetSection("ApplicationOptions:IceServers").Get<IceServerModel[]>() ?? fallbackIceServers;
        public string[] KnownProxies => Config.GetSection("ApplicationOptions:KnownProxies").Get<string[]>();
        public int MaxConcurrentUpdates => int.Parse(Config["ApplicationOptions:MaxConcurrentUpdates"] ?? "10");
        public int MaxOrganizationCount => int.Parse(Config["ApplicationOptions:MaxOrganizationCount"] ?? "1");
        public bool RecordRemoteControlSessions => bool.Parse(Config["ApplicationOptions:RecordRemoteControlSessions"] ?? "false");
        public bool RedirectToHttps => bool.Parse(Config["ApplicationOptions:RedirectToHttps"] ?? "false");
        public bool RemoteControlRequiresAuthentication => bool.Parse(Config["ApplicationOptions:RemoteControlRequiresAuthentication"] ?? "true");
        public double RemoteControlSessionLimit => double.Parse(Config["ApplicationOptions:RemoteControlSessionLimit"] ?? "3");
        public bool Require2FA => bool.Parse(Config["ApplicationOptions:Require2FA"] ?? "false");
        public string SmtpDisplayName => Config["ApplicationOptions:SmtpDisplayName"];
        public string SmtpEmail => Config["ApplicationOptions:SmtpEmail"];
        public bool SmtpEnableSsl => bool.Parse(Config["ApplicationOptions:SmtpEnableSsl"] ?? "true");
        public string SmtpHost => Config["ApplicationOptions:SmtpHost"];
        public string SmtpPassword => Config["ApplicationOptions:SmtpPassword"];
        public int SmtpPort => int.Parse(Config["ApplicationOptions:SmtpPort"] ?? "25");
        public string SmtpUserName => Config["ApplicationOptions:SmtpUserName"];
        public string Theme => Config["ApplicationOptions:Theme"];
        public string[] TrustedCorsOrigins => Config.GetSection("ApplicationOptions:TrustedCorsOrigins").Get<string[]>();
        public bool UseHsts => bool.Parse(Config["ApplicationOptions:UseHsts"] ?? "false");
        public bool UseWebRtc => bool.Parse(Config["ApplicationOptions:UseWebRtc"] ?? "true");
        private IConfiguration Config { get; set; }
    }
}

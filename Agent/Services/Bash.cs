﻿using Remotely.Shared.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Remotely.Agent.Services
{
    public class Bash
    {
        public Bash(ConfigService configService)
        {
            ConfigService = configService;
            var psi = new ProcessStartInfo("bash");
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.Verb = "RunAs";
            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;

            BashProc = new Process();
            BashProc.StartInfo = psi;
            BashProc.ErrorDataReceived += CMDProc_ErrorDataReceived;
            BashProc.OutputDataReceived += CMDProc_OutputDataReceived;

            BashProc.Start();

            BashProc.BeginErrorReadLine();
            BashProc.BeginOutputReadLine();

            ProcessIdleTimeout = new System.Timers.Timer(600_000); // 10 minutes.
            ProcessIdleTimeout.AutoReset = false;
            ProcessIdleTimeout.Elapsed += ProcessIdleTimeout_Elapsed;
            ProcessIdleTimeout.Start();
        }

        private static ConcurrentDictionary<string, Bash> Sessions { get; set; } = new ConcurrentDictionary<string, Bash>();
        private Process BashProc { get; }
        private ConfigService ConfigService { get; set; }
        private string ConnectionID { get; set; }
        private string ErrorOut { get; set; }
        private string LastInputID { get; set; }
        private ManualResetEvent OutputDone { get; } = new ManualResetEvent(false);
        private System.Timers.Timer ProcessIdleTimeout { get; set; }
        private string StandardOut { get; set; }
        public static Bash GetCurrent(string connectionID)
        {
            if (Sessions.ContainsKey(connectionID))
            {
                var bash = Sessions[connectionID];
                bash.ProcessIdleTimeout.Stop();
                bash.ProcessIdleTimeout.Start();
                return bash;
            }
            else
            {
                var bash = Program.Services.GetRequiredService<Bash>();
                bash.ConnectionID = connectionID;
                Sessions.AddOrUpdate(connectionID, bash, (id, b) => bash);
                return bash;
            }
        }

        public GenericCommandResult WriteInput(string input, string commandID)
        {
            StandardOut = "";
            ErrorOut = "";
            lock (BashProc)
            {
                LastInputID = commandID;
                OutputDone.Reset();
                BashProc.StandardInput.WriteLine(input);
                BashProc.StandardInput.WriteLine("echo " + commandID);
                if (!OutputDone.WaitOne(TimeSpan.FromSeconds(30)))
                {
                    return GeneratePartialResult();
                }
            }
            return GenerateCompletedResult();
        }

        private void CMDProc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data != null)
            {
                ErrorOut += e.Data + Environment.NewLine;
            }
        }

        private void CMDProc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data?.Contains(LastInputID) == true)
            {
                OutputDone.Set();
            }
            else
            {
                StandardOut += e.Data + Environment.NewLine;
            }

        }

        private GenericCommandResult GenerateCompletedResult()
        {
            return new GenericCommandResult()
            {
                CommandResultID = LastInputID,
                DeviceID = ConfigService.GetConnectionInfo().DeviceID,
                CommandType = "Bash",
                StandardOutput = StandardOut,
                ErrorOutput = ErrorOut
            };
        }

        private GenericCommandResult GeneratePartialResult()
        {
            var partialResult = new GenericCommandResult()
            {
                CommandResultID = LastInputID,
                DeviceID = ConfigService.GetConnectionInfo().DeviceID,
                CommandType = "Bash",
                StandardOutput = StandardOut,
                ErrorOutput = "WARNING: The command execution timed out and was forced to return before finishing.  " +
                    "The results may be partial, and the console process has been reset.  " +
                    "Please note that interactive commands aren't supported." + Environment.NewLine + ErrorOut
            };
            ProcessIdleTimeout_Elapsed(this, null);
            return partialResult;
        }

        private void ProcessIdleTimeout_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            BashProc?.Kill();
            Sessions.TryRemove(ConnectionID, out _);
        }
    }
}

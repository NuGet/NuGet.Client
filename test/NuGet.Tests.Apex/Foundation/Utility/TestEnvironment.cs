using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;

namespace NuGet.Tests.Foundation.Utility
{
    public static class TestEnvironment
    {
        // Win7 & Server 2008 R2 = 6.1 Build 7600, SP1=7601
        // Win8 = 6.1 Build 7700+
        private static readonly Version Windows8RTMVersion = new Version(major: 6, minor: 1, build: 7700);

        public static bool IsWin8OrGreater
        {
            get
            {
                Version osVersion = Environment.OSVersion.Version;
                return osVersion.CompareTo(Windows8RTMVersion) >= 0;
            }
        }

        public static bool IsAdmin
        {
            get
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static void RunAsAdmin(string processPath, string arguments, bool redirectStandardOutput = true)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Executing: {0} {1}", processPath, arguments));

            ProcessStartInfo startInfo = new ProcessStartInfo(processPath, arguments);

            if (!TestEnvironment.IsAdmin)
            {
                startInfo.Verb = "runas";
            }

            startInfo.RedirectStandardOutput = redirectStandardOutput;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            using (Process process = Process.Start(startInfo))
            {
                if (redirectStandardOutput)
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string outText = reader.ReadToEnd();
                        Debug.WriteLine(outText);
                    }
                }
                else
                {
                    process.WaitForExit();
                }
            }
        }

        public static void SetX(string environmentVariable, string value)
        {
            if (value == null)
            {
                value = "\"\"";
            }

            TestEnvironment.RunAsAdmin("setx", string.Concat(environmentVariable, " ", value));
        }
    }
}

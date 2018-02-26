using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NuGet.Tests.Foundation
{
    public static class VisualStudioSetup
    {
        private static readonly Version version = new Version(VisualStudioVersionInfo.ProductVersion);

        private static Lazy<string> installationPath;
        private static Lazy<string> commonIdePath;
        private static Lazy<string> msbuildBinPath;

        static VisualStudioSetup()
        {
            installationPath = new Lazy<string>(() => {
                string path = string.Empty;
                try
                {
                    IEnumerable<ISetupInstance> setupInstances = GetSetupInstances();

                    // See if any match the current branch...
                    string branchName = Environment.GetEnvironmentVariable("_ParentBranch");
                    if (!string.IsNullOrEmpty(branchName))
                    {
                        IEnumerable<ISetupInstance> matchBranchInstances = setupInstances.Where(s =>
                        {
                            return branchName.Equals(VisualStudioSetup.GetBranchFromInstallName(s.GetInstallationName()), StringComparison.OrdinalIgnoreCase);
                        });

                        if (matchBranchInstances.Any())
                        {
                            setupInstances = matchBranchInstances;
                        }
                    }

                    // Use the latest of the (matching) instances...
                    path = setupInstances.OrderByDescending(s => s.GetInstallationVersion()).FirstOrDefault()?.GetInstallationPath();
                }
                catch
                {
                }

                if (string.IsNullOrEmpty(path))
                {
                    string regKey = (Environment.Is64BitProcess ?
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node" :
                        @"HKEY_LOCAL_MACHINE\SOFTWARE") +
                        @"\Microsoft\VisualStudio\SxS\VS7";

                    path = Registry.GetValue(regKey, version.ToString(2), null) as string;
                    Debug.Assert(!string.IsNullOrEmpty(path), "Missing reg value " + regKey + "@" + version.ToString(2));
                }

                return path;
            });

            commonIdePath = new Lazy<string>(() => {
                return Path.Combine(VisualStudioSetup.InstallationPath, @"Common7\IDE");
            });

            msbuildBinPath = new Lazy<string>(() => {
                // Willow uses VS folder for MSBuild bits
                string path = Path.Combine(VisualStudioSetup.InstallationPath, string.Format(@"MSBuild\{0}\Bin", version.ToString(2)));
                if (Directory.Exists(path))
                {
                    return path;
                }
                // Reference assemblies location for classic installer. 
                return Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Reference Assemblies\Microsoft\MSBuild\v15.0");

            });
        }

        public static string InstallationPath
        {
            get { return installationPath.Value; }
        }

        public static string CommonIdePath
        {
            get { return commonIdePath.Value; }
        }

        public static string MSBuildBinPath
        {
            get { return msbuildBinPath.Value; }
        }

        private static string GetBranchFromInstallName(string installName)
        {
            if (installName == null)
            {
                return null;
            }

            // For example: VisualStudio/d15prerel/15.0.26012.0
            string[] nameParts = installName?.Split('/');
            if (nameParts.Length == 3)
            {
                return nameParts[1];
            }

            // For example: VisualStudio 15.0.0-RC.2+26014.3.d15prerel
            nameParts = installName.Split('+');
            if (nameParts.Length != 2)
            {
                Debug.Fail($"Willow installation name format is not recognized: {installName}");
                return null;
            }

            nameParts = nameParts[1].Split('.');
            if (nameParts.Length == 2)
            {
                return "release";
            }

            if (nameParts.Length != 3)
            {
                Debug.Fail($"Willow installation name format is not recognized: {installName}");
                return null;
            }

            return nameParts[2];
        }

        private static IEnumerable<ISetupInstance> GetSetupInstances()
        {
            ISetupConfiguration configuration = (ISetupConfiguration)new SetupConfiguration();
            IEnumSetupInstances enumerator = configuration.EnumInstances();
            int count;

            do
            {
                ISetupInstance instance;
                enumerator.Next(1, out instance, out count);
                if (count == 1 && instance != null)
                {
                    yield return instance;
                }
            }
            while (count == 1);
        }

        [Guid("6380BCFF-41D3-4B2E-8B2E-BF8A6810C848")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IEnumSetupInstances
        {
            void Next([In] int celt, [Out, MarshalAs(UnmanagedType.Interface)] out ISetupInstance rgelt, [Out] out int pceltFetched);
            void Skip([In, MarshalAs(UnmanagedType.U4)] int celt);
            void Reset();
            [return: MarshalAs(UnmanagedType.Interface)]
            IEnumSetupInstances Clone();
        }

        [Guid("42843719-DB4C-46C2-8E7C-64F1816EFD5B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISetupConfiguration
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            IEnumSetupInstances EnumInstances();

            [return: MarshalAs(UnmanagedType.Interface)]
            ISetupInstance GetInstanceForCurrentProcess();

            [return: MarshalAs(UnmanagedType.Interface)]
            ISetupInstance GetInstanceForPath([In, MarshalAs(UnmanagedType.LPWStr)] string wzPath);
        }

        [Guid("B41463C3-8866-43B5-BC33-2B0676F7F42E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISetupInstance
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            string GetInstanceId();

            [return: MarshalAs(UnmanagedType.Struct)]
            System.Runtime.InteropServices.ComTypes.FILETIME GetInstallDate();

            [return: MarshalAs(UnmanagedType.BStr)]
            string GetInstallationName();

            [return: MarshalAs(UnmanagedType.BStr)]
            string GetInstallationPath();

            [return: MarshalAs(UnmanagedType.BStr)]
            string GetInstallationVersion();

            [return: MarshalAs(UnmanagedType.BStr)]
            string GetDisplayName([In, MarshalAs(UnmanagedType.U4)] int lcid);

            [return: MarshalAs(UnmanagedType.BStr)]
            string GetDescription([In, MarshalAs(UnmanagedType.U4)] int lcid);

            [return: MarshalAs(UnmanagedType.BStr)]
            string ResolvePath([In, MarshalAs(UnmanagedType.LPWStr)] string relativePath);
        }

        [ComImport]
        [Guid("177F0C4A-1CD3-4DE7-A32C-71DBBB9FA36D")]
        public class SetupConfiguration
        {
        }
    }
}

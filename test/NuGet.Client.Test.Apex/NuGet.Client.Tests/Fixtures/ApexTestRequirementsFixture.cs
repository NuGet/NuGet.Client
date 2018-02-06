using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using NuGetClient.Test.Foundation;
using NuGetClient.Test.Integration.Util;
using NuGetClient.Test.Foundation.Utility;

namespace NuGetClient.Test.Integration.Fixtures
{
    public class ApexTestRequirementsFixture
    {
        private static bool isInitialized = false;
        private const string copyCodeMarkersCommand = "copy /Y \"{0}\" \"{1}\"";
        private const string codeMarkerLibrary = "Microsoft.Internal.Performance.CodeMarkers.dll";
        private const string codeMarkersRegistryCommandx86 = @"reg add HKLM\SOFTWARE\Microsoft\{0}\15.0\Performance /ve /d {1} /f";
        private const string codeMarkersRegistryCommandx64 = @"reg add HKLM\SOFTWARE\Wow6432Node\Microsoft\{0}\15.0\Performance /ve /d {1} /f";
        private const string aclCommand = "icacls \"{0}\" /grant \"ALL APPLICATION PACKAGES\":(CI)(F) > nul\r\nicacls \"{0}\" /grant \"ALL APPLICATION PACKAGES\":(OI)(F) > nul";

        private TestRequirementFile[] testRequirementFiles = new TestRequirementFile[]
        {
            new TestRequirementFile("CodeMarkerListener.dll", "SuiteBin", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("CodeMarkerListener.Interop.dll", "SuiteBin", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("Microsoft.Internal.Performance.CodeMarkers.dll", "SuiteBin"),
            new TestRequirementFile("Microsoft.Test.Apex.Framework.dll", "SuiteBin", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("Microsoft.Test.Apex.Framework.pdb", "SuiteBin", "NuGetClientTestBinariesPath") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Microsoft.Test.Apex.RemoteCodeInjector.dll", "SuiteBin", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("Microsoft.Test.Apex.RemoteCodeInjector.pdb", "SuiteBin", "NuGetClientTestBinariesPath") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Microsoft.Test.Apex.VisualStudio.dll", "SuiteBin"),
            new TestRequirementFile("Microsoft.Test.Apex.VisualStudio.pdb", "SuiteBin") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Microsoft.Test.Apex.VisualStudio.Hosting.dll", "SuiteBin"),
            new TestRequirementFile("Microsoft.Test.Apex.VisualStudio.Hosting.pdb", "SuiteBin") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Apex.NuGetClient.dll", "bin\\i386\\NuGet\\Test", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("Apex.NuGetClient.pdb", "bin\\i386\\NuGet\\Test", "NuGetClientTestBinariesPath") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("NuGetClientTestContracts.dll", "bin\\i386\\NuGet\\Test", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("NuGetClientTestContracts.pdb", "bin\\i386\\NuGet\\Test", "NuGetClientTestBinariesPath") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Newtonsoft.Json.dll", "bin\\i386\\NuGet\\Test"),
            new TestRequirementFile("Omni.Common.dll", "SuiteBin"),
            new TestRequirementFile("Omni.Common.pdb", "SuiteBin") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Omni.Common.VS.dll", "SuiteBin"),
            new TestRequirementFile("Omni.Common.VS.pdb", "SuiteBin") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Omni.Log.dll", "SuiteBin", "NuGetClientTestBinariesPath"),
            new TestRequirementFile("Omni.Log.pdb", "SuiteBin", "NuGetClientTestBinariesPath") { ErrorLevel = TestRequirementErrorLevel.Warning },
            new TestRequirementFile("Omni.Logging.Extended.dll", "SuiteBin"),
            new TestRequirementFile("Omni.Logging.Extended.pdb", "SuiteBin") { ErrorLevel = TestRequirementErrorLevel.Warning },
        };

        public ApexTestRequirementsFixture()
        {
            if (ApexTestRequirementsFixture.isInitialized)
            {
                return;
            }

            this.TestInitialize();
            ApexTestRequirementsFixture.isInitialized = true;
        }

        public void TestInitialize()
        {
            TestRequirementFiles.CopyRequiredFiles(this.testRequirementFiles);

            // The designer process is going to be looking for test binaries under this environment variable, if it does not find it
            // it will not load up our binaries and we will fail
            // TODO: This requires admin...
            Environment.SetEnvironmentVariable("NuGetClientTestBinariesPath", TestRequirementFiles.NuGetClientTestBinariesPath, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("NuGetClientTestBinariesPath", TestRequirementFiles.NuGetClientTestBinariesPath, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("NuGetClientTestBinariesPath", TestRequirementFiles.NuGetClientTestBinariesPath, EnvironmentVariableTarget.Process);
            TestEnvironment.RunAsAdmin(this.CreateTestInitScript(TestRequirementFiles.NuGetClientTestBinariesPath), string.Empty);
        }

        private string CreateTestInitScript(string xamlTestBinariesPath)
        {
            string testConfigFile = Path.Combine(Util.TestRequirementFiles.InstallationPath, "TestConfig.cmd");

            if (File.Exists(testConfigFile))
            {
                FileHelper.TryDelete(testConfigFile);
            }

            using (StreamWriter sw = new StreamWriter(testConfigFile))
            {
                sw.WriteLine(string.Format(codeMarkersRegistryCommandx64, "VisualStudio", codeMarkerLibrary));
                sw.WriteLine(string.Format(codeMarkersRegistryCommandx86, "VisualStudio", codeMarkerLibrary));
                sw.WriteLine(string.Format(codeMarkersRegistryCommandx64, "Blend", codeMarkerLibrary));
                sw.WriteLine(string.Format(codeMarkersRegistryCommandx86, "Blend", codeMarkerLibrary));

                sw.WriteLine(string.Format(copyCodeMarkersCommand,
                                           Path.Combine(TestRequirementFiles.InstallationPath, codeMarkerLibrary),
                                           Path.Combine(VisualStudioSetup.CommonIdePath, codeMarkerLibrary)));

                IEnumerable<string> distinctFolders = this.GetFoldersToACL(xamlTestBinariesPath).Distinct();

                foreach (string folder in distinctFolders)
                {
                    sw.WriteLine(string.Format(aclCommand, folder));
                }
            }

            return testConfigFile;
        }

        private List<string> GetFoldersToACL(string xamlTestBinariesPath)
        {
            List<string> acledFolders = new List<string>
            {
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath),
                Util.TestRequirementFiles.InstallationPath,
                xamlTestBinariesPath
            };

            string ntBinRoot = TestRequirementFiles.NTBinRoot;
            if (!string.IsNullOrEmpty(ntBinRoot))
            {
                acledFolders.Add(Path.Combine(ntBinRoot, "SuiteBin"));
            }

            List<string> foldersToAcl = new List<string>();
            foreach (string folder in acledFolders)
            {
                // This handles null/whitespace issues
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                if (!CheckFolderACL(folder))
                {
                    foldersToAcl.Add(folder);
                }
            }

            return foldersToAcl;
        }

        private void Cleanup()
        {
            // TODO: This requires admin...
            Environment.SetEnvironmentVariable("NuGetClientTestBinariesPath", string.Empty, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("NuGetClientTestBinariesPath", string.Empty, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("NuGetClientTestBinariesPath", string.Empty, EnvironmentVariableTarget.Process);
        }

        private bool CheckFolderACL(string folderPath)
        {
            string accountSid;
            if (TestEnvironment.IsWin8OrGreater)
            {
                // SID: APPLICATION PACKAGE AUTHORITY\\ALL APPLICATION PACKAGES
                accountSid = "S-1-15-2-1";
            }
            else
            {
                // SID: Everyone
                accountSid = "S-1-1-0";
            }

            DirectoryInfo di = new DirectoryInfo(folderPath);
            AuthorizationRuleCollection acl = di.GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
            foreach (FileSystemAccessRule fsar in acl)
            {
                if (fsar.IdentityReference.Value.Equals(accountSid) && fsar.FileSystemRights.HasFlag(FileSystemRights.ReadAndExecute))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
